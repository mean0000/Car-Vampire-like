using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// 파트너 AI 음성 자막 UI.
/// ShowBark()로 메시지를 표시하며, 페이드인 → 대기 → 페이드아웃 순서로 처리.
/// SyncRateManager 이벤트를 구독해 임계값 도달 시 자동 bark.
/// </summary>
public class PartnerAIUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI barkText;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float displayDuration = 4f;
    [SerializeField] private float fadeDuration = 0.5f;

    // threshold bark 1회 제한 플래그
    private bool _barkedAt60 = false;
    private bool _barkedAt90 = false;

    private Coroutine _barkCoroutine;

    private void OnEnable()
    {
        _barkedAt60 = false;
        _barkedAt90 = false;
        if (SyncRateManager.Instance != null)
            SyncRateManager.Instance.OnSyncChanged += HandleSyncChanged;
    }

    private void OnDisable()
    {
        if (SyncRateManager.Instance != null)
            SyncRateManager.Instance.OnSyncChanged -= HandleSyncChanged;
    }

    /// <summary>외부에서 메시지를 표시. 현재 표시 중이면 즉시 교체.</summary>
    public void ShowBark(string message)
    {
        if (_barkCoroutine != null)
            StopCoroutine(_barkCoroutine);

        _barkCoroutine = StartCoroutine(BarkRoutine(message));
    }

    private IEnumerator BarkRoutine(string message)
    {
        if (barkText == null || canvasGroup == null) yield break;
        barkText.text = message;

        // 페이드인
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeDuration);
            yield return null;
        }
        canvasGroup.alpha = 1f;

        // 대기
        yield return new WaitForSecondsRealtime(displayDuration);

        // 페이드아웃
        elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Clamp01(1f - elapsed / fadeDuration);
            yield return null;
        }
        canvasGroup.alpha = 0f;
        _barkCoroutine = null;
    }

    private void HandleSyncChanged(float syncRate)
    {
        // 높은 임계값 먼저 체크 — 60%를 건너뛰어 단번에 90%+ 도달 시에도 정확히 발동
        if (!_barkedAt90 && syncRate > 0.9f)
        {
            _barkedAt60 = true; // 60% bark는 스킵된 것으로 처리
            _barkedAt90 = true;
            ShowBark("...너무 올라갔어. 빨리");
        }
        else if (!_barkedAt60 && syncRate > 0.6f)
        {
            _barkedAt60 = true;
            ShowBark("억제율 경계선 근접. 속도 줄여");
        }
    }
}
