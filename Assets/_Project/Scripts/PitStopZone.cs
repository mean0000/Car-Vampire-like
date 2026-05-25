using UnityEngine;

/// <summary>
/// 피트스톱 거점 트리거 존.
/// CarController를 가진 오브젝트가 진입하면 업그레이드 메뉴를 연다.
/// </summary>
[RequireComponent(typeof(Collider))]
public class PitStopZone : MonoBehaviour
{
    [SerializeField] float cooldown = 10f;
    [SerializeField] float syncReduction = 0.2f;
    [SerializeField] float hullHeal = 30f;

    float _lastTriggerTime = -999f;

    void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<CarController>() == null) return;
        if (GameOverUI.Instance != null && GameOverUI.Instance.IsPanelOpen) return;
        if (UpgradeMenuUI.Instance == null) return;
        if (UpgradeMenuUI.Instance.IsPanelOpen) return;
        if (Time.time - _lastTriggerTime < cooldown) return;

        _lastTriggerTime = Time.time;
        SyncRateManager.Instance?.ReduceSync(syncReduction);
        HullManager.Instance?.Heal(hullHeal);

        // 힐/Sync 감소는 항상 발동, 업그레이드 메뉴는 PendingLevels가 있을 때만 열기
        if (XPManager.Instance == null || XPManager.Instance.PendingLevels <= 0) return;
        UpgradeMenuUI.Instance.Show();
    }
}
