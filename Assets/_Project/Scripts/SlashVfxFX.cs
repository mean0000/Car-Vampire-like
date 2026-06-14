// 카타나 슬래시 궤적 VFX 1개 인스턴스 — (C)하이브리드: 기성 Vefects 슬래시(화려한 궤적=뽕) + ThreatArc SDF(정확한 범위).
//   ★유저 요구(2026-06-14): "공격 범위/궤적이 안 보여 답답 — 휩쓰는 슬래시가 팍 나와서 '내가 이 콤보 쓰네'가 보여야."
//   자작 LineRenderer 트레일(약함)을 이 인스턴스가 대체한다. SmashImpactFX/TelegraphPad의 슬래시 버전.
//
// 구성(공격 순간 같은 자리에서 함께 터짐):
//   1) 슬래시 메시 쿼드(Vefects URP 슬래시 셰이더, 시안 리컬러) — *화려한 휩쓰는 궤적*. 셰이더가 자체 스크롤·왜곡.
//   2) 범위 SDF 쿼드(ThreatArc.shader) — *정직한 히트박스*(월드미터). 시각=실제 판정. 짧게 떴다 페이드.
//   3) 스파크 버스트(검증된 KillBurst 가산 머티리얼 복제) — 임팩트 반짝(뽕 보강). 선택.
//
// ════════ 머티리얼 조달 — [[killburst-fx]] 함정 회피 ════════
//   ★Vefects "Stylized VFX URP" 팩은 *네이티브 URP*(Amplify, RenderPipeline=UniversalPipeline) — 다른 Vefects 팩의
//     BIRP surface 함정과 다르다. 리컬러 = 머티리얼 색 프로퍼티(_Color_1/_Color_2/_Emissive_Color) 교체로 충분(텍스처 추출 불필요).
//   ★시안/마젠타 리컬러 머티리얼은 디스크에 author됨(Resources/VFX/Slash/Materials/) — 풀이 Resources.Load해 복제 주입.
//     코드생성 new Material(Shader.Find)는 텍스처 흰폴백 → 반드시 검증된 디스크 머티리얼 복제(SmashImpactFX 규약).
//
// ════════ 렌더 경로(동결 — TelegraphPad와 동일, 재발명 금지) ════════
//   레이어 PickupInfo(13) → PickupInfoOverlay RenderObjects(이벤트 600, 투명 큐) = 시야콘 어둠 면제(내 액션은 항상 보임).
//   슬래시 = renderQueue 3050(장판 3000보다 위 — 궤적이 범위 위에 얹힘). _ZTest=Always(공중 궤적은 깊이 무시, 항상 보임).
//   범위 SDF = renderQueue 3000, _ZTest LEqual(바닥 장판처럼 깊이 존중).
using UnityEngine;

public class SlashVfxFX : MonoBehaviour
{
    // ── ThreatArc 셰이더 프로퍼티 ID(범위 SDF용 — TelegraphPad와 동일) ──
    static readonly int StyleId     = Shader.PropertyToID("_Style");
    static readonly int SizeWorldId = Shader.PropertyToID("_SizeWorld");
    static readonly int RangeColorId = Shader.PropertyToID("_Color");
    static readonly int AlphaId     = Shader.PropertyToID("_Alpha");
    static readonly int ProgressId  = Shader.PropertyToID("_Progress");
    static readonly int SeedId      = Shader.PropertyToID("_Seed");
    static readonly int ZTestId     = Shader.PropertyToID("_ZTest");
    static readonly int AngleDegId  = Shader.PropertyToID("_AngleDeg");
    static readonly int EdgeWorldId = Shader.PropertyToID("_EdgeWorld");
    static readonly int FillAlphaId = Shader.PropertyToID("_FillAlpha");
    static readonly int EdgeAlphaId = Shader.PropertyToID("_EdgeAlpha");

