using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 속도 언어 지휘자(07-08) — "처리 문법(Clock-Out Latency)"의 킬 측 오케스트레이션.
/// 속도 = 접수→종결 확인의 빠르기. 이 컴포넌트는 킬(EnemyDamageReceiver.AnyDied)을 받아
/// 종결 확인을 3티어로 낸다(티어드 주스 — 호드 밀도에 스케일):
///   잡몹 킬   → 티켓 스탬프만("№0042 처리 종결" — P5 계보: UI가 속도를 연기)
///   멀티킬    → + 임팩트 프레임(처리 스냅샷 + 집중선)
///   엘리트 킬 → + 카메라(시네마틱 펀치인) + 히트스탑 (유저 픽 07-07, ZZZ 계보 축소판 — 컷 대신 펀치인: 쿼터뷰 가독성 보호)
///
/// 배선: 씬에 이 컴포넌트 1개(랩 판정용 — RunFeel_Whitebox). UI/폰트 전부 코드 생성(PurgeSnapshotFX 패턴, 씬 의존 0).
/// ⚠️폰트 = 맑은 고딕 OS 동적(랩 플레이스홀더 — 본게임 승격 시 Pretendard SDF + Story 카피 패스).
/// ⚠️정적 이벤트 구독 — OnEnable/OnDisable 짝맞춤(누수 방지, EnemyDamageReceiver 주석 계약).
/// 레퍼 보드 = docs/02_logs/2026-07-08-anime-speed-notation-reference-board.md (판정 전 동결 금지).
/// </summary>
public class SpeedLanguageDirector : MonoBehaviour
{
    [Header("티어 판정")]
    [Tooltip("이 시간(초, unscaled) 안에 이 수만큼 죽으면 멀티킬 — 임팩트 프레임 발화.")]
    [SerializeField, Min(2)] int multikillCount = 3;
    [SerializeField, Min(0.02f)] float multikillWindow = 0.12f;
    [Tooltip("최대 HP가 이 이상이면 엘리트로 판정(스포너 주입 HP 기준). 랩 잡몹은 보통 2~4.")]
    [SerializeField, Min(1)] int eliteHpThreshold = 8;
    [Tooltip("★랩 판정용 — N킬마다 엘리트 취급(HP 무관). 0=끄기. 본게임 승격 시 0으로.")]
    [SerializeField, Min(0)] int debugEveryNthElite = 7;

    [Header("임팩트 프레임 (처리 스냅샷)")]
    [Tooltip("표기 스킨 — A=PaperInk(종이+잉크)/B=SignalCollapse(블랙아웃+마젠타). 플레이 중 F9 토글. 팔레트 캡처 게이트 A/B.")]
    [SerializeField] PurgeSnapshotFX.Skin skin = PurgeSnapshotFX.Skin.SignalCollapse;
    [Tooltip("멀티킬에 임팩트 프레임을 낼까.")]
    [SerializeField] bool snapshotOnMultikill = true;
    [Tooltip("엘리트 킬에 임팩트 프레임을 낼까.")]
    [SerializeField] bool snapshotOnElite = true;

    [Header("엘리트 카메라 (처형 확정 순간 한정 — 상시 금지)")]
    [Tooltip("시네마틱 상태 진입 세기(0~1) — PlayerCameraFollow.EngageCinematic.")]
    [SerializeField, Range(0f, 1f)] float eliteCinematicWeight = 0.55f;
    [Tooltip("시네마틱 유지(초) 후 자동 복귀.")]
    [SerializeField, Min(0f)] float eliteCinematicHold = 0.4f;
    [Tooltip("FOV 줌펀치(도) — 위치 불변·빠른 원복.")]
    [SerializeField, Min(0f)] float eliteFovPunch = 5f;
    [Tooltip("엘리트 킬 히트스탑(초). 상한 0.08(EnemyDamageReceiver.MaxHitStop 규율).")]
    [SerializeField, Range(0f, 0.08f)] float eliteHitStop = 0.08f;

