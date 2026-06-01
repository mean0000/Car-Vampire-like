using UnityEngine;

/// <summary>
/// 채널링 제작 시스템. C 키를 "누르고 있는" 동안 craftTime 동안 제자리에서 채널링한다.
/// 핵심 긴장: 제작 중에는 무방비(이동 불가, 전투 잠금) + 망치질 소음이 "탕…탕…탕"
/// 펄스로 점점 커지고 빨라져 완료 직전 추격 임계(50)를 넘긴다 — "안전한 곳에서 빠르게,
/// 아니면 위험을 감수"라는 선택을 만든다. 부품은 좀비 처치 시 확률 드롭(NotifyKill)으로 쌓인다.
/// </summary>
public class CraftingSystem : MonoBehaviour
{
    public static CraftingSystem Instance { get; private set; }

    [Header("Cost / Output")]
    [SerializeField] int partsPerCraft = 5;
    [SerializeField] float craftTime = 3f;
    [SerializeField] float medkitHeal = 50f;

    [Header("Noise (망치질 펄스 — 탕…탕…탕 점점 커지고 빨라짐)")]
    [SerializeField] float noiseFloorStart = 5f;     // 펄스 사이 바닥(초반) — 거의 무음
    [SerializeField] float noiseFloorEnd = 18f;      // 펄스 사이 바닥(막판) — 근처 유인
    [SerializeField] float hitNoiseStart = 30f;      // 망치 한 타격(초반)
    [SerializeField] float hitNoiseEnd = 75f;        // 망치 한 타격(막판) — 임계 50 크게 초과
    [SerializeField] float hitIntervalStart = 0.6f;  // 타격 간격(초반) — 느긋한 "탕…탕"
    [SerializeField] float hitIntervalEnd = 0.28f;   // 타격 간격(막판) — 다급한 "탕탕탕"

    [Header("Parts Drop")]
    [SerializeField] GameObject partPickupPrefab;    // 좀비 죽은 자리에 떨어지는 물리 부품
    [SerializeField] float dropChance = 0.5f;        // 좀비 처치 시 부품 드롭 확률
    [SerializeField] float doubleDropChance = 0.15f; // 드롭 성공 시 2개 나올 확률

    int _parts;
    bool _isCrafting;
    float _craftTimer;
    float _hitTimer;       // 다음 망치 타격까지 남은 시간(0 이하면 친다)

    public int Parts => _parts;
    public bool IsCrafting => _isCrafting;
    public float CraftProgress01 => _isCrafting ? Mathf.Clamp01(_craftTimer / Mathf.Max(0.0001f, craftTime)) : 0f;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        bool holdingCraft = Input.GetKey(KeyCode.C);

        if (!_isCrafting)
        {
            // 시작 조건: C 누름 + 부품 충분 + 정지 상태
            if (Input.GetKeyDown(KeyCode.C) && _parts >= partsPerCraft && !IsMoving())
            {
                _isCrafting = true;
                _craftTimer = 0f;
                _hitTimer = 0f;   // 첫 프레임에 즉시 첫 "탕" — 시작이 분명하게 들리도록
            }
            return;
        }

        // 채널링 중: 손 떼거나 움직이면 즉시 취소(부품 보존). 무방비 비용을 감수하지 않으면 못 만든다.
        if (!holdingCraft || IsMoving())
        {
            CancelCraft();
            return;
        }

        _craftTimer += Time.deltaTime;
        float p = CraftProgress01;

        // 펄스 사이 바닥 소음(베이스라인) — 망치 타격이 감쇠한 뒤에도 근처엔 들리게 깐다.
        NoiseManager.Instance?.SetMovementNoise(Mathf.Lerp(noiseFloorStart, noiseFloorEnd, p));

        // 망치 타격 펄스 — 진행도에 따라 간격이 좁아지며(빨라짐) 강도가 세진다.
        // NoiseManager의 release 엔벨로프가 각 타격을 "탕! → 스르륵" 톱니로 감쇠시킨다.
        _hitTimer -= Time.deltaTime;
        if (_hitTimer <= 0f)
        {
            // 누적(+=)이 아니라 리셋(=) — 프레임 히치로 타이머가 크게 밀려도 다음 타격까지
            // 항상 한 박자를 기다리게 해 "따라잡기 더블탭"을 막는다. 짧은 채널링이라 박자 드리프트는 무시 가능.
            _hitTimer = Mathf.Lerp(hitIntervalStart, hitIntervalEnd, p);
            NoiseManager.Instance?.EmitImpulse(Mathf.Lerp(hitNoiseStart, hitNoiseEnd, p));
        }

        if (_craftTimer >= craftTime)
            CompleteCraft();
    }

    void CompleteCraft()
    {
        _isCrafting = false;
        _parts -= partsPerCraft;
        PlayerController.Instance?.Heal(medkitHeal);
    }

    void CancelCraft()
    {
        _isCrafting = false;
        // 부품은 소비하지 않음 — 채널링을 못 끝냈을 뿐. 소음은 다음 프레임 자동 0으로 감쇠.
    }

    bool IsMoving()
    {
        return Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.001f
            || Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.001f;
    }

    /// <summary>픽업이 수집될 때 호출. 부품을 더한다.</summary>
    public void AddParts(int amount)
    {
        if (amount <= 0) return;
        _parts += amount;
    }

    /// <summary>좀비 처치 시 호출. 확률로 죽은 자리에 물리 부품을 떨군다(줍기 전엔 카운트 안 됨).</summary>
    public void NotifyKill(Vector3 position)
    {
        if (partPickupPrefab == null) return;        // 미연결 시 무드롭 — 그레이박스 안전
        if (Random.value > dropChance) return;

        int count = (Random.value < doubleDropChance) ? 2 : 1;
        for (int i = 0; i < count; i++)
        {
            // 둘 이상이면 갈라지게 각도 분배, 하나면 랜덤 방향.
            float angle = count > 1 ? (360f / count) * i + Random.Range(-25f, 25f) : Random.Range(0f, 360f);
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            var obj = Instantiate(partPickupPrefab, position, Quaternion.identity);
            obj.GetComponent<PartPickup>()?.Init(dir);
        }
    }
}