    // ── 시안 액션 색(팔레트 #2DE2E6) ──  (마젠타는 참격파 슬래시 머티리얼 _Color_2가 담당 — 평타엔 안 씀=색 캐넌)
    static readonly Color Cyan    = new Color(0.176f, 0.886f, 0.902f, 1f);
    // 코어 핫화이트(HDR) — 평타 슬래시 코어는 흰색으로 타고, 엣지(머티리얼 _Color_2)가 시안.
    static readonly Color HotWhite = Color.white;

    // ════════ 슬래시 평면 노브 (2026-06-14 v2 재시공 — 유저 시각 튜닝용) ════════
    //   v1 실패: 쿼드를 거의 수평으로 눕혀(틸트 15~18°) 슬래시가 바닥 스티커가 됨(부감 짜부).
    //   v2 교정: 휘두르는 호의 평면을 *세운다* — 완전 바닥(90° X회전)도 완전 빌보드(0°)도 아닌 중간.
    //   카메라 부감 45°에서 가슴 높이에 호가 일어서 카메라 쪽으로 보이게.
    //
    //   PlaneTiltDeg = "수직(빌보드)에서 카메라 반대로 뒤로 눕히는 각". 0=완전 빌보드(정면), 90=완전 바닥.
    //     35~50 권장(카메라 부감 45°와 어울리는 '비스듬히 선 칼자국'). ★유저가 보고 튜닝하는 1순위 노브.
    static float PlaneTiltDeg = 42f;
    //   슬래시 호가 뜨는 높이(가슴 높이). 0.7=v1(낮음, 발치). 1.0~1.2=가슴.
    static float SlashHeight = 1.1f;
    //   발도 직선 슬래시 평면 틸트(레인은 바닥을 쓸지만 살짝 세워 칼날이 보이게). 0=바닥, 90=수직.
    static float PierceTiltDeg = 35f;
    static float PierceHeight = 0.85f;
    //   참격파 확장 파동 — 반경 디스크라 바닥에 가깝게(AoE), 단 살짝 띄워 부감에서 안 묻히게.
    //   0=바닥 완전 평면(X90), 90=수직. 65~80 권장(거의 바닥이되 카메라로 약간 일어섬).
    static float WaveTiltDeg = 72f;
    static float WaveHeight = 0.25f;

    SlashVfxPool _pool;

    // 슬래시 메시 쿼드(화려한 궤적)
    Transform _slashQuad;
    MeshRenderer _slashMr;
    Material _slashMat;      // 인스턴스별 복제(색 일괄 유지, 회수 시 Destroy)
    Material _arcBaseMat;    // 풀이 준 시안 호 베이스(참격/거합 평타·참격파)
    Material _pierceBaseMat; // 풀이 준 시안 찌르기 베이스(거합 발도)
    Material _waveBaseMat;   // 풀이 준 시안+마젠타 베이스(참격파 임계)

    // 범위 SDF 쿼드(정확한 히트박스)
    Transform _rangeQuad;
    MeshRenderer _rangeMr;
    Material _rangeMat;
    bool _hasRange;

    // 스파크 버스트(뽕 보강)
    ParticleSystem _spark;

    // 활성 수명
    bool _active;
    float _elapsed;
    float _slashLife;   // 슬래시 궤적 유지(초) — 짧게 팍 떴다 페이드
    float _rangeLife;   // 범위 SDF 유지(초)
    float _sparkLife;   // 스파크 입자 최대 수명(초) — 잔류 방지(H-1)
    float _life;        // 전체 인스턴스 수명
    int _gen;