    [Header("티켓 UI (종결 스탬프)")]
    [Tooltip("티켓 잔류(초, unscaled) 후 페이드.")]
    [SerializeField, Min(0.2f)] float ticketLifetime = 0.9f;
    [Tooltip("슬라이드 인 시간(초) — P5 계보: 빠르게 꽂혀야 스탬프다.")]
    [SerializeField, Min(0.03f)] float slideTime = 0.09f;
    [SerializeField, Min(2)] int maxTickets = 6;
    [Tooltip("잡몹 티켓 색 — 시안(색 캐넌: 나=시안 액션).")]
    [SerializeField] Color ticketColor = new Color(0.24f, 0.96f, 1f);
    [Tooltip("엘리트 티켓 색 — 금(색 캐넌: 보상/수확).")]
    [SerializeField] Color eliteTicketColor = new Color(1f, 0.83f, 0.30f);
    [Tooltip("연속 처리 판정 창(초, unscaled) — 이 안에 다음 킬이 오면 ×N 증가.")]
    [SerializeField, Min(0.3f)] float streakWindow = 1.6f;

    // ── 런타임 상태 ──
    int _killCount;
    int _streak;
    float _lastKillTime = float.NegativeInfinity;
    readonly Queue<float> _recentKills = new Queue<float>();   // 멀티킬 창 판정(unscaled 시각)

    // ── 티켓 UI 자원(코드 생성) ──
    class Ticket
    {
        public RectTransform rt;
        public CanvasGroup cg;
        public Text label;
        public Sequence seq;      // 생명주기(스케일 팝→잔류→페이드) 전용
        public Tween posTween;    // ★위치 전용 단일 핸들(Stab H-1≡Codex M-1) — 같은 anchoredPosition을 두 트윈이 잡으면 늦게 끝난 쪽이 슬롯을 되돌린다. 재트윈 전 반드시 Kill.
        public int slot;
        public bool live;
    }

    Canvas _canvas;
    RectTransform _column;
    Text _streakLabel;
    Tween _streakTween;
    readonly List<Ticket> _tickets = new List<Ticket>();
    static Font s_font;

    const int CanvasOrder = 150;      // HUD(0) 위, 처리 스냅샷(200) 아래 — 1컷 순간엔 스냅샷이 티켓까지 덮는 게 맞다
    const float LineW = 300f;
    const float LineH = 34f;
    const float ColumnTilt = -3f;     // P5 계보 — 수평 정렬은 서류함, 살짝 기운 게 스탬프

    void OnEnable()
    {
        // ★Stab M-3: 도메인 리로드 off 환경 대비 — 세션 카운터를 명시 리셋(디버그 Nth 엘리트 케이던스 어긋남 방지)
        _killCount = 0;
        _streak = 0;
        _lastKillTime = float.NegativeInfinity;
        _recentKills.Clear();
        EnemyDamageReceiver.AnyDied += OnAnyDied;
        PurgeSnapshotFX.SetSkin(skin);
        if (_canvas == null) BuildUi();
    }

    void OnDisable()
    {
        EnemyDamageReceiver.AnyDied -= OnAnyDied;   // ★정적 이벤트 — 반드시 짝맞춰 해제
        foreach (var t in _tickets) Despawn(t);     // ★Stab L-1: 트윈 kill+비활성까지 완전 idle — disable→enable 시 유령 티켓 잔존 방지
        _streakTween?.Kill();
    }

    void Update()
    {
        // F9 = 스킨 A/B 라이브 토글(팔레트 캡처 게이트 판정용) — 개발 전용(릴리스 빌드 숨은 치트키 차단)
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Input.GetKeyDown(KeyCode.F9))
        {
            skin = skin == PurgeSnapshotFX.Skin.PaperInk
                ? PurgeSnapshotFX.Skin.SignalCollapse : PurgeSnapshotFX.Skin.PaperInk;
            PurgeSnapshotFX.SetSkin(skin);
            SpawnTicket(skin == PurgeSnapshotFX.Skin.SignalCollapse ? "표기 전환 — 신호 붕괴" : "표기 전환 — 종이 기록",
                        ticketColor, false);
        }
#endif

