using UnityEngine;

/// <summary>
/// ★시야 콘 렌더러(Blindspot 문법, 2026-06-11 유저 콜) — 플레이어에서 조준 방향으로 펼쳐지는
/// 부채꼴 가시 영역을 레이캐스트 팬으로 메시화 → 전용 마스크 카메라(★오쏘 수직 하방 — 퍼스펙티브
/// 금지, 경계 다겹 계단의 근본 원인)가 월드 XZ 평면 마스크 RT로 렌더 →
/// TiltShift 패스가 씬 깊이로 월드좌표를 재구성해 XZ→마스크 UV로 샘플,
/// 마스크 밖(콘 밖 + 벽 시야 그림자)을 어둠 처리한다(탑다운 fog-of-war 장르 표준).
/// 벽(Obstacle)이 시야 그림자를 드리운다: 코너 너머는 콘 각도 안이어도 어둠 = 화면이 곧 정보.
///
/// 좀비 가시성 게이트(ZombieLKPSilhouette)의 월드 판정 소스(InCone)를 겸한다 —
/// 화면의 어둠과 좀비 숨김이 같은 진실을 공유해야 모순이 없다.
///
/// 배치: 씬 루트 GO. 마스크 카메라/메시는 런타임 생성(HideAndDontSave — 씬 오염 없음).
/// </summary>
[ExecuteAlways]
public class VisionConeRenderer : MonoBehaviour
{
    public static VisionConeRenderer Instance { get; private set; }

    [Header("Cone Shape (정보 콘과 동일 의미 — 평시 넓게, 조준 시 수축)")]
    [SerializeField, Range(20f, 90f)] float baseHalfAngle = 70f;
    [SerializeField, Range(15f, 80f)] float aimHalfAngle = 45f;
    [Tooltip("수렴 완료 시 반각(도) — 조준의 비용 = 시야 자체가 터널로 무너진다(유저 콜).")]
    [SerializeField, Range(10f, 40f)] float convergedHalfAngle = 24f;
    [SerializeField, Min(5f)] float range = 18f;
    [Tooltip("밀착 전방위 가시 반경(m) — 등 뒤 밀착 위협 보호(작은 원). 벽은 여전히 가림.")]
    [SerializeField, Min(0f)] float proximityRadius = 3f;
    [Tooltip("부채꼴 레이 개수 — 시야 그림자 해상도.")]
    [SerializeField, Range(32, 256)] int rayCount = 120;
    [Tooltip("콘 방향 최대 각속도(도/초). 0 = 즉각(레이저처럼 마우스 일치 — 유저 콜).")]
    [SerializeField, Min(0f)] float dirMaxDegPerSec = 0f;
    [SerializeField] LayerMask obstacleMask = 1 << 8;
    [SerializeField, Min(0f)] float eyeHeight = 1f;
    [Tooltip("시야 메시가 깔리는 높이(m) — 지면 살짝 위.")]
    [SerializeField] float meshHeight = 0.1f;
    [Tooltip("시야 밖 어둠 강도(0~1) — Blindspot의 '안 보임' 깊이. 풀다크(1.0)는 가독성 헌법 위반.")]
    [SerializeField, Range(0f, 0.95f)] float maskDarkness = 0.7f;

    static readonly int MaskTexId      = Shader.PropertyToID("_TS_VisionMask");
    static readonly int MaskOnId       = Shader.PropertyToID("_TS_VisionMaskOn");
    static readonly int MaskCenterId   = Shader.PropertyToID("_TS_VisionMaskCenter");
    static readonly int MaskHalfSizeId = Shader.PropertyToID("_TS_VisionMaskHalfSize");

    GameObject _meshGO;
    Mesh _mesh;
    Material _meshMat;
    Camera _maskCam;
    RenderTexture _rt;
    PlayerCombat _player;
    PlayerCameraRig _rig;
    Camera _mainCam;
    Vector3 _dir = Vector3.forward;
    float _curHalf = 70f;
    int _visionLayer = -1;

