// Venosaur 격투 랩 스포너 — 플레이 진입 시 플레이어 캡슐 1 + Venosaur 격투체 여러 + 공유 토큰 풀 생성·와이어링.
//   ★DimaxillosaurusLabSpawner(슬렌더 클로월)의 *직접 재활용* — 프리팹/컨트롤러 경로만 Venosaur로 교체. 같은 LabPlayerController/LabSimpleCamera.
//
// ★Tint 가드(Story 인계): Venosaur는 ×5 Tint 변형(Brown/Grey/Green/Purple/Pink)이 있으나 *현장조 통째 동질 변이*라 틴트별 개별 모션 금지.
//   랩에선 베이스 프리팹(Venosaur_Tint_Green = 풀 프리팹, 나머지는 그 Variant) 1색만 스폰. 모두 같은 컨트롤러/드라이버 공유.
//
// ★2026-06-14 추가: ClawHit 컨택 임팩트 VFX용 SmashImpactPool 생성·주입.
//   SmashShock.shader 완전 재활용 — 절제 파라미터(r1.2, intensity 0.9, duration 0.14s)로 적 클로 = 슬램보다 약하게.
using UnityEngine;

public class VenosaurLabSpawner : MonoBehaviour
{
    [Header("프리팹/컨트롤러 (빌더가 와이어링)")]
    [SerializeField] GameObject venosaurPrefab;
    [SerializeField] RuntimeAnimatorController attackController;

    [Header("스폰 (체감 노브)")]
    [SerializeField, Range(1, 12)] int enemyCount = 4;
    [Tooltip("플레이어 중심 스폰 반경(m). ★클로월: 멀리서부터 포효→클로질로 다가오는 '벽' 진입이 보이게 넉넉히.")]
    [SerializeField, Min(4f)] float spawnRadius = 10f;   // ★Venosaur는 R 클로 전진 4.094m라 한 스윙 도달거리가 큼 — Dimax(9)보다 살짝 넓게
    [SerializeField, Min(0f)] float radiusJitter = 1.5f;

    [Header("군중 AI — 동시 교전 토큰 (★클로월에선 비게이팅)")]
    [Tooltip("토큰 풀 크기(시스템 보존용). ★클로월 드라이버는 비게이팅 — 토큰 못 잡아도 전진은 멈추지 않음(모두가 벽).")]
    [SerializeField, Range(1, 4)] int maxAttackTokens = 2;

    [Header("플레이어")]
    [SerializeField] Vector3 playerStart = Vector3.zero;

    // ★클로 임팩트 풀 — SmashImpactPool 재활용. poolSize=4(슬램 8보다 작음 — 단발 클로는 연타 간격이 있어 동시 활성 ≤ 2~3).
    SmashImpactPool _clawImpactPool;

    void Awake()
    {
        ResolveRefsIfNeeded();
        BuildClawImpactPool();
        var player = SpawnPlayer();
        AttachCamera(player.transform);
        SpawnEnemies(player.transform);
    }

    // SmashImpactPool을 비활성 GO에서 생성 — Awake가 Build() 호출 전에 poolSize를 주입하는 규약(TelegraphPool·SmashImpactPool 동일).
    void BuildClawImpactPool()
    {
        var poolGo = new GameObject("VenosaurClawImpactPool");
        poolGo.transform.SetParent(transform, false);
        // 비활성 상태로 AddComponent → InitPoolSize → SetActive(true) → Awake → Build() 순서 보장.
        poolGo.SetActive(false);
        _clawImpactPool = poolGo.AddComponent<SmashImpactPool>();
        _clawImpactPool.InitPoolSize(enemyCount * 2);   // ★enemyCount 연동(마진 ×2) — 4마리 무한 클로질이 같은 프레임에 ClawHit 다발 시 풀 고갈→RecycleOldest 팝핑 방지.
        poolGo.SetActive(true);   // 이제 Awake → Build() 실행 (M_KillBurst_Body + SmashShock 로드).

        // ★VfxDirector에 풀 등록 — 디렉터 경유 RequestImpact가 이 풀을 쓸 수 있게.
        //   디렉터가 씬에 없으면 Instance가 자동 생성(SlashVfxPool 패턴). 항상 안전.
        VfxDirector.Instance.RegisterImpactPool(_clawImpactPool);
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
        if (camGo == null) { Debug.LogWarning("[VenosaurLabSpawner] JudgeCam 미발견 — 카메라 추종 비활성"); return; }
        var cam = camGo.GetComponent<LabSimpleCamera>();
        if (cam == null) cam = camGo.AddComponent<LabSimpleCamera>();
        cam.SetTarget(playerT);
    }

    void SpawnEnemies(Transform playerT)
    {
        if (venosaurPrefab == null) { Debug.LogError("[VenosaurLabSpawner] venosaurPrefab 미할당"); return; }
        if (attackController == null) Debug.LogError("[VenosaurLabSpawner] attackController 미할당");

        // 공유 공격 토큰 풀(동시 교전 수 제한 시스템 — 클로월에선 비게이팅, 드라이버 참조).
        var tokenPool = new AttackTokenPool(maxAttackTokens);

        for (int i = 0; i < enemyCount; i++)
        {
            float ang = (360f / enemyCount) * i * Mathf.Deg2Rad;
            float r = spawnRadius + Random.Range(-radiusJitter, radiusJitter);
            Vector3 pos = playerStart + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);

            // ★함정 회피: 비활성 생성 → 필드 와이어링 → 활성화(활성 상태로 AddComponent하면 Awake가 즉시 돌며 modelAnimator null로 자살).
            var enemy = Instantiate(venosaurPrefab, pos, Quaternion.identity);
            enemy.name = $"Venosaur_{i}";
            enemy.SetActive(false);

            Vector3 look = playerStart - pos; look.y = 0f;
            if (look.sqrMagnitude > 0.0001f) enemy.transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);

            var brawler = enemy.AddComponent<VenosaurBrawler>();
            brawler.model = enemy.transform;
            brawler.modelAnimator = enemy.GetComponentInChildren<Animator>();   // Venosaur는 Animator가 루트에(검증됨)
            brawler.target = playerT;
            brawler.attackController = attackController;
            brawler.tokenPool = tokenPool;
            // ★ClawHit 컨택 임팩트 풀 주입 — 공유 풀 1개를 모든 Venosaur가 쓴다(SmashImpactPool 설계와 동일).
            brawler.clawImpactPool = _clawImpactPool;

            // ★시그니처 오라 주입 (LV3 = Venosaur 기본).
            //   ★activating 전에 AddComponent → 필드 셋업 → SetActive(true) 규약 유지.
            var aura = enemy.AddComponent<MonsterSignatureAura>();
            // auraRenderers는 지금 비워둠 — Inspector에서 프리팹 본 구조 보고 변이 핵심부 지정 필요.
            // ★SetActive(true) 후 Awake에서 SkinnedMeshRenderer 전신 폴백 경고가 뜰 것 — 의도된 경고(부위 지정 유도).
            brawler.signatureAura = aura;

            enemy.SetActive(true);   // 이제 Awake 발화 — 필드 다 채워진 상태에서 컨트롤러 스왑·applyRootMotion.
        }
    }

    void ResolveRefsIfNeeded()
    {
#if UNITY_EDITOR
        if (venosaurPrefab == null)
            venosaurPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Protofactor/Monster Full Pack Vol 2/Monster Pack Vol 12/Venosaur/Prefabs/Venosaur_Tint_Green.prefab");
        if (attackController == null)
            attackController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/_Project/Animations/VenosaurBrawler.controller");
#endif
    }
}
