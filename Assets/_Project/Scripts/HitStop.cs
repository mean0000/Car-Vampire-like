using UnityEngine;

/// <summary>
/// 전역 마이크로 히트스탑 — 수십 ms의 시간 정지로 "한 발이 박혔다"의 무게를 만든다.
/// ★시간의 사다리(2026-06-11): 수렴샷 명중 60ms 정지 < Zone 클리어 0.2s 슬로우 < 산데비스탄(전유).
/// ★연사 매발 호출 금지 — 10Hz 정지는 스터터(카메라 쉐이크 멀미의 시간판). 수렴샷·킬급 이벤트 전용.
/// 카메라(PlayerCameraRig)는 unscaled라 정지 중에도 정상 반응. 좀비 측 경직(TakeBulletHit의 hitStop)과는 별개 레이어.
/// 사용: HitStop.Do(0.06f) — 필요 시 자동 생성(씬 로컬, DDOL 아님 — runloop_pitfalls).
/// </summary>
public class HitStop : MonoBehaviour
{
    static HitStop _inst;
    const float ActiveTimeScale = 0.05f;   // 정지 중 timeScale(완전 0은 물리/이펙트 얼어붙은 티).

    float _resumeAt;          // unscaled 기준 복귀 시각
    bool _active;

    /// <summary>지금 히트스탑이 timeScale을 잡고 있나 — 잡고 있으면 그 스케일, 아니면 1. 다른 timeScale 연출
    /// (ParrySlowMotion 클래시 프리즈 등)이 자기 값과 Max로 조율해 서로 0으로 덮어쓰지 않게(소유권 경합 완화).</summary>
    public static float ActiveScale => (_inst != null && _inst._active) ? ActiveTimeScale : 1f;

    /// <summary>seconds 동안 시간 정지(스케일 0.05 — 완전 0은 물리/이펙트가 얼어붙은 티가 남). 중첩 시 더 긴 쪽으로 연장.</summary>
    public static void Do(float seconds)
    {
        if (seconds <= 0f) return;
        if (_inst == null)
        {
            var go = new GameObject("HitStop");
            _inst = go.AddComponent<HitStop>();
        }
        _inst.Begin(seconds);
    }

    void Begin(float seconds)
    {
        // 다른 시간 연출(Zone 슬로우 등)이 생기면 여기서 소유권 조율 — 현재는 단독 소유.
        if (!_active) Time.timeScale = ActiveTimeScale;
        _active = true;
        _resumeAt = Mathf.Max(_resumeAt, Time.unscaledTime + seconds);
    }

    void Update()
    {
        if (_active && Time.unscaledTime >= _resumeAt)
        {
            Time.timeScale = 1f;
            _active = false;
        }
    }

    void OnDestroy()
    {
        // 씬 전환 중 파괴되면 시간을 복구 — 0.05 잔류 사고 방지.
        if (_active) Time.timeScale = 1f;
        if (_inst == this) _inst = null;
    }
}
