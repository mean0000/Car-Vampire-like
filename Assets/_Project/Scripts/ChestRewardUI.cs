using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 보급 상자를 열면 무기 강화 카드 2장 중 1장을 고르게 하는 패널. Chest가 Open()을 호출한다.
/// 레벨업 카드(LevelUpChoiceUI)와는 별개 트랙 — 이쪽은 progression-system 기둥1 "무기 강화 설계도/부품".
///
/// ★ 지금은 "작동만" — 실제 강화 효과는 미구현. 카드를 고르면 해당 부품의 누적 레벨(_levels)만 오른다.
///   나중에 무기 강화가 생기면 ApplyPart(name)에서 실제 효과를 적용하면 된다.
///
/// 패널은 시작 비활성이므로 이 컴포넌트는 항상-활성 GO에 둔다.
/// 선택 중에는 Time.timeScale=0으로 멈춘다. UI 버튼/코루틴은 언스케일드로 동작하므로 정지 중에도 클릭 가능.
/// </summary>
public class ChestRewardUI : MonoBehaviour
{
    public static ChestRewardUI Instance { get; private set; }

    [SerializeField] GameObject panel;            // 카드 패널 루트 (시작 비활성)
    [SerializeField] UpgradeCardView[] cards;     // 카드 뷰 2장 (UpgradeMenuUI의 구조 재사용)

    // 플레이스홀더 무기 부품 풀 — 실제 효과는 미구현. 카드 UI/선택 플로우를 굴리기 위한 더미.
    static readonly string[] PartNames =
    {
        "소음기", "확장 탄창", "관통탄", "근접 강화날", "조준기", "경량 프레임"
    };
    static readonly string[] PartDescs =
    {
        "사격 소음을 줄인다", "재장전 없이 더 오래 쏜다", "적을 관통한다",
        "근접 처치가 강해진다", "명중률이 오른다", "더 가볍게 움직인다"
    };

    readonly Dictionary<string, int> _levels = new Dictionary<string, int>();  // 부품별 누적 레벨
    readonly List<int> _current = new List<int>();                            // 현재 표시 중인 부품 인덱스
    bool _open;
    bool _chosen;

    public bool IsOpen => _open;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    void Start()
    {
        if (panel != null) panel.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        // 패널이 떠 있는 채로 파괴(씬 재로드 등)되면 timeScale이 0에 갇히므로 복구
        if (Time.timeScale == 0f) Time.timeScale = 1f;
    }

    /// <summary>Chest가 호출. 이미 열려 있으면 무시(한 번에 하나만).</summary>
    public void Open()
    {
        if (_open || panel == null) return;
        StartCoroutine(ChoiceRoutine());
    }

    IEnumerator ChoiceRoutine()
    {
        _open = true;
        BuildCards();

        panel.SetActive(true);
        Time.timeScale = 0f;

        _chosen = false;
        float elapsed = 0f;
        while (!_chosen)
        {
            // 카드 미연결 등으로 선택 불가 시 30초 후 자동 탈출(정지 영구화 방지). 보상은 스킵된다.
            elapsed += Time.unscaledDeltaTime;
            if (elapsed > 30f) break;
            yield return null;
        }

        panel.SetActive(false);
        Time.timeScale = 1f;
        _open = false;
    }

    void BuildCards()
    {
        // 부품 풀을 섞어 앞에서 카드 수만큼 뽑는다(중복 없이).
        var pool = new List<int>();
        for (int i = 0; i < PartNames.Length; i++) pool.Add(i);
        for (int i = 0; i < pool.Count; i++)
        {
            int j = Random.Range(i, pool.Count);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        _current.Clear();
        int n = cards != null ? Mathf.Min(cards.Length, PartNames.Length) : 0;
        for (int i = 0; i < n; i++) _current.Add(pool[i]);

        if (cards == null) return;
        for (int i = 0; i < cards.Length; i++)
        {
            var v = cards[i];
            if (v == null || v.button == null) continue;

            if (i < _current.Count)
            {
                int part = _current[i];
                int lvl = _levels.TryGetValue(PartNames[part], out int L) ? L : 0;

                v.button.gameObject.SetActive(true);
                if (v.titleText != null) v.titleText.text = PartNames[part];
                if (v.descText != null)
                    v.descText.text = PartDescs[part] + (lvl > 0 ? $"\n(현재 Lv.{lvl})" : "");

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
        string name = PartNames[_current[idx]];
        int next = (_levels.TryGetValue(name, out int L) ? L : 0) + 1;
        _levels[name] = next;
        ApplyPart(name, next);
        _chosen = true;
    }

    /// <summary>무기 강화 적용 지점. 실제 효과가 생기면 여기서 분기해 적용한다(지금은 카운트만).</summary>
    void ApplyPart(string partName, int newLevel)
    {
        Debug.Log($"[Chest] '{partName}' 선택 → Lv.{newLevel} (효과는 아직 미구현 플레이스홀더)");
    }
}
