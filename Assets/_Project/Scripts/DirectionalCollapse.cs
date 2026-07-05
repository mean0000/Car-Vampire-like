using System.Collections;
using UnityEngine;

/// <summary>
/// ★방향성 붕괴(거합 문법) — AtomLab 채널 7. 벤다 → (히트스탑 정적) → **반 박자 늦게** 시체가
/// **베인 방향으로** 지면 모서리를 축으로 넘어간다(가속 낙하) → 잔류 → 지면 아래로 잠기며 소멸.
/// "베기의 간지는 검이 아니라 피해자가 증언한다"(07-05 Fable 진단) — 결과 측 신호의 첫 물리 구현.
///
/// ★넉백 절대 금지(캐넌): 임펄스/밀림 없음 — 무게중심이 무너져 *쓰러진다*(방망이가 아니라 거합.
/// "납도 후에야 쓰러진다" 문법 = 수평 반토막 궁극과 한 계보). 갈라짐 버스트 VFX는 후속 비트(artist).
///
/// 시간축 = ★전부 unscaled — 거합 박자(반 박자 지연·낙하 속도)는 히트스탑/읽기 슬로모에 늘어지면
/// 랙으로 읽힌다(핸드오프 게이트 질문: "반 박자 늦음이 간지냐 랙이냐" — 박자 고정이 전제).
/// 수명주기: <see cref="EnemyDamageReceiver.Die"/>가 IDeathStager로 위임 → 연출 끝에 이 컴포넌트가
/// SetActive(false) + 포즈 원복. 컴포넌트 비활성이면 StageDeath가 거절(false) → 기존 즉시 소멸(A/B 토글).
/// 도중 리스폰(AtomLab R키 → ResetReceiver로 IsDead=false)이면 즉시 중단·포즈 원복(고아 코루틴 방지).
/// </summary>
[DisallowMultipleComponent]
public class DirectionalCollapse : MonoBehaviour, IDeathStager
{
    [Header("참조")]
    [Tooltip("넘어뜨릴 비주얼 transform. 비우면 이 GameObject(EnemyDamageReceiver.model 규약과 동형).")]
    [SerializeField] Transform model;

    [Header("★박자 (전부 unscaled — 슬로모/히트스탑 무관 고정 박자)")]
    [Tooltip("★반 박자 지연(초) — 베인 뒤 이 시간 동안 '정지'했다가 무너진다(납도 문법의 심장). 히트스탑(킬 ~0.077s 실시간)보다 길어야 '정적 → 붕괴' 2박이 선다.")]
    [SerializeField, Min(0f)] float collapseDelay = 0.22f;
    [Tooltip("넘어가는 시간(초) — 가속 낙하(t²). 짧을수록 '털썩', 길수록 '스르륵'.")]
    [SerializeField, Min(0.05f)] float fallDuration = 0.30f;
    [Tooltip("최종 기울기(deg) — 90 살짝 넘겨 지면에 '철퍼덕' 눌리는 읽기.")]
    [SerializeField, Range(30f, 120f)] float fallAngle = 96f;
    [Tooltip("쓰러진 채 잔류(초) — 시체가 잠깐 남아 '결과'를 증언한다.")]
    [SerializeField, Min(0f)] float restDuration = 0.45f;
    [Tooltip("지면 아래로 잠기며 소멸(초).")]
    [SerializeField, Min(0.05f)] float sinkDuration = 0.35f;
    [Tooltip("잠김 깊이(m).")]
    [SerializeField, Min(0.1f)] float sinkDepth = 1.4f;

    [Header("피벗")]
    [Tooltip("회전축(지면 모서리)을 베인 방향으로 이만큼 전진(m) — 발끝을 축으로 넘어가는 근사. 캡슐 반경 정도.")]
    [SerializeField, Min(0f)] float pivotRadius = 0.3f;

    EnemyDamageReceiver _receiver;   // 도중 리스폰(IsDead=false 복귀) 감지 — 고아 코루틴 중단용
    Collider _collider;              // 지면 높이 근사(bounds.min.y). 없으면 model 기준 폴백
    Collider[] _allColliders;        // ★붕괴 중 판정 차단(Codex HIGH) — 시체 잔류 ~1.3s 동안 콜라이더가 살아 있으면
                                     //   죽은 몸에 헛 판정("맞았다" 임팩트음/킥)·타겟 쿼리 오염("비활성=죽음" 계약 위반).
    Vector3 _homeLocalPos;           // 포즈 원복 캐시(Awake 시점 로컬)
    Quaternion _homeLocalRot;
    Coroutine _staging;

    void Awake()
    {
        if (model == null) model = transform;
        _receiver = GetComponent<EnemyDamageReceiver>();
        _collider = GetComponent<Collider>();
        _allColliders = GetComponentsInChildren<Collider>(true);
        _homeLocalPos = model.localPosition;
        _homeLocalRot = model.localRotation;
#if UNITY_EDITOR
        // ★2박 역전 가드(Stab M-3) — 반 박자가 킬 히트스탑(실시간, EnemyDamageReceiver.MaxHitStop 상한)보다 짧으면
        //   '멈춘 세계 속에서 시체만 초고속 요동' 글리치. 두 파일에 흩어진 교차 의존이라 에디터 경고로 승격.
        if (collapseDelay < EnemyDamageReceiver.MaxHitStop + 0.02f)
            Debug.LogWarning($"[DirectionalCollapse] collapseDelay({collapseDelay:0.###}s)가 킬 히트스탑 상한" +
                             $"({EnemyDamageReceiver.MaxHitStop}s)에 근접/미달 — '정적→반박자→붕괴' 2박이 무너진다. 0.1s 이상 권장.", this);
#endif
    }

