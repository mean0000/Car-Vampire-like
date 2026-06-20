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

    float _baseFixedDelta;
    float _hitStop;     // unscaled 남은 정지 시간
    bool _slowActive;
    float _slowHold;
    float _slowRamp;

    void Awake()
    {
        if (health == null) health = GetComponentInParent<PlayerHealth>();
        if (health == null) health = FindObjectOfType<PlayerHealth>();
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
        if (_hitStop > 0f || _slowActive) Restore();   // 비활성화 중 timeScale 잔존 방지
    }

    void OnParry()
    {
        // 프리즈 먼저, 그다음 슬로모 — 타이머만 세팅, 실제 timeScale은 Update가 매 프레임 구동(소유권 단일화).
        _hitStop = hitStopDuration;
        _slowActive = true;
        _slowHold = holdDuration;
        _slowRamp = rampDuration;
    }

    void Update()
    {
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
            final = hitStopScale;
        }
        else if (_slowHold > 0f)
        {
            _slowHold -= udt;
            final = slowFactor;
        }
        else
        {
            _slowRamp -= udt;
            float t = Mathf.Clamp01(1f - _slowRamp / Mathf.Max(0.0001f, rampDuration));
            final = Mathf.Lerp(slowFactor, 1f, t);
            if (_slowRamp <= 0f) { _slowActive = false; Restore(); return; }
        }

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
