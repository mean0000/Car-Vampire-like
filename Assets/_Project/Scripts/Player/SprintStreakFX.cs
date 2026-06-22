using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 달리기(스프린트) 속도감 VFX — 자족 컴포넌트.
///
/// 탑다운 부감에서 "이동 반대 방향으로 흘러가는 속도선"으로 읽히게 한다.
/// <see cref="PlayerMotor"/> 훅(IsSprinting / SprintTier / SprintBurstedThisFrame / Velocity)만
/// 읽고, 모터/드라이버 코드는 일절 건드리지 않는다.
///
/// VFX 두 레이어:
///   1. Trail Stream   — 파티클을 이동 반대 방향으로 흘리는 연속 스트릭. SprintTier에 따라
///                       방출량/크기/속도 강도가 단계별 올라간다.
///   2. Burst Flash    — SprintBurstedThisFrame(단계 상승 프레임)에 짧고 굵은 라인 버스트 1회.
///                       "40!!" 순간의 한 방 펀치.
///
/// 색=시안(HDR, 블룸 받음). 블렌드=가산(Additive) — 어두운 탑다운 배경에 빛나게.
///
/// 부착 위치: PlayerAnimatorDriver가 있는 비주얼 오브젝트 또는 플레이어 루트.
///            Inspector에서 motor를 직접 연결하거나, 부모/자신에서 자동 탐색.
/// </summary>
public class SprintStreakFX : MonoBehaviour
{
    // ────────── 참조 ──────────
    [Header("참조")]
    [Tooltip("스프린트 상태 소스. 비우면 부모/자신에서 탐색.")]
    [SerializeField] PlayerMotor motor;

    // ────────── Trail Stream 노브 ──────────
    [Header("Trail Stream")]
    [Tooltip("Tier 0/1/2에서 초당 방출 파티클 수. 높을수록 촘촘한 스트릭.")]
    [SerializeField] float[] tierEmitRate = { 18f, 36f, 60f };
    [Tooltip("파티클 생존 시간(초). 길수록 꼬리가 멀리 뻗는다.")]
    [SerializeField] float particleLifetime = 0.18f;
    [Tooltip("파티클 시작 크기. Tier 높을수록 곱연산으로 커진다.")]
    [SerializeField] float baseParticleSize = 0.06f;
    [Tooltip("Tier별 크기 배율(0=×1, 1=×이값, 2=×이값²). 1.35면 0→1→1.35→1.82배.")]
    [SerializeField] float tierSizeMultiplier = 1.35f;
    [Tooltip("파티클을 이동 반대 방향으로 밀어내는 초기 속력(m/s). 속도벡터에 이 값을 곱해 날린다.")]
    [SerializeField] float streakSpeed = 3.5f;
    [Tooltip("스트릭 파티클의 Stretch 배율(RenderMode=Stretch). 클수록 가는 선으로 읽힘.")]
    [SerializeField] float stretchLength = 6f;
    [Tooltip("스폰 오프셋 — 플레이어 발 기준 높이. 탑다운에서 바닥 레벨에 보이게 낮게 둔다.")]
    [SerializeField] float spawnHeightOffset = 0.15f;
    [Tooltip("스폰 반경 내 랜덤 오프셋(발 근처에서 퍼지게).")]
    [SerializeField] float spawnRadius = 0.25f;
    [Tooltip("Trail 파티클 색(HDR 시안). 블룸 세기는 RGB 크기로 조절.")]
    [ColorUsage(true, true)]
    [SerializeField] Color trailColor = new Color(0.15f, 1.2f, 1.5f, 1f);

    // ────────── Burst Flash 노브 ──────────
    [Header("Burst Flash (단계 상승 펀치)")]
    [Tooltip("버스트 1회에 방출할 파티클 수. 많을수록 한 방이 굵다.")]
    [SerializeField] int burstCount = 22;
    [Tooltip("버스트 파티클 생존 시간(초). 짧을수록 날카롭게 터지고 사라짐.")]
    [SerializeField] float burstLifetime = 0.12f;
    [Tooltip("버스트 파티클 크기(Trail과 별도).")]
    [SerializeField] float burstParticleSize = 0.09f;
    [Tooltip("버스트 파티클이 이동 반대로 날아가는 속력.")]
    [SerializeField] float burstSpeed = 8f;
    [Tooltip("버스트 파티클의 Stretch 배율.")]
    [SerializeField] float burstStretchLength = 10f;
    [Tooltip("Burst 파티클 색(더 밝은 HDR — 펀치감).")]
    [ColorUsage(true, true)]
    [SerializeField] Color burstColor = new Color(0.4f, 2.0f, 2.5f, 1f);

