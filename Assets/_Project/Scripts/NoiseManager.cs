using UnityEngine;

/// <summary>
/// 소음 시스템. "지속 소스"(이동)와 "순간 충격음"(총성/충돌 등)을 분리하고
/// 비대칭 attack/release 엔벨로프로 값을 따라가게 해 "확 커졌다 확 줄어드는" 소리의 질감을 낸다.
/// - 지속 소스: 매 프레임 SetMovementNoise(level) 로 타깃을 지정(정지 시 0).
/// - 순간 충격: EmitImpulse(value) 로 즉시 스파이크.
/// </summary>
[DefaultExecutionOrder(-100)]
public class NoiseManager : MonoBehaviour
{
    public static NoiseManager Instance { get; private set; }

    [Header("Envelope (frame-rate independent)")]
    [SerializeField] float attackTau = 0.08f;   // 커질 때: 짧을수록 확 오름
    [SerializeField] float releaseTau = 0.45f;  // 줄어들 때: 멈추면 ~1.5초 안에 quiet
    [SerializeField] float silenceSnap = 0.5f;  // 이 값 아래 + 타깃 0 이면 0으로 스냅

    [Header("Hearing Radius Mapping")]
    [SerializeField] float radiusPerNoise = 0.25f;
    [SerializeField] float maxRadius = 25f;
    [SerializeField] float chaseThreshold = 50f;

    float _noise;          // 내부 raw (충격 시 100 초과 가능, 읽을 때만 클램프)
    float _sustainTarget;  // 이번 프레임 지속 소스 레벨

    public float CurrentNoise => Mathf.Clamp(_noise, 0f, 100f);
    public float HearingRadius => Mathf.Clamp(CurrentNoise * radiusPerNoise, 0f, maxRadius);
    public bool IsChaseThreshold => CurrentNoise >= chaseThreshold;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        // 지속 타깃은 매 프레임 자동 클리어 — 소스(이동 등)가 SetMovementNoise로 재선언하지 않으면 0.
        // 이렇게 해야 플레이어가 죽거나 비활성화돼도 마지막 소음 레벨이 영구히 유지되지 않는다.
        float target = _sustainTarget;
        _sustainTarget = 0f;

        // 타깃을 향해 지수 추종 — 오를 땐 attackTau, 내릴 땐 releaseTau (비대칭)
        float tau = target > _noise ? attackTau : releaseTau;
        _noise += (target - _noise) * (1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.0001f, tau)));

        // 꼬리 스냅: 타깃이 0이고 충분히 작아지면 완전 무음
        if (target <= 0f && _noise < silenceSnap)
            _noise = 0f;
    }

    /// <summary>지속 소스(이동·제작 등). 매 프레임 호출하며 정지 시 0을 넘긴다.
    /// 같은 프레임 여러 소스가 호출하면 가장 큰 값을 채택(이동+제작 공존).</summary>
    public void SetMovementNoise(float level)
    {
        _sustainTarget = Mathf.Max(_sustainTarget, Mathf.Max(0f, level));
    }

    /// <summary>순간 충격음(총성/충돌/던지기 등). 즉시 스파이크.</summary>
    public void EmitImpulse(float value)
    {
        if (value > _noise) _noise = value;
    }
}
