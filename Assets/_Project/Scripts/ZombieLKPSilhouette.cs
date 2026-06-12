using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// LKP(Last Known Position) 실루엣 — "움직이던" 좀비가 시야 콘에서 벗어나는 순간,
/// 마지막 위치에 반투명 실루엣(SMR 베이크)을 남기고 서서히 지운다. (Splinter Cell 계보,
/// 2026-06-11 브레인스토밍: 모퉁이 문법의 보상 — "저기 있었는데 지금 어디 갔지?")
///
/// ★2026-06-11 유저 콜(상시 시야 콘 = 정보 게이트, hideOutOfSight):
///  - 정보 콘(TiltShiftConeDriver.IsWorldPosInInfoCone) ∩ LOS(벽 레이캐스트) 밖의 좀비는 "실물을 숨긴다".
///  - 시야를 잃는 순간 회색 반투명 잔상(기억)을 남기고 천천히 소멸 — 정지 좀비도 포함(실물이 사라지므로 잔상=기억).
///  - 처음부터 본 적 없는 좀비는 잔상 없이 그냥 안 보임 — 발견의 드라마 보존.
///  - 다시 시야에 들어오면 실물 복원 + 그 좀비의 잔상 즉시 제거(기억이 실물로 대체).
///
/// 설계 가드 (legacy 모드 hideOutOfSight=false — 구 동작 그대로):
///  - 정지 좀비(Dormant 배회 포함, minSpeed 미만)는 제외 — 잔상=실물 위치라 정보가치 0, 조준 회전 스윕 스팸 방지.
///  - 동시 잔상 상한(maxGhosts, 호드 과부하 방지) + 좀비당 재생성 쿨다운.
///  - 콘 비활성(드라이버 부재)이면 전부 "보이는" 상태라 자동 무동작(+게이트 모드는 실물 전부 복원).
///  - 고스트는 Default 레이어 — Zombie 레이어(7)의 아웃라인/콘 고스트 패스에 안 걸리게.
/// Main Camera(TiltShiftConeDriver 옆)에 부착.
/// </summary>
public class ZombieLKPSilhouette : MonoBehaviour
{
    /// <summary>씬 싱글톤 — 외부 스포너의 RegisterImmediate 진입점(ReturnWaveTrigger 등).</summary>
    public static ZombieLKPSilhouette Instance { get; private set; }

    [Tooltip("실루엣 머티리얼(ZombieCrush/LKPGhost). 고스트마다 인스턴스 복제해 _Fade를 개별 구동.")]
    [SerializeField] Material ghostMaterial;
    [Tooltip("잔상이 완전히 사라질 때까지(초).")]
    [SerializeField, Min(0.5f)] float fadeDuration = 2.5f;
    [Tooltip("동시 잔상 상한 — 호드에서 잔상 도배 방지. 초과 시 가장 오래된 것부터 제거.")]
    [SerializeField, Min(1)] int maxGhosts = 8;
    [Tooltip("(legacy 모드 전용) 이 속도(m/s) 이상으로 움직이던 좀비만 잔상을 남긴다.")]
    [SerializeField, Min(0f)] float minSpeed = 0.5f;
    [Tooltip("같은 좀비가 잔상을 다시 남기기까지 쿨다운(초) — 콘 경계 들락날락 스팸 방지.")]
    [SerializeField, Min(0f)] float perZombieCooldown = 1.5f;
    [Tooltip("씬의 좀비 목록 재스캔 주기(초).")]
    [SerializeField, Min(0.2f)] float rescanInterval = 1f;

    [Header("Info Gate (★상시 시야 콘 — 콘∩LOS 밖 좀비 실물 숨김)")]
    [Tooltip("켜면 정보 콘∩LOS 밖 좀비의 렌더러를 끈다(정보 게이트). 끄면 구 동작(잔상만).")]
    [SerializeField] bool hideOutOfSight = true;
    [Tooltip("LOS를 막는 차폐 레이어 — 프로젝트 표준 Obstacle(8). 벽 너머 좀비는 콘 안이어도 안 보임.")]
    [SerializeField] LayerMask losObstacleMask = 1 << 8;
    [Tooltip("LOS 시점 높이(m) — 플레이어 눈높이.")]
    [SerializeField, Min(0f)] float eyeHeight = 1f;

    class Tracked
    {
        public ZombieController z;
        public Vector3 prevPos;
        public bool inCone;            // 게이트 모드에선 "보이는 상태"(콘∩LOS) 캐시
        public float cooldownUntil;
        public Renderer[] renderers;   // 게이트 모드 숨김/복원용 캐시
        public bool hidden;
    }

    class Ghost
    {
        public GameObject go;
        public Material mat;
        public List<Mesh> meshes = new List<Mesh>();
        public float age;
        public ZombieController owner;   // 재발견 시 잔상 즉시 제거용
    }