    // ────────── 내부 ──────────
    ParticleSystem _trailPS;
    ParticleSystem _burstPS;
    Material _trailMat;
    Material _burstMat;
    ParticleSystem.EmitParams _emit;   // struct 재사용 — per-frame GC 할당 없음

    float _emitAccum;
    Vector3 _lastPos;
    bool _hasPrev;
    Vector3 _moveDir = Vector3.forward;

    void Awake()
    {
        if (motor == null) motor = GetComponentInParent<PlayerMotor>();
        if (motor == null) motor = GetComponent<PlayerMotor>();
        if (motor == null)
        {
            Debug.LogError("[SprintStreakFX] PlayerMotor 미연결 — 비활성. Inspector 확인.", this);
            enabled = false;
            return;
        }

        // M-2: 배열 0 길이 방어 — Inspector 실수로 비었을 때 IndexOutOfRange 방지
        if (tierEmitRate == null || tierEmitRate.Length == 0)
        {
            Debug.LogError("[SprintStreakFX] tierEmitRate 배열이 비어 있음 — 비활성. Inspector 확인.", this);
            enabled = false;
            return;
        }

        // H-2: 셰이더 폴백 체인 — 최종 null 시 크래시 방지(InternalErrorShader=마젠타로 표시)
        _trailMat = CreateAdditiveMaterial(trailColor);
        _burstMat = CreateAdditiveMaterial(burstColor);

        _trailPS = BuildParticleSystem("SprintTrailPS", _trailMat, stretchLength, particleLifetime, 200);
        _burstPS = BuildParticleSystem("SprintBurstPS", _burstMat, burstStretchLength, burstLifetime, 80);
    }

    void OnDisable()
    {
        // null 가드 — Awake 실패(enabled=false 경로)에서 호출돼도 안전
        if (_trailPS != null) { _trailPS.Clear(); _trailPS.Stop(); }
        if (_burstPS != null) { _burstPS.Clear(); _burstPS.Stop(); }
        _emitAccum = 0f;
        _hasPrev = false;
    }

    // H-1: OnDestroy — 자식 GO + Material 명시적 파괴(DontSave GO 누수·Material 누수 방지)
    void OnDestroy()
    {
        if (_trailPS != null) Destroy(_trailPS.gameObject);
        if (_burstPS != null) Destroy(_burstPS.gameObject);
        if (_trailMat != null) Destroy(_trailMat);
        if (_burstMat != null) Destroy(_burstMat);
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // ── 이동 방향 추정 (Velocity 훅 우선, 위치 델타 폴백) ──
        Vector3 vel = motor.Velocity;
        vel.y = 0f;
        if (vel.sqrMagnitude > 0.01f)
        {
            _moveDir = vel.normalized;
        }
        else
        {
            Vector3 pos = motor.transform.position;
            if (_hasPrev)
            {
                Vector3 d = pos - _lastPos; d.y = 0f;
                if (d.sqrMagnitude > 1e-6f) _moveDir = d.normalized;
            }
            _lastPos = pos;
            _hasPrev = true;
        }

        // ── Trail Stream + Burst Flash (스프린트 중에만) ──
        // M-1: Burst를 IsSprinting 가드 안으로 이동 — 비스프린트 프레임의 의도치 않은 방출 방지
        if (!motor.IsSprinting)
        {
            _emitAccum = 0f;
            return;
        }

        // Burst Flash — 단계 상승(버스트) 엣지 1프레임에 한 방
        if (motor.SprintBurstedThisFrame)
            EmitBurst();

        // Trail Stream — Tier별 방출량/크기 스케일
        int tier = Mathf.Clamp(motor.SprintTier, 0, tierEmitRate.Length - 1);
        float rate = tierEmitRate[tier];
        float sizeMult = Mathf.Pow(tierSizeMultiplier, tier);
        float sz = baseParticleSize * sizeMult;

        _emitAccum += rate * dt;
        int toEmit = Mathf.FloorToInt(_emitAccum);
        _emitAccum -= toEmit;

        for (int i = 0; i < toEmit; i++)
            EmitTrail(sz);
    }

