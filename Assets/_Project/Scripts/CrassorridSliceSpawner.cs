// 읽고-베기 전투 슬라이스 — Crassorrid 브루트(읽기 티어 엘리트) 스포너.
//   ★2026-07-04 위협 재배역: 랩의 읽기 티어를 Caniathrox 40마리(장판 시트지옥)에서 거구 브루트 소수로 교체.
//     잡몹(WhiteboxVerbLabSpawner 캡슐)=질량·접촉 압박(텔레그래프 ❌) / 브루트 2~4=커밋 읽기 대상(장판=돌파 표식).
//   CombatSliceSpawner의 피격 배선(콜라이더+레이어7+receiver 런타임 부착)과 CrassorridLabSpawner의
//   풀 부트스트랩(inactive-GO InitPoolSize 패턴)을 합친 형태. 플레이어는 씬 정적(Frank) — 안 만든다.
//
// ════════ 레이어 정합 (★못 맞추면 영원히 못 때림 — CombatSliceSpawner 주석 계승) ════════
//   KatanaWeapon.DoHit이 enemyMask(레이어 7 "Zombie")로 OverlapSphere → GetComponentInParent<IDamageable>.
//   콜라이더·receiver·드라이버 전부 프리팹 루트(Animator도 루트)에 부착 = GetComponentInParent 정합.
//
// ════════ 스폰 함정 (전 스포너 공통 계승) ════════
//   비활성 생성 → 필드 와이어링 → 활성화. 활성 인스턴스에 AddComponent하면 Brawler.Awake가 즉시 동기
//   실행되는데 그때 modelAnimator가 null이라 스스로 죽는다.
using System.Collections.Generic;
using UnityEngine;

public class CrassorridSliceSpawner : MonoBehaviour
{
    [Header("프리팹/컨트롤러 (에디터 자동 해석 — 빌드 시 인스펙터 할당 필요)")]
    [Tooltip("Crassorrid 프리팹(7m 브루트).")]
    [SerializeField] GameObject crassorridPrefab;
    [Tooltip("CrassorridBrawler.controller — 승인된 상태머신(Idle→Approach→SmashWindup→Strike→Recovery).")]
    [SerializeField] RuntimeAnimatorController attackController;
    [Tooltip("★장판 텔레그래프 공유 풀 — 씬 TelegraphPool 할당. 비우면 씬에서 찾는다(없으면 장판 생략 경고).")]
    [SerializeField] TelegraphPool telegraphPool;

    [Header("타겟 (Frank 플레이어 — 씬 정적)")]
    [Tooltip("추격 대상 = 플레이어 루트. 비우면 PlayerBrain을 가진 GameObject를 씬에서 찾는다.")]
    [SerializeField] Transform playerTarget;

    [Header("읽기 티어 규모 (노브 — 화면에 1~3커밋이 '읽히는' 상한)")]
    [Tooltip("동시에 살아있게 유지할 브루트 수. ★소수 엘리트(2~4) — 장판 하나하나가 읽을 가치 있는 정보로. 동시 슬램은 BruteSlamCoordinator가 각·박자로 분산. **0=엘리트 없음**(잡몹 스택 격리 테스트 — 07-04 유저 지시).")]
    [SerializeField, Range(0, 4)] int bruteCount = 2;
    [Tooltip("켜면 죽은 만큼 respawnDelay 후 보충(읽기 티어 상시 유지). 끄면 1회만.")]
    [SerializeField] bool continuousBrutes = true;
    [Tooltip("브루트 사망 → 보충까지 지연(초). 엘리트라 즉시 보충 ❌ — 처치의 공백(전선 붕괴 체감)을 준다.")]
    [SerializeField, Min(0f)] float respawnDelay = 4f;
    [Tooltip("플레이어 중심 스폰 반경(m). 거구의 접근이 보이게 넉넉히(카메라 15m).")]
    [SerializeField, Min(6f)] float spawnRadius = 14f;
    [SerializeField, Min(0f)] float radiusJitter = 3f;

    [Header("피격 (읽고-베기 = 금방 안 죽어야 함)")]
    [Tooltip("브루트 HP. 잡몹(2)보다 단단 — 커밋 창을 여러 번 읽게. 크리 ×4 기준 2~3회 읽기 처치.")]
    [SerializeField, Range(1, 40)] int enemyHp = 10;

    [Header("콜라이더 (런타임 부착 — 프리팹엔 콜라이더 없음. 7m 거구 치수)")]
    [SerializeField, Min(0.1f)] float colliderRadius = 1.8f;
    [SerializeField, Min(0.2f)] float colliderHeight = 5.0f;
    [SerializeField] float colliderCenterY = 2.4f;

    [Header("★슬램 임팩트 VFX 풀 (런타임 생성 — 씬 오염 없음)")]
    [SerializeField, Range(2, 16)] int impactPoolSize = 4;

    int _enemyLayer;
    SmashImpactPool _impactPool;
    readonly List<GameObject> _alive = new List<GameObject>();
    float _respawnTimer;
    bool _canSpawn;

