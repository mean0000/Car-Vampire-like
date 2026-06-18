using UnityEngine;

/// <summary>
/// 톱다운 추적 카메라 — 플레이어를 고정 각도(하강 pitch)·고정 거리에서 부드럽게 따라간다.
/// 회전은 고정(맵 절대 방향 유지 — 카메라가 돌면 조작 방향이 흔들려 멀미), 위치만 추종.
/// 게임 카메라 확정값: pitch 45도 / distance 15m(2026-06-10 킬 도파민 세션).
/// </summary>
public class PlayerCameraFollow : MonoBehaviour
{
    [SerializeField] Transform target;
    [Tooltip("하강 각도(도). 45 = 비스듬한 톱다운.")]
    [SerializeField] float pitch = 45f;
    [Tooltip("타깃으로부터 거리(m).")]
    [SerializeField] float distance = 15f;
    [Tooltip("수평 방위각(도). 0 = 정북에서 내려봄.")]
    [SerializeField] float yaw = 0f;
    [Tooltip("추종 부드러움(작을수록 즉각, 클수록 느슨).")]
    [SerializeField] float followSmooth = 0.12f;

    Vector3 _vel;

    /// <summary>런타임에 타깃을 바꿔 끼울 때(플레이어 스폰 후 와이어링).</summary>
    public void SetTarget(Transform t) => target = t;

    void LateUpdate()
    {
        if (target == null) return;
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 desired = target.position + rot * (Vector3.back * distance);
        transform.position = Vector3.SmoothDamp(transform.position, desired, ref _vel, followSmooth);
        transform.rotation = rot;
    }
}
