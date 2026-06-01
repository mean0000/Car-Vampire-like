using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 레벨업 시 카드를 띄워 능력 하나를 고르게 하는 패널.
/// XPManager.OnLevelChanged를 구독 → PendingLevels가 0이 될 때까지 선택 루프를 반복하므로
/// 한 번에 여러 레벨이 오르면 카드도 그만큼 연속으로 뜬다.
///
/// MVP 기본 카드 6종(속사·강선·강화 골격·경량화·자력 채집·효율 흡수)을 제시한다.
/// 카드를 고르면 누적 레벨(_levels)이 오르고 ApplyAbility가 PlayerStats에 실제 효과를 반영한다.
/// 각 카드는 Lv3에서 만렙 — 만렙 카드는 드로우 풀에서 제외된다(진화는 미구현).
///
/// 패널은 시작 시 비활성이므로 이 컴포넌트는 항상-활성 GO에 둔다(비활성 GO는 Start가 안 돌아 구독을 못 함).
/// 선택 중에는 Time.timeScale=0으로 멈춘다. UI 버튼/코루틴은 언스케일드로 동작하므로 정지 중에도 클릭 가능.
/// </summary>
public class LevelUpChoiceUI : MonoBehaviour
{
    [SerializeField] GameObject panel;            // 카드 패널 루트 (시작 비활성)
    [SerializeField] UpgradeCardView[] cards;     // 카드 뷰 3장 (UpgradeMenuUI의 구조 재사용)

    // MVP 기본 카드 6종 (cards-catalog.md §2.1·2.3 — 스탯 전용, 신규 훅 불필요). 각 카드 Lv1→3 누적.
    const int MaxLevel = 3;

    static readonly string[] AbilityNames =
    {
        "속사", "강선", "강화 골격", "경량화", "자력 채집", "효율 흡수"
    };
    // [카드][레벨] 청크 설명 (해당 레벨 도달 시 효과). BuildCards가 "다음 레벨" 설명을 보여준다.
    static readonly string[][] AbilityDescByLevel =
    {
        new[] { "연사 속도 +25%", "연사 속도 +40%", "연사 속도 +55%" }, // 속사
        new[] { "사격 피해 +1",  "사격 피해 +2",  "사격 피해 +3"  },    // 강선
        new[] { "최대 체력 +30%", "최대 체력 +50%", "최대 체력 +75%" }, // 강화 골격
        new[] { "이동 속도 +15%", "이동 속도 +25%", "이동 속도 +35%" }, // 경량화
        new[] { "수집 반경 +40%", "수집 반경 +65%", "수집 반경 +90%" }, // 자력 채집
        new[] { "XP 획득 +25%",  "XP 획득 +40%",  "XP 획득 +60%"  },    // 효율 흡수
    };

    // 카드별 Lv1/2/3 누적 값(델타 아님 — 해당 레벨의 최종값).
    static readonly float[] FireRateByLevel  = { 1.25f, 1.40f, 1.55f };
    static readonly int[]   DamageByLevel    = { 1, 2, 3 };
    static readonly float[] MaxHPByLevel     = { 1.30f, 1.50f, 1.75f };
    static readonly float[] MoveSpeedByLevel = { 1.15f, 1.25f, 1.35f };
    static readonly float[] PickupByLevel    = { 1.40f, 1.65f, 1.90f };
    static readonly float[] XPGainByLevel    = { 1.25f, 1.40f, 1.60f };

    readonly Dictionary<string, int> _levels = new Dictionary<string, int>();  // 능력별 누적 레벨
    readonly List<int> _current = new List<int>();                            // 현재 표시 중인 능력 인덱스
    bool _chosen;

    public bool IsOpen => panel != null && panel.activeSelf;

    void Start()
    {
        if (panel != null) panel.SetActive(false);
        if (XPManager.Instance != null)
            XPManager.Instance.OnLevelChanged += HandleLevelUp;
    }

    void OnDestroy()
    {
        if (XPManager.Instance != null)
            XPManager.Instance.OnLevelChanged -= HandleLevelUp;
        // 패널이 떠 있는 채로 파괴(씬 재로드 등)되면 timeScale이 0에 갇히므로 복구
        if (Time.timeScale == 0f) Time.timeScale = 1f;
    }

