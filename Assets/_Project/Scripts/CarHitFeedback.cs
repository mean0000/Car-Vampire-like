using System.Collections;
using UnityEngine;

/// <summary>좀비 충돌 피드백: 속도 감소·히트스탑·카메라 쉐이크</summary>
[RequireComponent(typeof(Rigidbody))]
public class CarHitFeedback : MonoBehaviour
{
    [SerializeField] private float hitShakeBase    = 0.2f;
    [SerializeField] private float hitShakeMax     = 0.8f;
    [SerializeField] private float minSpeedAfterHit = 3f;
    [SerializeField] private float hitStopScale    = 0.4f;
    [SerializeField] private float hitStopDuration = 0.04f;

    private Rigidbody _rb;
    private CameraController _cam;
    private CarMovement _movement;

    private void Awake()
    {
        _rb       = GetComponent<Rigidbody>();
        _movement = GetComponent<CarMovement>();
        _cam      = GetComponentInChildren<CameraController>();
        if (_cam == null) _cam = FindFirstObjectByType<CameraController>();
    }

    // ZombieCollision에서 호출
    public void ApplySpeedPenalty(float amount)
    {
        if (_rb == null) return;
        Vector3 vel = _rb.linearVelocity;
        float flatSpeed = new Vector3(vel.x, 0f, vel.z).magnitude;
        float floor     = Mathf.Min(flatSpeed, minSpeedAfterHit);
        float reduced   = Mathf.Max(floor, flatSpeed - amount);
        Vector3 flatDir = flatSpeed > 0.001f
            ? new Vector3(vel.x, 0f, vel.z).normalized
            : Vector3.zero;
        _rb.linearVelocity = flatDir * reduced + new Vector3(0f, vel.y, 0f);

        if (_cam != null && _movement != null)
        {
            float maxSpd = _movement.MaxSpeed;
            float ratio  = maxSpd > 0f ? Mathf.Clamp01(flatSpeed / maxSpd) : 0f;
            _cam.TriggerShake(Mathf.Lerp(hitShakeBase, hitShakeMax, ratio));
        }
        StopCoroutine("HitStopRoutine");
        StartCoroutine("HitStopRoutine");
    }

    private IEnumerator HitStopRoutine()
    {
        Time.timeScale = hitStopScale;
        Time.fixedDeltaTime = 0.02f * hitStopScale;
        float elapsed = 0f;
        while (elapsed < hitStopDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        if (!IsPanelOpen())
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
        }
    }

    private void OnDisable()
    {
        if (!IsPanelOpen())
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
        }
    }

    private void OnDestroy()
    {
        if (Time.timeScale < 1f && !IsPanelOpen())
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
        }
    }

    private static bool IsPanelOpen() =>
        (UpgradeMenuUI.Instance != null && UpgradeMenuUI.Instance.IsPanelOpen) ||
        (GameOverUI.Instance     != null && GameOverUI.Instance.IsPanelOpen);
}