    readonly List<Tracked> _tracked = new List<Tracked>();
    readonly List<Ghost> _ghosts = new List<Ghost>();
    float _rescanTimer;
    PlayerCombat _player;

    void Awake()
    {
        Instance = this;
    }

    void LateUpdate()
    {
        // 플레이 전용 — 에디터에서 외부 틱(SendMessage 등)으로 실행되면 렌더러 off가 씬에 저장되는 사고 방지.
        if (!Application.isPlaying) return;
        float dt = Time.deltaTime;
        UpdateGhosts(dt);

        var drv = TiltShiftConeDriver.Instance;
        bool gateReady = hideOutOfSight && drv != null && drv.InfoConeReady;
        bool legacyReady = !hideOutOfSight && drv != null && drv.ConeActive;

        if (!gateReady && !legacyReady)
        {
            if (hideOutOfSight) RestoreAllHidden();   // 드라이버 부재/미준비 = 전부 보임 폴백
            return;
        }
        if (ghostMaterial == null && !hideOutOfSight) return;

        if (hideOutOfSight && _player == null)
        {
            _player = FindObjectOfType<PlayerCombat>();
            if (_player == null) { RestoreAllHidden(); return; }
        }

        _rescanTimer -= dt;
        if (_rescanTimer <= 0f) { Rescan(drv); _rescanTimer = rescanInterval; }

        float now = Time.time;
        for (int i = _tracked.Count - 1; i >= 0; i--)
        {
            var t = _tracked[i];
            if (t.z == null || t.z.IsDead)
            {
                if (t.hidden) SetRenderers(t, true);   // 시체 연출(DeathFX)이 실물을 필요로 함
                if (t.z != null) RemoveGhostOf(t.z);   // 시체+잔상 공존 방지(리뷰 M)
                _tracked.RemoveAt(i);
                continue;
            }

            Vector3 pos = t.z.transform.position;
            float speed = dt > 0f ? (pos - t.prevPos).magnitude / dt : 0f;

            if (hideOutOfSight)
            {
                bool vis = t.z.IsAttacking || CanSee(drv, pos);   // 공격 중 실물 강제 공개(공정성 바닥)
                if (t.inCone && !vis)
                {
                    // 시야 상실 — 기억(회색 잔상)을 남기고 실물 숨김. 정지 좀비도 잔상(실물이 사라지므로).
                    // 쿨다운 중이어도 살아있는 잔상이 없으면 생성 — "잔상 없는 증발" 방지(리뷰 M).
                    if (ghostMaterial != null && (now >= t.cooldownUntil || !HasGhost(t.z)))
                    {
                        RemoveGhostOf(t.z);
                        SpawnGhost(t.z);   // 숨기기 전에 베이크(마지막 포즈)
                        t.cooldownUntil = now + perZombieCooldown;
                    }
                    SetRenderers(t, false);
                }
                else if (!t.inCone && vis)
                {
                    SetRenderers(t, true);
                    RemoveGhostOf(t.z);   // 기억이 실물로 대체
                }
                else if (t.renderers != null && t.renderers.Length > 0 && t.renderers[0] != null
                         && t.renderers[0].enabled != vis)
                {
                    // 외부 시스템(사망 FX·리스폰·스폰 연출)이 렌더러를 직접 만져 게이트와 어긋난 경우 재동기화.
                    // 엣지 토글만으로는 어긋남이 영구 고착된다 — 06-12 실측: 죽인 좀비 재소환 시 콘 밖인데 계속 보임.
                    SetRenderers(t, vis);
                }
                t.inCone = vis;
            }
            else
            {
                bool inCone = drv.IsWorldPosInCone(pos);
                // 콘 안→밖 전환 + 움직이는 중 → 마지막 위치에 잔상.
                if (t.inCone && !inCone && speed >= minSpeed && now >= t.cooldownUntil)
                {
                    SpawnGhost(t.z);
                    t.cooldownUntil = now + perZombieCooldown;
                }
                t.inCone = inCone;
            }
            t.prevPos = pos;
        }
    }