    // ──────────── Emit helpers ────────────

    void EmitTrail(float size)
    {
        // 이동 반대 방향으로 밀어냄 — 현재 속력에 비례해 꼬리 속도를 살짝 늘림
        Vector3 v = -_moveDir * (streakSpeed * (1f + motor.Velocity.magnitude * 0.05f));

        _emit.position    = SpawnPos();
        _emit.velocity    = v;
        _emit.startSize   = size;
        _emit.startLifetime = particleLifetime;
        _trailPS.Emit(_emit, 1);
    }

    void EmitBurst()
    {
        for (int i = 0; i < burstCount; i++)
        {
            // 이동 반대 + 좁은 각도 퍼짐 — "섬광 폭발" 느낌
            float angle = Random.Range(-18f, 18f);
            Vector3 spread = Quaternion.AngleAxis(angle, Vector3.up) * (-_moveDir);
            Vector3 v = spread * (burstSpeed * Random.Range(0.7f, 1.3f));

            _emit.position    = SpawnPos();
            _emit.velocity    = v;
            _emit.startSize   = burstParticleSize * Random.Range(0.8f, 1.2f);
            _emit.startLifetime = burstLifetime;
            _burstPS.Emit(_emit, 1);
        }
    }

    Vector3 SpawnPos()
    {
        Vector3 p = motor.transform.position;
        p.y += spawnHeightOffset;
        Vector2 rnd = Random.insideUnitCircle * spawnRadius;
        p.x += rnd.x;
        p.z += rnd.y;
        return p;
    }

    // ──────────── 파티클 시스템 생성 ────────────

    ParticleSystem BuildParticleSystem(string goName, Material mat, float stretch, float life, int maxP)
    {
        // H-1: HideFlags.DontSave 제거 — 자식 GO라 부모 Destroy 시 자동 파괴됨.
        //      DontSave는 에디터 반복 플레이 시 GO 누적 누수를 유발하는 알려진 함정.
        var go = new GameObject(goName);
        go.transform.SetParent(transform, false);

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop();

        var main = ps.main;
        main.startLifetime   = life;
        main.startSpeed      = 0f;   // 속도는 EmitParams로 직접 주입
        main.startSize       = baseParticleSize;
        main.maxParticles    = maxP;
        main.playOnAwake     = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor      = Color.white;   // 색은 머티리얼(HDR 가산)이 소유

        // 방출·형태 모듈 끔 — 전부 스크립트 수동 방출(EmitParams)
        var em = ps.emission; em.enabled = false;
        var sh = ps.shape;    sh.enabled = false;

        // 알파 페이드 — 끝이 자연스럽게 사라지게
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.85f, 0.3f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.renderMode        = ParticleSystemRenderMode.Stretch;
        psr.lengthScale       = stretch;
        psr.velocityScale     = 0.12f;
        psr.shadowCastingMode = ShadowCastingMode.Off;
        psr.receiveShadows    = false;
        psr.sharedMaterial    = mat;
        psr.sortingOrder      = 1;

        ps.Play();
        return ps;
    }

    // H-2: sh==null 최종 폴백 — InternalErrorShader(마젠타)로 크래시 없이 표시
    static Material CreateAdditiveMaterial(Color hdrColor)
    {
        var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit")
              ?? Shader.Find("Universal Render Pipeline/Unlit")
              ?? Shader.Find("Sprites/Default");

        if (sh == null)
        {
            Debug.LogError("[SprintStreakFX] 가산 셰이더를 찾지 못함 — URP Particles/Unlit이 Always Included에 있는지 확인. 마젠타로 표시.");
            sh = Shader.Find("Hidden/InternalErrorShader");
        }

        var mat = new Material(sh);
        mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)BlendMode.One);
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = (int)RenderQueue.Transparent + 1;

        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", hdrColor);
        if (mat.HasProperty("_Color"))     mat.SetColor("_Color", hdrColor);
        if (mat.HasProperty("_Surface"))   mat.SetFloat("_Surface", 1f);
        if (mat.HasProperty("_Blend"))     mat.SetFloat("_Blend", 2f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

        return mat;
    }
}
