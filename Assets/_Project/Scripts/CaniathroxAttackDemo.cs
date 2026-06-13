// Caniathrox(개[犬]형 추격자 LV2) 공격 연출 데모 — ★Animator 상태머신 주도 전면 재구현(v4).
//
// ════════ 헌법 (불가침) ════════
//   제0원칙: 정체성 동작(도약·물기·스핏) 재생 중엔 그 클립만 돈다. crossfade로 두 동작을 뭉개지 않는다.
//            → 상태머신이 이를 강제한다(Lunge/Spit 진입 전이 duration=0 컷, ExitTime 후 전환). 로코모션 이음새(Idle↔Run)만 0.12s 블렌드.
//   제1원칙: 공격은 상태 시퀀스다. IdleAngry → Approach(Run_RM, 루트모션 접근) → [도착] → Lunge(Jump_RM, 도약 덮침)
//            → [Exit] → Spit → [Exit] → IdleAngry. 각 단계 = 1클립 = 1상태, 순차·완결·전환.
//   제2원칙: 애니메이션이 진실. 위치·궤적·포즈는 전부 클립 루트모션(applyRootMotion=true)이 만든다.
//            코드는 모션을 만들지 않는다. 코드 역할 셋: ①상태 전이 조건 ②VFX 타이밍을 클립 진행에서 읽어 트리거 ③AI 의도→상태 입력.
//
// ════════ 실측 루트모션 (Animator 스텝 측정, generic rig) ════════
//   Run_RM   : len 0.600s, 전진 2.457m/cyc, 상승 0 (루프 접근 로코모션).
//   Jump_RM  : len 0.833s, 전진 4.673m, 상승 0.278m. 도약 타이밍 liftN0.14 / apexN0.39 / landN0.59. ★진짜 도약(덮침).
//   Spit     : len 0.667s, 제자리.  IdleAngry: len 1.867s, 제자리.
//   ⚠️ JumpBite_RM은 이름과 달리 루트모션 0(제자리) — 도약이 없어 안 쓴다. 도약=Jump_RM이 진실.
//
// ════════ 코드가 위치/포즈를 만드는 곳: 0 ════════
//   - 더미를 향한 yaw 1회 설정 = 로코모션 전 "머리 방향"(AI 의도 번역). 클립이 그 방향으로 루트모션을 만든다.
//   - 사이클 경계(휴지 복귀)에서 시작 트랜스폼으로 리셋 = 데모 루프 리셋(모션 덮어쓰기 아님, 정지 상태에서만).
//   - 그 외 모든 프레임의 위치/높이/포즈는 Animator 루트모션이 만든다. SetTime/SampleAnimation/포물선 = 없음.
//
// 렌더 경로(동결): 장판·임팩트 링 = 레이어 PickupInfo(13). ZTest LEqual. 색 = 레드-오렌지 단색(시안/앰버/마젠타 금지).
using UnityEngine;

public class CaniathroxAttackDemo : MonoBehaviour
{
    [Header("참조 (씬 빌더가 와이어링)")]
    public Transform model;            // Caniathrox 인스턴스(Animator 보유, 루트모션이 이걸 움직인다)
    public Animator modelAnimator;     // 상태머신 재생 주체(applyRootMotion=true 강제)
    public Transform spitTarget;       // 더미(플레이어 대용)
    public RuntimeAnimatorController attackController;  // CaniathroxAttack.controller (빌더가 할당)

    [Header("접근/공격 판정 (플레이로 확정 — 거리감)")]
    [Tooltip("이 거리 안에 들면 도약(Lunge)을 발동한다. Jump_RM 전진 4.67m이므로 이 거리에서 도약하면 더미에 덮친다.")]
    [SerializeField] float lungeRange = 5.0f;
    [Tooltip("휴지 후 접근 시작까지의 정지 시간(초). 윈드업 호흡.")]
    [SerializeField] float restBeforeApproach = 0.6f;

    [Header("발사 노브 (플레이로 확정 — 시각 의존)")]
    [Tooltip("스핏 글롭이 입에서 떠나는 정규화 시점(0~1).")]
    [Range(0f, 1f)] [SerializeField] float spitFireNorm = 0.45f;

    // ── 클립 실측 타이밍(Jump_RM, Animator 스텝 측정) — 코드 발명 아님, 클립 데이터 ──
    const float LungeLandNorm = 0.59f;   // 도약 정점 후 착지 프레임(임팩트/장판 발동)

    // 색 캐넌 — 레드-오렌지 단색
    static readonly Color ThreatRed  = new Color(1f, 0.30f, 0.08f, 1f);
    static readonly Color GlowOrange = new Color(2.2f, 0.62f, 0.16f, 1f);

