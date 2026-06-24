// 읽고-베기 전투 슬라이스 스포너 — 정적 Frank 카타나 플레이어(씬 배치) 주위에 Caniathrox N마리를 런타임
//   생성·와이어링한다. LabCombatSpawner의 Caniathrox 스폰 패턴을 본떴으나 3가지가 다르다:
//     ① 플레이어를 만들지 않는다(Frank가 씬에 정적 — PlayerBrain/카메라 정적 배선 보존). 타겟은 씬에서 찾는다.
//     ② 각 Caniathrox에 TelegraphPool을 주입(Coil 진입 시 돌진 예고 부채꼴).
//     ③ 각 Caniathrox를 *피격 가능*하게 만든다 — KatanaWeapon.enemyMask(레이어 7 "Zombie") 콜라이더 +
//        EnemyDamageReceiver 부착(Caniathrox 프리팹엔 콜라이더가 없음 → 런타임으로 단다). 사망 시 정리(OnDied).
//
// ════════ 레이어 정합 (★못 맞추면 영원히 못 때림) ════════
//   KatanaWeapon.DoHit이 enemyMask로 OverlapSphere → GetComponentInParent<IDamageable>로 receiver를 찾는다.
//   enemyMask = 레이어 7("Zombie"). 따라서 Caniathrox 콜라이더 GO가 레이어 7이어야 하고, receiver가 그 GO
//   또는 그 부모(여기선 루트 = 콜라이더·Animator·receiver 모두 루트)에 있어야 GetComponentInParent가 잡는다.
//
// ════════ 스폰 함정 (LabCombatSpawner 주석 계승) ════════
//   비활성으로 생성 → 필드 와이어링 → 활성화. 활성 인스턴스에 AddComponent하면 Chaser.Awake가 즉시 동기
//   실행되는데 그때 modelAnimator가 아직 null이라 스스로 죽는다. 비활성이면 Awake가 SetActive(true)까지 지연.
using System.Collections.Generic;
using UnityEngine;

public class CombatSliceSpawner : MonoBehaviour
{
    [Header("프리팹/컨트롤러 (빌더가 와이어링)")]
    [Tooltip("Caniathrox 프리팹.")]
    [SerializeField] GameObject caniathroxPrefab;
    [Tooltip("CaniathroxAttack.controller — 승인된 상태머신(IdleAngry→Approach→Coil→Lunge/Bite).")]
    [SerializeField] RuntimeAnimatorController attackController;
    [Tooltip("★장판 텔레그래프 공유 풀(씬에 1개) — 모든 Chaser에 같은 참조 주입. 비우면 Awake에서 자식/씬에서 찾는다.")]
    [SerializeField] TelegraphPool telegraphPool;

    [Header("타겟 (Frank 플레이어 — 씬 정적)")]
    [Tooltip("추격 대상 = 플레이어 루트. 비우면 PlayerBrain을 가진 GameObject를 씬에서 찾는다.")]
    [SerializeField] Transform playerTarget;

    [Header("몰려오기 (호드 크기 — 노브)")]
    [Tooltip("동시에 살아있게 유지할 적 수. ★엄청 많이 = 크게(최대 120). 죽으면 계속 보충돼 끊임없이 몰려온다. ★많으면 성능 부하(스킨드 애니 N개) — 버벅이면 낮춰라.")]
    [SerializeField, Range(1, 120)] int swarmSize = 40;
    [Tooltip("켜면 죽은 만큼 계속 보충(끊임없는 호드). 끄면 1회만 swarmSize 깔고 끝.")]
    [SerializeField] bool continuousSwarm = true;
    [Tooltip("보충 간격(초) — 한 번에 한 마리씩 밀려옴. 작을수록 빨리 채워짐.")]
    [SerializeField, Min(0.02f)] float respawnInterval = 0.12f;
    [Tooltip("플레이어 중심 스폰 반경(m). 호드가 사방에서 밀려오는 거리. 도약 전진보다 커야 '달려오는' 게 보인다.")]
    [SerializeField, Min(4f)] float spawnRadius = 18f;
    [Tooltip("스폰 반경 무작위 흔들림(m) — 일렬 방지(±).")]
    [SerializeField, Min(0f)] float radiusJitter = 4f;