    /// <summary>정보 게이트 가시성 — 시야 콘(각도·거리·근접) ∩ LOS(차폐 레이캐스트).
    /// VisionConeRenderer(월드 콘 = 화면 어둠과 동일 기하)가 있으면 그게 진실,
    /// 없으면 드라이버의 화면공간 정보 콘으로 폴백.</summary>
    bool CanSee(TiltShiftConeDriver drv, Vector3 zombiePos)
    {
        Vector3 target = zombiePos + Vector3.up * 1f;
        var cone = VisionConeRenderer.Instance;
        if (cone != null && cone.Ready)
        {
            if (!cone.InCone(zombiePos)) return false;
        }
        else if (!drv.IsWorldPosInInfoCone(target)) return false;
        Vector3 eye = _player.transform.position + Vector3.up * eyeHeight;
        return !Physics.Linecast(eye, target, losObstacleMask, QueryTriggerInteraction.Ignore);
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

            Register(z, drv);
        }
    }

    /// <summary>단일 등록 — 현재 가시 상태로 초기화(등록 직후 가짜 "이탈" 방지). Rescan/RegisterImmediate 공용.</summary>
    void Register(ZombieController z, TiltShiftConeDriver drv)
    {
        var t = new Tracked
        {
            z = z,
            prevPos = z.transform.position,
            renderers = z.GetComponentsInChildren<Renderer>(true)
        };
        if (hideOutOfSight)
        {
            t.inCone = z.IsAttacking || CanSee(drv, z.transform.position);
            // 등록 시 가시 상태와 렌더러를 양방향 동기화 — 꺼진 채 저장된 씬 상태도 치유.
            // (본 적 없는 좀비는 잔상 없이 즉시 숨김 — "기억"은 본 것에만 생긴다.)
            SetRenderers(t, t.inCone);
        }
        else
        {
            t.inCone = drv.IsWorldPosInCone(z.transform.position);
        }
        _tracked.Add(t);
    }

    /// <summary>
    /// 외부 스포너용 즉시 편입 — 리스캔 주기(rescanInterval)를 기다리지 않고 등록한다.
    /// 등록 시 가시 판정 + 렌더러 동기화까지 즉시 수행(Rescan과 동일 로직). 이미 추적 중이면 무시.
    /// 게이트 비활성(드라이버 부재 등)이면 무동작 — 전부 보이는 상태라 다음 리스캔이 처리.
    /// </summary>
    public void RegisterImmediate(ZombieController z)
    {
        if (!Application.isPlaying || z == null || z.IsDead) return;
        for (int i = 0; i < _tracked.Count; i++)
            if (_tracked[i].z == z) return;

        var drv = TiltShiftConeDriver.Instance;
        bool gateReady = hideOutOfSight && drv != null && drv.InfoConeReady;
        bool legacyReady = !hideOutOfSight && drv != null && drv.ConeActive;
        if (!gateReady && !legacyReady) return;

        if (hideOutOfSight && _player == null)
        {
            _player = FindObjectOfType<PlayerCombat>();
            if (_player == null) return;
        }
        Register(z, drv);
    }

    /// <summary>정보 게이트 기준 "실물이 숨겨진" 좀비인가 — 외부 힌트 채널(RearThreatHint 등)용
    /// 단일 진실. 콘∩LOS 판정을 바깥에 중복 구현하지 않기 위한 조회 전용 API.
    /// 미추적/게이트 비활성 = false(보임) — 힌트가 보이는 좀비에 발화하지 않게 보수적으로.</summary>
    public bool IsHiddenFromPlayer(ZombieController z)
    {
        if (z == null) return false;
        for (int i = 0; i < _tracked.Count; i++)
            if (_tracked[i].z == z) return _tracked[i].hidden;
        return false;
    }

    void SetRenderers(Tracked t, bool visible)
    {
        if (t.renderers != null)
            foreach (var r in t.renderers)
                if (r != null) r.enabled = visible;
        t.hidden = !visible;
    }

    void RestoreAllHidden()
    {
        for (int i = 0; i < _tracked.Count; i++)
            if (_tracked[i].hidden && _tracked[i].z != null) SetRenderers(_tracked[i], true);
    }

    void RemoveGhostOf(ZombieController z)
    {
        for (int i = _ghosts.Count - 1; i >= 0; i--)
            if (_ghosts[i].owner == z) DestroyGhost(_ghosts[i]);
    }

    bool HasGhost(ZombieController z)
    {
        for (int i = 0; i < _ghosts.Count; i++)
            if (_ghosts[i].owner == z) return true;
        return false;
    }

    void SpawnGhost(ZombieController z)
    {
        // 상한 초과 — 가장 오래된 잔상 제거. (씬 저장값이 0이어도 무한루프 금지 — 리뷰 H-3)
        int cap = Mathf.Max(1, maxGhosts);
        while (_ghosts.Count >= cap && _ghosts.Count > 0) DestroyGhost(_ghosts[0]);

        var smrs = z.GetComponentsInChildren<SkinnedMeshRenderer>(false);
        if (smrs.Length == 0) return;

        var ghost = new Ghost();
        ghost.owner = z;
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

    void OnDisable()
    {
        RestoreAllHidden();   // 게이트가 꺼지면 좀비가 영구 투명으로 남지 않게
        for (int i = _ghosts.Count - 1; i >= 0; i--) DestroyGhost(_ghosts[i]);   // 페이드 정지 잔류 방지(리뷰 M)
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        RestoreAllHidden();
        for (int i = _ghosts.Count - 1; i >= 0; i--) DestroyGhost(_ghosts[i]);
    }
}