    // ── 진행형 궤적 드라이브(거합 발도/참격파) ── 시각=실제 누적 거리/반경 동기.
    //   드라이버(KatanaController)가 매 프레임 실제 값으로 Drive*()를 호출한다.
    //   발도: from·dir·width 고정, dist만 실제 누적으로 늘인다(벽 막히면 거기서 멈춤).
    //   참격파: origin·forward·halfAngle 고정, radius만 실제 확장값으로 늘인다.
    bool _drivePierce;
    bool _driveWave;
    Vector3 _driveOrigin;   // 발도 시작점(발도 전용 — 레인 중점 산출)
    Vector3 _driveDir;      // 정규화 방향(수평, 발도 전용)
    float _driveYaw;        // 발도 전용
    float _driveWidth;      // 발도 폭(레인 폭, 발도 전용)

    // 슬래시 페이드 캐시
    Color _slashColorBright;   // 페이드 시작 색
    float _slashStartAlpha;

    public int Gen => _gen;

    /// <summary>풀이 1회 바인딩 — 쿼드·렌더러를 만들고 시안/마젠타 베이스 머티리얼을 받는다.</summary>
    public void Build(SlashVfxPool pool, Mesh quadMesh, Shader rangeShader,
                      Material arcBase, Material pierceBase, Material waveBase,
                      Material sparkBase, int layer)
    {
        _pool = pool;
        _arcBaseMat = arcBase;
        _pierceBaseMat = pierceBase;
        _waveBaseMat = waveBase;

        // ── 슬래시 메시 쿼드(화려한 궤적) ── 표준 쿼드 + Vefects 슬래시 머티리얼(텍스처가 슬래시 형태, UV0).
        var slashGo = new GameObject("Slash");
        slashGo.transform.SetParent(transform, false);
        slashGo.layer = layer;
        var smf = slashGo.AddComponent<MeshFilter>();
        smf.sharedMesh = quadMesh;
        _slashMr = slashGo.AddComponent<MeshRenderer>();
        _slashMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _slashMr.receiveShadows = false;
        _slashQuad = slashGo.transform;

        // ── 범위 SDF 쿼드(정직한 히트박스) ── ★셰이더 null이면(빌드 스트립) 범위 레이어 생략(슬래시는 동작 — 부분 가동).
        if (rangeShader != null)
        {
            var rangeGo = new GameObject("Range");
            rangeGo.transform.SetParent(transform, false);
            rangeGo.layer = layer;
            var rmf = rangeGo.AddComponent<MeshFilter>();
            rmf.sharedMesh = quadMesh;
            _rangeMr = rangeGo.AddComponent<MeshRenderer>();
            _rangeMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _rangeMr.receiveShadows = false;
            _rangeMat = new Material(rangeShader);
            _rangeMat.renderQueue = VfxLayers.TelegraphFloor;  // 바닥 범위 — 슬래시(SlashTrail=3050)보다 아래
            _rangeMr.sharedMaterial = _rangeMat;
            _rangeQuad = rangeGo.transform;
            _hasRange = true;
        }

        // ── 스파크 버스트(뽕 보강) ── 검증된 가산 머티리얼 복제(흰폴백 회피). sparkBase null이면 생략.
        if (sparkBase != null)
        {
            var sparkGo = new GameObject("Spark");
            sparkGo.transform.SetParent(transform, false);
            sparkGo.layer = layer;
            _spark = sparkGo.AddComponent<ParticleSystem>();
            ConfigureSpark(_spark, sparkBase);
        }

        Deactivate();
    }

