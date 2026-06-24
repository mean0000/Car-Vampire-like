// 적 피격 수신기 — 무기(KatanaWeapon)가 IDamageable로 때리는 단일 진입점. 모든 신규 몹(Caniathrox/Crassorrid/
// Dimax/Venodonte) 공용(OCP: 종마다 새 코드 0, 이 컴포넌트 1개 + Renderer만). ZombieController의 거대 피격
// 로직을 끌고 오지 않고, "읽고-베기 슬라이스에 필요한 최소 반응"만 외과적으로 담는다.
//
// ════════ 책임 (좁게) ════════
//   1) HP — TakeHit 누적, 0 이하면 사망(OnDied 이벤트 + 비활성).
//   2) 피격 플래시 — MaterialPropertyBlock으로 흰 점멸(머티리얼 인스턴싱·배칭 안 깸).
//   3) 타격 Feel — SmashFeel.Shake + HitStop 재사용(프로젝트 단일 시간 소유자, 전역 timeScale 직접 만지지 않음).
//   4) 넉백(약·짧음) — ★루트모션 소유 몹(applyRootMotion=true)과의 이중소유를 피하려 *작게*만. 기본 약하게,
//      필요시 0으로. 진짜 무게의 넉백/플린치는 히트리액트 *애니 상태*로 가야 함(Animation 에이전트 후속).
//   5) 인터럽트 훅 — OnDamaged 이벤트로 드라이버가 구독(윈드업 중 맞으면 공격 취소 = "끊고 베기"). v1은 이벤트만
//      쏘고, 실제 취소는 드라이버가 구현(없으면 무해 — 피격/사망만 동작).
//
// ★위치 소유: 넉백은 LateUpdate(Animator 이후)에서 model에 *감쇠 오프셋*으로 더한다. 작고 짧아 루트모션 전진과
//   섞여도 체감 버그 없음(Idle 중엔 플린치로 읽히고, 접근 중엔 ~수 cm라 무시 가능). 크게 키우려면 히트리액트 애니로.
using System;
using UnityEngine;

[DisallowMultipleComponent]
public class EnemyDamageReceiver : MonoBehaviour, IDamageable, ICritReact
{
    [Header("체력")]
    [Tooltip("최대 HP. 카타나 1타 데미지 대비로 잡는다(잡몹=1~2타, 정예=더). 읽고-베기는 한 마리가 금방 안 죽어야 '읽을' 시간이 생김.")]
    [SerializeField] int maxHp = 3;

    [Header("참조")]
    [Tooltip("색 점멸을 입힐 렌더러들. 비우면 자식에서 자동 수집(SkinnedMeshRenderer 포함).")]
    [SerializeField] Renderer[] renderers;
    [Tooltip("넉백 오프셋을 더할 대상(보통 비주얼 model Transform). 비우면 이 GameObject의 transform.")]
    [SerializeField] Transform model;

    [Header("피격 플래시")]
    [Tooltip("맞은 순간 칠하는 색(흰 점멸 권장 — 어느 바디색에도 '맞았다'가 읽힘).")]
    [SerializeField] Color flashColor = Color.white;
    [Tooltip("플래시 지속(초). 짧게 — 번쩍이고 사라짐.")]
    [SerializeField, Min(0f)] float flashTime = 0.10f;

    [Header("타격 Feel (SmashFeel 재사용 — 음색/무게는 유저 ▶ 판정)")]
    [Tooltip("타격 시 카메라 쉐이크 진폭(m). 잡몹 베기라 작게(브루트 슬램 0.5보다 훨씬 작게). 0이면 생략.")]
    [SerializeField, Min(0f)] float shakeAmplitude = 0.10f;
    [Tooltip("쉐이크 빈도(Hz). 베기는 날카롭게 높게.")]
    [SerializeField, Min(0f)] float shakeFrequency = 28f;
    [Tooltip("쉐이크 지속(초). 아주 짧게.")]
    [SerializeField, Min(0f)] float shakeDuration = 0.10f;
    [Tooltip("피격 히트스탑(초). ★0.03~0.05 — 연타라 길면 거슬림. 프로젝트 HitStop이 timeScale 복원 가드 보유.")]
    [SerializeField, Range(0f, 0.1f)] float hitStopDuration = 0.035f;
    [Tooltip("사망(피니시) 타격은 더 강조 — 위 값에 곱하는 배율(쉐이크·히트스탑 공통). 1=동일, 2~3=킬 강조.")]
    [SerializeField, Range(1f, 4f)] float killFeelScale = 2.2f;

