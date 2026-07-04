using UnityEngine;

/// <summary>
/// ★패링 보상 — 히트스탑 + 슬로모. 회피 성공(<see cref="PlayerHealth.Parried"/>) 시:
///   ① 히트스탑(짧은 완전 정지)로 '탁' 멎는 임팩트 → ② 슬로모(timeScale 딥→복귀)로 '한 박자 늘어지는' 손맛.
/// 둘 다 Time.timeScale을 건드리므로 ★이 컴포넌트가 단일 소유자로 내부에서 중재한다(히트스탑이 슬로모보다 우선,
/// 히트스탑이 끝나면 슬로모가 이어짐). 슬로모 타이머는 히트스탑 동안 멈춰 '프리즈→슬로우' 순서를 보존.
///
/// 모든 타이밍은 unscaledDeltaTime으로 구동 — 효과 자신이 느려진 시간에 끌려가 늘어지는 것을 막는다.
/// Time.fixedDeltaTime은 슬로우 중 비례 조정(물리 매끄럽게)·프리즈/정상 시 base. 종료/비활성 시 원복.
/// ⚠️ 외부 시스템(예: 온히트 히트스탑, Feel)도 timeScale을 건드리면 충돌 — 그땐 전역 중재 레이어 필요(Stab I-1).
/// </summary>
public class ParrySlowMotion : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("패링 이벤트 소스. 비우면 부모/씬에서 탐색.")]
    [SerializeField] PlayerHealth health;

    [Header("히트스탑 (순간 정지)")]
    [Tooltip("정지 시간(초, 실시간). 0이면 끔.")]
    [SerializeField, Min(0f)] float hitStopDuration = 0.06f;
    [Tooltip("정지 중 timeScale — 0=완전 정지(가장 '탁'), 살짝 올리면 파티클/애니가 미세하게 살아있다.")]
    [SerializeField, Range(0f, 0.3f)] float hitStopScale = 0f;

    [Header("슬로모")]
    [Tooltip("슬로우 배율(timeScale) — 작을수록 더 느림.")]
    [SerializeField, Range(0.02f, 1f)] float slowFactor = 0.25f;
    [Tooltip("최저속 유지(초, 실시간).")]
    [SerializeField, Min(0f)] float holdDuration = 0.12f;
    [Tooltip("1.0으로 복귀하는 시간(초, 실시간).")]
    [SerializeField, Min(0.01f)] float rampDuration = 0.4f;

    [Header("★클래시(공격 맞받음) — 짧은 프리즈만(긴 슬로모 ❌)")]
    [Tooltip("맞받음 성공 순간 짧은 정지(초, 실시간) = '탁'. 슬로모 없이 프리즈만 — 호드서 빈번해도 안 늘어지게. 0이면 끔.")]
    [SerializeField, Min(0f)] float clashFreeze = 0.05f;
    [Tooltip("연속 클래시 프리즈 최소 간격(초) — 호드서 매 타격 클래시해도 화면이 계속 멎지 않게(스터터 방지). 이 안엔 프리즈 생략(데미지·시안은 무기가 별도).")]
    [SerializeField, Min(0f)] float clashCooldown = 0.13f;

    [Header("★읽기 슬로모 (지각 배려 — 고가치 커밋 *시작* 순간 세상이 잠깐 늘어짐. 입력 요구 없음 = 타이밍-듀얼 아님)")]
    [Tooltip("읽기 슬로우 배율(timeScale). 카탈로그 시작값 0.3.")]
    [SerializeField, Range(0.05f, 1f)] float readSlowFactor = 0.3f;
    [Tooltip("읽기 슬로우 유지(초, 실시간). 0.15~0.25 탐색. 0이면 끔(랩 토글).")]
    [SerializeField, Min(0f)] float readSlowDuration = 0.18f;
    [Tooltip("읽기 슬로우 최소 간격(초) — 호드 커밋 연발이 화면을 계속 늘어지게 하지 않게(Codex 반증 조건 4의 시간축 가드).")]
    [SerializeField, Min(0f)] float readSlowCooldown = 2.5f;
    [Tooltip("읽기 슬로우 복귀 시간(초) — 패링 ramp와 독립(Stab M-1). 짧게 스냅 복귀해야 호드 템포를 안 깎는다.")]
    [SerializeField, Min(0.01f)] float readSlowRampDuration = 0.15f;

    float _baseFixedDelta;
    float _hitStop;     // unscaled 남은 정지 시간
    bool _slowActive;
    float _slowHold;
    float _slowRamp;
    float _activeSlowFactor = 0.25f;   // 이번 슬로우의 배율 — 패링(slowFactor)/읽기(readSlowFactor)가 진입 시 세팅
    float _activeRampDuration = 0.4f;  // 이번 슬로우의 복귀 시간 — 진입 경로별 캡처(Stab M-1: 패링/읽기 독립 튜닝)
    float _clashCdTimer;   // 클래시 프리즈 쿨다운 잔여(unscaled)
    float _readCdTimer;    // 읽기 슬로모 쿨다운 잔여(unscaled)

    void Awake()
    {
        if (health == null) health = GetComponentInParent<PlayerHealth>();   // 부모 체인만 — FindObjectOfType 제거(다중 인스턴스 임의 픽업 방지)
        _baseFixedDelta = Time.fixedDeltaTime;
        // 전역 슬로우 잔존 오염 가드 — 비정상 fixedDeltaTime을 baseline으로 캐시하면 물리 영구 왜곡(Stab H-2).
        if (_baseFixedDelta < 0.001f || _baseFixedDelta > 0.05f)
        {
            Debug.LogWarning($"[ParrySlowMotion] fixedDeltaTime={_baseFixedDelta} 비정상 — 0.02f 폴백.", this);
            _baseFixedDelta = 0.02f;
        }
        if (health == null) Debug.LogWarning("[ParrySlowMotion] PlayerHealth 미발견 — 보상 발동 안 함.", this);
    }

    void OnEnable()
    {
        if (health == null) return;
        health.Parried -= OnParry;   // 중복 구독 방지(Stab H-1)
        health.Parried += OnParry;
    }

    void OnDisable()
    {
        if (health != null) health.Parried -= OnParry;
        // ★Codex: timeScale 소유자 — 클래시 프리즈가 _hitStop<=0로 막 전환된 1프레임 갭에 비활성화되면 _slowActive=false라
        //   조건부 복원이 누수(timeScale 0 잔존=게임 프리즈). Update 안전망과 동일 기준(timeScale≠1)까지 포함해 복원.
        if (_hitStop > 0f || _slowActive || !Mathf.Approximately(Time.timeScale, 1f)) Restore();
    }

    void OnParry()
    {
        // 프리즈 먼저, 그다음 슬로모 — 타이머만 세팅, 실제 timeScale은 Update가 매 프레임 구동(소유권 단일화).
        _hitStop = hitStopDuration;
        _activeSlowFactor = slowFactor;
        _activeRampDuration = rampDuration;
        _slowActive = true;
        _slowHold = holdDuration;
        _slowRamp = rampDuration;
    }

    /// <summary>★읽기 슬로모 — 고가치 커밋 시작 순간의 지각 배려(짧은 슬로우만, 프리즈 ❌). ReadSlowmoTrigger가 호출.
    /// 진행 중인 패링 슬로모/프리즈에는 양보(기존 연출 우선 — timeScale 경합 방지). 내부 쿨다운으로 호드 연발 억제.</summary>
    public void ReadSlow()
    {
        if (readSlowDuration <= 0f || _readCdTimer > 0f) return;
        if (_hitStop > 0f || _slowActive) return;
        _readCdTimer = readSlowCooldown;
        _activeSlowFactor = readSlowFactor;
        _activeRampDuration = readSlowRampDuration;   // ★패링 ramp와 독립(Stab M-1) — 짧게 스냅 복귀
        _slowActive = true;
        _slowHold = readSlowDuration;
        _slowRamp = readSlowRampDuration;
    }

    /// <summary>★공격 맞받음(클래시) — 짧은 프리즈만(긴 슬로모 ❌). 무기(KatanaWeapon)가 클래시 성공 시 호출.
    /// 내부 쿨다운으로 호드 빈번 클래시가 화면을 계속 멎게 하지 않는다(timeScale 단일 소유자 경유로 안전).</summary>
    public void Clash()
    {
        if (_clashCdTimer > 0f) return;       // 쿨다운 중 — 프리즈 생략(데미지/시안 플래시는 무기가 별도 처리)
        _clashCdTimer = clashCooldown;
        if (clashFreeze <= 0f) return;
        _hitStop = Mathf.Max(_hitStop, clashFreeze);   // 프리즈만 — _slowActive는 안 켠다(짧은 탁).
    }

    void Update()
    {
        if (_clashCdTimer > 0f) _clashCdTimer -= Time.unscaledDeltaTime;   // 쿨다운은 상태 무관 상시 감쇠(아래 조기 반환 전).
        if (_readCdTimer > 0f) _readCdTimer -= Time.unscaledDeltaTime;     // 읽기 슬로모 쿨다운 — 동일 정책(상시 감쇠).

        if (_hitStop <= 0f && !_slowActive)
        {
            if (!Mathf.Approximately(Time.timeScale, 1f)) Restore();   // 안전망 — 잔존 복구
            return;
        }

        float udt = Time.unscaledDeltaTime;
        float final;

        if (_hitStop > 0f)
        {
            _hitStop -= udt;     // 슬로모 타이머는 멈춤(프리즈→슬로우 순서 보존)
            // ★M-1(부분): HitStop.cs가 동시 활성이면 그 스케일 아래로 안 덮어쓴다(0으로 stomp 방지).
            //   ★게이트 수정(2026-07-02): HitStop *비활성* 시 ActiveScale=1이라 무조건 Max는 프리즈 자체를 1로
            //   무효화(잠복 버그) — HitStop이 잡고 있을 때만 그 스케일을 바닥으로 쓴다.
            final = HitStop.ActiveScale < 1f ? Mathf.Max(hitStopScale, HitStop.ActiveScale) : hitStopScale;
        }
        else if (_slowHold > 0f)
        {
            _slowHold -= udt;
            final = _activeSlowFactor;
        }
        else
        {
            _slowRamp -= udt;
            float t = Mathf.Clamp01(1f - _slowRamp / Mathf.Max(0.0001f, _activeRampDuration));
            final = Mathf.Lerp(_activeSlowFactor, 1f, t);
            if (_slowRamp <= 0f) { _slowActive = false; Restore(); return; }
        }

        // ★Codex P1(게이트): HitStop(잡몹 피격 '탁')과 동시 활성 시 슬로우(0.3)가 프리즈(0.05)를 덮지 않게 — 더 깊은 쪽 우선.
        //   프리즈 분기는 위에서 이미 중재하므로 슬로우(hold/ramp) 경로에만 적용.
        if (_hitStop <= 0f) final = Mathf.Min(final, HitStop.ActiveScale);

        Time.timeScale = final;
        // 프리즈(≈0)엔 base 유지(timeScale 0이라 물리 어차피 멈춤), 슬로우엔 비례(물리 스텝 매끄럽게).
        Time.fixedDeltaTime = _hitStop > 0f ? _baseFixedDelta : _baseFixedDelta * Mathf.Max(0.05f, final);
    }

    void Restore()
    {
        _hitStop = 0f;
        _slowActive = false;
        Time.timeScale = 1f;
        Time.fixedDeltaTime = _baseFixedDelta;
    }
}
