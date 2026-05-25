using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 레벨업 업그레이드 선택 UI.
/// PitStopZone에서 Show()를 호출하면 PendingLevels가 소진될 때까지 카드 선택 루프를 반복.
/// </summary>
public class UpgradeMenuUI : MonoBehaviour
{
    public static UpgradeMenuUI Instance { get; private set; }

    [SerializeField] GameObject upgradePanel;
    [SerializeField] WeaponData[] allWeapons;       // 모든 무기 ScriptableObject 연결
    [SerializeField] UpgradeCardView[] cardViews;   // 카드 UI 뷰 4개
    [SerializeField] Button rerollButton;
    [SerializeField] TMPro.TextMeshProUGUI rerollCostText;

    // ── 희귀도별 테두리 색 ──────────────────────────────────────────────────
    static readonly Color ColorNormal    = new Color(0.533f, 0.533f, 0.533f); // #888888
    static readonly Color ColorRare      = new Color(0.282f, 0.584f, 0.937f); // #4895ef
    static readonly Color ColorLegendary = new Color(0.976f, 0.780f, 0.310f); // #f9c74f

    // ── 내부 상태 ───────────────────────────────────────────────────────────
    bool _cardChosen;
    UpgradeCard _selectedCard;
    int _rerollsUsed;
    const int MaxRerolls = 2;

    bool _isFirstPitStop = true;
    float _progression => Mathf.Clamp01((XPManager.Instance != null ? XPManager.Instance.CurrentLevel : 1) / 40f);

    // 라운드 간 유지되는 시스템
    TreeAffinityTracker _affinity;
    PityTracker _pity;

    List<UpgradeCard> _currentCards = new List<UpgradeCard>();

    // ── 수명주기 ────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _affinity = new TreeAffinityTracker();
        _pity     = new PityTracker();
    }

    void Start()
    {
        if (upgradePanel != null)
            upgradePanel.SetActive(false);

        if (rerollButton != null)
            rerollButton.onClick.AddListener(OnReroll);
    }

    // ── 공개 API ─────────────────────────────────────────────────────────────

    public bool IsPanelOpen => upgradePanel != null && upgradePanel.activeSelf;

    /// <summary>PitStopZone에서 호출. PendingLevels 소진까지 선택 루프 실행.</summary>
    public void Show()
    {
        if (upgradePanel == null) return;
        if (IsPanelOpen) return;

        StartCoroutine(SelectionLoop());
    }

    // ── 선택 루프 ────────────────────────────────────────────────────────────

    IEnumerator SelectionLoop()
    {
        while (XPManager.Instance != null && XPManager.Instance.PendingLevels > 0)
        {
            // 카드를 먼저 구성한 뒤 성공한 경우에만 레벨 소비
            _rerollsUsed = 0;
            _currentCards = BuildCards();

            if (_currentCards.Count == 0) break;

            XPManager.Instance.ConsumePendingLevel();

            upgradePanel.SetActive(true);
            Time.timeScale = 0f;

            _cardChosen = false;
            DisplayCards();
            UpdateRerollButton();

            // 타임아웃 안전망: cardViews 미연결 등으로 선택이 불가한 경우 30초 후 자동 탈출
            float elapsed = 0f;
            while (!_cardChosen)
            {
                elapsed += Time.unscaledDeltaTime;
                if (elapsed > 30f) { ClosePanel(); yield break; }
                yield return null;
            }

            // 라운드 종료 처리 — 제시된 카드가 아닌 실제 선택한 카드 기준으로 pity 갱신
            _affinity.Decay();
            _pity.OnCardPicked(_selectedCard);

            _isFirstPitStop = false;

            // 다음 레벨업이 있으면 잠깐 대기 후 계속
            if (XPManager.Instance.PendingLevels > 0)
                yield return null;
        }

        ClosePanel();
    }

    // ── 카드 구성 & 표시 ────────────────────────────────────────────────────

    List<UpgradeCard> BuildCards()
    {
        if (PlayerWeaponInventory.Instance == null || allWeapons == null)
            return new List<UpgradeCard>();

        var builder = new CardPoolBuilder(PlayerWeaponInventory.Instance, _affinity, allWeapons);
        return builder.Build(_progression, GetSafePendingLevels(), _pity, _isFirstPitStop);
    }

    void RebuildAndDisplay()
    {
        _currentCards = BuildCards();
        if (_currentCards.Count > 0)
            DisplayCards();
    }

    void DisplayCards()
    {
        if (cardViews == null) return;

        for (int i = 0; i < cardViews.Length; i++)
        {
            var view = cardViews[i];
            if (view == null || view.button == null) continue;

            if (i < _currentCards.Count)
            {
                UpgradeCard card = _currentCards[i];
                view.button.gameObject.SetActive(true);

                if (view.titleText != null) view.titleText.text = card.GetTitle();
                if (view.descText  != null) view.descText.text  = card.GetDesc();
                if (view.rarityBorder != null) view.rarityBorder.color = RarityColor(card.TargetRarity);

                int idx = i; // 클로저 캡처용
                view.button.onClick.RemoveAllListeners();
                view.button.onClick.AddListener(() => OnCardSelected(idx));
            }
            else
            {
                view.button.gameObject.SetActive(false);
            }
        }
    }

    void OnCardSelected(int idx)
    {
        if (idx < 0 || idx >= _currentCards.Count) return;

        UpgradeCard card = _currentCards[idx];
        if (PlayerWeaponInventory.Instance != null)
        {
            if (card.Type == CardType.Unlock)
            {
                PlayerWeaponInventory.Instance.Unlock(card.Weapon);
                _affinity.OnUnlocked(card.Weapon.tree);
            }
            else
            {
                PlayerWeaponInventory.Instance.Upgrade(card.Weapon);
                _affinity.OnUpgraded(card.Weapon.tree);
            }
        }

        _selectedCard = card;
        _cardChosen = true;
    }

    // ── 리롤 ────────────────────────────────────────────────────────────────

    void OnReroll()
    {
        if (_rerollsUsed >= MaxRerolls) return;
        _rerollsUsed++;

        RebuildAndDisplay();
        UpdateRerollButton();
    }

    void UpdateRerollButton()
    {
        if (rerollButton == null) return;

        bool canReroll = _rerollsUsed < MaxRerolls;
        rerollButton.interactable = canReroll;

        if (rerollCostText != null)
            rerollCostText.text = canReroll
                ? $"리롤 ({MaxRerolls - _rerollsUsed}회 남음)"
                : "리롤 불가";
    }

    // ── 패널 닫기 ───────────────────────────────────────────────────────────

    void ClosePanel()
    {
        if (upgradePanel != null)
            upgradePanel.SetActive(false);

        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;

        // upgradePanel 파괴 여부와 무관하게 timeScale 복구
        if (Time.timeScale == 0f)
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
        }
    }

    // ── 유틸 ────────────────────────────────────────────────────────────────

    int GetSafePendingLevels() => XPManager.Instance != null ? XPManager.Instance.PendingLevels : 0;

    static Color RarityColor(WeaponRarity r)
    {
        switch (r)
        {
            case WeaponRarity.Rare:      return ColorRare;
            case WeaponRarity.Legendary: return ColorLegendary;
            default:                     return ColorNormal;
        }
    }
}

/// <summary>업그레이드 카드 하나의 UI 구성 요소 묶음.</summary>
[System.Serializable]
public class UpgradeCardView
{
    public Button button;
    public TMPro.TextMeshProUGUI titleText;
    public TMPro.TextMeshProUGUI descText;
    public Image rarityBorder;
}