    void HandleLevelUp(int newLevel)
    {
        if (panel == null) return;
        // 이미 열려 있으면 루프가 PendingLevels를 마저 소비하므로 새로 시작하지 않는다.
        if (!IsOpen) StartCoroutine(SelectionLoop());
    }

    IEnumerator SelectionLoop()
    {
        while (XPManager.Instance != null && XPManager.Instance.PendingLevels > 0)
        {
            XPManager.Instance.ConsumePendingLevel();
            BuildCards();

            // 모든 카드가 만렙이라 제시할 게 없으면 더 띄우지 않고 닫는다(정지 영구화 방지).
            if (_current.Count == 0) break;

            panel.SetActive(true);
            Time.timeScale = 0f;

            _chosen = false;
            float elapsed = 0f;
            while (!_chosen)
            {
                // 카드 미연결 등으로 선택 불가 시 30초 후 자동 탈출(정지 영구화 방지)
                elapsed += Time.unscaledDeltaTime;
                if (elapsed > 30f) break;
                yield return null;
            }
        }

        panel.SetActive(false);
        Time.timeScale = 1f;
    }

    void BuildCards()
    {
        // 만렙(Lv3) 카드는 제외하고 섞어 앞에서 카드 수만큼 뽑는다(중복 없이).
        var pool = new List<int>();
        for (int i = 0; i < AbilityNames.Length; i++)
        {
            int cur = _levels.TryGetValue(AbilityNames[i], out int L) ? L : 0;
            if (cur < MaxLevel) pool.Add(i);
        }
        for (int i = 0; i < pool.Count; i++)
        {
            int j = Random.Range(i, pool.Count);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        _current.Clear();
        int n = cards != null ? Mathf.Min(cards.Length, pool.Count) : 0;
        for (int i = 0; i < n; i++) _current.Add(pool[i]);

        if (cards == null) return;
        for (int i = 0; i < cards.Length; i++)
        {
            var v = cards[i];
            if (v == null || v.button == null) continue;

            if (i < _current.Count)
            {
                int ab = _current[i];
                int cur = _levels.TryGetValue(AbilityNames[ab], out int L) ? L : 0;
                int nextLvl = cur + 1;   // 이번에 고르면 도달할 레벨 (1~3)

                v.button.gameObject.SetActive(true);
                if (v.titleText != null) v.titleText.text = AbilityNames[ab];
                if (v.descText != null)
                    v.descText.text = $"{AbilityDescByLevel[ab][nextLvl - 1]}\n(Lv.{cur} → {nextLvl})";

                int idx = i; // 클로저 캡처
                v.button.onClick.RemoveAllListeners();
                v.button.onClick.AddListener(() => OnPick(idx));
            }
            else
            {
                v.button.gameObject.SetActive(false);
            }
        }
    }

    void OnPick(int idx)
    {
        if (_chosen) return;   // 패널 닫히기 전 빠른 더블클릭 흡수 — 중복 증가 방지
        if (idx < 0 || idx >= _current.Count) return;
        int ab = _current[idx];
        string name = AbilityNames[ab];
        int cur = _levels.TryGetValue(name, out int L) ? L : 0;
        if (cur >= MaxLevel) { _chosen = true; return; }   // 안전장치: 만렙 카드는 무효
        int next = cur + 1;
        _levels[name] = next;
        ApplyAbility(ab, next);
        _chosen = true;
    }

    /// <summary>선택한 기본 카드의 누적 효과를 PlayerStats에 반영한다. newLevel은 1~3.</summary>
    void ApplyAbility(int ab, int newLevel)
    {
        int i = newLevel - 1;
        switch (ab)
        {
            case 0: PlayerStats.FireRateMult     = FireRateByLevel[i];  break; // 속사
            case 1: PlayerStats.DamageBonus      = DamageByLevel[i];    break; // 강선
            case 2:                                                            // 강화 골격
                float old = PlayerStats.MaxHPMult;
                PlayerStats.MaxHPMult = MaxHPByLevel[i];
                PlayerController.Instance?.RaiseMaxHP(old, PlayerStats.MaxHPMult);
                break;
            case 3: PlayerStats.MoveSpeedMult    = MoveSpeedByLevel[i]; break; // 경량화
            case 4: PlayerStats.PickupRadiusMult = PickupByLevel[i];    break; // 자력 채집
            case 5: PlayerStats.XPGainMult       = XPGainByLevel[i];    break; // 효율 흡수
        }
    }
}