    // ThreatArc 프로퍼티 ID
    static readonly int StyleId    = Shader.PropertyToID("_Style");
    static readonly int SizeWorldId= Shader.PropertyToID("_SizeWorld");
    static readonly int ColorId    = Shader.PropertyToID("_Color");
    static readonly int AlphaId    = Shader.PropertyToID("_Alpha");
    static readonly int ProgressId = Shader.PropertyToID("_Progress");
    static readonly int SeedId     = Shader.PropertyToID("_Seed");
    static readonly int ZTestId    = Shader.PropertyToID("_ZTest");
    static readonly int InnerR01Id = Shader.PropertyToID("_InnerR01");
    static readonly int EdgeAlphaId= Shader.PropertyToID("_EdgeAlpha");
    static readonly int FillAlphaId= Shader.PropertyToID("_FillAlpha");

    // 애니 파라미터
    static readonly int PApproach = Animator.StringToHash("isApproaching");
    static readonly int PAttack   = Animator.StringToHash("attack");
    static readonly int SIdle  = Animator.StringToHash("IdleAngry");
    static readonly int SApp   = Animator.StringToHash("Approach");
    static readonly int SLunge = Animator.StringToHash("Lunge");
    static readonly int SSpit  = Animator.StringToHash("Spit");

    // 런타임 VFX
    GameObject _padQuad, _ringQuad, _spitGlob, _spitTrail, _muzzle;
    Material _padMat, _ringMat, _spitMat, _trailMat, _muzzleMat, _groundMat;
    LineRenderer _trailLine;

    const float PadRadius  = 1.5f;
    const float RingExpand = 0.15f;
    const float RingFade   = 0.10f;
    const float MuzzleFlash= 0.05f;
    const float SpitSpeed  = 10f;

    Vector3 _startPos; Quaternion _startRot;

    // 사이클 1회성 가드
    bool _attackFired;     // 이 사이클에 attack 트리거를 쐈나
    bool _impactFired;     // 도약 착지 임팩트 발동했나
    bool _spitFired;       // 스핏 글롭 발사했나
    float _restTimer;      // 휴지 카운트다운(IdleAngry 진입 후)
    bool _approaching;     // isApproaching 상태 미러

    // VFX 라이브 시계
    float _ringTimer = -1f, _muzzleTimer = -1f, _spitFlightT = -1f;
    Vector3 _spitFrom, _spitTo;

    void Awake()
    {
        if (modelAnimator == null)
        {
            Debug.LogError("[CaniathroxDemo] modelAnimator 미할당 — 상태머신 재생 불가");
            enabled = false; return;
        }
        ResolveControllerIfNeeded();   // 씬 재저장 없이 컨트롤러 확보(에디터 폴백)
        if (attackController != null) modelAnimator.runtimeAnimatorController = attackController;
        else Debug.LogError("[CaniathroxDemo] attackController 미할당 — CaniathroxAttack.controller 로드 실패");
        modelAnimator.applyRootMotion = true;   // ★루트모션이 캐릭터를 움직인다(제2원칙)

        if (model != null) { _startPos = model.position; _startRot = model.rotation; }

        TintGround();
        BuildPad();
        BuildRing();
        BuildSpitGlob();

        ResetCycle();
    }

    // 사이클 시작: 시작 위치로 리셋 + 더미를 향해 머리 방향(로코모션 전 1회) + 휴지 타이머.
    //   ★위치 리셋은 정지 상태(휴지)에서만 = 데모 루프 리셋. 모션 덮어쓰기가 아니다.
    void ResetCycle()
    {
        if (model != null)
        {
            model.SetPositionAndRotation(_startPos, _startRot);
            FaceTarget();   // 도약/접근이 더미 쪽으로 향하도록 머리 방향 1회 설정(AI 의도 번역)
        }
        SetApproaching(false);
        _attackFired = false;
        _impactFired = false;
        _spitFired = false;
        _restTimer = restBeforeApproach;
        HideAllVfx();
    }

    // 씬에 직렬화된 컨트롤러 참조가 없을 때(씬 재저장 금지 제약), 에디터에서 경로로 로드.
    //   런타임 빌드에선 빌더/Inspector가 attackController를 채워야 한다(이 데모는 에디터 랩 전용).
    void ResolveControllerIfNeeded()
    {
        if (attackController != null) return;
#if UNITY_EDITOR
        attackController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/_Project/Animations/CaniathroxAttack.controller");
#endif
    }

    void SetApproaching(bool v)
    {
        _approaching = v;
        if (modelAnimator != null) modelAnimator.SetBool(PApproach, v);
    }