    [Header("★커밋 신호 (읽고-베기 — 적이 '언제' 칠지 본체로 읽힘. mockup: 회→주황→백 + NOW 흰 플래시)")]
    [Tooltip("켜면 공격 커밋(윈드업~타격) 동안 본체 색/발광이 차오른다(회→주황) + 타격 순간(NOW) 흰 플래시. " +
             "★드라이버(Chaser)가 매 프레임 DriveCommit으로 구동. 신호가 *본체*에 박혀 둘러싼 호드서도 각 적이 따로 읽힌다(uiux 핵심).")]
    [SerializeField] bool enableCommitSignal = true;
    [Tooltip("윈드업 가득(직전) 색 — 레드오렌지(위협 차오름, 색 캐넌 §5 적 위협).")]
    [SerializeField] Color commitColor = new Color(1f, 0.45f, 0.1f, 1f);
    [Tooltip("NOW(타격 순간) 플래시 색 — 흰색(최대 위험·최대 무방비).")]
    [SerializeField] Color commitNowColor = Color.white;
    [Tooltip("NOW 흰 플래시 지속(초) — ★진입 1회 펄스. 짧게(발사 순간을 '점'으로). 발사 클립 전체가 흰색으로 굳지 않게.")]
    [SerializeField, Min(0.02f)] float commitNowFlashTime = 0.1f;
    [Tooltip("발광(_EmissionColor) 강도 배수 — URP 블룸과 함께 '차오름'을 발광으로. 머티리얼에 _EmissionColor 없으면 자동 무시.")]
    [SerializeField, Min(0f)] float commitEmission = 2f;

    [Header("★크리티컬 피격 (커밋 적을 벤 순간 — 시안=내 액션, 일반 흰 피격과 구별)")]
    [Tooltip("크리티컬 피격 시 본체 플래시 색 — 시안(색 캐넌: 나=시안 액션). 일반 피격(흰색)과 구별돼 '크리티컬을 냈다'가 읽힌다.")]
    [SerializeField] Color critFlashColor = new Color(0.3f, 1f, 1f, 1f);
    [Tooltip("크리티컬 플래시 지속(초) — 일반 피격 플래시보다 약간 길게(또렷이).")]
    [SerializeField, Min(0f)] float critFlashTime = 0.18f;

    [Header("넉백 (약·짧음 — 루트모션 충돌 회피)")]
    [Tooltip("무기 넉백(m/s)에 곱하는 응답 계수. ★기본 0 — Caniathrox 등 루트모션 소유 몹(applyRootMotion)은 코드가 위치를 쓰면 안 됨(이중소유, 헌법/Codex C). 물리 넉백·플린치는 HitReact *애니 상태*로(GetHit 클립, Animation 비트). 루트모션 안 쓰는 몹에서만 >0.")]
    [SerializeField, Range(0f, 1f)] float knockbackResponse = 0f;
    [Tooltip("넉백 감쇠율(클수록 빨리 멈춤). 짧게 튕기고 멈추게.")]
    [SerializeField, Min(0.1f)] float knockbackDamp = 14f;

    // ── 이벤트 (드라이버가 구독: 인터럽트/정리). 구독자 없어도 무해. ──
    /// <summary>피격 시. 인자 = (데미지, 가해 위치). 드라이버가 윈드업 취소(스태거 인터럽트)에 쓴다.</summary>
    public event Action<int, Vector3> OnDamaged;
    /// <summary>사망 시(HP 0). 드라이버/스포너가 정리(토큰 반납·장판 취소 등)에 쓴다.</summary>
    public event Action OnDied;

    public bool IsDead => _dead;
    public int Hp => _hp;

    /// <summary>스포너가 런타임 주입 시 최대 HP 설정. ★Awake 전(권장: AddComponent 직후, SetActive 전)에 부르면
    /// Awake가 이 값으로 _hp를 초기화한다. Awake 후 호출 시 현재 _hp도 즉시 갱신(미피격 상태 가정 — 풀 재활용 경로).</summary>
    public void SetMaxHp(int hp)
    {
        maxHp = Mathf.Max(1, hp);
        _hp = maxHp;   // ★무조건 갱신(Stab H-1): Awake 전 호출이면 Awake가 동일값으로 재초기화, 후면 즉시 반영 — 두 경로 다 안전.
    }

    int _hp;
    bool _dead;
    MaterialPropertyBlock _mpb;
    Color[] _baseColors;     // 렌더러별 원래 _BaseColor(플래시 복원용)
    float _flashTimer;
    Vector3 _knockVel;