    [Header("군중 AI — 공격 토큰 (동시 공격 제한 노브)")]
    [Tooltip("동시에 돌진(Lunge/Bite)할 수 있는 최대 적 수 = 동시 텔레그래프 수. ★호드가 커도 이 수만 동시 공격이라 *읽힌다*(DMC식 군중). 크게=정신없고 위협적, 작게=하나씩 읽기 쉬움. ★플레이 중 슬라이더 조정 즉시 반영.")]
    [SerializeField, Range(1, 12)] int maxAttackTokens = 4;
    [Tooltip("★cap 풀기(읽고-베기 핵심 게이트) — 켜면 maxAttackTokens 무시하고 swarmSize까지 전부 동시 커밋(사실상 무제한). " +
             "'동시 커밋 cap이 재미를 만드나, 결함(호드-듀얼 충돌)을 가리나'를 양쪽으로 본다. 풀어도 떼-듀얼이 결합돼 있으면 동사가 진짜.")]
    [SerializeField] bool uncapTokens = false;

    [Header("피격 (EnemyDamageReceiver — 읽고-베기 = 금방 안 죽어야 함)")]
    [Tooltip("Caniathrox HP. 3~4 권장(읽을 시간 확보). receiver의 maxHp 인스펙터 값을 덮는다.")]
    [SerializeField, Range(1, 10)] int enemyHp = 4;

    [Header("콜라이더 (런타임 부착 — 프리팹엔 콜라이더 없음)")]
    [Tooltip("부착할 캡슐 콜라이더 반경(m). Caniathrox 몸통 대략 폭.")]
    [SerializeField, Min(0.1f)] float colliderRadius = 0.9f;
    [Tooltip("캡슐 콜라이더 높이(m).")]
    [SerializeField, Min(0.2f)] float colliderHeight = 2.0f;
    [Tooltip("캡슐 중심 Y(m) — 바닥에 발 둔 몸통 중앙.")]
    [SerializeField] float colliderCenterY = 1.0f;

    // 레이어 7 = "Zombie" = KatanaWeapon.enemyMask 기본값. 이름으로 해석(인덱스 하드코드 회피, 미존재 시 경고).
    int _enemyLayer;
    AttackTokenPool _tokenPool;                                    // 공유 공격 토큰(동시 돌진 수 제한) — Awake 1회 생성.
    readonly List<GameObject> _alive = new List<GameObject>();     // 살아있는 적 추적(보충 판단·시체 회수).
    float _respawnTimer;
    bool _canSpawn;

    void Awake()
    {
        ResolveRefsIfNeeded();

        _enemyLayer = LayerMask.NameToLayer("Zombie");
        if (_enemyLayer < 0)
        {
            Debug.LogError("[CombatSliceSpawner] 'Zombie' 레이어(7) 미존재 — KatanaWeapon.enemyMask와 불일치하면 적을 못 때린다. Default(0) 폴백.");
            _enemyLayer = 0;
        }

        if (playerTarget == null)
        {
            var brain = FindObjectOfType<PlayerBrain>();
            if (brain != null) playerTarget = brain.transform;
        }
        if (playerTarget == null)
        {
            Debug.LogError("[CombatSliceSpawner] playerTarget 미할당 + PlayerBrain 미발견 — 적이 추격할 대상 없음(스폰 중단).");
            return;
        }

        if (telegraphPool == null)
        {
            telegraphPool = GetComponentInChildren<TelegraphPool>();
            if (telegraphPool == null) telegraphPool = FindObjectOfType<TelegraphPool>();
            if (telegraphPool == null)
                Debug.LogWarning("[CombatSliceSpawner] TelegraphPool 미발견 — Coil 돌진 예고 장판 생략(모션만). 씬에 TelegraphPool을 두거나 필드 할당.");
        }

        if (caniathroxPrefab == null)
        {
            Debug.LogError("[CombatSliceSpawner] caniathroxPrefab 미할당 — 적 스폰 불가");
            return;
        }
        if (attackController == null)
        {
            Debug.LogError("[CombatSliceSpawner] attackController 미할당 — 상태머신 없음(스폰 중단)");
            return;
        }

        // 공유 토큰 풀 — 호드가 커도 동시 돌진/텔레그래프 수를 maxAttackTokens로 제한(가독성 유지).
        _tokenPool = new AttackTokenPool(maxAttackTokens);
        _canSpawn = true;

        // 초기 버스트 — swarmSize만큼 즉시 깔아 "엄청 많이 몰려온" 상태로 시작(많으면 로드 시 1프레임 히치).
        for (int i = 0; i < swarmSize; i++) SpawnOne(playerTarget.position);
    }

