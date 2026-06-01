using UnityEngine;

/// <summary>
/// 맵에 흩어진 보급 상자. 플레이어가 openRadius 안으로 들어오면 자동으로 열려
/// ChestRewardUI(무기 강화 카드 2장 중 1택)를 띄운다. 한 번 열면 소멸.
/// 콜라이더 없이 거리(sqrMagnitude) 기반 — 좀비 트리거 콜라이더 함정(메모) 회피.
/// 살짝 회전+상하 보빙으로 "주울 수 있는 것"임을 표시.
///
/// ★ 상자는 "지켜진다": 플레이어가 guardActivationRange 안에 들어오면 주변 링에 좀비 무리를
///   1회 스폰한다(home=스폰 지점). 좀비는 Idle 상태에서 home 주변을 맴돌다가 플레이어를
///   보거나 들으면 추격 — 기존 AI로 "전리품을 위험을 뚫고 가지러 간다"는 긴장이 나온다.
/// </summary>
public class Chest : MonoBehaviour
{
    [SerializeField] float openRadius = 1.8f;
    [SerializeField] float spinSpeed = 30f;
    [SerializeField] float bobHeight = 0.15f;
    [SerializeField] float bobSpeed = 2f;

    [Header("Guards (상자를 지키는 좀비 무리)")]
    [SerializeField] GameObject guardZombiePrefab;
    [SerializeField] int guardCount = 12;
    [SerializeField] float guardRingRadius = 5f;       // 좀비가 깔리는 링 반경
    [SerializeField] float guardActivationRange = 38f; // 이 거리 안에 들어오면 1회 스폰(화면 밖 — 팝인 회피)
    [SerializeField] LayerMask groundMask = 1 << 6;

    Transform _player;
    bool _opened;
    bool _guardsSpawned;
    float _baseY;

    void Start()
    {
        _player = PlayerController.Instance != null ? PlayerController.Instance.transform : null;
        _baseY = transform.position.y;
    }

    void Update()
    {
        // 보빙+스핀 (timeScale=0이면 Time.time/deltaTime이 멈춰 자연히 정지 — 의도된 동작)
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
        Vector3 p = transform.position;
        p.y = _baseY + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = p;

        if (_player == null) return;

        // 가드 스폰: 활성 범위 진입 시 1회 (XZ 평면 거리)
        if (!_guardsSpawned)
        {
            Vector3 d = _player.position - transform.position; d.y = 0f;
            if (d.sqrMagnitude <= guardActivationRange * guardActivationRange)
            {
                SpawnGuards();
                return;   // 같은 프레임에 오픈 체크까지 가지 않도록(스폰 직후 파괴 방지)
            }
        }

        if (_opened) return;

        Vector3 to = _player.position - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude <= openRadius * openRadius)
        {
            var ui = ChestRewardUI.Instance;
            if (ui == null || ui.IsOpen) return;   // 다른 상자 처리 중이면 이 상자는 대기

            _opened = true;
            ui.Open();
            Destroy(gameObject);
        }
    }

    void SpawnGuards()
    {
        _guardsSpawned = true;
        if (guardZombiePrefab == null || guardCount <= 0) return;

        for (int i = 0; i < guardCount; i++)
        {
            float angle = (360f / guardCount) * i + Random.Range(-12f, 12f);
            float r = Random.Range(guardRingRadius * 0.4f, guardRingRadius);
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            Vector3 pos = transform.position + dir * r;
            pos.y = SampleGround(pos);

            var obj = Instantiate(guardZombiePrefab, pos, Quaternion.identity);
            var z = obj.GetComponent<ZombieController>();
            if (z != null)
            {
                z.Init(_player, pos);   // home=스폰 지점 → Idle 시 상자 주변을 지킨다
                z.PlaySpawnAnimation(SpawnAnimation.RiseFromGround);
            }
        }
    }

    float SampleGround(Vector3 pos)
    {
        Vector3 origin = new Vector3(pos.x, 200f, pos.z);
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 400f, groundMask, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        return _baseY - 0.5f;   // 상자 baseY = 지면+0.5로 배치됨
    }
}
