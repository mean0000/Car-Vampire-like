using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 연료 충전/드레인 이벤트를 화면 로그로 표시.
/// Canvas 위에 TMP_Text를 연결하면 최근 5개 이벤트가 스크롤됨.
/// </summary>
public class FuelLogUI : MonoBehaviour
{
    [SerializeField] private TMP_Text logText;
    [SerializeField] private int maxEntries = 5;
    [SerializeField] private float entryLifetime = 3f;

    private readonly Queue<(string text, float expireTime)> _entries = new();

    private void OnEnable()
    {
        CarBoost.OnFuelEvent += HandleFuelEvent;
    }

    private void OnDisable()
    {
        CarBoost.OnFuelEvent -= HandleFuelEvent;
    }

    private void HandleFuelEvent(string source, float amount)
    {
        string line;
        if (amount > 0f)
            line = $"<color=#00FFAA>+{amount:F0}</color> {source}";
        else if (amount < 0f)
            line = $"<color=#FF6644>-{Mathf.Abs(amount):F0}/s</color> {source}";
        else
            line = $"<color=#FF4444>소진</color>";

        _entries.Enqueue((line, Time.time + entryLifetime));

        while (_entries.Count > maxEntries)
            _entries.Dequeue();

        RefreshText();
    }

    private void Update()
    {
        if (_entries.Count == 0) return;

        // 만료된 항목 제거
        bool changed = false;
        while (_entries.Count > 0 && _entries.Peek().expireTime < Time.time)
        {
            _entries.Dequeue();
            changed = true;
        }
        if (changed) RefreshText();
    }

    private void RefreshText()
    {
        if (logText == null) return;
        var sb = new System.Text.StringBuilder();
        foreach (var (text, _) in _entries)
        {
            if (sb.Length > 0) sb.AppendLine();
            sb.Append(text);
        }
        logText.text = sb.ToString();
    }
}
