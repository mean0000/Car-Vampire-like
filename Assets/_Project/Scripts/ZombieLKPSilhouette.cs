using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// LKP(Last Known Position) 실루엣 — "움직이던" 좀비가 시야 콘에서 벗어나는 순간,
/// 마지막 위치에 반투명 실루엣(SMR 베이크)을 남기고 서서히 지운다. (Splinter Cell 계보,
/// 2026-06-11 브레인스토밍: 모퉁이 문법의 보상 — "저기 있었는데 지금 어디 갔지?")
///
/// 설계 가드:
///  - 정지 좀비(Dormant 배회 포함, minSpeed 미만)는 제외 — 잔상=실물 위치라 정보가치 0, 조준 회전 스윕 스팸 방지.
///  - 동시 잔상 상한(maxGhosts, 호드 과부하 방지) + 좀비당 재생성 쿨다운.
///  - 콘 비활성(coneBlend 0/드라이버 부재)이면 전부 "보이는" 상태라 자동 무동작.
///  - 고스트는 Default 레이어 — Zombie 레이어(7)의 아웃라인/콘 고스트 패스에 안 걸리게.
/// Main Camera(TiltShiftConeDriver 옆)에 부착.
/// </summary>
public class ZombieLKPSilhouette : MonoBehaviour
{
    [Tooltip("실루엣 머티리얼(ZombieCrush/LKPGhost). 고스트마다 인스턴스 복제해 _Fade를 개별 구동.")]
    [SerializeField] Material ghostMaterial;
    [Tooltip("잔상이 완전히 사라질 때까지(초).")]
    [SerializeField, Min(0.5f)] float fadeDuration = 2.5f;
    [Tooltip("동시 잔상 상한 — 호드에서 잔상 도배 방지. 초과 시 가장 오래된 것부터 제거.")]
    [SerializeField, Min(1)] int maxGhosts = 8;
    [Tooltip("이 속도(m/s) 이상으로 움직이던 좀비만 잔상을 남긴다 — 정지 좀비는 잔상=실물이라 무의미.")]
    [SerializeField, Min(0f)] float minSpeed = 0.5f;
    [Tooltip("같은 좀비가 잔상을 다시 남기기까지 쿨다운(초) — 콘 경계 들락날락 스팸 방지.")]
    [SerializeField, Min(0f)] float perZombieCooldown = 1.5f;
    [Tooltip("씬의 좀비 목록 재스캔 주기(초).")]
    [SerializeField, Min(0.2f)] float rescanInterval = 1f;

    class Tracked
    {
        public ZombieController z;
        public Vector3 prevPos;
        public bool inCone;
        public float cooldownUntil;
    }

    class Ghost
    {
        public GameObject go;
        public Material mat;
        public List<Mesh> meshes = new List<Mesh>();
        public float age;
    }

    readonly List<Tracked> _tracked = new List<Tracked>();
    readonly List<Ghost> _ghosts = new List<Ghost>();
    float _rescanTimer;

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        UpdateGhosts(dt);

        var drv = TiltShiftConeDriver.Instance;
        if (drv == null || !drv.ConeActive || ghostMaterial == null) return;

        _rescanTimer -= dt;
        if (_rescanTimer <= 0f) { Rescan(drv); _rescanTimer = rescanInterval; }

        float now = Time.time;
        for (int i = _tracked.Count - 1; i >= 0; i--)
        {
            var t = _tracked[i];
            if (t.z == null || t.z.IsDead) { _tracked.RemoveAt(i); continue; }

            Vector3 pos = t.z.transform.position;
            float speed = dt > 0f ? (pos - t.prevPos).magnitude / dt : 0f;
            bool inCone = drv.IsWorldPosInCone(pos);

            // 콘 안→밖 전환 + 움직이는 중 → 마지막 위치에 잔상.
            if (t.inCone && !inCone && speed >= minSpeed && now >= t.cooldownUntil)
            {
                SpawnGhost(t.z);
                t.cooldownUntil = now + perZombieCooldown;
            }
            t.inCone = inCone;
            t.prevPos = pos;
        }
    }

    void Rescan(TiltShiftConeDriver drv)
    {
        var all = FindObjectsOfType<ZombieController>();
        foreach (var z in all)
        {
            if (z.IsDead) continue;
            bool known = false;
            for (int i = 0; i < _tracked.Count; i++)
                if (_tracked[i].z == z) { known = true; break; }
            if (known) continue;
            // 신규 등록 — 현재 콘 상태로 초기화(등록 직후 가짜 "이탈" 방지).
            _tracked.Add(new Tracked
            {
                z = z,
                prevPos = z.transform.position,
                inCone = drv.IsWorldPosInCone(z.transform.position)
            });
        }
    }

    void SpawnGhost(ZombieController z)
    {
        // 상한 초과 — 가장 오래된 잔상 제거. (씬 저장값이 0이어도 무한루프 금지 — 리뷰 H-3)
        int cap = Mathf.Max(1, maxGhosts);
        while (_ghosts.Count >= cap && _ghosts.Count > 0) DestroyGhost(_ghosts[0]);

        var smrs = z.GetComponentsInChildren<SkinnedMeshRenderer>(false);
        if (smrs.Length == 0) return;

        var ghost = new Ghost();
        ghost.go = new GameObject("LKPGhost");
        ghost.go.transform.SetPositionAndRotation(z.transform.position, z.transform.rotation);
        ghost.mat = new Material(ghostMaterial);   // 인스턴스 — _Fade 개별 구동(SRP 배처에서 MPB 비신뢰)

        foreach (var smr in smrs)
        {
            var mesh = new Mesh();
            smr.BakeMesh(mesh, true);   // 스케일 포함 베이크 → 자식 트랜스폼 스케일 1
            ghost.meshes.Add(mesh);

            var child = new GameObject(smr.name);
            child.transform.SetParent(ghost.go.transform, false);
            child.transform.SetPositionAndRotation(smr.transform.position, smr.transform.rotation);
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = child.AddComponent<MeshRenderer>();
            mr.sharedMaterial = ghost.mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        }
        _ghosts.Add(ghost);
    }

    void UpdateGhosts(float dt)
    {
        for (int i = _ghosts.Count - 1; i >= 0; i--)
        {
            var g = _ghosts[i];
            g.age += dt;
            if (g.age >= fadeDuration || fadeDuration <= 0f || g.go == null) { DestroyGhost(g); continue; }
            g.mat.SetFloat("_Fade", 1f - g.age / fadeDuration);
        }
    }

    void DestroyGhost(Ghost g)
    {
        _ghosts.Remove(g);
        if (g.go != null) Destroy(g.go);
        if (g.mat != null) Destroy(g.mat);
        foreach (var m in g.meshes) if (m != null) Destroy(m);   // 베이크 메시 누수 방지(ZombieDeathFX 교훈)
    }

    void OnDestroy()
    {
        for (int i = _ghosts.Count - 1; i >= 0; i--) DestroyGhost(_ghosts[i]);
    }
}
