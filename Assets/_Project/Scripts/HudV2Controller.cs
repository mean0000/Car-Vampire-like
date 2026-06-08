using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 인게임 HUD v2 — 목업(_mockups/ui_mockup.html "02 · IN-GAME HUD")을 UGUI로 재현한 단일 드라이버.
/// 이벤트가 있는 위젯은 OnEnable에서 구독/OnDisable에서 해제, 나머지는 Update에서 폴링한다.
/// 레거시 그레이박스 HUD(XPBarUI/SyncRateUI/HUDController/DashChargesUI)를 대체하며,
/// SYNC 비네트도 이 드라이버가 단독으로 구동한다(중복 구동 방지).
///
/// REAL = 라이브 데이터 바인딩 / PLACEHOLDER = 백엔드 없음(코스메틱, 후일 바인딩용 훅 노출).
/// </summary>
public class HudV2Controller : MonoBehaviour
{
    // ───────────────────── 팔레트 (목업 :root) ─────────────────────
    static readonly Color CYAN   = H("#34E3FF");
    static readonly Color CYAND  = H("#0FA8C4");
    static readonly Color AMBER  = H("#FFB02E");
    static readonly Color RED    = H("#FF4242");
    static readonly Color WHITE  = H("#EAF6F8");
    static readonly Color TEXT   = H("#CDD9DC");
    static readonly Color MUTED  = H("#6F8388");
    static readonly Color HP_LO  = H("#3AD07A");
    static readonly Color HP_HI  = H("#7BE0A0");

    // 패널 패닉 펄스 색(SYNC fail 상태 — 목업 panicpulse 키프레임)
    static readonly Color PANIC_A = new Color(40f/255f,  8f/255f,  8f/255f, 0.86f);
    static readonly Color PANIC_B = new Color(120f/255f, 12f/255f, 12f/255f, 0.92f);
    static readonly Color SYNC_PANEL = new Color(16f/255f, 28f/255f, 31f/255f, 0.86f); // --panel

    // ───────────────────── XP (REAL) ─────────────────────
    [Header("XP (REAL — XPManager)")]
    public Image xpFill;
    public TextMeshProUGUI levelLabel;   // "Lv.N" — 목업엔 별도 라벨 없지만 플레이어 카드 캡션과 함께 갱신

    // ───────────────────── SYNC RATE 둠클락 (REAL) ─────────────────────
    [Header("SYNC RATE (REAL — SyncRateManager)")]
    public RectTransform syncPanel;          // 패널 루트(패닉 펄스 배경 대상)
    public Image syncPanelBg;                // 패널 배경 이미지(패닉 펄스 색 구동)
    public TextMeshProUGUI syncLabel;        // "SYNC RATE"
    public TextMeshProUGUI syncPct;          // "NN%" (위험 이상 시 흔들림)
    public Image syncBarFill;                // 바 채움
    public TextMeshProUGUI syncState;        // "상태: 안정 / STABLE" 등
    public Image vignette;                   // 화면 비네트(SYNC 70%+부터 alpha 램프)
    public RawImage scanlines;               // 최상단 스캔라인 오버레이(런타임 텍스처 주입)

    // ───────────────────── 타이머 + 킬 (REAL — RunStats) / 보스바 (PLACEHOLDER) ─────────────────────
    [Header("Run timer + kills (REAL — RunStats)")]
    public TextMeshProUGUI timeLabel;        // "MM:SS"
    public TextMeshProUGUI killsLabel;       // "REMOVED · NNNN"
    int _lastSecs = -1;                      // 변경 시에만 텍스트 재할당(매프레임 GC 방지)
    int _lastKills = -1;

    [Header("Boss bar (PLACEHOLDER — ShowBoss/HideBoss)")]
    public GameObject bossRoot;              // START HIDDEN — 외부에서 ShowBoss로 켬
    public TextMeshProUGUI bossName;
    public TextMeshProUGUI bossPct;
    public Image bossBarFill;

