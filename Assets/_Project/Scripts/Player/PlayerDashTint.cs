using UnityEngine;

/// <summary>
/// ★회피 무적 중 몸 발광 — <see cref="PlayerMotor.IsInvulnerable"/>(무적 창) 동안 몸 렌더러의 모든 서브메시
/// 머티리얼을 시안으로 발광시켜 "회피 = 무적 에너지"를 몸 전체에 새긴다. 시안 잔상(<see cref="PlayerAfterimage"/>)과 같은 색.
///
/// 주의점 셋:
///  ① 몸은 머티리얼 4개(피부/머리/재킷/로고)라 첫 머티리얼만 칠하면 일부만 변한다 → body.materials 전부에 건다.
///  ② 어두운 _BaseMap이라 알베도 틴트는 묻힌다 → 발광(_EmissionColor·더해지는 빛)만 쓴다(텍스처 무관·HDR 블룸 통과).
///  ③ SRP Batcher가 MaterialPropertyBlock의 CBUFFER 오버라이드를 무시 → 렌더러 전용 인스턴스(body.materials)를 직접 구동.
///
/// ★끄는 타이밍·속도 분리: 회피 시작(무적 상승 엣지)부터 glowHold 동안 발광 유지 → 그 뒤 fadeOutSpeed로 끈다.
/// 켜짐은 fadeInSpeed. 무적이 먼저 끝나면 그때 끈다(둘 중 빠른 쪽). _BaseColor는 안 건드림(원색 오염 방지).
/// </summary>
public class PlayerDashTint : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("무적 상태 소스. 비우면 부모에서 탐색.")]
    [SerializeField] PlayerMotor motor;
    [Tooltip("발광시킬 몸 렌더러. 비우면 자식에서 탐색.")]
    [SerializeField] SkinnedMeshRenderer body;

    [Header("회피 발광")]
    [Tooltip("무적 중 몸 발광 색(HDR 시안=액션·블룸). 어두운 텍스처를 무시하고 더해지는 빛 — 밝게 줄수록 강한 글로우.")]
    [ColorUsage(true, true)]
    [SerializeField] Color dashEmission = new Color(0.3f, 1.8f, 2.2f, 1f);

    [Header("끄는 타이밍·속도")]
    [Tooltip("발광 유지 시간(초) — 회피 시작 후 이만큼 지나면 꺼지기 시작. 무적창(iframeDuration)보다 짧으면 일찍 꺼진다.")]
    [SerializeField, Min(0f)] float glowHold = 0.18f;
    [Tooltip("켜지는 속도(클수록 즉각).")]
    [SerializeField, Min(0.1f)] float fadeInSpeed = 30f;
    [Tooltip("꺼지는 속도(클수록 빨리 꺼짐).")]
    [SerializeField, Min(0.1f)] float fadeOutSpeed = 12f;

    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    Material[] _mats;   // 렌더러 전용 인스턴스(모든 서브메시)
    bool _has;
    bool _wasInvuln;
    float _elapsed;     // 회피 시작 후 경과(unscaled)
    float _t;           // 0=원색, 1=발광 최대

    void Awake()
    {
        if (motor == null) motor = GetComponentInParent<PlayerMotor>();
        if (body == null) body = GetComponentInChildren<SkinnedMeshRenderer>();
        if (motor == null || body == null)
        {
            Debug.LogError("[PlayerDashTint] motor/body 미연결 — 비활성. Inspector/계층 확인.", this);
            enabled = false;
            return;
        }
        _mats = body.materials;   // ★모든 서브메시 인스턴스화(피부/머리/재킷/로고 전부)
        foreach (var m in _mats)
        {
            if (m == null || !m.HasProperty(EmissionId)) continue;
            m.EnableKeyword("_EMISSION");                                       // ★발광 켜기(원본은 꺼져 있었음)
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;   // 동적 발광 — GI 베이크 영향 없음
            m.SetColor(EmissionId, Color.black);
        }
        _has = _mats != null && _mats.Length > 0;
    }

    void OnDisable()
    {
        // 무적 중 비활성화(사망·씬전환) 시 발광 고착 방지.
        _t = 0f; _elapsed = 0f; _wasInvuln = false;
        if (_mats == null) return;
        foreach (var m in _mats) if (m != null && m.HasProperty(EmissionId)) m.SetColor(EmissionId, Color.black);
    }

    void LateUpdate()
    {
        if (!_has) return;
        float udt = Time.unscaledDeltaTime;   // 슬로모 무관 즉각 신호

        bool invuln = motor.IsInvulnerable;
        if (invuln && !_wasInvuln) _elapsed = 0f;   // 새 회피 시작(무적 상승 엣지) — 유지 타이머 리셋
        _wasInvuln = invuln;
        if (invuln) _elapsed += udt;

        // 발광 ON = 무적 중 且 유지 시간 내. glowHold가 무적창보다 짧으면 일찍 꺼지기 시작(무적은 유지).
        bool glowOn = invuln && _elapsed < glowHold;
        float target = glowOn ? 1f : 0f;
        float speed = glowOn ? fadeInSpeed : fadeOutSpeed;
        _t = Mathf.MoveTowards(_t, target, speed * udt);

        Color emis = dashEmission * _t;
        foreach (var m in _mats) if (m != null && m.HasProperty(EmissionId)) m.SetColor(EmissionId, emis);
    }
}