    void ConfigureSpark(ParticleSystem ps, Material sparkBase)
    {
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.4f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.35f);
        main.startColor = Cyan;
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 24;
        main.stopAction = ParticleSystemStopAction.None;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 10) });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 22f;
        shape.radius = 0.1f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var pr = ps.GetComponent<ParticleSystemRenderer>();
        pr.renderMode = ParticleSystemRenderMode.Billboard;
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        pr.receiveShadows = false;
        var sparkMat = new Material(sparkBase);   // 복제(가산 보존, 흰폴백 회피)
        sparkMat.renderQueue = VfxLayers.SlashSpark;
        pr.sharedMaterial = sparkMat;
    }

    void Deactivate()
    {
        _active = false;
        if (_slashMr != null) _slashMr.enabled = false;
        if (_rangeMr != null) _rangeMr.enabled = false;
    }

    /// <summary>풀이 강제 회수 시 — 즉시 비활성. Acquire 직후 Play가 재세팅한다.</summary>
    public void ForceDeactivate()
    {
        Deactivate();
        if (_spark != null) _spark.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    // ════════════════════════════════════════════════════════════
    //  참격 평타 / 거합 평타 — ★v2: 공중 선 크레센트(코어 핫화이트 HDR + 엣지 시안). 채움 부채꼴 SDF 제거.
    //   tier01: 콤보 단(0~1). 높을수록 코어가 더 밝게 작열(크기 고정·평타 마젠타 ❌=색 캐넌).
    // ════════════════════════════════════════════════════════════
    public void PlayFan(Vector3 origin, Vector3 forward, float range, float halfAngleDeg, float tier01)
    {
        _gen++;
        _drivePierce = false; _driveWave = false;
        tier01 = Mathf.Clamp01(tier01);   // ★클램프(콤보 노브 footgun → 색·밝기 폭주 차단)
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();
        float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;

        // ★시각=판정 정합: 실제 최대 히트 거리 = SwingFan gather(range+0.5). 콤보 크기 오버슈트 제거 —
        //   5단 화려함은 *밝기/HDR/마젠타 틴트*로만 표현(크기는 판정 그대로, 불공정 0).
        float reach = range + 0.5f;
        float diameter = reach * 2f;

        // ── 공중 선 크레센트(코어 핫화이트 HDR + 엣지 시안). 콤보 높을수록 밝음 — 크기는 고정 ──
        SetSlashMaterial(_arcBaseMat);
        // ★v2: 슬래시 쿼드를 세운다(공중 크레센트). 바닥 스티커 ❌.
        PlaceSlashFlat(origin, yaw, tier01);
        _slashQuad.localScale = new Vector3(diameter, diameter, 1f);
        // ★v2 색 정리: 코어=핫화이트(HDR, 콤보 높을수록 더 밝게 타오름). 평타 마젠타 폐기(색 캐넌 위반).
        //   엣지 시안은 머티리얼 _Color_2가 담당(디스크 author). 코어를 흰색으로 밀면 가운데가 작열, 가장자리는 시안.
        Color core = HotWhite * Mathf.Lerp(1.1f, 2.0f, tier01);   // HDR(>1) 가산 블룸 — 코어가 작열
        ApplySlashColor(core, Mathf.Lerp(0.75f, 1f, tier01));
        _slashLife = Mathf.Lerp(0.16f, 0.26f, tier01);   // 콤보 높을수록 살짝 더 머묾
        _slashMr.enabled = true;

        // ★v2: 평타에서 ThreatArc 채움 부채꼴 제거 — 슬래시 크레센트(diameter=실제 reach)가 범위를 말한다.
        //   SDF 채움은 적 텔레그래프 전용으로 환원(평타에 겹치면 보드판 + 탁함). 범위 쿼드 비활성 유지.
        if (_rangeMr != null) _rangeMr.enabled = false;
        _rangeLife = 0f;

        EmitSpark(origin + forward * (reach * 0.5f) + Vector3.up * SlashHeight, forward, Mathf.Lerp(0.6f, 1.2f, tier01));
        BeginLife();
    }

    // ════════════════════════════════════════════════════════════
    //  거합 발도 — 시안 직선 찌르기(돌진 경로) + ThreatArc 레인 범위
    //   charge01: 차지량(0~1). 길수록 긴 절단선.
    // ════════════════════════════════════════════════════════════
    public void PlayPierce(Vector3 from, Vector3 dir, float width, float charge01)
    {
        _gen++;
        _drivePierce = true; _driveWave = false;
        charge01 = Mathf.Clamp01(charge01);
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
        dir.Normalize();
        float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        from.y = 0f;

        // ★진행형 드라이브: 목표 거리로 미리 찍지 않는다 — 실제 누적 이동(DrivePierce)으로 늘인다(벽=거기서 멈춤).
        _driveOrigin = from;
        _driveDir = dir;
        _driveYaw = yaw;
        _driveWidth = Mathf.Max(0.3f, width * 2f);

        // ── 화려한 직선 슬래시(시안 찌르기) ── 길이 0에서 시작, 드라이버가 실제 누적으로 확장.
        SetSlashMaterial(_pierceBaseMat);
        Color cyanBright = Cyan * Mathf.Lerp(1f, 1.7f, charge01);
        ApplySlashColor(cyanBright, Mathf.Lerp(0.8f, 1f, charge01));
        _slashLife = 0.22f;
        ApplyPierceLength(0f);   // 첫 프레임 = 0 길이(아직 안 움직임)
        _slashMr.enabled = true;

        // ── 범위 = 희미한 엣지 선만(ThreatArc 레인) ── ★v2: 채움 폐기(FillAlpha 0). 칼날 스트릭이 주연,
        //   레인 외곽선은 "여기까지 베인다"를 희미하게 보조만(보드판 채움 ❌). 길이 0→드라이버 확장.
        if (_hasRange)
        {
            _rangeMat.SetFloat(StyleId, 4f);
            _rangeMat.SetColor(RangeColorId, Cyan);
            _rangeMat.SetFloat(AlphaId, 1f);
            _rangeMat.SetFloat(FillAlphaId, 0f);     // ★채움 제거 — 외곽선만
            _rangeMat.SetFloat(EdgeAlphaId, 0.4f);   // ★희미하게(0.95→0.4)
            _rangeMat.SetFloat(EdgeWorldId, 0.08f);
            _rangeMat.SetFloat(SeedId, Random.value * 37f);
            _rangeMat.SetFloat(ProgressId, 1f);
            _rangeMat.SetFloat(ZTestId, (float)UnityEngine.Rendering.CompareFunction.LessEqual);
            ApplyPierceRange(0f);
            _rangeMr.enabled = true;
        }
        _rangeLife = 0.18f;

        EmitSpark(from + Vector3.up * 0.6f, dir, Mathf.Lerp(0.8f, 1.4f, charge01));
        BeginLife();
    }

    /// <summary>발도 드라이브 — 실제 누적 이동 거리(m)로 절단선·레인 범위를 늘인다. 벽 막히면 traveled가 멈춰 VFX도 멈춤.</summary>
    public void DrivePierce(float traveled, Vector3 tip)
    {
        if (!_active || !_drivePierce) return;
        traveled = Mathf.Max(0f, traveled);
        ApplyPierceLength(traveled);
        ApplyPierceRange(traveled);
        if (_spark != null) _spark.transform.position = tip + Vector3.up * 0.6f;   // 스파크가 칼끝 따라감
    }

    void ApplyPierceLength(float length)
    {
        length = Mathf.Max(0.01f, length);
        Vector3 mid = _driveOrigin + _driveDir * (length * 0.5f);
        // ★v2: 발도 직선 슬래시도 살짝 세운다(칼날 스트릭이 보이게) — 바닥 완전 평면 ❌. PierceTiltDeg 노브.
        mid.y = PierceHeight;
        _slashQuad.SetPositionAndRotation(mid, Quaternion.Euler(PierceTiltDeg, _driveYaw, 0f));
        _slashQuad.localScale = new Vector3(_driveWidth, length, 1f);
    }

    void ApplyPierceRange(float length)
    {
        if (!_hasRange) return;
        length = Mathf.Max(0.01f, length);
        Vector3 mid = _driveOrigin + _driveDir * (length * 0.5f); mid.y = 0f;
        _rangeMat.SetVector(SizeWorldId, new Vector4(_driveWidth, length, 0f, 0f));
        _rangeQuad.localScale = new Vector3(_driveWidth, length, 1f);
        _rangeQuad.SetPositionAndRotation(mid + Vector3.up * 0.05f, Quaternion.Euler(90f, _driveYaw, 0f));
    }

    // ════════════════════════════════════════════════════════════
    //  참격파 — 확장 부채꼴 파동(시안 코어+마젠타 엣지) + ThreatArc 확장 부채꼴
    //   ★진행형: 즉시 max로 안 찍는다 — 드라이버(KatanaController.StepWave)가 실제 radius(base→max)를
    //   매 프레임 DriveWave로 넘겨 슬래시 크기·범위 _Progress를 판정 확장과 동기한다.
    // ════════════════════════════════════════════════════════════
    /// <summary>참격파 스폰. baseRange=시작 반경(평타 리치), maxRange=끝 반경. 드라이버가 DriveWave로 실제 확장 동기.</summary>
    public void PlayWave(Vector3 origin, Vector3 forward, float baseRange, float maxRange, float halfAngleDeg)
    {
        _gen++;
        _driveWave = true; _drivePierce = false;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();
        float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        origin.y = 0f;

        // ── 화려한 확장 파동(시안 코어+마젠타 엣지=오버드라이브 캐넌, 평타와 달리 참격파는 마젠타 허용) ──
        //   baseRange에서 시작, 드라이버가 실제 radius로 확장. ★v2: 반경 디스크라 바닥에 가깝게 띄움(WaveTilt).
        SetSlashMaterial(_waveBaseMat);
        Vector3 wavePos = origin; wavePos.y = WaveHeight;
        _slashQuad.SetPositionAndRotation(wavePos, Quaternion.Euler(WaveTiltDeg, yaw, 0f));
        ApplyWaveSlashSize(baseRange);
        // 마젠타 엣지는 머티리얼 _Color_2가 담당 — 여기선 코어를 강한 시안 HDR로.
        ApplySlashColor(Cyan * 1.8f, 1f);
        _slashLife = 0.4f;   // 참격파는 더 오래(0.4s 확장 연출)
        _slashMr.enabled = true;

        // ── 범위 = 희미한 확장 엣지 선만(ThreatArc 부채꼴) ── ★v2: 채움 폐기(보드판 ❌). 휩쓰는 파동이 주연.
        //   _Progress를 실제 확장 비율(base→max)로 매 프레임 구동 → 외곽선이 휩쓸며 퍼지는 리딩 엣지.
        if (_hasRange)
        {
            _rangeMat.SetFloat(StyleId, 5f);
            _rangeMat.SetFloat(AngleDegId, Mathf.Clamp(halfAngleDeg * 2f, 1f, 180f));
            _rangeMat.SetColor(RangeColorId, Cyan);
            _rangeMat.SetFloat(AlphaId, 1f);
            _rangeMat.SetFloat(FillAlphaId, 0f);     // ★채움 제거 — 외곽선(확장 리딩엣지)만
            _rangeMat.SetFloat(EdgeAlphaId, 0.45f);  // ★희미하게
            _rangeMat.SetFloat(EdgeWorldId, 0.1f);
            _rangeMat.SetFloat(SeedId, Random.value * 37f);
            _rangeMat.SetFloat(ZTestId, (float)UnityEngine.Rendering.CompareFunction.LessEqual);
            // SizeWorld는 최대 반경으로 고정(셰이더가 _Progress로 채움 반경 결정) — 실제 radius/maxRange를 _Progress로.
            float maxDiameter = Mathf.Max(0.2f, maxRange) * 2f;
            _rangeMat.SetVector(SizeWorldId, new Vector4(maxDiameter, maxDiameter, 0f, 0f));
            _rangeQuad.localScale = new Vector3(maxDiameter, maxDiameter, 1f);
            _rangeQuad.SetPositionAndRotation(origin + Vector3.up * 0.05f, Quaternion.Euler(90f, yaw, 0f));
            ApplyWaveProgress(baseRange, baseRange, maxRange);
            _rangeMr.enabled = true;
        }
        _rangeLife = 0.34f;

        EmitSpark(origin + forward * (baseRange * 0.6f) + Vector3.up * 0.7f, forward, 1.8f);
        BeginLife();
    }

    /// <summary>참격파 드라이브 — 실제 확장 radius로 슬래시 크기·범위 _Progress를 동기.</summary>
    public void DriveWave(float radius, float baseRange, float maxRange)
    {
        if (!_active || !_driveWave) return;
        ApplyWaveSlashSize(radius);
        ApplyWaveProgress(radius, baseRange, maxRange);
    }

    void ApplyWaveSlashSize(float radius)
    {
        float diameter = Mathf.Max(0.2f, radius) * 2f;
        _slashQuad.localScale = new Vector3(diameter, diameter, 1f);
    }

    void ApplyWaveProgress(float radius, float baseRange, float maxRange)
    {
        if (!_hasRange) return;
        // 셰이더 _Progress = 채움 반경 비율. 실제 radius를 maxRange 대비 비율로(확장 동기).
        float denom = Mathf.Max(0.01f, maxRange);
        _rangeMat.SetFloat(ProgressId, Mathf.Clamp01(radius / denom));
    }

    // ── 슬래시 머티리얼 교체(베이스 복제, 이전 복제 정리) ──
    void SetSlashMaterial(Material baseMat)
    {
        if (baseMat == null) return;
        if (_slashMat != null) Destroy(_slashMat);
        _slashMat = new Material(baseMat);   // 복제(텍스처·블렌드 보존, 흰폴백 회피)
        _slashMat.renderQueue = VfxLayers.SlashTrail;  // 범위(TelegraphFloor=3000) 위 — 궤적이 범위 위에 얹힘
        _slashMat.SetFloat(ZTestId, (float)UnityEngine.Rendering.CompareFunction.Always);  // 공중 궤적 = 깊이 무시(항상 보임)
        _slashMr.sharedMaterial = _slashMat;
    }

    // Vefects 슬래시 색 = _Color_1(코어)·_Emissive_Color(발광). 알파는 _Opacity_Boost로.
    void ApplySlashColor(Color core, float opacity)
    {
        if (_slashMat == null) return;
        if (_slashMat.HasProperty("_Color_1")) _slashMat.SetColor("_Color_1", core);
        if (_slashMat.HasProperty("_Emissive_Color")) _slashMat.SetColor("_Emissive_Color", core);
        if (_slashMat.HasProperty("_Opacity_Boost")) _slashMat.SetFloat("_Opacity_Boost", opacity * 1.2f);
        _slashColorBright = core;
        _slashStartAlpha = opacity;
    }

    // ★v2 교정: 슬래시 쿼드를 *세운다*(공중 선 크레센트). 바닥 스티커 ❌.
    //   회전 = 먼저 yaw(공격 방향)로 돌리고, 그 다음 *세계 X축이 아니라 야우-회전된 좌우축*을 기준으로
    //   PlaneTiltDeg만큼 뒤로 눕힌다. tilt 0 = 쿼드가 수직으로 서서 정면(빌보드 비슷), 90 = 바닥 완전 평면.
    //   Quaternion.Euler(tilt, yaw, 0)에서 X(tilt)는 로컬이 아닌 월드 순서지만, Euler는 ZXY 적용이라
    //   yaw(Y) 먼저·tilt(X) 나중 → 칼자국이 공격 방향을 향하면서 카메라 쪽으로 일어선다.
    //   높이 = 가슴(SlashHeight). v1의 발치 0.7 → 1.1.
    void PlaceSlashFlat(Vector3 origin, float yaw, float tilt01)
    {
        Vector3 pos = origin; pos.y += SlashHeight;
        _slashQuad.SetPositionAndRotation(pos, Quaternion.Euler(PlaneTiltDeg, yaw, 0f));
    }

    // (★v2: 평타 SetupRangeFan 제거 — 평타는 슬래시 크레센트가 범위를 말한다. 채움 부채꼴 SDF는 적 텔레그래프 전용.)
    // (레인/확장 범위 SDF는 PlayPierce/PlayWave가 진행형으로 직접 세팅 — 채움 0·엣지 희미.)

    // 스파크 발동 — SmashImpactFX와 동일한 Play() 경로(정지 PS의 Emit() 미시뮬레이션 함정 회피).
    //   burstScale: 버스트 개수 배율(콤보/차지 높을수록 더 많이). 1.0 = 기본 10개.
    void EmitSpark(Vector3 pos, Vector3 dir, float burstScale)
    {
        if (_spark == null) { _sparkLife = 0f; return; }
        _spark.transform.position = pos;
        if (dir.sqrMagnitude > 0.0001f)
            _spark.transform.rotation = Quaternion.LookRotation(dir);
        var emission = _spark.emission;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Clamp(Mathf.RoundToInt(10 * burstScale), 4, 24)) });
        // ★H-1: 스파크 입자가 다 죽을 때까지 = duration + 최대 lifetime(~0.68s). _life에 포함해야 입자가
        //   먼저 Return되어 다음 Acquire에 잔류/끊기지 않음(쌍둥이 SmashImpactFX의 dustLife와 동형).
        _sparkLife = _spark.main.duration + _spark.main.startLifetime.constantMax;
        _spark.Clear(true);
        _spark.Play(true);
    }

    void BeginLife()
    {
        _elapsed = 0f;
        _active = true;
        _life = Mathf.Max(Mathf.Max(_slashLife, _rangeLife), _sparkLife) + 0.05f;
    }

    void Update()
    {
        if (!_active) return;
        // ★시간원 정렬(item 4): 카타나 로직(StepLunge/StepWave/쿨다운)이 Time.deltaTime을 쓴다 →
        //   VFX 페이드·수명도 deltaTime으로 같은 시간원(히트스탑 timeScale↓ 시 판정·VFX가 함께 느려져 동기).
        //   unscaled를 쓰면 히트스탑 중 궤적만 앞서가 진행형 드라이브(DrivePierce/DriveWave)와 어긋난다.
        _elapsed += Time.deltaTime;

        // 슬래시 궤적 페이드(밝게 떴다 잦아듦) — _Opacity_Boost로 알파, 색은 약간 어둡게.
        if (_slashMr != null && _slashMr.enabled)
        {
            float p = Mathf.Clamp01(_elapsed / Mathf.Max(0.01f, _slashLife));
            if (_slashMat != null && _slashMat.HasProperty("_Opacity_Boost"))
            {
                float fade = 1f - p * p;   // 끝에서 부드럽게
                _slashMat.SetFloat("_Opacity_Boost", _slashStartAlpha * 1.2f * fade);
            }
            if (p >= 1f) _slashMr.enabled = false;
        }

        // 범위 SDF 페이드(_Alpha 마스터로 잦아듦).
        if (_rangeMr != null && _rangeMr.enabled)
        {
            float rp = Mathf.Clamp01(_elapsed / Mathf.Max(0.01f, _rangeLife));
            if (_rangeMat != null) _rangeMat.SetFloat(AlphaId, 1f - rp);
            if (rp >= 1f) _rangeMr.enabled = false;
        }

        if (_elapsed >= _life)
        {
            Deactivate();
            if (_spark != null) _spark.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (_pool != null) _pool.Return(this);
        }
    }

    void OnDestroy()
    {
        if (_slashMat != null) Destroy(_slashMat);
        if (_rangeMat != null) Destroy(_rangeMat);
        if (_spark != null)
        {
            var pr = _spark.GetComponent<ParticleSystemRenderer>();
            if (pr != null && pr.sharedMaterial != null) Destroy(pr.sharedMaterial);
        }
    }
}
