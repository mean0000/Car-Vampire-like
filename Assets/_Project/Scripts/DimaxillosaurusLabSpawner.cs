// Dimaxillosaurus 격투 랩 스포너 — 플레이 진입 시 플레이어 캡슐 1 + Dimax 격투체 여러 + 공유 풀(토큰·장판) 생성·와이어링.
// VenodonteLabSpawner(원거리)의 근접 버전. 같은 LabPlayerController/LabSimpleCamera 재사용.
//
// 셋업 흐름(Awake):
//   1) 플레이어 캡슐(Rigidbody + LabPlayerController), 원점.
//   2) JudgeCam에 LabSimpleCamera 부착 + target=플레이어.
//   3) 공유 AttackTokenPool 1개 생성. ★장판 텔레그래프 제거(Dimax=클로월, 미사용) — TelegraphPool 미생성/미주입(시스템 파일은 보존, 타 종용).
//   4) Dimax N마리를 플레이어 둘레 반경에 원형 분산, 각자 DimaxillosaurusBrawler 부착 + 토큰 풀 주입.
using UnityEngine;

public class DimaxillosaurusLabSpawner : MonoBehaviour
{
    [Header("프리팹/컨트롤러 (빌더가 와이어링)")]
    [SerializeField] GameObject dimaxPrefab;
    [SerializeField] RuntimeAnimatorController attackController;

    [Header("스폰 (체감 노브)")]
    [SerializeField, Range(1, 12)] int enemyCount = 4;
    [Tooltip("플레이어 중심 스폰 반경(m). ★클로월: 멀리서부터 포효→클로질로 다가오는 '벽' 진입이 보이게 넉넉히.")]
    [SerializeField, Min(4f)] float spawnRadius = 9f;
    [SerializeField, Min(0f)] float radiusJitter = 1.5f;

    [Header("군중 AI — 동시 교전 토큰 (★클로월에선 비게이팅)")]
    [Tooltip("토큰 풀 크기(시스템 보존용). ★클로월 드라이버는 비게이팅 — 토큰 못 잡아도 전진은 멈추지 않음(모두가 벽). 동시 교전 제한 의미는 약함.")]
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
        if (camGo == null) { Debug.LogWarning("[DimaxLabSpawner] JudgeCam 미발견 — 카메라 추종 비활성"); return; }
        var cam = camGo.GetComponent<LabSimpleCamera>();
        if (cam == null) cam = camGo.AddComponent<LabSimpleCamera>();
        cam.SetTarget(playerT);
    }

    void SpawnEnemies(Transform playerT)
    {
        if (dimaxPrefab == null) { Debug.LogError("[DimaxLabSpawner] dimaxPrefab 미할당"); return; }
        if (attackController == null) Debug.LogError("[DimaxLabSpawner] attackController 미할당");

        // ★장판 텔레그래프 제거(Dimax=클로월, 미사용) — TelegraphPool 미생성. TelegraphPad/Pool 클래스는 타 종용으로 보존.
        // 공유 공격 토큰 풀(동시 교전 수 제한 시스템 — 클로월에선 비게이팅, 드라이버 참조).
        var tokenPool = new AttackTokenPool(maxAttackTokens);

        for (int i = 0; i < enemyCount; i++)
        {
            float ang = (360f / enemyCount) * i * Mathf.Deg2Rad;
            float r = spawnRadius + Random.Range(-radiusJitter, radiusJitter);
            Vector3 pos = playerStart + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);

            // ★함정 회피(Caniathrox/Venodonte 전례): 비활성 생성 → 필드 와이어링 → 활성화.
            //   활성 상태로 AddComponent하면 Awake가 즉시 돌며 modelAnimator null로 자살.
            var enemy = Instantiate(dimaxPrefab, pos, Quaternion.identity);
            enemy.name = $"Dimaxillosaurus_{i}";
            enemy.SetActive(false);

            Vector3 look = playerStart - pos; look.y = 0f;
            if (look.sqrMagnitude > 0.0001f) enemy.transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);

            var brawler = enemy.AddComponent<DimaxillosaurusBrawler>();
            brawler.model = enemy.transform;
            brawler.modelAnimator = enemy.GetComponentInChildren<Animator>();   // Dimax는 Animator가 루트에
            brawler.target = playerT;
            brawler.attackController = attackController;
            brawler.tokenPool = tokenPool;

            enemy.SetActive(true);   // 이제 Awake 발화 — 필드 다 채워진 상태에서 컨트롤러 스왑·applyRootMotion.
        }
    }

    void ResolveRefsIfNeeded()
    {
#if UNITY_EDITOR
        if (dimaxPrefab == null)
            dimaxPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Protofactor/Monster Full Pack Vol 2/Monster Pack Vol 7/Dimaxillosaurus/Prefab/Dimaxillosaurus.prefab");
        if (attackController == null)
            attackController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/_Project/Animations/DimaxillosaurusBrawler.controller");
#endif
    }
}