    // 더미를 바라보게(평지 yaw만). 도약(+Z 로컬)이 더미 쪽으로 향하도록.
    void FaceTarget()
    {
        if (model == null || spitTarget == null) return;
        Vector3 dir = spitTarget.position - model.position; dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
            model.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }

    // ════════ Update — 상태머신이 모션을 돌리고, 코드는 조건/VFX만 ════════
    void Update()
    {
        if (modelAnimator == null) return;
        var info = modelAnimator.GetCurrentAnimatorStateInfo(0);

        // ── 휴지 → 접근 시작 ──
        if (info.shortNameHash == SIdle)
        {
            // 사이클 경계: 휴지로 돌아왔으면 리셋(단, 막 리셋한 직후 중복 방지 위해 _attackFired로 게이트)
            if (_attackFired)            // 직전 사이클이 공격까지 끝났다 → 새 사이클 리셋
            {
                ResetCycle();
            }
            else
            {
                _restTimer -= Time.deltaTime;
                if (_restTimer <= 0f && !_approaching)
                {
                    FaceTarget();        // 접근 직전 최종 조준(1회)
                    SetApproaching(true); // → Approach(Run_RM) 진입
                }
            }
        }
        // ── 접근 중: 도착 판정 → 도약 트리거 ──
        else if (info.shortNameHash == SApp)
        {
            if (!_attackFired && ArrivedAtTarget())
            {
                _attackFired = true;
                SetApproaching(false);          // 다음 사이클까지 접근 끔
                modelAnimator.SetTrigger(PAttack); // → Lunge(Jump_RM) 컷 진입
            }
        }
        // ── 도약 중: 착지 프레임에 임팩트/장판 발동(클립 진행에서 읽음) ──
        else if (info.shortNameHash == SLunge)
        {
            float norm = info.normalizedTime % 1f;
            UpdatePadDuringLunge(norm);
            if (!_impactFired && norm >= LungeLandNorm)
            {
                _impactFired = true;
                FireImpact();
            }
        }
        // ── 스핏 중: 발사 프레임에 글롭 ──
        else if (info.shortNameHash == SSpit)
        {
            if (_padQuad != null) _padQuad.SetActive(false);
            float norm = info.normalizedTime % 1f;
            if (!_spitFired && norm >= spitFireNorm) { _spitFired = true; FireSpit(); }
        }

        UpdateVfxLive(Time.deltaTime);
    }

    // 도착 판정: 더미와의 평면 거리 ≤ lungeRange. (위치를 읽는 것 = 조건, 위치를 만드는 것 아님)
    bool ArrivedAtTarget()
    {
        if (model == null || spitTarget == null) return false;
        Vector3 a = model.position, b = spitTarget.position; a.y = b.y = 0f;
        return Vector3.Distance(a, b) <= lungeRange;
    }

    // ════════ 장판: 도약 진행에 따라 채워지고 착지 norm에 100% ════════
    void UpdatePadDuringLunge(float norm)
    {
        if (_padQuad == null) return;
        bool visible = norm < LungeLandNorm + 0.06f;
        _padQuad.SetActive(visible);
        if (!visible) return;
        _padQuad.transform.position = LandingPoint() + Vector3.up * 0.05f;
        float fill = Mathf.Clamp01(norm / LungeLandNorm);
        _padMat.SetFloat(ProgressId, fill * fill);   // EaseInQuad
        _padMat.SetFloat(AlphaId, 0.9f);
    }

    // 도약 착지 지점 = 시작 위치 + 머리방향 * 전진거리(Jump_RM 실측 4.67m). 장판 표식용 좌표(클립이 실제로 데려갈 지점).
    Vector3 LandingPoint()
    {
        if (model == null) return _startPos;
        // 도약 시작 시점의 발판 추정: 현재 더미 근처. 단순히 더미 발치로.
        Vector3 p = spitTarget != null ? spitTarget.position : model.position + model.forward * 2f;
        p.y = 0f; return p;
    }

    Vector3 GroundUnderModel()
    {
        if (model == null) return LandingPoint();
        return new Vector3(model.position.x, 0f, model.position.z);
    }

    Vector3 MuzzlePos()
    {
        if (model == null) return Vector3.up;
        return model.position + model.forward * 0.9f + Vector3.up * 1.0f;
    }

    // ════════ 임팩트(착지) ════════
    void FireImpact()
    {
        if (_ringQuad != null)
        {
            _ringQuad.transform.position = GroundUnderModel() + Vector3.up * 0.05f;
            _ringQuad.SetActive(true);
            _ringTimer = 0f;
        }
    }

