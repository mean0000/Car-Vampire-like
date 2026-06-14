using System;
using UnityEngine;

/// <summary>
/// 전투 → 런/성장 시스템 단방향 이벤트 허브. ★연동 계약의 단일 지점.
/// 전투(ZombieController/PlayerController)는 이 정적 이벤트만 발화 — 런 시스템 내부 의존 0.
/// QuotaRunController(·필요 시 성장)가 구독한다.
///
/// 정적 이벤트라 씬 재로드 시 구독 누수 위험 → 두 겹으로 막는다:
///   ① 구독자(QuotaRunController)가 Begin/End에서 짝맞춰 +=/-= (RunManager 생명주기에 묶임).
///   ② [RuntimeInitializeOnLoadMethod]로 플레이 진입 시 델리게이트를 비운다
///      (Enter Play Mode without Domain Reload 환경에서 이전 세션 구독이 남는 것을 차단).
/// </summary>
public static class CombatEvents
{
    /// <summary>이상개체 1건 처리(처치) 시 발화. 인자 = 그 개체의 위협 분류 티어(변이/가중용, 0=기본).
    /// ★할당량 카운터가 이걸 센다. 발화 위치 = ZombieController.Die()/DieByWeapon() 내부 1줄.</summary>
    public static event Action<int> OnAnomalyProcessed;

    /// <summary>이상개체 처리 발화. threatTier 기본값 0(분류 변이는 Phase 2 — P7 판정).</summary>
    public static void RaiseAnomalyProcessed(int threatTier = 0) => OnAnomalyProcessed?.Invoke(threatTier);

    // 사망은 PlayerController.OnPlayerDied 기존 이벤트 재사용 — 중복 채널 만들지 않음(스펙 §6.2).

    /// <summary>플레이 진입 시 정적 구독을 초기화한다(도메인 리로드 비활성 환경 안전).</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => OnAnomalyProcessed = null;
}
