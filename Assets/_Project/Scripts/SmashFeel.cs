// 슬램 임팩트 게임감(Feel) 브로드캐스트 — 카메라 쉐이크 + 히트스탑(프레임 정지).
//   ★유저 피드백(2026-06-14): "덩치 큰데 위협이 안 됨" → 닿는 순간의 *물리적 무게*를 화면 흔들림 + 시간 정지로.
//
// ════════ ★★히트스탑 = 프로젝트 네이티브 HitStop.Do() 재사용 (기존 권위 재정합) ════════
//   ★MMFreezeFrameEvent(timeScale=0)로 자체 시간 정지를 짓지 않는다 — 프로젝트엔 이미 HitStop.Do(seconds)가 있고
//    (timeScale=0.05·중첩 연장·OnDestroy 복원 가드·"시간의 사다리" 도큐멘트), 두 시간 소유자가 Time.timeScale을
//    두고 싸우면 복원 경합 사고가 난다(Codex/Stab 지적 + HitStop.cs 주석 "다른 시간 연출 생기면 소유권 조율").
//   → SmashFeel.HitStop은 HitStop.Do()로 위임 = 단일 시간 소유자. timeScale 복원 안전(OnDestroy 가드 상속).
//   ★HitStop은 timeScale=0.05(완전 0 아님)이라 정지 중에도 이펙트/물리가 미세하게 흘러 "얼어붙은 티" 없음 — 영구정지 불가능.
//
// ════════ 카메라 쉐이크 = LabCameraShake 오프셋 합성 ════════
//   ★(구) Feel MMCameraShakeEvent → MMCameraShaker(MMWiggle localPosition)는 *무효였다*(Stab H-1/H-2):
//    추종 카메라(LabSimpleCamera)가 매 프레임 transform.position을 덮어써 흔들림을 지움 + MMWiggle.PositionActive 기본 false.
//   → SmashFeel.Shake는 LabCameraShake.Add로 임펄스만 넣고, LabSimpleCamera가 *추종값 위에* 오프셋으로 합성(SmoothDamp 피드백 차단).
//    리스너/씬 오브젝트 불필요 = 씬 안 더티(병렬 세션 보호 · 랩 부트스트랩 철학 유지).
using UnityEngine;

public static class SmashFeel
{
    /// <summary>스포너 호환용 유지 — ★쉐이크는 이제 LabCameraShake 오프셋(LabSimpleCamera가 합성)이라 리스너 셋업 불필요.
    /// (Feel MMCameraShaker는 추종 카메라가 position을 덮어써 무효였음 — Stab H-1/H-2.) 호출은 무해 no-op.</summary>
    public static void EnsureListeners(Camera shakeCamera) { }

    /// <summary>카메라 쉐이크 — ★LabCameraShake 오프셋으로 위임(LabSimpleCamera가 추종값 위에 합성). amplitude=오프셋 크기(월드 m),
    /// frequency=진동 빈도, duration=지속(초). unscaledTime이라 히트스탑(timeScale 0.05) 중에도 진동(쾅 정지+흔들림 동시 = 무게).
    /// ★amplitude가 직접 월드미터 오프셋이라(이전 Feel 단위와 다름) 강도 재튜닝 필요할 수 있음 — 유저 ▶ 판정.</summary>
    public static void Shake(float duration, float amplitude, float frequency)
    {
        LabCameraShake.Add(duration, amplitude, frequency);
    }

    /// <summary>히트스탑(시간 정지) — ★프로젝트 네이티브 HitStop.Do()로 위임(단일 시간 소유자, 복원 안전).
    /// ★duration은 짧게(0.04~0.06s) — 브루트 연타라 길면 거슬린다. HitStop이 중첩 시 더 긴 쪽으로 연장 처리.</summary>
    public static void HitStop(float duration)
    {
        if (duration <= 0f) return;
        global::HitStop.Do(duration);
    }
}
