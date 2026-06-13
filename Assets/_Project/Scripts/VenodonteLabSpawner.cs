// Venodonte 룩 랩 스포너 — 플레이 진입 시 플레이어 캡슐 1 + Venodonte 사수 여러 마리 + 공유 풀(토큰·투사체) 생성·와이어링.
// LabCombatSpawner(Caniathrox 근접)의 사수 버전. 같은 LabPlayerController/LabSimpleCamera 재사용.
//
// 셋업 흐름(Awake):
//   1) 플레이어 캡슐(Rigidbody + LabPlayerController), 원점.
//   2) JudgeCam에 LabSimpleCamera 부착 + target=플레이어.
//   3) 공유 ProjectilePool 1개 + 공유 AttackTokenPool 1개 생성.
//   4) Venodonte N마리를 플레이어 둘레 반경에 원형 분산, 각자 VenodonteShooter 부착 + 풀 주입.
using UnityEngine;

public class VenodonteLabSpawner : MonoBehaviour
{
    [Header("프리팹/컨트롤러 (빌더가 와이어링)")]
    [SerializeField] GameObject venodontePrefab;
    [SerializeField] RuntimeAnimatorController attackController;

    [Header("스폰 (사선 체감 노브)")]
    [SerializeField, Range(1, 12)] int enemyCount = 5;
    [Tooltip("플레이어 중심 스폰 반경(m). 사수 선호거리(9)보다 약간 멀게 — '다가와 사거리 잡고 쏘는' 진입이 보이게.")]
    [SerializeField, Min(4f)] float spawnRadius = 12f;
    [SerializeField, Min(0f)] float radiusJitter = 2f;

    [Header("군중 AI — 동시 사격 제한 (탄막 밀도 규율: 직사 사수 ≤4)")]
    [Tooltip("동시에 사격(Aim→Fire)할 수 있는 최대 사수 수. 1=한 마리씩, 2~3=교차 사격. 나머진 거리 유지·대기.")]
    [SerializeField, Range(1, 4)] int maxAttackTokens = 2;

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
            mat.SetColor("_BaseColor", new Color(0.35f, 0.7f, 0.95f, 1f));  // 밝은 시안 — 적 레드오렌지와 구분
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
        if (camGo == null) { Debug.LogWarning("[VenodonteLabSpawner] JudgeCam 미발견 — 카메라 추종 비활성"); return; }
        var cam = camGo.GetComponent<LabSimpleCamera>();
        if (cam == null) cam = camGo.AddComponent<LabSimpleCamera>();
        cam.SetTarget(playerT);
    }

    void SpawnEnemies(Transform playerT)
    {
        if (venodontePrefab == null) { Debug.LogError("[VenodonteLabSpawner] venodontePrefab 미할당"); return; }
        if (attackController == null) Debug.LogError("[VenodonteLabSpawner] attackController 미할당");

        // 공유 투사체 풀 1개 — 씬 오브젝트로 생성(MonoBehaviour). 모든 사수가 같은 풀로 글롭 발사.
        var poolGo = new GameObject("ProjectilePool");
        var projectilePool = poolGo.AddComponent<ProjectilePool>();

        // 공유 공격 토큰 풀(동시 사격 제한).
        var tokenPool = new AttackTokenPool(maxAttackTokens);

        for (int i = 0; i < enemyCount; i++)
        {
            float ang = (360f / enemyCount) * i * Mathf.Deg2Rad;
            float r = spawnRadius + Random.Range(-radiusJitter, radiusJitter);
            Vector3 pos = playerStart + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);

            // ★함정 회피(Caniathrox 전례): 비활성 생성 → 필드 와이어링 → 활성화.
            //   활성 상태로 AddComponent하면 Awake가 즉시 돌며 modelAnimator null로 자살.
            var enemy = Instantiate(venodontePrefab, pos, Quaternion.identity);
            enemy.name = $"Venodonte_{i}";
            enemy.SetActive(false);

            Vector3 look = playerStart - pos; look.y = 0f;
            if (look.sqrMagnitude > 0.0001f) enemy.transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);

            var shooter = enemy.AddComponent<VenodonteShooter>();
            shooter.model = enemy.transform;
            shooter.modelAnimator = enemy.GetComponentInChildren<Animator>();   // Venodonte는 Animator가 루트에
            shooter.target = playerT;
            shooter.attackController = attackController;
            shooter.tokenPool = tokenPool;
            shooter.projectilePool = projectilePool;

            enemy.SetActive(true);   // 이제 Awake 발화 — 필드 다 채워진 상태에서 컨트롤러 스왑·applyRootMotion.
        }
    }

    void ResolveRefsIfNeeded()
    {
#if UNITY_EDITOR
        if (venodontePrefab == null)
            venodontePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Protofactor/Monster Full Pack Vol 2/Monster Pack Vol 11/Venodonte/Prefabs/Venodonte_Tint1.prefab");
        if (attackController == null)
            attackController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/_Project/Animations/VenodonteAttack.controller");
#endif
    }
}