    // 끊임없이 몰려오기 — 죽은(비활성) 적을 회수하고 swarmSize가 되게 보충(respawnInterval throttle).
    void Update()
    {
        // ★cap 런타임 반영 — 인스펙터 maxAttackTokens/uncapTokens를 플레이 중 즉시 풀에 밀어넣는다(슬라이더로 손맛 튜닝·게이트 테스트).
        //   continuousSwarm 여부와 무관하게 매 프레임(저비용) — 1회 스폰 모드에서도 cap 조정이 먹게.
        if (_tokenPool != null)
        {
            int desired = uncapTokens ? Mathf.Max(maxAttackTokens, swarmSize) : maxAttackTokens;
            if (_tokenPool.MaxTokens != desired) _tokenPool.SetMax(desired);
        }

        if (!_canSpawn || !continuousSwarm || playerTarget == null) return;

        // 시체 회수: receiver.Die가 SetActive(false) → 여기서 Destroy(메모리 누적 방지). null도 정리.
        for (int i = _alive.Count - 1; i >= 0; i--)
        {
            var e = _alive[i];
            if (e == null) { _alive.RemoveAt(i); continue; }
            if (!e.activeSelf) { Destroy(e); _alive.RemoveAt(i); }
        }

        if (_alive.Count >= swarmSize) return;
        _respawnTimer -= Time.deltaTime;
        if (_respawnTimer <= 0f)
        {
            _respawnTimer = respawnInterval;
            SpawnOne(playerTarget.position);
        }
    }

    // 한 마리 생성·와이어링·활성화 후 _alive 등록. (★비활성 생성 → 주입 → 활성화 = Chaser/Receiver Awake 함정 회피.)
    void SpawnOne(Vector3 center)
    {
        float ang = Random.value * Mathf.PI * 2f;
        float r = spawnRadius + Random.Range(-radiusJitter, radiusJitter);
        Vector3 pos = center + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
        pos.y = 0f;

        var enemy = Instantiate(caniathroxPrefab, pos, Quaternion.identity);
        enemy.name = "Caniathrox";
        enemy.SetActive(false);

        Vector3 look = center - pos; look.y = 0f;
        if (look.sqrMagnitude > 0.0001f) enemy.transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);

        // ① 피격 가능: 콜라이더 + 레이어 7(Zombie) — KatanaWeapon.enemyMask 정합. 트리거+kinematic(루트모션이 콜라이더 이동).
        enemy.layer = _enemyLayer;
        var col = enemy.AddComponent<CapsuleCollider>();
        col.radius = colliderRadius;
        col.height = colliderHeight;
        col.center = new Vector3(0f, colliderCenterY, 0f);
        col.direction = 1;
        col.isTrigger = true;
        var rb = enemy.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.None;

        // ② receiver(피격 반응): 루트에 부착(콜라이더·Animator와 같은 GO → GetComponentInParent<IDamageable> 정합).
        var receiver = enemy.AddComponent<EnemyDamageReceiver>();
        receiver.SetMaxHp(enemyHp);

        // ③ Chaser 와이어링 + 텔레그래프 풀.
        // ★H-1: Animator 검증 — 없으면 Chaser가 Awake에서 자기비활성화되며 GO가 _alive 슬롯을 영구 점유해 보충이 무음 중단된다.
        //   GetComponentInChildren는 루트(self)도 포함하므로 정상 프리팹은 통과. 프리팹 파손 시 즉시 fail-fast(취소+Destroy).
        var animator = enemy.GetComponentInChildren<Animator>();
        if (animator == null)
        {
            Debug.LogError("[CombatSliceSpawner] Caniathrox 프리팹에 Animator 없음 — 이 스폰 취소(슬롯 누수 방지).", caniathroxPrefab);
            Destroy(enemy);
            return;
        }
        var chaser = enemy.AddComponent<CaniathroxChaser>();
        chaser.model = enemy.transform;
        chaser.modelAnimator = animator;
        chaser.target = playerTarget;
        chaser.attackController = attackController;
        chaser.tokenPool = _tokenPool;
        chaser.telegraphPool = telegraphPool;
        chaser.SetSurroundSlot(Random.Range(-90f, 90f));   // 포위 슬롯 무작위(사방 포위 — 호드라 균등분배 불필요).

        enemy.SetActive(true);   // 이제 Receiver.Awake + Chaser.Awake 발화(필드 채워진 상태).
        _alive.Add(enemy);
    }

    void ResolveRefsIfNeeded()
    {
#if UNITY_EDITOR
        if (caniathroxPrefab == null)
            caniathroxPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Protofactor/Monster Full Pack Vol 2/Monster Pack Vol 8/Caniathrox/Prefab/Caniathrox.prefab");
        if (attackController == null)
            attackController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/_Project/Animations/CaniathroxAttack.controller");
#endif
    }
}
