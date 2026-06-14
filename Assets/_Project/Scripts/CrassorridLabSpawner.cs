// Crassorrid 브루트 랩 스포너 — 플레이 진입 시 플레이어 캡슐 1 + Crassorrid 브루트 여러 + 공유 풀(토큰·★장판) 생성·와이어링.
//   DimaxillosaurusLabSpawner의 브루트 버전. ★결정적 차이 = TelegraphPool을 *실제로 생성·주입*(Crassorrid가 장판 첫 소비자).
//   같은 LabPlayerController/LabSimpleCamera 재사용.
//
// 셋업 흐름(Awake):
//   1) 플레이어 캡슐(Rigidbody + LabPlayerController), 원점.
//   2) JudgeCam에 LabSimpleCamera 부착 + target=플레이어.
//   3) 공유 AttackTokenPool(동시 스매시 제한) + ★공유 TelegraphPool(장판 풀, 첫 가동) 생성.
//   4) Crassorrid N마리를 플레이어 둘레 반경에 원형 분산, 각자 CrassorridBrawler 부착 + 두 풀 주입.
using UnityEngine;

public class CrassorridLabSpawner : MonoBehaviour
{
    [Header("프리팹/컨트롤러 (빌더가 와이어링)")]
    [SerializeField] GameObject crassorridPrefab;
    [SerializeField] RuntimeAnimatorController attackController;

    [Header("스폰 (체감 노브)")]
    [Tooltip("플레이어 둘레 스폰 적 수. 브루트라 적게(거구 4마리도 위협).")]
    [SerializeField, Range(1, 8)] int enemyCount = 3;
    [Tooltip("플레이어 중심 스폰 반경(m). 거구가 멀리서 포효→접근→스매시 사이클이 보이게 넉넉히.")]
    [SerializeField, Min(5f)] float spawnRadius = 11f;
    [SerializeField, Min(0f)] float radiusJitter = 1.5f;

    [Header("군중 AI — 동시 스매시 토큰")]
    [Tooltip("토큰 풀 크기 = 동시에 내려찍을 수 있는 브루트 수. 거구 여럿 동시 슬램은 불공정 → 1~2로 제한.")]
    [SerializeField, Range(1, 4)] int maxAttackTokens = 1;

    [Header("★장판 텔레그래프 풀 (이 종이 첫 소비자)")]
    [Tooltip("미리 생성할 장판 패드 수. 동시 활성 스매시 장판이 이보다 많으면 가장 오래된 것 회수.")]
    [SerializeField, Range(2, 16)] int telegraphPoolSize = 8;

    [Header("★슬램 임팩트 VFX 풀 (충격파·먼지·그을림)")]
    [Tooltip("미리 생성할 임팩트 FX 수. 동시 활성 임팩트가 이보다 많으면 가장 오래된 것 회수. 브루트 연타 고려.")]
    [SerializeField, Range(2, 16)] int impactPoolSize = 8;

    [Header("플레이어")]
    [SerializeField] Vector3 playerStart = Vector3.zero;

    void Awake()
    {
        ResolveRefsIfNeeded();
        var player = SpawnPlayer();
        AttachCamera(player.transform);
        SpawnEnemies(player.transform);
    }

