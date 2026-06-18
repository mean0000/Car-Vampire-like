using UnityEngine;

/// <summary>
/// 마우스 스크린 좌표를 플레이어 발밑 높이의 수평면에 투영해 조준 방향(facing)을 만든다.
/// 톱다운 트윈스틱 — 몸은 항상 조준을 향하고(회전은 비주얼 드라이버가, 루트는 안 돈다 = 자식 카메라 보호),
/// 이동(어디로 가는가)과 독립한다.
///
/// <see cref="PlayerBrain"/>이 Motor보다 먼저 Tick해, 같은 프레임의 대시 방향 폴백·공격 방향이
/// 항상 최신 조준을 본다(1프레임 지연 방지).
/// </summary>
public class PlayerAim : MonoBehaviour
{
    [Tooltip("조준 평면 높이 오프셋(플레이어 발밑 기준 m). 0 = 발밑 평면.")]
    [SerializeField] float aimPlaneHeight = 0f;

    Camera _cam;
    Vector3 _direction = Vector3.forward;

    /// <summary>수평 정규화된 조준 방향(facing). Motor·Weapon이 읽는다.</summary>
    public Vector3 Direction => _direction;
    /// <summary>커서가 지면 평면과 만나는 점(월드). 조준 마커·사거리 산출용.</summary>
    public Vector3 GroundPoint { get; private set; }

    void Awake() => _cam = Camera.main;

    /// <summary>PlayerBrain이 매 프레임 가장 먼저 호출.</summary>
    public void Tick(in PlayerInputState input)
    {
        if (_cam == null) { _cam = Camera.main; if (_cam == null) return; }

        Ray ray = _cam.ScreenPointToRay(input.aimScreen);
        float planeY = transform.position.y + aimPlaneHeight;

        // 수평면 y=planeY 와의 교차점. 카메라가 평면을 향해 내려다보므로 ray.direction.y < 0.
        if (Mathf.Abs(ray.direction.y) < 1e-5f) return;   // 평면과 평행 — 갱신 스킵(직전 방향 유지)
        float t = (planeY - ray.origin.y) / ray.direction.y;
        if (t <= 0f) return;                               // 평면이 카메라 뒤 — 스킵

        Vector3 hit = ray.origin + ray.direction * t;
        GroundPoint = hit;

        Vector3 dir = hit - transform.position; dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f) _direction = dir.normalized;
    }
}