    // ───────────────────── 이벤트 트래커 (PLACEHOLDER — 코스메틱 카운트다운) ─────────────────────
    [Header("Event tracker (PLACEHOLDER — cosmetic countdowns)")]
    public TextMeshProUGUI[] eventTimers = new TextMeshProUGUI[3];
    // 목업 cd(): ev1=45s, ev2=80s, ev3=130s(2:10). 0 미만이면 50~170s 랜덤 재설정.
    readonly float[] _evRemain = { 45f, 80f, 130f };
    float _evTick;

    // ───────────────────── 플레이어 카드 (REAL HP/name/level + 버프) ─────────────────────
    [Header("Player card (REAL — PlayerController / XPManager)")]
    public Image hpFill;
    public TextMeshProUGUI playerName;       // "김도현"
    public TextMeshProUGUI playerSub;        // "현장요원 · LV.N"

    [Header("Buffs (PLACEHOLDER-ish — PlayerStats가 trivial하면 점등)")]
    public Image buffSpd;                    // MoveSpeedMult>1 이면 점등
    public Image buffArm;                    // MaxHPMult>1 이면 점등
    public Image buffDot;                    // 항상 비활성(DoT 시스템 미구현) — 후일 바인딩
    static readonly Color BUFF_ON  = H("#34E3FF");
    static readonly Color BUFF_OFF = new Color(1f, 1f, 1f, 0.18f);

    // ───────────────────── 무기 박스 (REAL 이름) + 탄약 (PLACEHOLDER) + 대시 (REAL) ─────────────────────
    [Header("Weapon box (REAL name — WeaponLoadout)")]
    public TextMeshProUGUI weaponName;       // 무기 이름
    public TextMeshProUGUI weaponIcon;       // "REV" 등 3글자 아이콘
    [Tooltip("미선택 시 폴백 무기 이름.")]
    public string weaponFallback = "리볼버";

    [Header("Ammo (REAL — PlayerCombat)")]
    public TextMeshProUGUI ammoLabel;        // 코너 상세
    public TextMeshProUGUI ammoMini;         // 중앙 미니

    // ───────────────────── 중앙 추종 위젯 (탄약/대시 미니가 플레이어를 매프레임 추적) ─────────────────────
    [Header("Center follow (REAL — 플레이어 머리 위 탄약 / 발 밑 대시)")]
    [Tooltip("탄약 미니(위) + 대시 미니(아래)를 담은 루트. 매 프레임 플레이어 스크린좌표로 이동.")]
    public RectTransform centerFollowRoot;
    [Tooltip("탄약 미니의 머리 위 오프셋(1920 캔버스 px). +는 화면 위.")]
    public float headOffset = 68f;
    [Tooltip("대시 미니의 발 밑 오프셋(1920 캔버스 px). +는 발 아래 거리(내부에서 음수로 적용).")]
    public float feetOffset = 56f;
    Camera _followCam;
    RectTransform _followParent;   // centerFollowRoot의 부모(캔버스 로컬 변환 대상) — Start에서 1회 캐시
    PlayerCombat _combat;          // 플레이어의 전투 컴포넌트(탄약 실데이터) — Start에서 캐시
    bool _warnedNoCombat;          // PlayerCombat 누락 경고 1회 가드

    [Header("Dash pips (REAL — PlayerController)")]
    public Image[] dashPips = new Image[3];      // 코너 상세(skew bar)
    public Image[] dashMiniPips = new Image[3];  // 중앙 미니
    static readonly Color PIP_ON    = H("#34E3FF");
    static readonly Color PIP_OFF   = new Color(0.08f, 0.16f, 0.18f, 1f); // #16323a 근사
    static readonly Color PIP_CHARGE= new Color(0.20f, 0.89f, 1f, 0.45f); // 충전 중 반투명

    // ───────────────────── 부품 토스트 (REAL — CraftingSystem 폴링) ─────────────────────
    [Header("Parts toast (REAL — CraftingSystem.Parts 증가 감지)")]
    public CanvasGroup partsToastGroup;      // "[ PART ] +N 회수"
    public TextMeshProUGUI partsToastLabel;
    public RectTransform partsToastRect;     // 위로 떠오르는 애니메이션 대상
    int _lastParts = -1;
    float _toastTimer;                       // >0 동안 토스트 재생
    Vector2 _toastBasePos;
    const float ToastDur = 1.8f;

