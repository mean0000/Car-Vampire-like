// 룩 랩 미니 전투 스포너 — 플레이 진입 시 플레이어(캡슐) 1 + Caniathrox 여러 마리를 런타임 생성·와이어링.
// 씬 비대화 회피용(런타임 스폰). 각 Caniathrox는 프리팹 인스턴스라 Animator를 각자 가진다 → 독립 상태머신 자동 보장.
//
// 셋업 흐름(Awake):
//   1) 플레이어 캡슐 생성(Rigidbody + CapsuleCollider + LabPlayerController), 원점 배치.
//   2) JudgeCam에 LabSimpleCamera 부착 + target = 플레이어(빌더가 잡은 45°/15m 프레이밍 보존).
//   3) Caniathrox N마리를 플레이어 주변 반경에 원형 분산 배치, 각자 CaniathroxChaser 부착 + 컨트롤러 할당.
//      → 각자 target = 플레이어. 각자 독립 Animator/루트모션.
using UnityEngine;

public class LabCombatSpawner : MonoBehaviour
{
    [Header("프리팹/컨트롤러 (빌더가 와이어링)")]
    [Tooltip("Caniathrox 프리팹.")]
    [SerializeField] GameObject caniathroxPrefab;
    [Tooltip("CaniathroxAttack.controller — 승인된 상태머신(IdleAngry→Approach→Lunge→Spit).")]
    [SerializeField] RuntimeAnimatorController attackController;

    [Header("스폰 (피하기 체감 노브)")]
    [Tooltip("스폰할 Caniathrox 마리 수.")]
    [SerializeField, Range(1, 12)] int enemyCount = 6;
    [Tooltip("플레이어 중심에서 적 스폰 반경(m). 도약 전진(4.67m)+lungeRange보다 커야 '달려오는' 접근이 보인다.")]
    [SerializeField, Min(4f)] float spawnRadius = 12f;
    [Tooltip("스폰 반경 무작위 흔들림(m) — 일렬 배치 방지(±).")]
    [SerializeField, Min(0f)] float radiusJitter = 2f;

    [Header("군중 AI — 공격 토큰 (동시 공격 제한 노브)")]
    [Tooltip("동시에 도약(Lunge) 공격할 수 있는 최대 적 수. 1=한 마리씩, 2=두 마리까지. 나머진 접근만(서성).")]
    [SerializeField, Range(1, 6)] int maxAttackTokens = 2;

    [Header("플레이어")]
    [Tooltip("플레이어 캡슐 시작 위치.")]
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

        // 비주얼 — 회피 가독성 위해 밝은 단색(적의 레드-오렌지와 구분).
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit != null)
        {
            var mat = new Material(lit);
            mat.SetColor("_BaseColor", new Color(0.35f, 0.7f, 0.95f, 1f));
            mat.SetFloat("_Smoothness", 0.1f);
            player.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        // 캡슐 기본 콜라이더는 유지(트리거 아님) — 강체 이동에 필요.
        player.AddComponent<Rigidbody>();
        player.AddComponent<LabPlayerController>();
        return player;
    }

    void AttachCamera(Transform playerT)
    {
        var camGo = GameObject.Find("JudgeCam");
        if (camGo == null) { Debug.LogWarning("[LabCombatSpawner] JudgeCam 미발견 — 카메라 추종 비활성"); return; }
        var cam = camGo.GetComponent<LabSimpleCamera>();
        if (cam == null) cam = camGo.AddComponent<LabSimpleCamera>();
        cam.SetTarget(playerT);   // 명시 할당(자동 검색 폴백도 있지만 명시가 안전)
    }

    void SpawnEnemies(Transform playerT)
    {
        if (caniathroxPrefab == null)
        {
            Debug.LogError("[LabCombatSpawner] caniathroxPrefab 미할당 — 적 스폰 불가");
            return;
        }
        if (attackController == null)
            Debug.LogError("[LabCombatSpawner] attackController 미할당 — 상태머신 없음");

        // 공유 공격 토큰 풀 1개 — 모든 Chaser에 같은 참조 주입(동시 도약 수 = maxAttackTokens로 제한).
        var tokenPool = new AttackTokenPool(maxAttackTokens);

        for (int i = 0; i < enemyCount; i++)
        {
            // 원형 분산 + 반경 지터 — 플레이어를 둘러싸고 사방에서 달려오게.
            float ang = (360f / enemyCount) * i * Mathf.Deg2Rad;
            float r = spawnRadius + Random.Range(-radiusJitter, radiusJitter);
            Vector3 pos = playerStart + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);

            // ★함정 회피: 비활성으로 생성한 뒤 필드 와이어링 → 활성화.
            //   활성 인스턴스에 AddComponent하면 Chaser.Awake가 그 자리에서 즉시 동기 실행되는데,
            //   그 시점엔 modelAnimator가 아직 null이라 Chaser가 스스로 enabled=false로 죽는다.
            //   비활성 상태로 만들어 두면 Awake가 SetActive(true)까지 지연돼, 와이어링된 필드를 보고 Awake가 돈다.
            var enemy = Instantiate(caniathroxPrefab, pos, Quaternion.identity);
            enemy.name = $"Caniathrox_{i}";
            enemy.SetActive(false);

            // 스폰 즉시 플레이어를 바라보게(첫 IdleAngry 조준 전 초기 방향).
            Vector3 look = playerStart - pos; look.y = 0f;
            if (look.sqrMagnitude > 0.0001f) enemy.transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);

            var chaser = enemy.AddComponent<CaniathroxChaser>();
            chaser.model = enemy.transform;
            chaser.modelAnimator = enemy.GetComponentInChildren<Animator>();   // 각 인스턴스 독립 Animator
            chaser.target = playerT;
            chaser.attackController = attackController;   // 인스턴스별 runtimeAnimatorController 할당 → 공유 상태 없음
            chaser.tokenPool = tokenPool;                 // 공유 토큰 풀 주입(동시 공격 제한)

            // 포위 슬롯: 적마다 다른 각도를 분배해 정면 일변도가 아니라 측면·후방에서도 파고들게(surround).
            //   ±60° 범위로 분산 — 너무 넓으면 등 뒤로 돌아 비효율, 좁으면 포위감 약함.
            float slot = enemyCount > 1 ? Mathf.Lerp(-60f, 60f, (float)i / (enemyCount - 1)) : 0f;
            chaser.SetSurroundSlot(slot);

            enemy.SetActive(true);   // 이제 Chaser.Awake 발화 — 필드가 다 채워진 상태에서 컨트롤러 스왑·applyRootMotion 설정.
        }
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