    // BuildMesh 매 프레임 호출 — 관리 배열 재사용(GC 회피, 리뷰 M). rayCount 변경 시에만 재할당.
    const int CircleRays = 48;
    Vector2[] _circlePts;
    Vector2[] _conePts;
    Vector3[] _verts;
    int[] _tris;
    int _allocRayCount = -1;

    /// <summary>월드 좌표가 현재 시야 콘 안인가(각도+거리+근접 — LOS는 호출자 몫).
    /// 화면 어둠과 동일 기하 → 게이트와 비주얼이 한 진실.</summary>
    public bool InCone(Vector3 worldPos)
    {
        if (_player == null) return true;
        Vector3 to = worldPos - _player.transform.position;
        to.y = 0f;
        float d = to.magnitude;
        if (d <= proximityRadius) return true;
        if (d > range) return false;
        return Vector3.Angle(_dir, to) <= _curHalf + 4f;   // +4° 경계 여유(깜박임 방지)
    }

    public bool Ready => _player != null && isActiveAndEnabled;

    /// <summary>unity-mcp 플레이 모드 실측용(RunCommand는 Reflection 금지) — 수축 동작 검증 접근자.</summary>
    public float DebugCurHalf => _curHalf;
    /// <summary>unity-mcp 실측용 — PlayerCameraRig 캐시 바인딩 여부.</summary>
    public bool DebugRigBound => _rig != null;

    void OnEnable()
    {
        if (Application.isPlaying || Instance == null) Instance = this;
        _visionLayer = LayerMask.NameToLayer("VisionMask");
    }

    void OnDisable()
    {
        Shader.SetGlobalFloat(MaskOnId, 0f);
        if (Instance == this) Instance = null;
        Cleanup();
    }

    void Cleanup()
    {
        if (_maskCam != null) { DestroyImmediate(_maskCam.gameObject); _maskCam = null; }
        if (_meshGO != null) { DestroyImmediate(_meshGO); _meshGO = null; }
        if (_rt != null) { _rt.Release(); DestroyImmediate(_rt); _rt = null; }
        if (_mesh != null) { DestroyImmediate(_mesh); _mesh = null; }
        if (_meshMat != null) { DestroyImmediate(_meshMat); _meshMat = null; }
    }

    void EnsureResources()
    {
        if (_visionLayer < 0) return;
        if (_mesh == null) _mesh = new Mesh { name = "VisionConeMesh" };
        if (_meshMat == null)
        {
            _meshMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            _meshMat.SetColor("_BaseColor", Color.white);
            if (_meshMat.HasProperty("_Cull")) _meshMat.SetFloat("_Cull", 0f);   // 양면
            _meshMat.hideFlags = HideFlags.HideAndDontSave;
        }
        if (_meshGO == null)
        {
            _meshGO = new GameObject("VisionConeMesh") { hideFlags = HideFlags.HideAndDontSave, layer = _visionLayer };
            _meshGO.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var mr = _meshGO.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _meshMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        }
        if (_rt == null)
        {
            // RGBA32+뎁스16 — R8은 플랫폼 미지원 폴백 경고, 렌더그래프는 뎁스 요구.
            // 정사각 512 — 오쏘 톱다운 마스크는 월드 XZ 평면 텍스처라 화면 비율과 무관.
            _rt = new RenderTexture(512, 512, 16, RenderTextureFormat.Default) { name = "VisionMaskRT" };
            _rt.Create();
        }
        if (_maskCam == null)
        {
            // ★오쏘 수직 하방 고정(탑다운 fog-of-war 표준) — 게임플레이 퍼스펙티브로 찍으면
            //   "몸 높이 커버"와 "경계 한 줄"이 충돌한다(시도1·2의 3겹 계단 근본 원인).
            var go = new GameObject("VisionMaskCam") { hideFlags = HideFlags.HideAndDontSave };
            _maskCam = go.AddComponent<Camera>();
            _maskCam.enabled = false;   // 수동 Render()
            _maskCam.orthographic = true;
            _maskCam.aspect = 1f;
            _maskCam.nearClipPlane = 0.1f;
            _maskCam.farClipPlane = 100f;
            _maskCam.clearFlags = CameraClearFlags.SolidColor;
            _maskCam.backgroundColor = Color.black;
            _maskCam.cullingMask = 1 << _visionLayer;
            _maskCam.targetTexture = _rt;
            _maskCam.allowMSAA = false;
            var add = go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            add.renderPostProcessing = false;
            add.renderShadows = false;
        }
    }

