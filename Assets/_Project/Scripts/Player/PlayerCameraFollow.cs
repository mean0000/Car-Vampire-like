using UnityEngine;

/// <summary>
/// 톱다운 추적 카메라 — 플레이어를 고정 각도(하강 pitch)·고정 거리에서 부드럽게 따라간다.
/// 회전은 고정(맵 절대 방향 유지 — 카메라가 돌면 조작 방향이 흔들려 멀미), 위치만 추종.
/// 게임 카메라 확정값: pitch 45도 / distance 15m(2026-06-10 킬 도파민 세션).
///
/// ★전투 임팩트 킥(2026-06-21): 칼이 적에 닿는 순간 카메라를 조준 방향으로 짧게 밀었다 복귀 = 스냅 손맛의 주력 채널.
///   ★unscaledDeltaTime로 감쇠 — 히트스탑(timeScale 0.05) 중에도 킥이 살아 "박혔다"의 펀치가 보인다.
///   기저 추종(SmoothDamp)과 분리: 추종 결과(_followPos) 위에 킥 오프셋을 얹어 피드백 루프를 막는다.
/// </summary>
public class PlayerCameraFollow : MonoBehaviour
{
    /// <summary>현재 활성 카메라(전투 코드가 킥을 쏠 진입점). HitStop과 동일한 정적 접근 패턴.</summary>
    public static PlayerCameraFollow Active { get; private set; }

    [SerializeField] Transform target;
    [Tooltip("하강 각도(도). 45 = 비스듬한 톱다운.")]
    [SerializeField] float pitch = 45f;
    [Tooltip("타깃으로부터 거리(m).")]
    [SerializeField] float distance = 15f;
    [Tooltip("수평 방위각(도). 0 = 정북에서 내려봄.")]
    [SerializeField] float yaw = 0f;
    [Tooltip("추종 부드러움(작을수록 즉각, 클수록 느슨).")]
    [SerializeField] float followSmooth = 0.12f;

    [Header("전투 임팩트 킥 (스냅 손맛)")]
    [Tooltip("임팩트 킥 복귀 속도(지수 감쇠율) — 클수록 빨리 제자리로(스냅). 14 ≈ 시정수 0.07s. 너무 크면 1프레임만 보임.")]
    [SerializeField] float impactKickReturn = 14f;
    [Tooltip("임팩트 킥 누적 상한(m) — 연타 시 킥이 무한 누적돼 카메라가 날아가는 것 방지.")]
    [SerializeField] float impactKickMax = 0.6f;

    Vector3 _vel;
    Vector3 _followPos;     // 킥을 뺀 순수 추종 위치(SmoothDamp 대상 — 킥과 분리해 피드백 루프 차단)
    Vector3 _impactKick;    // 전투 임팩트 킥 오프셋(unscaled 감쇠)
    bool _followInit;

    void OnEnable()  { Active = this; }
    void OnDisable() { if (Active == this) Active = null; }

    /// <summary>런타임에 타깃을 바꿔 끼울 때(플레이어 스폰 후 와이어링).</summary>
    public void SetTarget(Transform t) { target = t; }

    /// <summary>전투 임팩트 킥 — worldDir 방향으로 카메라를 amount(m)만큼 즉시 민다(decay로 복귀).
    /// 칼이 적에 닿는 순간 호출(KatanaWeapon). 누적 상한으로 클램프해 연타 폭주 방지.</summary>
    public void AddKick(Vector3 worldDir, float amount)
    {
        if (amount <= 0f) return;
        worldDir.y = 0f;
        if (worldDir.sqrMagnitude < 0.0001f) return;
        _impactKick = Vector3.ClampMagnitude(_impactKick + worldDir.normalized * amount, impactKickMax);
    }

    void LateUpdate()
    {
        if (target == null) return;
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 desired = target.position + rot * (Vector3.back * distance);

        // 기저 추종 — 킥을 뺀 위치를 SmoothDamp(킥과 분리해 피드백 루프 차단).
        if (!_followInit) { _followPos = desired; _followInit = true; }
        _followPos = Vector3.SmoothDamp(_followPos, desired, ref _vel, followSmooth);

        // ★킥을 먼저 적용한 뒤(이번 프레임 풀 세기로 보임) 감쇠 — 같은 프레임에 추가→감쇠로 킥이 안 보이던 버그 수정(Codex P1).
        transform.position = _followPos + _impactKick;
        transform.rotation = rot;

        // 임팩트 킥 지수 감쇠(프레임률 독립) — 적용 후. unscaled라 다른 시간 연출(슬로모 등)과 독립.
        _impactKick = Vector3.Lerp(_impactKick, Vector3.zero, 1f - Mathf.Exp(-impactKickReturn * Time.unscaledDeltaTime));
    }
}