    // ════════ 스핏 발사 ════════
    void FireSpit()
    {
        if (_spitGlob == null) return;
        _spitFrom = MuzzlePos();
        _spitTo = spitTarget != null ? spitTarget.position + Vector3.up * 0.8f : _spitFrom + model.forward * 6f;
        _spitFlightT = 0f;
        _spitGlob.SetActive(true);
        if (_spitTrail != null) _spitTrail.SetActive(true);
        if (_muzzle != null) { _muzzle.SetActive(true); _muzzleTimer = 0f; _muzzle.transform.position = _spitFrom; }
    }

    // ════════ VFX 라이브 갱신 ════════
    void UpdateVfxLive(float dt)
    {
        if (_ringTimer >= 0f && _ringQuad != null)
        {
            _ringTimer += dt;
            if (_ringTimer >= RingExpand + RingFade) { _ringQuad.SetActive(false); _ringTimer = -1f; }
            else SetRingByTime(_ringTimer);
        }
        if (_muzzleTimer >= 0f && _muzzle != null)
        {
            _muzzleTimer += dt;
            if (_muzzleTimer >= MuzzleFlash) { _muzzle.SetActive(false); _muzzleTimer = -1f; }
        }
        if (_spitFlightT >= 0f && _spitGlob != null)
        {
            _spitFlightT += dt;
            float dist = Vector3.Distance(_spitFrom, _spitTo);
            float p = dist > 0.001f ? (SpitSpeed * _spitFlightT) / dist : 1f;
            if (p >= 1f) { _spitGlob.SetActive(false); if (_spitTrail != null) _spitTrail.SetActive(false); _spitFlightT = -1f; }
            else PlaceGlob(Vector3.Lerp(_spitFrom, _spitTo, p), _spitFrom, _spitTo);
        }
    }

    void HideAllVfx()
    {
        if (_padQuad != null) _padQuad.SetActive(false);
        if (_ringQuad != null) _ringQuad.SetActive(false);
        if (_spitGlob != null) _spitGlob.SetActive(false);
        if (_spitTrail != null) _spitTrail.SetActive(false);
        if (_muzzle != null) _muzzle.SetActive(false);
        _ringTimer = _muzzleTimer = _spitFlightT = -1f;
    }