    void Awake()
    {
        ResolveRefsIfNeeded();

        _enemyLayer = LayerMask.NameToLayer("Zombie");
        if (_enemyLayer < 0)
        {
            Debug.LogError("[CrassorridSliceSpawner] 'Zombie' 레이어(7) 미존재 — KatanaWeapon.enemyMask와 불일치하면 못 때린다. Default(0) 폴백.");
            _enemyLayer = 0;
        }

        if (playerTarget == null)
        {
            var brain = FindObjectOfType<PlayerBrain>();
            if (brain != null) playerTarget = brain.transform;
        }
        if (playerTarget == null)
        {
            Debug.LogError("[CrassorridSliceSpawner] playerTarget 미할당 + PlayerBrain 미발견 — 스폰 중단.");
            return;
        }

        if (telegraphPool == null)
        {
            telegraphPool = FindObjectOfType<TelegraphPool>();
            if (telegraphPool == null)
                Debug.LogWarning("[CrassorridSliceSpawner] TelegraphPool 미발견 — 슬램 예고 장판 생략(모션만). 씬에 TelegraphPool 필요.");
        }

        if (crassorridPrefab == null) { Debug.LogError("[CrassorridSliceSpawner] crassorridPrefab 미할당 — 스폰 불가"); return; }
        if (attackController == null) { Debug.LogError("[CrassorridSliceSpawner] attackController 미할당 — 상태머신 없음(스폰 중단)"); return; }

        // ★임팩트 풀 — inactive-GO 주입 패턴(InitPoolSize는 Build 전에 박혀야 prewarm 반영, CrassorridLabSpawner 계승).
        var impGo = new GameObject("SmashImpactPool_Slice");
        impGo.SetActive(false);
        _impactPool = impGo.AddComponent<SmashImpactPool>();
        _impactPool.InitPoolSize(impactPoolSize);
        impGo.SetActive(true);

        // ★카메라 쉐이크 리스너 1회 보장 — 슬램 쾅의 무게(씬 안 더티, 런타임 부트스트랩).
        SmashFeel.EnsureListeners(Camera.main);

        _canSpawn = true;
        for (int i = 0; i < bruteCount; i++) SpawnOne(playerTarget.position);
    }

    void Update()
    {
        if (!_canSpawn || !continuousBrutes || playerTarget == null) return;

        // 시체 회수: receiver.Die가 SetActive(false) → Destroy(메모리 누적 방지). null도 정리.
        for (int i = _alive.Count - 1; i >= 0; i--)
        {
            var e = _alive[i];
            if (e == null) { _alive.RemoveAt(i); continue; }
            if (!e.activeSelf) { Destroy(e); _alive.RemoveAt(i); }
        }

        if (_alive.Count >= bruteCount) { _respawnTimer = respawnDelay; return; }
        _respawnTimer -= Time.deltaTime;
        if (_respawnTimer <= 0f)
        {
            _respawnTimer = respawnDelay;
            SpawnOne(playerTarget.position);
        }
    }

    void SpawnOne(Vector3 center)
    {
        float ang = Random.value * Mathf.PI * 2f;
        float r = spawnRadius + Random.Range(-radiusJitter, radiusJitter);
        Vector3 pos = center + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
        pos.y = 0f;

        var enemy = Instantiate(crassorridPrefab, pos, Quaternion.identity);
        enemy.name = "Crassorrid";
        enemy.SetActive(false);

        Vector3 look = center - pos; look.y = 0f;
        if (look.sqrMagnitude > 0.0001f) enemy.transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);

        // ① 피격 가능: 콜라이더 + 레이어 7(Zombie). 트리거+kinematic(루트모션이 콜라이더 이동 — CombatSliceSpawner 계승).
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

        // ② receiver — Brawler.Awake가 GetComponent<EnemyDamageReceiver>로 커밋 신호 대상을 잡는다(존재만 필요 —
        //   첫 실사용은 Update의 DriveCommit이라 Awake 순서 무관. Stab L-1). knockbackResponse 기본 0 = 안 밀림(질량 티어 상단).
        var receiver = enemy.AddComponent<EnemyDamageReceiver>();
        receiver.SetMaxHp(enemyHp);

        // ③ Brawler 와이어링. ★H-1: Animator 검증 — 없으면 Awake 자기비활성화로 _alive 슬롯 영구 점유(보충 무음 중단).
        var animator = enemy.GetComponentInChildren<Animator>();
        if (animator == null)
        {
            Debug.LogError("[CrassorridSliceSpawner] Crassorrid 프리팹에 Animator 없음 — 이 스폰 취소(슬롯 누수 방지).", crassorridPrefab);
            Destroy(enemy);
            return;
        }
        var brawler = enemy.AddComponent<CrassorridBrawler>();
        brawler.model = enemy.transform;
        brawler.modelAnimator = animator;
        brawler.target = playerTarget;
        brawler.attackController = attackController;
        brawler.tokenPool = null;                 // ★브루트는 토큰 수 게이팅 폐기 — BruteSlamCoordinator가 각·박자 분산(2026-06-14).
        brawler.telegraphPool = telegraphPool;
        brawler.impactPool = _impactPool;
        brawler.SetSurroundSlot(Random.Range(-90f, 90f));

        enemy.SetActive(true);   // 이제 Receiver.Awake + Brawler.Awake 발화(필드 채워진 상태).
        _alive.Add(enemy);
    }

    void ResolveRefsIfNeeded()
    {
#if UNITY_EDITOR
        if (crassorridPrefab == null)
            crassorridPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Protofactor/Monster Full Pack Vol 2/Monster Pack vol 9/Crassorrid/Prefab/Crassorrid.prefab");
        if (attackController == null)
            attackController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/_Project/Animations/CrassorridBrawler.controller");
#endif
    }
}