        // 연속 처리 만료 — 창 지나면 ×N 접기
        if (_streak > 0 && Time.unscaledTime - _lastKillTime > streakWindow)
        {
            _streak = 0;
            if (_streakLabel != null)
            {
                _streakTween?.Kill();
                _streakTween = _streakLabel.DOFade(0f, 0.2f).SetUpdate(true);
            }
        }
    }

    void OnValidate()
    {
        if (Application.isPlaying) PurgeSnapshotFX.SetSkin(skin);   // 인스펙터 스킨 변경 즉시 반영
    }

    // ──────────── 킬 수신 → 3티어 발화 ────────────

    void OnAnyDied(EnemyDamageReceiver victim)
    {
        if (victim == null || _canvas == null) return;
        float now = Time.unscaledTime;
        _killCount++;

        // 연속 처리(스트릭) — 케이던스의 가시화
        _streak = (now - _lastKillTime <= streakWindow) ? _streak + 1 : 1;
        _lastKillTime = now;

        // 멀티킬 창 — 오래된 킬 배출 후 적재
        while (_recentKills.Count > 0 && now - _recentKills.Peek() > multikillWindow) _recentKills.Dequeue();
        _recentKills.Enqueue(now);

        bool elite = victim.MaxHp >= eliteHpThreshold
                     || (debugEveryNthElite > 0 && _killCount % debugEveryNthElite == 0);
        bool multikill = _recentKills.Count >= multikillCount;

        // 베기 방향 — 가해 원점→피해자(방향성 붕괴와 동일 소스). 원점 미기록(제로)이면 전방 폴백.
        Vector3 pos = victim.transform.position;
        Vector3 dir = pos - victim.LastHitFrom;
        dir.y = 0f;
        if (dir.sqrMagnitude < 1e-4f) dir = Vector3.forward; else dir.Normalize();

        // 티어 1 — 티켓은 매 킬(종결 확인은 전수. 접수 없는 처리 없음)
        SpawnTicket(elite ? $"№{_killCount:0000}  특이개체 종결" : $"№{_killCount:0000}  처리 종결",
                    elite ? eliteTicketColor : ticketColor, elite);
        UpdateStreakLabel();

        // 티어 2 — 임팩트 프레임(스냅샷 자체 발작 가드 0.5s가 연발을 걸러준다)
        // ★Codex M-3: 삼항이면 엘리트 토글 off가 유효한 멀티킬 발화까지 삼킨다 — 두 조건 OR 합성(독립 발화)
        bool fireSnapshot = (elite && snapshotOnElite) || (multikill && snapshotOnMultikill);
        if (fireSnapshot)
        {
            PurgeSnapshotFX.Play(pos, dir);
            if (multikill) _recentKills.Clear();   // 한 버스트 1컷 — 창 잔량으로 후속 킬마다 재발화 방지
        }

        // 티어 3 — 엘리트 카메라(처형 확정 순간 한정)
        if (elite)
        {
            var cam = PlayerCameraFollow.Active;
            if (cam != null)
            {
                cam.EngageCinematic(eliteCinematicWeight, eliteCinematicHold);
                cam.AddFovPunch(eliteFovPunch);
            }
            HitStop.Do(eliteHitStop);
        }
    }

    // ──────────── 티켓 UI(코드 생성 — PurgeSnapshotFX 패턴) ────────────

    void BuildUi()
    {
        var canvasGO = new GameObject("SpeedLanguageCanvas");
        canvasGO.transform.SetParent(transform, false);
        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = CanvasOrder;

        // 티켓 컬럼 — 우상단 고정, 살짝 기울임
        var colGO = new GameObject("TicketColumn");
        colGO.transform.SetParent(canvasGO.transform, false);
        _column = colGO.AddComponent<RectTransform>();
        _column.anchorMin = _column.anchorMax = new Vector2(1f, 1f);
        _column.pivot = new Vector2(1f, 1f);
        _column.anchoredPosition = new Vector2(-28f, -28f);
        _column.localRotation = Quaternion.Euler(0f, 0f, ColumnTilt);

        // 연속 처리 라벨 — 컬럼 아래 ×N
        var stGO = new GameObject("Streak");
        stGO.transform.SetParent(colGO.transform, false);
        _streakLabel = stGO.AddComponent<Text>();
        _streakLabel.font = UiFont();
        _streakLabel.fontSize = 40;
        _streakLabel.fontStyle = FontStyle.Bold;
        _streakLabel.alignment = TextAnchor.UpperRight;
        _streakLabel.color = new Color(ticketColor.r, ticketColor.g, ticketColor.b, 0f);
        _streakLabel.raycastTarget = false;
        var srt = _streakLabel.rectTransform;
        srt.anchorMin = srt.anchorMax = new Vector2(1f, 1f);
        srt.pivot = new Vector2(1f, 1f);
        srt.sizeDelta = new Vector2(LineW, 48f);
        srt.anchoredPosition = new Vector2(0f, -(maxTickets * LineH) - 14f);
    }

    /// <summary>티켓 스탬프 1장 — 기존 티켓 한 칸씩 밀고 슬롯 0에 고속 슬라이드 인.</summary>
    void SpawnTicket(string text, Color color, bool elite)
    {
        // 기존 티켓 한 칸 아래로 — 넘치면 즉시 회수
        foreach (var t in _tickets)
        {
            if (!t.live) continue;
            t.slot++;
            if (t.slot >= maxTickets) { Despawn(t); continue; }
            // ★Stab H-1≡Codex M-1: 위치는 posTween 단일 소유 — 진행 중 슬라이드인까지 kill 후 X·Y 풀 좌표로 재트윈
            //   (Y만 트윈하면 슬라이드인 도중 밀린 티켓의 X가 화면 밖 잔량에 고착된다)
            t.posTween?.Kill();
            t.posTween = t.rt.DOAnchorPos(new Vector2(0f, -t.slot * LineH), 0.06f).SetUpdate(true);
        }

        var tk = GetPooled();
        tk.live = true;
        tk.slot = 0;
        tk.label.text = text;
        tk.label.color = color;
        tk.label.fontSize = elite ? 26 : 22;
        tk.cg.alpha = 1f;
        tk.rt.anchoredPosition = new Vector2(220f, 0f);   // 오른쪽 화면 밖에서
        tk.rt.localScale = Vector3.one * 1.15f;
        tk.rt.gameObject.SetActive(true);

        tk.seq?.Kill();
        tk.posTween?.Kill();
        // 위치(슬라이드인)는 posTween, 생명주기(팝→잔류→페이드)는 seq — 소유 분리(Stab H-1 처방)
        tk.posTween = tk.rt.DOAnchorPos(Vector2.zero, slideTime).SetEase(Ease.OutCubic).SetUpdate(true);
        tk.seq = DOTween.Sequence().SetUpdate(true)   // unscaled — 킬은 대개 히트스탑(0.05×) 한복판에서 온다
            .Append(tk.rt.DOScale(1f, slideTime).SetEase(Ease.OutBack))
            .AppendInterval(ticketLifetime)
            .Append(tk.cg.DOFade(0f, 0.15f))
            .OnComplete(() => Despawn(tk));
    }

    void UpdateStreakLabel()
    {
        if (_streakLabel == null || _streak < 2) return;
        _streakLabel.text = $"연속 처리 ×{_streak}";
        _streakTween?.Kill();
        var c = _streakLabel.color; c.a = 1f;
        _streakLabel.color = c;
        _streakLabel.rectTransform.localScale = Vector3.one * 1.3f;
        _streakTween = _streakLabel.rectTransform.DOScale(1f, 0.14f).SetEase(Ease.OutBack).SetUpdate(true);
    }

    Ticket GetPooled()
    {
        foreach (var t in _tickets)
            if (!t.live) return t;

        var go = new GameObject("Ticket");
        go.transform.SetParent(_column, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(LineW, LineH - 4f);

        var cg = go.AddComponent<CanvasGroup>();

        var bg = go.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.55f);   // 검은 슬리버 — 세계 채도 억제 위에서 글자가 뜨게
        bg.raycastTarget = false;

        var lGO = new GameObject("Label");
        lGO.transform.SetParent(go.transform, false);
        var label = lGO.AddComponent<Text>();
        label.font = UiFont();
        label.alignment = TextAnchor.MiddleRight;
        label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        var lrt = label.rectTransform;
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = new Vector2(10f, 0f); lrt.offsetMax = new Vector2(-12f, 0f);

        var tk = new Ticket { rt = rt, cg = cg, label = label };
        _tickets.Add(tk);
        return tk;
    }

    void Despawn(Ticket t)
    {
        t.seq?.Kill();
        t.seq = null;
        t.posTween?.Kill();   // ★Codex M-1: 위치 트윈도 회수 — 풀 재사용 시 유령 이동 방지
        t.posTween = null;
        t.live = false;
        if (t.rt != null) t.rt.gameObject.SetActive(false);
    }

    /// <summary>랩 플레이스홀더 폰트 — 맑은 고딕 OS 동적(한글 렌더). 본게임 승격 시 Pretendard SDF로 교체.</summary>
    static Font UiFont()
    {
        if (s_font == null) s_font = Font.CreateDynamicFontFromOSFont("Malgun Gothic", 32);
        if (s_font == null)
        {
            try { s_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
            catch { /* 빌트인 부재 환경 — 아래 경고로 표면화 */ }
        }
        if (s_font == null) Debug.LogWarning("[SpeedLanguageDirector] UI 폰트 확보 실패 — 티켓 텍스트가 렌더되지 않는다(배경 슬리버만).");   // ★Codex L-1: 조용한 무렌더 방지
        return s_font;
    }
}
