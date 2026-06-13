// 룩 랩 전용 간단 추종 카메라 — 플레이어를 45°/15m 탑다운으로 부드럽게 따라간다(피하기 가독성 우선).
// ⚠️실제 게임용 PlayerCameraRig(커서 리드/충격/조준 의식 등)와 무관한 신규. 룩 랩 회피 테스트엔 단순 추종이 더 읽힌다.
//
// 설계: 시작 시 카메라-타겟 월드 오프셋(높이/각도 프레이밍)을 캡처해 보존. XZ만 SmoothDamp 지연 추종, Y/회전 고정.
//   ★회전·피치는 절대 안 건드린다(탑다운 멀미 방지). 시간은 unscaledDeltaTime(슬로모와 무관하게 실시간 반응).
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class LabSimpleCamera : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("따라갈 플레이어. 비우면 LabPlayerController를 자동 검색.")]
    [SerializeField] Transform target;

    [Header("Follow")]
    [Tooltip("SmoothDamp 추종 시간(초). 0.1~0.25 = 액션 기준. 작을수록 즉각, 클수록 미끄러짐.")]
    [SerializeField, Min(0f)] float followSmoothTime = 0.15f;

    Vector3 _baseOffset;     // 시작 시 카메라-타겟 월드 오프셋(45°/15m 프레이밍 보존)
    float _velX, _velZ;      // 축별 SmoothDamp 속도 상태(Y 변위가 XZ에 안 섞이게 분리)
    bool _ready;

    /// <summary>타겟 지정(스포너가 런타임에 호출) — 현재 카메라-타겟 오프셋(45°/15m 프레이밍)을 즉시 캡처해 추종 무장.</summary>
    public void SetTarget(Transform t)
    {
        target = t;
        if (target == null) { _ready = false; return; }
        _baseOffset = transform.position - target.position;
        _ready = true;
    }

    void Start()
    {
        if (_ready) return;   // SetTarget이 이미 무장했으면 자동 검색 생략.
        if (target == null)
        {
            var pc = FindObjectOfType<LabPlayerController>();
            if (pc != null) target = pc.transform;
        }
        if (target == null) { Debug.LogWarning("[LabSimpleCamera] target 미설정 — LabPlayerController 미발견"); return; }

        // 현재 카메라 트랜스폼이 곧 프레이밍(빌더가 45°/15m로 배치). 그 오프셋을 보존.
        _baseOffset = transform.position - target.position;
        _ready = true;
    }

    void LateUpdate()
    {
        if (!_ready || target == null) return;

        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        if (dt <= 0f) return;

        Vector3 p = target.position;
        Vector3 desired = p + _baseOffset;
        desired.y = p.y + _baseOffset.y;   // 높이 즉시 고정(상하 흔들림 방지)

        Vector3 cur = transform.position;
        float smooth = Mathf.Max(0.0001f, followSmoothTime);
        float nx = Mathf.SmoothDamp(cur.x, desired.x, ref _velX, smooth, Mathf.Infinity, dt);
        float nz = Mathf.SmoothDamp(cur.z, desired.z, ref _velZ, smooth, Mathf.Infinity, dt);
        transform.position = new Vector3(nx, desired.y, nz);
        // 회전은 빌더가 잡은 피치 그대로 유지 — 건드리지 않는다.
    }
}