    // ───────────────────── 상호작용 프롬프트 (PLACEHOLDER — ShowPrompt/HidePrompt) ─────────────────────
    [Header("Interaction prompt (PLACEHOLDER — ShowPrompt/HidePrompt)")]
    public GameObject promptRoot;            // START HIDDEN
    public TextMeshProUGUI promptLabel;

    // ───────────────────── SYNC 흔들림 대상 ─────────────────────
    Vector2 _syncPctBasePos;
    Vector2 _syncPanelBasePos;
    bool _haveBasePos;

    // ───────────────────── 런타임 생성 텍스처(에셋 직렬화 불가 — 매 실행 빌드) ─────────────────────
    Sprite _vignetteSprite;          // 비네트 라디얼 스프라이트(런타임)
    Texture2D _vignetteTex;          // 위 스프라이트의 텍스처
    Texture2D _scanlineTex;          // 스캔라인 줄무늬 텍스처

    // ───────────────────────────────────────────────────────────

    void Awake()
    {
        BuildVignetteSprite();
    }

    // 비네트 라디얼 스프라이트를 런타임 생성(에디터 베이크 텍스처는 씬 재로드 시 null).
    void BuildVignetteSprite()
    {
        if (vignette == null) return;
        const int s = 256;
        _vignetteTex = new Texture2D(s, s, TextureFormat.RGBA32, false)
        { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color[s * s];
        Vector2 c = new Vector2((s - 1) / 2f, (s - 1) / 2f);
        float maxD = c.x;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c) / maxD;
                // 중심 0 → 0.55부터 램프 → 가장자리 1.
                float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 0.55f) / 0.45f));
                px[y * s + x] = new Color(1f, 1f, 1f, a);
            }
        _vignetteTex.SetPixels(px);
        _vignetteTex.Apply();
        _vignetteSprite = Sprite.Create(_vignetteTex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        vignette.sprite = _vignetteSprite;
        vignette.type = Image.Type.Simple;
    }

    // 스캔라인 줄무늬 텍스처를 런타임 생성(TitleFX.BuildScanlines 미러).
    void BuildScanlineTexture()
    {
        if (scanlines == null) return;
        const int period = 3;
        _scanlineTex = new Texture2D(1, period, TextureFormat.RGBA32, false)
        { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
        var cols = new Color[period];
        cols[0] = new Color(0f, 0f, 0f, 0.14f);
        for (int i = 1; i < period; i++) cols[i] = new Color(0f, 0f, 0f, 0f);
        _scanlineTex.SetPixels(cols);
        _scanlineTex.Apply();
        scanlines.texture = _scanlineTex;
        scanlines.color = Color.white;
        scanlines.uvRect = new Rect(0f, 0f, 1f, 1080f / period);
    }

    void OnDestroy()
    {
        // Sprite.Create/Texture2D는 GC 대상이 아니므로 명시적으로 해제(DashChargesUI 패턴).
        if (_vignetteSprite != null) Destroy(_vignetteSprite);
        if (_vignetteTex != null) Destroy(_vignetteTex);
        if (_scanlineTex != null) Destroy(_scanlineTex);
    }

    void OnEnable()
    {
        if (XPManager.Instance != null)
        {
            XPManager.Instance.OnXPChanged += HandleXPChanged;
            XPManager.Instance.OnLevelChanged += HandleLevelChanged;
        }
        if (SyncRateManager.Instance != null)
            SyncRateManager.Instance.OnSyncChanged += HandleSyncChanged;
    }

    void OnDisable()
    {
        if (XPManager.Instance != null)
        {
            XPManager.Instance.OnXPChanged -= HandleXPChanged;
            XPManager.Instance.OnLevelChanged -= HandleLevelChanged;
        }
        if (SyncRateManager.Instance != null)
            SyncRateManager.Instance.OnSyncChanged -= HandleSyncChanged;
    }

    void Start()
    {
        CacheBasePositions();
        BuildScanlineTexture();

        // 시작 상태 동기화 — 이벤트는 변경 시에만 오므로 초기값을 직접 한 번 반영.
        if (XPManager.Instance != null)
        {
            HandleXPChanged(XPManager.Instance.CurrentXP, XPManager.Instance.GetCurrentThreshold());
            HandleLevelChanged(XPManager.Instance.CurrentLevel);
        }
        if (SyncRateManager.Instance != null)
            HandleSyncChanged(SyncRateManager.Instance.SyncRate);

        // 무기 이름 — 미선택이면 폴백.
        string wn = WeaponLoadout.HasSelection ? WeaponLoadout.Selected.name : weaponFallback;
        if (weaponName != null) weaponName.text = wn;

        // 보스/프롬프트는 시작 시 숨김(빌더에서 이미 꺼두지만 안전 차원).
        if (bossRoot != null) bossRoot.SetActive(false);
        if (promptRoot != null) promptRoot.SetActive(false);

        // 부품 토스트 시작 숨김.
        if (partsToastGroup != null) partsToastGroup.alpha = 0f;
        _lastParts = CraftingSystem.Instance != null ? CraftingSystem.Instance.Parts : -1;

        // 탄약 실데이터 소스 — 플레이어의 PlayerCombat(싱글톤 아님, 플레이어에 부착).
        if (PlayerController.Instance != null) _combat = PlayerController.Instance.GetComponent<PlayerCombat>();
    }

    void CacheBasePositions()
    {
        if (syncPct != null) _syncPctBasePos = syncPct.rectTransform.anchoredPosition;
        if (syncPanel != null) _syncPanelBasePos = syncPanel.anchoredPosition;
        if (partsToastRect != null) _toastBasePos = partsToastRect.anchoredPosition;
        if (centerFollowRoot != null) _followParent = centerFollowRoot.parent as RectTransform;
        _haveBasePos = true;
    }

    void Update()
    {
        UpdateRunStats();
        UpdateHP();
        UpdateDash();
        UpdateBuffs();
        UpdateAmmo();
        UpdateEventTrackers();
        UpdatePartsToast();
        UpdateSyncShakeAndPanic();
        UpdateCenterFollow();
    }

    // ───────────────────── REAL: 중앙 위젯이 플레이어를 스크린에서 추종 ─────────────────────
    void UpdateCenterFollow()
    {
        if (centerFollowRoot == null) return;

        var pc = PlayerController.Instance;
        // 파괴(== null)뿐 아니라 비활성/태그해제된 메인 카메라 스왑까지 잡아 재캐시.
        if (_followCam == null || !_followCam.isActiveAndEnabled) _followCam = Camera.main;

        // 플레이어/카메라가 없으면 이 프레임은 숨김(스킵).
        if (pc == null || _followCam == null)
        {
            if (centerFollowRoot.gameObject.activeSelf) centerFollowRoot.gameObject.SetActive(false);
            return;
        }

        Vector3 sp = _followCam.WorldToScreenPoint(pc.transform.position);
        if (sp.z <= 0f)   // 카메라 뒤 → 숨김
        {
            if (centerFollowRoot.gameObject.activeSelf) centerFollowRoot.gameObject.SetActive(false);
            return;
        }
        if (!centerFollowRoot.gameObject.activeSelf) centerFollowRoot.gameObject.SetActive(true);

        if (_followParent == null) return;
        // ScreenSpaceOverlay → 카메라 null로 변환. 변환 대상은 추종루트의 부모(Start에서 1회 캐시).
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_followParent, sp, null, out Vector2 local))
            centerFollowRoot.anchoredPosition = local;

        // 머리 위/발 밑 오프셋을 매 프레임 반영(런타임 튜닝 가능). 1920 캔버스 px 기준.
        if (ammoMini != null) ammoMini.rectTransform.anchoredPosition = new Vector2(0f, headOffset);
        // 대시 미니 행(핍들의 부모)만 발 밑으로 내림 — 핍 X 배치는 빌더가 잡아둔 값 유지.
        if (dashMiniPips != null && dashMiniPips.Length > 0 && dashMiniPips[0] != null)
        {
            var row = dashMiniPips[0].rectTransform.parent as RectTransform;
            if (row != null) row.anchoredPosition = new Vector2(0f, -feetOffset);
        }
    }

    // ───────────────────── REAL: XP ─────────────────────
    void HandleXPChanged(int cur, int max)
    {
        if (xpFill != null) xpFill.fillAmount = max > 0 ? Mathf.Clamp01(cur / (float)max) : 0f;
    }

    void HandleLevelChanged(int newLevel)
    {
        if (levelLabel != null) levelLabel.text = "Lv." + newLevel;
        if (playerSub != null) playerSub.text = "현장요원 · LV." + newLevel;
    }

    // ───────────────────── REAL: SYNC RATE 둠클락 ─────────────────────
    void HandleSyncChanged(float sync)
    {
        sync = Mathf.Clamp01(sync);

        if (syncBarFill != null) syncBarFill.fillAmount = sync;
        if (syncPct != null) syncPct.text = Mathf.FloorToInt(sync * 100f) + "%";

        // 4단 상태 색 + 라벨(목업 tick(): <30 calm / <60 caution / <90 crit / else fail)
        Color tier; string state;
        if (sync < 0.30f)      { tier = CYAN;       state = "상태: 안정 / STABLE"; }
        else if (sync < 0.60f) { tier = AMBER;      state = "상태: 주의 / CAUTION"; }
        else if (sync < 0.90f) { tier = RED;        state = "상태: 위험 / CRITICAL"; }
        else                   { tier = Color.white; state = "격리 실패 / CONTAINMENT FAILED"; }

        if (syncLabel != null) syncLabel.color = tier;
        if (syncPct != null)   syncPct.color = tier;
        if (syncBarFill != null) syncBarFill.color = tier;
        // 상태 텍스트는 caution부터 색이 입혀짐(목업 CSS: caution/crit/fail만 .state 색 지정).
        if (syncState != null)
        {
            syncState.text = state;
            syncState.color = (sync < 0.30f) ? MUTED : tier;
        }

        // 비네트 — 기존 SyncRateUI와 동일 램프(70%+부터 0→0.6).
        if (vignette != null)
        {
            float t = Mathf.Clamp01((sync - 0.7f) / 0.3f);
            Color c = vignette.color;
            c.a = t * 0.6f;
            vignette.color = c;
        }
    }

    // crit/fail 상태의 흔들림 + 패닉 펄스는 매 프레임 구동(이벤트로는 진동 불가).
    void UpdateSyncShakeAndPanic()
    {
        if (!_haveBasePos) return;
        float sync = SyncRateManager.Instance != null ? SyncRateManager.Instance.SyncRate : 0f;

        // pct 흔들림: 위험(>=0.6) 이상.
        if (syncPct != null)
        {
            if (sync >= 0.60f)
            {
                Vector2 j = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f));
                syncPct.rectTransform.anchoredPosition = _syncPctBasePos + j;
            }
            else syncPct.rectTransform.anchoredPosition = _syncPctBasePos;
        }

        // 패닉 펄스: 격리 실패(>=0.9) — 패널 배경색이 진동 + 패널 자체 흔들림.
        if (syncPanelBg != null)
        {
            if (sync >= 0.90f)
            {
                float p = Mathf.PingPong(Time.unscaledTime * 1.6f, 1f);
                syncPanelBg.color = Color.Lerp(PANIC_A, PANIC_B, p);
                if (syncPanel != null)
                {
                    Vector2 j = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f));
                    syncPanel.anchoredPosition = _syncPanelBasePos + j;
                }
            }
            else
            {
                syncPanelBg.color = SYNC_PANEL;
                if (syncPanel != null) syncPanel.anchoredPosition = _syncPanelBasePos;
            }
        }
    }

    // ───────────────────── REAL: 런 타이머 + 킬 ─────────────────────
    void UpdateRunStats()
    {
        var rs = RunStats.Instance;
        if (timeLabel != null)
        {
            float t = rs != null ? rs.ElapsedTime : 0f;
            int total = Mathf.FloorToInt(t);
            if (total != _lastSecs)
            {
                _lastSecs = total;
                timeLabel.text = (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
            }
        }
        if (killsLabel != null)
        {
            int k = rs != null ? rs.Kills : 0;
            if (k != _lastKills)
            {
                _lastKills = k;
                killsLabel.text = "REMOVED · " + k.ToString("0000");
            }
        }
    }

    // ───────────────────── REAL: HP ─────────────────────
    void UpdateHP()
    {
        var pc = PlayerController.Instance;
        if (hpFill == null || pc == null) return;
        float max = pc.MaxHP;
        hpFill.fillAmount = max > 0f ? Mathf.Clamp01(pc.CurrentHP / max) : 0f;
    }

    // ───────────────────── REAL: 대시 핍 (코너 + 미니) ─────────────────────
    void UpdateDash()
    {
        var pc = PlayerController.Instance;
        if (pc == null) return;
        int charges = pc.DashCharges;
        int max = pc.MaxDashCharges;
        float prog = pc.DashRechargeProgress01;

        ApplyPips(dashPips, charges, max, prog);
        ApplyPips(dashMiniPips, charges, max, prog);
    }

    void ApplyPips(Image[] pips, int charges, int max, float prog)
    {
        if (pips == null) return;
        for (int i = 0; i < pips.Length; i++)
        {
            var p = pips[i];
            if (p == null) continue;
            if (i >= max)           { p.color = PIP_OFF; }       // 최대 스택 초과 슬롯(여분) — 비활성
            else if (i < charges)   { p.color = PIP_ON; }        // 가득 찬 스택
            else if (i == charges)  { p.color = Color.Lerp(PIP_OFF, PIP_CHARGE, prog); } // 충전 중
            else                    { p.color = PIP_OFF; }       // 빈 슬롯
        }
    }

    // ───────────────────── 버프 점등 (trivial 시 PlayerStats) ─────────────────────
    void UpdateBuffs()
    {
        if (buffSpd != null) buffSpd.color = PlayerStats.MoveSpeedMult > 1.001f ? BUFF_ON : BUFF_OFF;
        if (buffArm != null) buffArm.color = PlayerStats.MaxHPMult   > 1.001f ? BUFF_ON : BUFF_OFF;
        // buffDot: DoT 시스템 미구현 — 항상 OFF. // TODO 후일 디버프 시스템에 바인딩.
        if (buffDot != null) buffDot.color = BUFF_OFF;
    }

    // ───────────────────── REAL: 탄약(PlayerCombat) ─────────────────────
    void UpdateAmmo()
    {
        if (_combat == null && PlayerController.Instance != null)
        {
            _combat = PlayerController.Instance.GetComponent<PlayerCombat>();
            // 플레이어는 있는데 PlayerCombat이 없다 = 오설정. 1회만 경고(매프레임 스팸 방지).
            if (_combat == null && !_warnedNoCombat)
            {
                _warnedNoCombat = true;
                Debug.LogWarning("[HudV2] PlayerController는 있으나 PlayerCombat 컴포넌트가 없어 탄약 UI가 숨겨집니다.");
            }
        }

        // 컴포넌트 없음 또는 탄약 무기 아님(근접/무탄약) → 탄약 표시 숨김.
        if (_combat == null || !_combat.UsesAmmo)
        {
            if (ammoLabel != null && ammoLabel.gameObject.activeSelf) ammoLabel.gameObject.SetActive(false);
            if (ammoMini != null && ammoMini.gameObject.activeSelf) ammoMini.gameObject.SetActive(false);
            return;
        }

        if (ammoLabel != null && !ammoLabel.gameObject.activeSelf) ammoLabel.gameObject.SetActive(true);
        if (ammoMini != null && !ammoMini.gameObject.activeSelf) ammoMini.gameObject.SetActive(true);

        if (_combat.IsReloading)
        {
            if (ammoLabel != null) { ammoLabel.text = "재장전…"; ammoLabel.color = AMBER; }
            if (ammoMini  != null) { ammoMini.text  = "재장전…"; ammoMini.color  = AMBER; }
        }
        else
        {
            int cur = _combat.CurrentAmmo;
            int mag = _combat.MagazineSize;
            if (ammoLabel != null)
            {
                ammoLabel.text = $"{cur}<size=60%><color=#6F8388> / {mag}</color></size>";
                ammoLabel.color = WHITE;
            }
            if (ammoMini != null)
            {
                ammoMini.text = $"{cur}<size=80%><color=#6F8388> / {mag}</color></size>";
                ammoMini.color = WHITE;
            }
        }
    }

    // ───────────────────── PLACEHOLDER: 이벤트 트래커 카운트다운 ─────────────────────
    // TODO: 실제 EventSystem에 바인딩. 지금은 목업 cd()처럼 코스메틱으로만 카운트다운.
    void UpdateEventTrackers()
    {
        _evTick += Time.unscaledDeltaTime;   // 코스메틱 — 일시정지(timeScale=0) 중에도 진행
        if (_evTick < 1f) return;     // 1초 간격(목업 setInterval 1000ms)
        _evTick -= 1f;

        for (int i = 0; i < eventTimers.Length && i < _evRemain.Length; i++)
        {
            _evRemain[i] -= 1f;
            if (_evRemain[i] < 0f) _evRemain[i] = 50f + Random.Range(0, 120);
            var el = eventTimers[i];
            if (el == null) continue;
            int t = Mathf.FloorToInt(_evRemain[i]);
            el.text = (t / 60) + ":" + (t % 60).ToString("00");
        }
    }

    // ───────────────────── REAL: 부품 픽업 토스트 ─────────────────────
    void UpdatePartsToast()
    {
        var cs = CraftingSystem.Instance;
        if (cs != null)
        {
            if (_lastParts < 0) _lastParts = cs.Parts;
            int delta = cs.Parts - _lastParts;
            if (delta > 0)
            {
                if (partsToastLabel != null) partsToastLabel.text = $"[ PART ] +{delta} 회수";
                _toastTimer = ToastDur;
            }
            _lastParts = cs.Parts;
        }

        if (partsToastGroup == null) return;
        if (_toastTimer > 0f)
        {
            _toastTimer -= Time.deltaTime;
            float life = 1f - Mathf.Clamp01(_toastTimer / ToastDur);   // 0→1 진행
            // 목업 toastup: 0%페이드인 15%까지, 65%까지 유지, 100%페이드아웃 + Y -34 상승.
            float a;
            if (life < 0.15f) a = life / 0.15f;
            else if (life < 0.65f) a = 1f;
            else a = 1f - (life - 0.65f) / 0.35f;
            partsToastGroup.alpha = Mathf.Clamp01(a);
            if (partsToastRect != null)
                partsToastRect.anchoredPosition = _toastBasePos + new Vector2(0f, Mathf.Lerp(10f, -34f, life));
        }
        else if (partsToastGroup.alpha != 0f)
        {
            partsToastGroup.alpha = 0f;
        }
    }

    // ───────────────────── PUBLIC HOOKS (PLACEHOLDER 위젯 구동용) ─────────────────────

    /// <summary>보스 등장 — 보스바를 켜고 이름/체력을 세팅. 후일 보스/이벤트 시스템이 호출.</summary>
    public void ShowBoss(string name, float fill01)
    {
        if (bossRoot != null) bossRoot.SetActive(true);
        if (bossName != null) bossName.text = name;
        SetBossFill(fill01);
    }

    /// <summary>보스 체력 갱신(0~1).</summary>
    public void SetBossFill(float fill01)
    {
        fill01 = Mathf.Clamp01(fill01);
        if (bossBarFill != null) bossBarFill.fillAmount = fill01;
        if (bossPct != null) bossPct.text = Mathf.RoundToInt(fill01 * 100f) + "%";
    }

    /// <summary>보스바 숨김.</summary>
    public void HideBoss()
    {
        if (bossRoot != null) bossRoot.SetActive(false);
    }

    /// <summary>상호작용 프롬프트 표시(예: "E 보급 상자 개봉"). 후일 상호작용 시스템이 호출.</summary>
    public void ShowPrompt(string message)
    {
        if (promptRoot != null) promptRoot.SetActive(true);
        if (promptLabel != null) promptLabel.text = message;
    }

    /// <summary>상호작용 프롬프트 숨김.</summary>
    public void HidePrompt()
    {
        if (promptRoot != null) promptRoot.SetActive(false);
    }

    // ───────────────────── helper ─────────────────────
    static Color H(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out var c);
        return c;
    }
}
