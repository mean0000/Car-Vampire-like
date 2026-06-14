// VFX 렌더 계층 단일 진실원 — 흩어진 renderQueue / sortingOrder 하드코딩을 이 상수로 교체.
//
// ════════ 위계 원칙 (Hades II 교훈: 경고가 항상 플레이어 이펙트 위에 있어야 함) ════════
//   지면 텔레그래프(최상) > 컨택 임팩트/충격파 > 플레이어 슬래시/범위 > 투사체/파티클
//   처치 킬버스트는 "처치 확인"이므로 경고보다 위 = 최상위.
//
// ════════ 렌더큐 맵 ════════
//   2999 : 바닥 그을림 / 잔흔(임팩트 레이어 아래)
//   3000 : 지면 장판 텔레그래프(ThreatArc), 슬래시 범위 SDF, 먼지 파티클   ← PickupInfoOverlay 필터
//   3050 : 슬래시 궤적 메시(ThreatArc 범위보다 위 — 궤적이 위에 얹힘)
//   3060 : 슬래시 스파크
//   3100 : 플레이어 레이저/LineRenderer 정보선
//   3101 : 레이저 코어(3100보다 위 — 실선이 뚜렷하게)
//   3600 : 처치 킬버스트(최상위 — 처치 확인)
//
// ════════ 캔버스 sortingOrder 맵 ════════
//   50  : RunLoop / 결과창 캔버스(HudV2 위)
//   150 : PurgeSnapshotFX(화이트아웃 = HUD까지 덮어야 "1컷")  ← 기존 CanvasOrder 200 수정 후보
//   200 : RearThreatHint(후방 힌트) / 기존 코드와 일치
//
// ★사용 방법: 하드코딩 수치를 이 상수 참조로 점진 교체.
//   새 VFX를 추가할 때 반드시 이 파일에 상수를 먼저 등록하고, 값 이유를 주석으로 명시.
//   이 파일에 없는 값을 코드에 직접 박으면 나중에 우선순위 충돌 디버그가 지옥이 된다.
public static class VfxLayers
{
    // ── 렌더큐 (RenderQueue 범위: Transparent = 3000~3999) ──

    /// <summary>바닥 잔흔 / 그을림 — 지면 장판보다 먼저 그려져 아래 깔림.</summary>
    public const int Scorch = 2999;

    /// <summary>지면 ThreatArc 텔레그래프 장판, 슬래시 범위 SDF, 먼지 파티클.
    /// PickupInfoOverlay RenderObjects(이벤트 600)의 필터 큐 = 3000이 기준점.</summary>
    public const int TelegraphFloor = 3000;

    /// <summary>충격파 링(SmashShock) — 텔레그래프와 같은 레이어(동시에 보이는 별개 이펙트).</summary>
    public const int ImpactShock = 3000;

    /// <summary>슬래시 궤적 메시(Vefects) — ThreatArc 범위 SDF 위에 얹힘.</summary>
    public const int SlashTrail = 3050;

    /// <summary>슬래시 스파크 버스트.</summary>
    public const int SlashSpark = 3060;

    /// <summary>플레이어 레이저 / LineRenderer 정보선(겨냥선·거합 레인).</summary>
    public const int PlayerInfoLine = 3100;

    /// <summary>레이저 코어 실선 — PlayerInfoLine보다 위, 뚜렷하게.</summary>
    public const int PlayerInfoLineCore = 3101;

    /// <summary>처치 킬버스트 — 처치 확인이 최우선, 최상위.</summary>
    public const int KillBurst = 3600;

    // ── 캔버스 sortingOrder ──

    /// <summary>일반 런루프 / 결과 캔버스 — HudV2 위.</summary>
    public const int CanvasRunLoop = 50;

    /// <summary>후방 위협 힌트 캔버스. ★실측 = RearThreatHint.cs const CanvasOrder = 10(실파일 우선, 이 SSOT를 따름).</summary>
    public const int CanvasRearHint = 10;

    /// <summary>처리 스냅샷 화이트아웃 캔버스 — HUD까지 덮어야 "1컷". 기존 코드와 일치.</summary>
    public const int CanvasPurge = 200;
}