    // ════════ IDeathStager ════════
    public bool StageDeath(Vector3 hitFrom)
    {
        // 채널 off(A/B)면 거절 → 수신기가 기존 즉시 소멸 폴백.
        if (!isActiveAndEnabled) return false;
        // ★새 죽음 우선(Stab M-1) — 리스폰 직후 같은 프레임 재사망 시 이전 스테이징이 Aborted() 프레임을 못 받아
        //   잔존할 수 있다. 거절하면 붕괴가 조용히 스킵되는 미스터리 → 이전 것을 강제 중단하고 새로 시작.
        if (_staging != null) { StopCoroutine(_staging); _staging = null; RestorePose(); }

        Vector3 dir = model.position - hitFrom; dir.y = 0f;   // 붕괴 방향 = 베인 방향(가해 원점 → 몸, 수평)
        if (dir.sqrMagnitude < 0.0001f) dir = model.forward;  // 원점 겹침 폴백(정점 밀착 타격)
        SetCollidersEnabled(false);   // ★시체=연출 전용(Codex HIGH) — 판정/쿼리에서 즉시 제외(기존 즉시-비활성과 동일 시맨틱). 복원은 RestorePose(리스폰 대비).
        _staging = StartCoroutine(Collapse(dir.normalized));
        return true;
    }

    IEnumerator Collapse(Vector3 fallDir)
    {
        // 지면 모서리 피벗 — 콜라이더 바닥 높이 + 베인 방향으로 반경만큼 전진(발끝 축 근사).
        float groundY = _collider != null ? _collider.bounds.min.y : model.position.y - 1f;
        Vector3 pivot = new Vector3(model.position.x, groundY, model.position.z) + fallDir * pivotRadius;
        Vector3 axis = Vector3.Cross(Vector3.up, fallDir);    // +각도 회전 = 상체가 fallDir로 넘어감

        // 1) ★반 박자 정지 — 히트스탑 정적과 이어지는 '납도' 간(間). unscaled 고정.
        for (float t = 0f; t < collapseDelay; t += Time.unscaledDeltaTime)
        {
            if (Aborted()) yield break;
            yield return null;
        }

        // 2) 낙하 — 가속(t²). RotateAround가 무게중심을 자연히 fallDir로 옮긴다(임펄스 0 = 넉백 아님).
        float applied = 0f;
        for (float t = 0f; t < fallDuration; t += Time.unscaledDeltaTime)
        {
            if (Aborted()) yield break;
            float target = fallAngle * Mathf.Pow(Mathf.Clamp01(t / fallDuration), 2f);
            model.RotateAround(pivot, axis, target - applied);
            applied = target;
            yield return null;
        }
        if (Aborted()) yield break;
        model.RotateAround(pivot, axis, fallAngle - applied);   // 최종각 정착

        // 3) 잔류 — 쓰러진 시체가 결과를 증언.
        for (float t = 0f; t < restDuration; t += Time.unscaledDeltaTime)
        {
            if (Aborted()) yield break;
            yield return null;
        }

        // 4) 잠기며 소멸.
        Vector3 start = model.position;
        for (float t = 0f; t < sinkDuration; t += Time.unscaledDeltaTime)
        {
            if (Aborted()) yield break;
            model.position = start + Vector3.down * (sinkDepth * Mathf.Clamp01(t / sinkDuration));
            yield return null;
        }

        _staging = null;
        gameObject.SetActive(false);   // 소멸 먼저(렌더 차단) → 포즈 원복(다음 리스폰 대비, 비활성 상태서 안전)
        RestorePose();
    }

    /// <summary>도중 리스폰 감지(AtomLab R키 → ResetReceiver가 IsDead=false 복귀) — 즉시 중단·포즈 원복.</summary>
    bool Aborted()
    {
        if (_receiver != null && !_receiver.IsDead)
        {
            _staging = null;
            RestorePose();
            return true;
        }
        return false;
    }

    void RestorePose()
    {
        if (model == null) return;   // 씬 언로드 중 파괴 순서 어긋남 방어(Stab L-3 — model이 별도 자식일 미래 대비)
        model.localPosition = _homeLocalPos;
        model.localRotation = _homeLocalRot;
        SetCollidersEnabled(true);   // 원상 복구 = 다음 리스폰이 온전한 개체(판정 포함)로 깨어나게.
    }

    void SetCollidersEnabled(bool on)
    {
        if (_allColliders == null) return;
        for (int i = 0; i < _allColliders.Length; i++)
            if (_allColliders[i] != null) _allColliders[i].enabled = on;
    }

    /// <summary>비활성 경로 정리 — ★컴포넌트 disable은 코루틴을 *멈추지 않는다*(Codex MEDIUM: 채널 7 off 직후
    /// 원복된 포즈에서 다시 넘어지는 글리치) → 명시 StopCoroutine. 스테이징 중이던 시체는 위임받은 비활성화
    /// 계약을 여기서 완수(GO 끔 — 고아 시체 방지). GO SetActive(false) 경유 진입 시엔 _staging이 이미 null이라 무해.</summary>
    void OnDisable()
    {
        bool wasStaging = _staging != null;
        if (wasStaging) { StopCoroutine(_staging); _staging = null; }
        RestorePose();
        if (wasStaging && _receiver != null && _receiver.IsDead)
            gameObject.SetActive(false);
    }
}