    // ── ★커밋 신호 + 크리티컬 플래시 ──
    float _commitWindup01;   // 커밋 윈드업 진행(0=비커밋, 1=타격 직전). 드라이버가 DriveCommit으로 구동.
    float _commitNowTimer;   // NOW 흰 펄스 잔여(초) — ★진입 엣지 1회만(발사 전 구간 흰색 고착 방지, Stab H-1/Codex#5).
    bool _nowLatched;        // now=true 연속 구간 내 1회 펄스 가드(매 프레임 재발동 스트로브 방지).
    float _critFlashTimer;   // 크리티컬 시안 플래시 잔여(초).
    bool _visualDirty;       // 직전 프레임에 base 아닌 색을 썼나 — idle 복귀 시 1회만 base 복원 후 쓰기 중단(idle 호드 매프레임 MPB 쓰기 회피).
    bool _hasEmission;       // 렌더러 머티리얼에 _EmissionColor 있나(Awake 캐시).

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    const float MaxHitStop = 0.08f;   // ★히트스탑 절대 상한(Stab H-2): killFeelScale 배율이 0.1×4=0.4s 스터터로 새는 것 차단.

    void Awake()
    {
        _hp = Mathf.Max(1, maxHp);
        if (model == null) model = transform;
        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<Renderer>(true);

        _mpb = new MaterialPropertyBlock();
        _baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            _baseColors[i] = (r != null && r.sharedMaterial != null && r.sharedMaterial.HasProperty(BaseColorId))
                ? r.sharedMaterial.GetColor(BaseColorId) : Color.white;
        }
        // 발광 구동 가능 여부 캐시(하나라도 _EmissionColor 보유 시 시도 — 없으면 _BaseColor만으로 신호).
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null && renderers[i].sharedMaterial != null
                && renderers[i].sharedMaterial.HasProperty(EmissionColorId)) { _hasEmission = true; break; }

        RestoreBase();   // ★M-2: 초기 MPB 명시 세팅(머티리얼 기본 _EmissionColor 잔존 방지).
    }

    // ════════ IDamageable — 무기가 부르는 단일 진입점 ════════
    public void TakeHit(int damage, Vector3 from, float knockback)
    {
        if (_dead) return;

        _hp = Mathf.Max(0, _hp - Mathf.Max(0, damage));   // ★음수 클램프(Stab M-3): public Hp가 -N으로 안 새게.
        bool lethal = _hp <= 0;

        // 1) 플래시 재무장 — ComposeVisual이 LateUpdate에 합성 적용(같은 프레임 반영).
        _flashTimer = flashTime;

        // 2) 넉백(약) — 가해 위치 반대 방향 평면 임펄스. 응답 계수로 줄여 루트모션과 안 싸우게.
        if (knockbackResponse > 0f && knockback > 0f)
        {
            Vector3 dir = model.position - from; dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
                _knockVel = dir.normalized * (knockback * knockbackResponse);
        }

        // 3) Feel — 사망이면 강조 배율.
        float fs = lethal ? killFeelScale : 1f;
        if (shakeAmplitude > 0f && shakeDuration > 0f)
            SmashFeel.Shake(shakeDuration * Mathf.Min(fs, 2f), shakeAmplitude * fs, shakeFrequency);
        if (hitStopDuration > 0f)
            SmashFeel.HitStop(Mathf.Min(hitStopDuration * fs, MaxHitStop));   // ★상한 클램프(Stab H-2)

        // 4) 인터럽트/정리 통지
        OnDamaged?.Invoke(damage, from);
        if (lethal) Die();
    }

    void Die()
    {
        if (_dead) return;
        _dead = true;
        _knockVel = Vector3.zero;
        _commitWindup01 = 0f; _commitNowTimer = 0f; _nowLatched = false; _critFlashTimer = 0f; _flashTimer = 0f;
        RestoreBase();                  // 플래시/커밋 글로우 원복(사망 연출이 색을 잡아먹지 않게)
        OnDied?.Invoke();               // 드라이버/스포너 정리 훅
        // ★사망 연출(디졸브·파쇄)은 스펙터클 비트에서 이 훅에 붙인다. v1은 비활성으로 마무리(시체 잔존 방지).
        gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        if (_dead) return;

        // 플래시/크리티컬/커밋 글로우 합성 — ★Codex#1: 감쇠 *전*에 합성(첫 프레임 풀 플래시 보존).
        //   ★unscaledDeltaTime(Stab M-2): 히트스탑(timeScale 0.05) 중에도 정상 속도로 감쇠.
        ComposeVisual();
        float udt = Time.unscaledDeltaTime;
        if (_flashTimer > 0f) _flashTimer -= udt;
        if (_critFlashTimer > 0f) _critFlashTimer -= udt;
        if (_commitNowTimer > 0f) _commitNowTimer -= udt;

        // 넉백 감쇠 오프셋 — ★기본 비활성(knockbackResponse=0, Codex C/헌법): 루트모션 몹은 코드가 위치를 안 쓴다.
        //   루트모션 안 쓰는 몹에서 response>0일 때만 동작(그땐 이중소유 아님).
        float dt = Time.deltaTime;
        if (dt > 0f && _knockVel.sqrMagnitude > 0.0001f)
        {
            model.position += _knockVel * dt;
            _knockVel = Vector3.Lerp(_knockVel, Vector3.zero, Mathf.Clamp01(knockbackDamp * dt));
        }
    }

    // ════════ ★커밋 신호 구동 — 드라이버(Chaser)가 매 프레임 호출 ════════
    /// <summary>커밋 신호 구동. windup01 = 윈드업 진행(0..1, 회→주황 차오름), now = 타격 순간(흰 플래시).
    /// 비커밋(접근·휴지) 프레임엔 DriveCommit(0,false)로 꺼야 한다(드라이버가 매 프레임 상태로 결정).</summary>
    public void DriveCommit(float windup01, bool now)
    {
        if (!enableCommitSignal) { _commitWindup01 = 0f; _commitNowTimer = 0f; _nowLatched = false; return; }
        _commitWindup01 = Mathf.Clamp01(windup01);
        // NOW = 진입 엣지 1회 펄스(연속 now=true 구간에 1번만). 발사 전 구간 흰색 고착·매프레임 재발동 방지.
        if (now) { if (!_nowLatched) { _commitNowTimer = commitNowFlashTime; _nowLatched = true; } }
        else _nowLatched = false;
    }

    // ════════ ICritReact — 크리티컬 피격 시안 플래시(일반 흰 피격과 구별) ════════
    public void OnCritHit()
    {
        if (_dead) return;
        _critFlashTimer = critFlashTime;
    }

    // ════════ 본체 색 합성 — 우선순위: 크리티컬(시안) > 일반 피격(흰) > NOW(흰) > 윈드업(회→주황) > base ════════
    //   활성 신호가 하나도 없으면 1회만 base 복원 후 쓰기 중단(idle 호드가 매 프레임 MPB를 쓰지 않게 — _visualDirty 가드).
    void ComposeVisual()
    {
        if (renderers == null) return;
        float hit01  = _flashTimer     > 0f ? Mathf.Clamp01(_flashTimer     / Mathf.Max(0.0001f, flashTime))     : 0f;
        float crit01 = _critFlashTimer > 0f ? Mathf.Clamp01(_critFlashTimer / Mathf.Max(0.0001f, critFlashTime)) : 0f;
        float now01  = _commitNowTimer > 0f ? Mathf.Clamp01(_commitNowTimer / Mathf.Max(0.0001f, commitNowFlashTime)) : 0f;
        bool active = hit01 > 0f || crit01 > 0f || now01 > 0f || _commitWindup01 > 0.001f;

        if (!active)
        {
            if (_visualDirty) { RestoreBase(); _visualDirty = false; }
            return;
        }
        _visualDirty = true;

        // 발광 강도 — 윈드업/NOW/크리티컬 중 최대치로(차오름이 발광으로 읽히게).
        float emi = commitEmission * Mathf.Max(_commitWindup01, Mathf.Max(now01, crit01));
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;
            Color baseC = _baseColors[i];
            Color c = baseC;
            if (_commitWindup01 > 0.001f) c = Color.Lerp(baseC, commitColor, _commitWindup01);
            if (now01 > 0f)               c = Color.Lerp(c, commitNowColor, now01);
            // ★M-1: 크리티컬(시안)은 일반 흰 피격을 완전 override — 동프레임 겹침 시 시안이 흰색에 희석되지 않게.
            if (crit01 > 0f)      c = Color.Lerp(c, critFlashColor, crit01);
            else if (hit01 > 0f)  c = Color.Lerp(c, flashColor, hit01);

            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, c);
            if (_hasEmission) _mpb.SetColor(EmissionColorId, c * emi);
            r.SetPropertyBlock(_mpb);
        }
    }

    // 본체 색/발광을 원래대로(MPB라 머티리얼 배칭 안 깸).
    void RestoreBase()
    {
        if (renderers == null) return;
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, _baseColors[i]);
            if (_hasEmission) _mpb.SetColor(EmissionColorId, Color.black);
            r.SetPropertyBlock(_mpb);
        }
    }

    // 풀링 재활성 대비(스포너가 재사용 시) — 상태 복구.
    public void ResetReceiver()
    {
        _hp = Mathf.Max(1, maxHp);
        _dead = false;
        _knockVel = Vector3.zero;
        _flashTimer = 0f;
        _critFlashTimer = 0f;
        _commitWindup01 = 0f;
        _commitNowTimer = 0f;
        _nowLatched = false;
        _visualDirty = false;
        RestoreBase();
    }
}
