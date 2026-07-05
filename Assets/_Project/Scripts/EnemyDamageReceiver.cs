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
    [Tooltip("★이 개체의 커밋이 '읽기 슬로모'를 발동할 가치가 있나 — 엘리트/시그니처 공격만 true. 호드 잡몹 전부가 슬로모 후보면 소음+쿨다운 낭비(Codex 조건 4).")]
    [SerializeField] bool highValueCommit = true;

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

    [Header("★피격 스태거 (잡몹 억제 상태 — 2026-07-04 나·Codex·gd 3자 수렴)")]
    [Tooltip("피격 시 스태거 상태 진입 — '맞으면 행동이 끊긴다'. 추격/접촉딜 정지는 드라이버(SwarmChaser)가 IsStaggered를 읽어 구현. " +
             "★기본 꺼짐 — RB 잡몹만 켠다(스포너 SetStaggerOnHit). 루트모션 몹(늑대/브루트)은 HitReact 애니/커밋 상태가 담당.")]
    [SerializeField] bool staggerOnHit = false;
    [Tooltip("스태거 지속(초). 0.4~0.8 권장(Codex 0.55) — 재타격 시 리프레시(스택 깊이 ❌, Vermintide 수치 깊이 수입 금지).")]
    [SerializeField, Min(0f)] float staggerDuration = 0.55f;
    [Tooltip("★스태거 중 받는 데미지 배수(Vermintide 커플링 — 억제=처치 가속). HP2 잡몹이 경직 중 후속타 원킬(1×2=2) = 처치 경제 기준선.")]
    [SerializeField, Min(1f)] float staggeredDamageMult = 2f;

    // ── 이벤트 (드라이버가 구독: 인터럽트/정리). 구독자 없어도 무해. ──
    /// <summary>피격 시. 인자 = (데미지, 가해 위치). 드라이버가 윈드업 취소(스태거 인터럽트)에 쓴다.</summary>
    public event Action<int, Vector3> OnDamaged;
    /// <summary>사망 시(HP 0). 드라이버/스포너가 정리(토큰 반납·장판 취소 등)에 쓴다.</summary>
    public event Action OnDied;
    /// <summary>피격 넉백 통지 — (가해 위치, 넉백 m/s). SwarmChaser 등 물리(RB) 몹이 구독해 임펄스로 *진짜* 밀린다(밀어버리기).
    /// knockbackResponse(비주얼 오프셋)와 독립 — RB 몹은 response=0으로 두고 이 이벤트로만 변위(이중소유 방지).</summary>
    public event Action<Vector3, float> OnKnocked;
    /// <summary>★전역(정적) — 아무 개체나 사망. PerformanceGauge(처리효율 스텁)가 구독. ⚠️구독자는 OnDisable서 해제(정적 누수 방지).</summary>
    public static event Action<EnemyDamageReceiver> AnyDied;
    /// <summary>★전역(정적) — 아무 개체나 크리티컬 피격(읽고-처리 성공). PerformanceGauge가 구독.</summary>
    public static event Action<EnemyDamageReceiver> AnyCritHit;
    /// <summary>★전역(정적) — 커밋 신호 *시작* 엣지(비커밋→커밋 전이 1회). ReadSlowmoTrigger(읽기 슬로모)가 구독.</summary>
    public static event Action<EnemyDamageReceiver> CommitStarted;

    public bool IsDead => _dead;
    public int Hp => _hp;
    /// <summary>이 개체의 커밋이 읽기 슬로모 발동 가치가 있나 — ReadSlowmoTrigger가 필터로 읽는다.</summary>
    public bool HighValueCommit => highValueCommit;
    /// <summary>★스태거(피격 경직) 중인가 — 드라이버(SwarmChaser)가 추격/접촉딜 정지에, TakeHit이 데미지 배수에 읽는다.</summary>
    public bool IsStaggered => staggerOnHit && !_dead && Time.time < _staggeredUntil;

    /// <summary>스포너가 런타임 주입 시 호출 — RB 잡몹만 스태거 켠다(루트모션 몹 기본 꺼짐 유지).</summary>
    public void SetStaggerOnHit(bool on) => staggerOnHit = on;

    /// <summary>★외부 스태거 부여(도미노 연쇄 등) — 기존 잔여 시간보다 길 때만 연장. staggerOnHit 꺼진 몹엔 무효.</summary>
    public void ApplyStagger(float duration)
    {
        if (!staggerOnHit || _dead) return;
        _staggeredUntil = Mathf.Max(_staggeredUntil, Time.time + duration);
    }

    /// <summary>스포너가 런타임 주입 시 최대 HP 설정. ★Awake 전(권장: AddComponent 직후, SetActive 전)에 부르면
    /// Awake가 이 값으로 _hp를 초기화한다. Awake 후 호출 시 현재 _hp도 즉시 갱신(미피격 상태 가정 — 풀 재활용 경로).</summary>
    public void SetMaxHp(int hp)
    {
        maxHp = Mathf.Max(1, hp);
        _hp = maxHp;   // ★무조건 갱신(Stab H-1): Awake 전 호출이면 Awake가 동일값으로 재초기화, 후면 즉시 반영 — 두 경로 다 안전.
    }

    int _hp;
    bool _dead;
    Vector3 _lastHitFrom;    // ★마지막 타격의 가해 원점 — 사망 연출(IDeathStager, 방향성 붕괴)이 붕괴 방향으로 읽는다.
    float _staggeredUntil;   // ★스태거 만료 시각 — ★의도적으로 *스케일* 시간(Time.time): 몹 이동/물리와 같은 도메인이라
                             //   슬로모/히트스탑 중 세계와 함께 늘어지는 게 일관(unscaled면 슬로모 중 적이 먼저 깨어남).
                             //   히트스탑 겹침 드리프트(+15~25%, 항상 관대한 방향)는 인지된 트레이드오프(Stab M-4=Codex P2, 07-04).
    MaterialPropertyBlock _mpb;
    Color[] _baseColors;     // 렌더러별 원래 _BaseColor(플래시 복원용)
    float _flashTimer;
    Vector3 _knockVel;

    // ── ★AtomLab 디버그 채널 토글 캐시 — 원자 테스트 랩(AtomLabRig)이 off↔on 스왑할 때의 원값(Awake 시점). ──
    float _origFlashTime, _origShakeAmplitude, _origHitStopDuration;

    // ── ★커밋 신호 + 크리티컬 플래시 ──
    float _commitWindup01;   // 커밋 윈드업 진행(0=비커밋, 1=타격 직전). 드라이버가 DriveCommit으로 구동.
    bool _commitActive;      // 직전 프레임 커밋 중이었나 — CommitStarted 엣지(1회) 검출용.
    float _commitNowTimer;   // NOW 흰 펄스 잔여(초) — ★진입 엣지 1회만(발사 전 구간 흰색 고착 방지, Stab H-1/Codex#5).
    bool _nowLatched;        // now=true 연속 구간 내 1회 펄스 가드(매 프레임 재발동 스트로브 방지).
    float _critFlashTimer;   // 크리티컬 시안 플래시 잔여(초).
    bool _visualDirty;       // 직전 프레임에 base 아닌 색을 썼나 — idle 복귀 시 1회만 base 복원 후 쓰기 중단(idle 호드 매프레임 MPB 쓰기 회피).
    bool _hasEmission;       // 렌더러 머티리얼에 _EmissionColor 있나(Awake 캐시).

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    public const float MaxHitStop = 0.08f;   // ★히트스탑 절대 상한(Stab H-2): killFeelScale 배율이 0.1×4=0.4s 스터터로 새는 것 차단. public=DirectionalCollapse 2박 역전 가드가 참조(Stab M-3).

    void Awake()
    {
        _hp = Mathf.Max(1, maxHp);
        // ★AtomLab 채널 토글 원값 캐시 — off는 이 값을 0으로 스왑, on은 이 값으로 복원(SetXEnabled 참조).
        _origFlashTime = flashTime;
        _origShakeAmplitude = shakeAmplitude;
        _origHitStopDuration = hitStopDuration;
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
        _lastHitFrom = from;   // 사망 연출의 붕괴 방향 소스(치명타가 어디서 왔나)

        // ★스태거 커플링(07-04) — *이미* 경직 중이면 배수 적용(억제→처치 가속), 그 후 경직 리프레시.
        int applied = Mathf.Max(0, damage);
        if (IsStaggered && staggeredDamageMult > 1f)
            applied = Mathf.Max(1, Mathf.RoundToInt(applied * staggeredDamageMult));
        if (staggerOnHit) _staggeredUntil = Mathf.Max(_staggeredUntil, Time.time + staggerDuration);   // ★Max 연장(Stab M-3) — 더 긴 외부 스태거를 평타가 단축 못 하게(ApplyStagger와 대칭).

        _hp = Mathf.Max(0, _hp - applied);   // ★음수 클램프(Stab M-3): public Hp가 -N으로 안 새게.
        bool lethal = _hp <= 0;

        // 1) 플래시 재무장 — ComposeVisual이 LateUpdate에 합성 적용(같은 프레임 반영).
        _flashTimer = flashTime;

        // 1.5) ★넉백 통지 — RB 몹(SwarmChaser)이 구독해 임펄스로 밀린다(밀어버리기 물리). 비주얼 오프셋(아래 2)과 독립.
        if (knockback > 0f) OnKnocked?.Invoke(from, knockback);

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

        // 4) 인터럽트/정리 통지 — ★applied(스태거 배수 반영값) 전달: 구독자가 실제 깎인 값을 본다.
        //   ⚠️암묵 결합(Stab M-1): CaniathroxChaser 포이즈가 이 값을 누적 — 루트모션 몹에 staggerOnHit을 켜는 순간
        //   경직 중 피격 ×2가 포이즈 임계 도달도 2배로 당긴다. 켤 일이 생기면 포이즈 임계를 같이 재튜닝할 것.
        OnDamaged?.Invoke(applied, from);
        if (lethal) Die();
    }

    void Die()
    {
        if (_dead) return;
        _dead = true;
        _knockVel = Vector3.zero;
        _commitWindup01 = 0f; _commitNowTimer = 0f; _nowLatched = false; _critFlashTimer = 0f; _flashTimer = 0f;
        _commitActive = false;
        RestoreBase();                  // 플래시/커밋 글로우 원복(사망 연출이 색을 잡아먹지 않게)
        OnDied?.Invoke();               // 드라이버/스포너 정리 훅
        AnyDied?.Invoke(this);          // ★전역 통지 — 처리효율 게이지(실적) 가산
        // ★사망 연출 위임(2026-07-05 방향성 붕괴, 채널 7) — IDeathStager가 붙어 있고 수락하면 비활성화를 연출이 소유
        //   (연출 끝에 스스로 SetActive(false)). 거절(채널 off/미배선)이면 기존 즉시 소멸 폴백(v1 동작 보존).
        var stager = GetComponent<IDeathStager>();
        if (stager == null || !stager.StageDeath(_lastHitFrom))
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
        // ★_dead 가드(Stab L-1): 외부 GO 드라이버가 사망 후 호출해도 죽은 개체가 CommitStarted(읽기 슬로모)를 못 쏘게.
        if (_dead || !enableCommitSignal) { _commitWindup01 = 0f; _commitNowTimer = 0f; _nowLatched = false; _commitActive = false; return; }
        _commitWindup01 = Mathf.Clamp01(windup01);
        // ★커밋 시작 엣지(비커밋→커밋) 1회 전역 통지 — 읽기 슬로모 트리거용(연속 프레임 재발동 없음).
        bool committing = _commitWindup01 > 0.001f;
        if (committing && !_commitActive) CommitStarted?.Invoke(this);
        _commitActive = committing;
        // NOW = 진입 엣지 1회 펄스(연속 now=true 구간에 1번만). 발사 전 구간 흰색 고착·매프레임 재발동 방지.
        if (now) { if (!_nowLatched) { _commitNowTimer = commitNowFlashTime; _nowLatched = true; } }
        else _nowLatched = false;
    }

    // ════════ ICritReact — 크리티컬 피격 시안 플래시(일반 흰 피격과 구별) ════════
    public void OnCritHit()
    {
        if (_dead) return;
        _critFlashTimer = critFlashTime;
        AnyCritHit?.Invoke(this);   // ★전역 통지 — 읽고-처리 성공 = 처리효율 게이지 가산
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
        _commitActive = false;
        _staggeredUntil = 0f;   // ★풀링 재사용 시 stale 스태거 잔존 방지(Stab M-2) — 형제 상태 필드와 동일 리셋 계약.
        _visualDirty = false;
        RestoreBase();
    }

    #region ★AtomLab 디버그 채널 토글 (원자 테스트 랩 전용 — AtomLabRig가 구동. off=0 스왑, on=Awake 캐시값 복원)
    /// <summary>피격 플래시 채널 on/off — off는 flashTime 0(ComposeVisual hit01이 항상 0 → 플래시 무발동).</summary>
    public void SetFlashEnabled(bool on)
    {
        flashTime = on ? _origFlashTime : 0f;
        if (!on) _flashTimer = 0f;   // ★Stab M-1: 진행 중 플래시 즉시 끔 — flashTime=0 재정규화(_flashTimer/0.0001=1)로 흰색 스파이크 새는 것 방지.
    }

    /// <summary>피격 카메라 쉐이크 채널 on/off — off는 shakeAmplitude 0(TakeHit의 SmashFeel.Shake 호출 자체가 가드로 스킵).</summary>
    public void SetShakeEnabled(bool on) => shakeAmplitude = on ? _origShakeAmplitude : 0f;

    /// <summary>피격 히트스탑 채널 on/off — off는 hitStopDuration 0(TakeHit의 SmashFeel.HitStop 호출이 가드로 스킵).</summary>
    public void SetHitStopEnabled(bool on) => hitStopDuration = on ? _origHitStopDuration : 0f;
    #endregion
}