    void LateUpdate()
    {
        if (_player == null) _player = FindObjectOfType<PlayerCombat>();
        if (_mainCam == null) { var go = GameObject.Find("Main Camera"); if (go != null) _mainCam = go.GetComponent<Camera>(); }
        if (_player == null || _mainCam == null || _visionLayer < 0)
        {
            Shader.SetGlobalFloat(MaskOnId, 0f);
            return;
        }
        EnsureResources();
        if (_maskCam == null) return;

        // ---- 콘 방향/각도 (0 = 즉각 일치 — 레이저 문법) ----
        Vector3 aim = _player.AimDirection; aim.y = 0f;
        if (aim.sqrMagnitude < 0.0001f) aim = _dir;
        aim.Normalize();
        if (dirMaxDegPerSec <= 0f) _dir = aim;
        else
        {
            float dt = Application.isPlaying ? Time.deltaTime : 1f;
            _dir = Vector3.RotateTowards(_dir, aim, dirMaxDegPerSec * Mathf.Deg2Rad * dt, 0f).normalized;
        }
        float aimF = 0f, conv = 0f;
        if (_rig == null) _rig = _mainCam.GetComponent<PlayerCameraRig>();
        if (_rig != null && Application.isPlaying) { aimF = _rig.AimBlend; conv = _rig.AimConvergence; }
        // 1단계: 70°→45° 수축, 2단계(수렴): →24° 터널 — 시야 자체가 수렴 게이지(시안 덮기 ❌).
        _curHalf = Mathf.Lerp(Mathf.Lerp(baseHalfAngle, aimHalfAngle, aimF), convergedHalfAngle, conv);

        // ---- 시야 메시 빌드 (근접 원 + 부채꼴, 레이캐스트로 벽 그림자) ----
        BuildMesh();

        // ---- 마스크 렌더 (오쏘 수직 하방 — 플레이어 상공에서 월드 XZ 평면을 그대로 찍는다) ----
        Vector3 origin = _player.transform.position;
        // 콘 range보다 +2m 여유 — 메시가 RT 가장자리에 닿지 않아 가장자리=검정,
        // 셰이더의 clamp 샘플이 "영역 밖=어둠"을 자연 처리한다.
        float orthoSize = range + 2f;
        _maskCam.transform.SetPositionAndRotation(origin + Vector3.up * 40f, Quaternion.Euler(90f, 0f, 0f));
        _maskCam.orthographicSize = orthoSize;
        _maskCam.Render();

        Shader.SetGlobalTexture(MaskTexId, _rt);
        // 셰이더 월드 XZ→마스크 UV 매핑 파라미터(매 프레임 — 플레이어 추종).
        Shader.SetGlobalVector(MaskCenterId, new Vector4(origin.x, origin.z, 0f, 0f));
        Shader.SetGlobalFloat(MaskHalfSizeId, orthoSize);
        // 에디터 편집 뷰 보호: 플레이 중에만 화면 어둠 활성(검증 캡처는 수동으로 켠다).
        // 값 = 시야 밖 어둠 강도(셰이더 maskHidden).
        Shader.SetGlobalFloat(MaskOnId, Application.isPlaying ? maskDarkness : 0f);
    }