    // ════════ VFX 빌드 ════════
    void TintGround()
    {
        var ground = GameObject.Find("Ground");
        if (ground == null) return;
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null) return;
        _groundMat = new Material(lit);
        _groundMat.SetColor("_BaseColor", new Color(0.16f, 0.16f, 0.17f, 1f));
        var mr = ground.GetComponent<MeshRenderer>();
        if (mr != null) mr.sharedMaterial = _groundMat;
    }

    int PadLayer()
    {
        int layer = LayerMask.NameToLayer("PickupInfo");
        if (layer < 0) { Debug.LogWarning("[CaniathroxDemo] 'PickupInfo' 레이어 미존재 — Default 폴백"); layer = 0; }
        return layer;
    }

    Shader ThreatArcShader()
    {
        var s = Shader.Find("ZombieCrush/ThreatArc");
        if (s == null) Debug.LogError("[CaniathroxDemo] ZombieCrush/ThreatArc 미발견 — Always Included 확인");
        return s;
    }

    void BuildPad()
    {
        if (ThreatArcShader() == null) return;
        _padQuad = MakeArcQuad("Caniathrox_TelePad", PadLayer(), new Vector2(PadRadius*2f, PadRadius*2f), out _padMat);
        _padMat.SetFloat(StyleId, 3f);
        _padMat.SetColor(ColorId, ThreatRed);
        _padMat.SetFloat(AlphaId, 0.9f);
        _padMat.SetFloat(SeedId, 21.9f);
        _padMat.SetFloat(EdgeAlphaId, 0.9f);
        _padMat.SetFloat(FillAlphaId, 0.4f);
        _padQuad.SetActive(false);
    }

    void BuildRing()
    {
        if (ThreatArcShader() == null) return;
        _ringQuad = MakeArcQuad("Caniathrox_ImpactRing", PadLayer(), new Vector2(1f, 1f), out _ringMat);
        _ringMat.SetFloat(StyleId, 6f);
        _ringMat.SetColor(ColorId, ThreatRed);
        _ringMat.SetFloat(AlphaId, 0f);
        _ringMat.SetFloat(SeedId, 3.3f);
        _ringMat.SetFloat(InnerR01Id, 0.62f);
        _ringMat.SetFloat(ProgressId, 1f);
        _ringMat.SetFloat(EdgeAlphaId, 1f);
        _ringMat.SetFloat(FillAlphaId, 0f);
        _ringQuad.SetActive(false);
    }

    GameObject MakeArcQuad(string name, int layer, Vector2 size, out Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        Destroy(go.GetComponent<Collider>());
        go.layer = layer;
        go.transform.SetPositionAndRotation(Vector3.up * 0.05f, Quaternion.Euler(90f, 0f, 0f));
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        mat = new Material(ThreatArcShader());
        mat.SetVector(SizeWorldId, new Vector4(size.x, size.y, 0f, 0f));
        mat.SetFloat(ZTestId, 4f);
        mat.renderQueue = 3000;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    void SetRingByTime(float t)
    {
        if (_ringQuad == null) return;
        float radius, alpha;
        if (t < RingExpand) { float u = t / RingExpand; radius = Mathf.Lerp(1.5f, 2.5f, 1f-(1f-u)*(1f-u)); alpha = 1f; }
        else { float u = (t - RingExpand) / RingFade; radius = 2.5f; alpha = 1f - u; }
        float d = radius * 2f;
        _ringQuad.transform.localScale = new Vector3(d, d, 1f);
        _ringMat.SetVector(SizeWorldId, new Vector4(d, d, 0f, 0f));
        _ringMat.SetFloat(AlphaId, alpha);
    }

    void BuildSpitGlob()
    {
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        var unlit = Shader.Find("Universal Render Pipeline/Unlit");

        _spitGlob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        _spitGlob.name = "Caniathrox_SpitGlob";
        Destroy(_spitGlob.GetComponent<Collider>());
        _spitGlob.transform.localScale = Vector3.one * 0.28f;
        _spitMat = new Material(unlit != null ? unlit : lit);
        _spitMat.SetColor("_BaseColor", new Color(1.6f, 0.48f, 0.13f, 1f));
        _spitGlob.GetComponent<MeshRenderer>().sharedMaterial = _spitMat;
        _spitGlob.SetActive(false);

        _spitTrail = new GameObject("Caniathrox_SpitTrail");
        _trailLine = _spitTrail.AddComponent<LineRenderer>();
        var partUnlit = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        _trailMat = new Material(partUnlit != null ? partUnlit : lit);
        _trailMat.SetColor("_BaseColor", ThreatRed);
        if (partUnlit != null)
        {
            _trailMat.EnableKeyword("_EMISSION");
            _trailMat.SetColor("_EmissionColor", GlowOrange * 0.7f);
            _trailMat.SetFloat("_Surface", 1f);
            _trailMat.SetFloat("_Blend", 1f);
            _trailMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _trailMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            _trailMat.SetFloat("_ZWrite", 0f);
            _trailMat.renderQueue = 3100;
        }
        _trailLine.material = _trailMat;
        _trailLine.widthMultiplier = 0.18f;
        _trailLine.numCapVertices = 4;
        _trailLine.positionCount = 2;
        _trailLine.textureMode = LineTextureMode.Stretch;
        _trailLine.startColor = ThreatRed;
        _trailLine.endColor = new Color(ThreatRed.r, ThreatRed.g, ThreatRed.b, 0f);
        _spitTrail.SetActive(false);

        _muzzle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        _muzzle.name = "Caniathrox_Muzzle";
        Destroy(_muzzle.GetComponent<Collider>());
        _muzzle.transform.localScale = Vector3.one * 0.5f;
        _muzzleMat = new Material(unlit != null ? unlit : lit);
        _muzzleMat.SetColor("_BaseColor", new Color(2.0f, 0.55f, 0.14f, 1f));
        _muzzle.GetComponent<MeshRenderer>().sharedMaterial = _muzzleMat;
        _muzzle.SetActive(false);
    }

    void PlaceGlob(Vector3 pos, Vector3 from, Vector3 to)
    {
        _spitGlob.transform.position = pos;
        if (_trailLine != null)
        {
            Vector3 tail = pos - (to - from).normalized * 0.6f;
            _trailLine.SetPosition(0, tail);
            _trailLine.SetPosition(1, pos);
        }
    }

    void OnDestroy()
    {
        DestroySafe(_padQuad); DestroySafe(_ringQuad); DestroySafe(_spitGlob);
        DestroySafe(_spitTrail); DestroySafe(_muzzle);
        DestroyMat(_padMat); DestroyMat(_ringMat); DestroyMat(_spitMat);
        DestroyMat(_trailMat); DestroyMat(_muzzleMat); DestroyMat(_groundMat);
    }

    static void DestroySafe(GameObject go) { if (go != null) Destroy(go); }
    static void DestroyMat(Material m) { if (m != null) Destroy(m); }
}