    GameObject SpawnPlayer()
    {
        var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "LabPlayer";
        player.transform.position = playerStart + Vector3.up * 1f;
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit != null)
        {
            var mat = new Material(lit);
            mat.SetColor("_BaseColor", new Color(0.35f, 0.7f, 0.95f, 1f));  // 밝은 시안 — 적 레드오렌지·장판과 구분
            mat.SetFloat("_Smoothness", 0.1f);
            player.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }
        player.AddComponent<Rigidbody>();
        player.AddComponent<LabPlayerController>();
        return player;
    }

    void AttachCamera(Transform playerT)
    {
        var camGo = GameObject.Find("JudgeCam");
        if (camGo == null) { Debug.LogWarning("[CrassorridLabSpawner] JudgeCam 미발견 — 카메라 추종 비활성"); return; }
        var cam = camGo.GetComponent<LabSimpleCamera>();
        if (cam == null) cam = camGo.AddComponent<LabSimpleCamera>();
        cam.SetTarget(playerT);
    }

    void SpawnEnemies(Transform playerT)
    {
        if (crassorridPrefab == null) { Debug.LogError("[CrassorridLabSpawner] crassorridPrefab 미할당"); return; }
        if (attackController == null) Debug.LogError("[CrassorridLabSpawner] attackController 미할당");

        // 공유 공격 토큰 풀(동시 스매시 제한 — 드라이버 참조).
        var tokenPool = new AttackTokenPool(maxAttackTokens);

        // ★공유 장판 텔레그래프 풀 (첫 가동) — MonoBehaviour. ★비활성 GO에 붙여 poolSize 주입 *후* 활성화:
        //   TelegraphPool.Awake()가 Build()로 poolSize만큼 prewarm하므로, AddComponent 직후(=Awake 즉시 실행) 전에 size를 박아야 한다.
        //   inactive GO면 AddComponent해도 Awake가 안 돌고, SetActive(true)에서 비로소 돈다 → 주입된 size로 prewarm.
        var telGo = new GameObject("TelegraphPool");
        telGo.SetActive(false);
        var telegraphPool = telGo.AddComponent<TelegraphPool>();
        telegraphPool.InitPoolSize(telegraphPoolSize);   // ★#if 없는 런타임 주입(빌드 무력화 함정 회피). Build() 전(inactive)이라 prewarm에 반영.
        telGo.SetActive(true);   // 이제 Awake→Build(주입된 poolSize로 prewarm).

        // ★슬램 임팩트 VFX 풀 — 같은 inactive-GO 주입 패턴(InitPoolSize는 Build 전에 박혀야 prewarm 반영).
        var impGo = new GameObject("SmashImpactPool");
        impGo.SetActive(false);
        var impactPool = impGo.AddComponent<SmashImpactPool>();
        impactPool.InitPoolSize(impactPoolSize);
        impGo.SetActive(true);

        // ★카메라 쉐이크 리스너 1회 보장 — JudgeCam MMCameraShaker. 씬 안 더티(런타임 부트스트랩).
        //   히트스탑은 프로젝트 HitStop.Do()가 자체 시간 소유라 리스너 불필요.
        var judgeCamGo = GameObject.Find("JudgeCam");
        SmashFeel.EnsureListeners(judgeCamGo != null ? judgeCamGo.GetComponent<Camera>() : Camera.main);

        for (int i = 0; i < enemyCount; i++)
        {
            float ang = (360f / enemyCount) * i * Mathf.Deg2Rad;
            float r = spawnRadius + Random.Range(-radiusJitter, radiusJitter);
            Vector3 pos = playerStart + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);

            // ★함정 회피(Caniathrox/Venodonte/Dimax 전례): 비활성 생성 → 필드 와이어링 → 활성화.
            //   활성 상태로 AddComponent하면 Awake가 즉시 돌며 modelAnimator null로 자살.
            var enemy = Instantiate(crassorridPrefab, pos, Quaternion.identity);
            enemy.name = $"Crassorrid_{i}";
            enemy.SetActive(false);

            Vector3 look = playerStart - pos; look.y = 0f;
            if (look.sqrMagnitude > 0.0001f) enemy.transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);

            var brawler = enemy.AddComponent<CrassorridBrawler>();
            brawler.model = enemy.transform;
            brawler.modelAnimator = enemy.GetComponentInChildren<Animator>();   // Crassorrid는 Animator가 루트에
            brawler.target = playerT;
            brawler.attackController = attackController;
            brawler.tokenPool = tokenPool;
            brawler.telegraphPool = telegraphPool;   // ★장판 풀 주입(첫 소비자)
            brawler.impactPool = impactPool;         // ★슬램 임팩트 VFX 풀 주입

            // 포위 슬롯 각도 분배(인스턴스별 측면/후방).
            brawler.SetSurroundSlot((360f / enemyCount) * i - 180f * (enemyCount > 1 ? 1f : 0f) / enemyCount);

            enemy.SetActive(true);   // 이제 Awake 발화 — 필드 다 채워진 상태에서 컨트롤러 스왑·applyRootMotion.
        }
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