    /// <summary>매 프레임 메시 빌드용 버퍼 확보 — rayCount(인스펙터 변경 가능) 변화 시에만 재할당(리뷰 M).</summary>
    void EnsureBuffers()
    {
        if (_allocRayCount == rayCount && _circlePts != null) return;
        _allocRayCount = rayCount;
        _circlePts = new Vector2[CircleRays + 1];
        _conePts = new Vector2[rayCount + 1];
        _verts = new Vector3[1 + (CircleRays + 1) + (rayCount + 1)];
        _tris = new int[(CircleRays + rayCount) * 3];
    }

    void BuildMesh()
    {
        Vector3 origin = _player.transform.position;
        Vector3 eye = origin + Vector3.up * eyeHeight;
        float baseAngle = Mathf.Atan2(_dir.z, _dir.x) * Mathf.Rad2Deg;

        EnsureBuffers();

        // 단층 평면 팬(y=meshHeight) — 높이 적층 폐지(시도2: 퍼스펙티브 카메라가 각 층 윤곽을
        // 따로 투영해 경계 3겹 계단). 좀비/플레이어 몸 커버는 셰이더가 담당 —
        // 깊이→월드 재구성으로 몸 픽셀도 자기 발밑 XZ로 마스크를 샘플하므로 몸 잘림이 원천 소멸.
        // 벽 클램프 +0.5m 오버슛: 벽 전면 픽셀의 XZ가 정확히 경계선에 떨어져 벽이 반쯤
        // 어두워지는 것을 방지(fog-of-war 표준 처치 — 벽 뒤 바닥은 벽 자신이 카메라에서 가린다).
        const float wallOvershoot = 0.5f;
        for (int i = 0; i <= CircleRays; i++)
        {
            float a = (360f / CircleRays) * i * Mathf.Deg2Rad;
            Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            float dist = proximityRadius;
            if (Physics.Raycast(eye, d, out RaycastHit hit, proximityRadius, obstacleMask, QueryTriggerInteraction.Ignore))
                dist = Mathf.Min(hit.distance + wallOvershoot, proximityRadius);
            _circlePts[i] = new Vector2(origin.x + d.x * dist, origin.z + d.z * dist);
        }
        for (int i = 0; i <= rayCount; i++)
        {
            float a = (baseAngle - _curHalf + (2f * _curHalf / rayCount) * i) * Mathf.Deg2Rad;
            Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            float dist = range;
            if (Physics.Raycast(eye, d, out RaycastHit hit, range, obstacleMask, QueryTriggerInteraction.Ignore))
                dist = Mathf.Min(hit.distance + wallOvershoot, range);
            _conePts[i] = new Vector2(origin.x + d.x * dist, origin.z + d.z * dist);
        }

        float y = meshHeight;
        int vi = 0, ti = 0;
        int center = vi;
        _verts[vi++] = new Vector3(origin.x, y, origin.z);

        // 1) 근접 원(전방위, 벽 클램프)
        int circleStart = vi;
        for (int i = 0; i <= CircleRays; i++)
            _verts[vi++] = new Vector3(_circlePts[i].x, y, _circlePts[i].y);
        for (int i = 0; i < CircleRays; i++)
        {
            _tris[ti++] = center; _tris[ti++] = circleStart + i + 1; _tris[ti++] = circleStart + i;
        }

        // 2) 부채꼴(조준 방향 ±_curHalf, 벽 클램프 = 시야 그림자)
        int coneStart = vi;
        for (int i = 0; i <= rayCount; i++)
            _verts[vi++] = new Vector3(_conePts[i].x, y, _conePts[i].y);
        for (int i = 0; i < rayCount; i++)
        {
            _tris[ti++] = center; _tris[ti++] = coneStart + i + 1; _tris[ti++] = coneStart + i;
        }

        // 버퍼 크기가 rayCount 고정이라 매 프레임 Clear+재할당 없이 같은 배열을 덮어쓴다.
        _mesh.Clear();
        _mesh.vertices = _verts;
        _mesh.triangles = _tris;
        _mesh.RecalculateBounds();
    }
}
