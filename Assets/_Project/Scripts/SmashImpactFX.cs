// 슬램 임팩트 VFX 1개 인스턴스 — 거구 브루트(Crassorrid)가 내려찍는 *닿는 순간*의 충격.
//   ★유저 피드백(2026-06-14): "덩치 큰데 위협이 안 됨, 압박감 없음 — 근접 몬스터도 VFX 필요."
//   슬램은 이미 빠르고(StrikeSpeed 2.8) AoE도 큼(r5). 이건 *닿는 순간의 무게*를 만든다.
//
// 구성(닿는 점에서 동시에 터짐):
//   1) 충격파 링(SmashShock.shader, 가산 발광 메시 쿼드) — 바깥으로 퍼지며 얇아지는 파문 + 중심 섬광.
//   2) 먼지 폭발(코드 빌드 ParticleSystem, 검증된 KillBurst 가산 머티리얼 복제) — 중립 먼지 + 위로 솟구침.
//   3) 바닥 그을림/균열(평평한 알파 쿼드, 짧게 페이드) — "여기가 찍혔다" 잔흔.
//
// ════════ 재사용 규약 (29종 몬스터 틀 — Caniathrox/Dimax 근접 종에 그대로 얹음) ════════
//   - 런타임 생성(스포너/Resources 주입), 풀링 — 누수 0(수명 끝나면 Return).
//   - URP 파티클 함정 회피([[killburst-fx]]): 코드생성 머티리얼 흰폴백 → *검증된 머티리얼 복제* + 색만 교체.
//   - 충격파 = additive 메시(파티클 함정 자체를 피함, Caniathrox 발광구체 패턴 변형).
//   - 색: 먼지=중립 회백(라이팅 받음), 충격파=레드오렌지 가산(위협 §5, HDR 블룸). 톤=유저 판정.
using UnityEngine;

public class SmashImpactFX : MonoBehaviour
{
    static readonly int ProgressId  = Shader.PropertyToID("_Progress");
    static readonly int SizeWorldId = Shader.PropertyToID("_SizeWorld");
    static readonly int ColorId     = Shader.PropertyToID("_Color");
    static readonly int RingWidthId = Shader.PropertyToID("_RingWidth");
    static readonly int CoreFlashId = Shader.PropertyToID("_CoreFlash");
    static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    static readonly int SeedId      = Shader.PropertyToID("_Seed");
    // 그을림/먼지 머티리얼 색 프로퍼티 — URP Particles/Unlit은 _BaseColor, 폴백 _Color(=ColorId와 동일 문자열, 충격파와 공유).
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    SmashImpactPool _pool;

    // 충격파 메시
    Transform _shockQuad;
    MeshRenderer _shockMr;
    Material _shockMat;

    // 바닥 그을림 쿼드
    Transform _scorchQuad;
    MeshRenderer _scorchMr;
    Material _scorchMat;

    // 먼지 파티클
    ParticleSystem _dust;

    // 활성 수명
    bool _active;
    float _elapsed;
    float _shockDuration;   // 충격파 확장 시간(초)
    float _scorchDuration;   // 그을림 페이드 시간(초)
    float _life;             // 전체 인스턴스 수명(가장 긴 요소 + 여유)
    float _radius;           // 이번 임팩트 반경(m)
    int _gen;

    public int Gen => _gen;

    /// <summary>풀이 1회 바인딩 — 충격파 메시·그을림 쿼드·먼지 PS를 생성하고 모은다.</summary>
    public void Build(SmashImpactPool pool, Mesh quadMesh, Shader shockShader, Material dustBaseMat, int layer)
    {
        _pool = pool;

        // ── 충격파 발광 링(가산 메시) ── ★셰이더 null이면(빌드 스트립 등) 충격파 생략 — new Material(null) 분홍 함정 회피.
        //   먼지·그을림은 그대로 동작(부분 가동 > 분홍 깨짐). 풀이 이미 경고 로그를 냈다.
        if (shockShader != null)
        {
            var shockGo = new GameObject("Shock");
            shockGo.transform.SetParent(transform, false);
            shockGo.layer = layer;
            var smf = shockGo.AddComponent<MeshFilter>();
            smf.sharedMesh = quadMesh;
            _shockMr = shockGo.AddComponent<MeshRenderer>();
            _shockMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _shockMr.receiveShadows = false;
            _shockMat = new Material(shockShader);
            _shockMat.renderQueue = VfxLayers.ImpactShock;
            _shockMr.sharedMaterial = _shockMat;
            _shockQuad = shockGo.transform;
        }

        // ── 바닥 그을림/균열(알파 쿼드) ── 충격파보다 살짝 아래, 어두운 잔흔.
        var scorchGo = new GameObject("Scorch");
        scorchGo.transform.SetParent(transform, false);
        scorchGo.layer = layer;
        var scmf = scorchGo.AddComponent<MeshFilter>();
        scmf.sharedMesh = quadMesh;
        _scorchMr = scorchGo.AddComponent<MeshRenderer>();
        _scorchMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _scorchMr.receiveShadows = false;
        // 그을림은 충격파 셰이더의 비확장(_Progress 고정) 모드로 재활용하기보다, 단순 어두운 가산-역(곱셈 느낌)
        //   대신 같은 SmashShock를 어둡게? → 그을림은 *어둡게 빼는* 느낌이 자연스러우니 별도 알파블렌드 머티리얼.
        //   URP/Particles/Unlit(검증된 dustBaseMat)을 복제해 알파블렌드로 바꿔 쓴다(흰폴백 회피).
        _scorchMat = new Material(dustBaseMat);
        // 알파 블렌드(어두운 그을림이 바닥을 덮음) — SrcAlpha OneMinusSrcAlpha.
        _scorchMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _scorchMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _scorchMat.SetFloat("_ZWrite", 0f);
        _scorchMat.renderQueue = VfxLayers.Scorch;  // 충격파보다 먼저(바닥)
        _scorchMr.sharedMaterial = _scorchMat;
        _scorchQuad = scorchGo.transform;

        // ── 먼지 폭발(코드 빌드 ParticleSystem) ──
        var dustGo = new GameObject("Dust");
        dustGo.transform.SetParent(transform, false);
        dustGo.layer = layer;
        _dust = dustGo.AddComponent<ParticleSystem>();
        ConfigureDust(_dust, dustBaseMat);

        Deactivate();
    }

    // 먼지 PS 모듈 셋업 — 코드 전부. 머티리얼은 검증된 가산 머티리얼 복제(흰폴백 함정 회피).
    void ConfigureDust(ParticleSystem ps, Material dustBaseMat)
    {
        // ★AddComponent된 PS는 즉시 재생 상태 — main.duration을 재생 중 바꾸면 예외("Setting duration while playing").
        //   완전 정지+클리어 후 설정한다(이후 풀이 Play로 발동).
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 6.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
        main.startColor = new Color(0.62f, 0.58f, 0.52f, 0.85f);   // 중립 먼지 회백(라이팅 안 받는 Unlit이라 직접 톤)
        main.gravityModifier = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;   // 임팩트 후 몬스터 안 따라오게
        main.maxParticles = 64;
        main.stopAction = ParticleSystemStopAction.None;   // 풀이 수명 관리(자기파괴 금지)

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 28) });   // 한 방 폭발

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;   // 바닥에서 위로 반구
        shape.radius = 0.6f;

        var vol = ps.velocityOverLifetime;
        vol.enabled = false;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        var sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.35f), new Keyframe(0.25f, 1f), new Keyframe(1f, 1.1f));
        sol.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);   // 작게 시작→부풀어 흩어짐

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.12f),
                    new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-90f, 90f);

        // 렌더러 — 검증된 가산 머티리얼 복제(URP 파티클 흰폴백 함정 회피).
        var pr = ps.GetComponent<ParticleSystemRenderer>();
        pr.renderMode = ParticleSystemRenderMode.Billboard;
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        pr.receiveShadows = false;
        var dustMat = new Material(dustBaseMat);   // 복제 = 텍스처·블렌드 보존(흰폴백 회피)
        // 먼지는 부드럽게 — 가산 그대로면 너무 밝으니 색으로 누름(중립 회백, 알파블렌드로 바꿔 흙먼지답게).
        dustMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        dustMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        dustMat.SetFloat("_ZWrite", 0f);
        dustMat.renderQueue = VfxLayers.TelegraphFloor;
        pr.sharedMaterial = dustMat;
    }

    void Deactivate()
    {
        _active = false;
        if (_shockMr != null) _shockMr.enabled = false;
        if (_scorchMr != null) _scorchMr.enabled = false;
    }

    /// <summary>풀이 강제 회수 시 호출 — 즉시 비활성(렌더러 끄고 _active=false). Acquire 직후 Play가 재세팅한다.</summary>
    public void ForceDeactivate()
    {
        Deactivate();
        if (_dust != null) _dust.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    /// <summary>임팩트를 origin에서 발동. radius=충격파/그을림 월드 반경(m, 텔레그래프 반경과 일치),
    /// shockColor=충격파 HDR 색, intensity=가산 밝기, shockDuration/scorchDuration=각 수명.</summary>
    public void Play(Vector3 origin, float radius, Color shockColor, float intensity,
                     float coreFlash, float ringWidth, float shockDuration, float scorchDuration,
                     float dustSizeScale, Color scorchColor)
    {
        _gen++;
        origin.y = 0f;
        _radius = Mathf.Max(0.2f, radius);
        _shockDuration = Mathf.Max(0.05f, shockDuration);
        _scorchDuration = Mathf.Max(0.05f, scorchDuration);
        _elapsed = 0f;
        _active = true;

        float diameter = _radius * 2f;

        // ── 충격파 메시 정렬(바닥에 평평하게, 쿼드 평면을 눕힘) ── ★셰이더 부재 시 _shockMat/_shockQuad null → 가드.
        if (_shockMat != null && _shockQuad != null)
        {
            _shockQuad.SetPositionAndRotation(origin + Vector3.up * 0.06f, Quaternion.Euler(90f, 0f, 0f));
            _shockQuad.localScale = new Vector3(diameter, diameter, 1f);
            _shockMat.SetVector(SizeWorldId, new Vector4(diameter, diameter, 0f, 0f));
            _shockMat.SetColor(ColorId, shockColor);
            _shockMat.SetFloat(IntensityId, intensity);
            _shockMat.SetFloat(CoreFlashId, coreFlash);
            _shockMat.SetFloat(RingWidthId, ringWidth);
            _shockMat.SetFloat(SeedId, Random.value * 53f);
            _shockMat.SetFloat(ProgressId, 0f);
            _shockMr.enabled = true;
        }

        // ── 그을림 쿼드(바닥 잔흔) ──
        float scorchDiam = _radius * 1.6f;   // ★유저 '바닥 깨진것 더 크게'(2026-06-14) → 1.3→1.6, 균열 자국이 AoE를 더 채움
        _scorchQuad.SetPositionAndRotation(origin + Vector3.up * 0.04f, Quaternion.Euler(90f, 0f, 0f));
        _scorchQuad.localScale = new Vector3(scorchDiam, scorchDiam, 1f);
        SetMatColor(_scorchMat, scorchColor);
        _scorchMr.enabled = true;

        // ── 먼지 폭발 ──
        if (_dust != null)
        {
            _dust.transform.position = origin;
            var main = _dust.main;
            main.startSizeMultiplier = dustSizeScale;
            var shape = _dust.shape;
            shape.radius = Mathf.Max(0.2f, _radius * 0.18f);   // 반경에 비례한 폭발 폭
            _dust.Clear(true);
            _dust.Play(true);
        }

        // 전체 수명 = max(충격파, 그을림, 먼지 maxLifetime+duration) + 여유.
        float dustLife = _dust != null ? (_dust.main.duration + _dust.main.startLifetime.constantMax) : 0f;
        _life = Mathf.Max(Mathf.Max(_shockDuration, _scorchDuration), dustLife) + 0.1f;
    }

    // 그을림 머티리얼 색 세팅 — _BaseColor 우선(URP Particles), 없으면 _Color(ColorId) 폴백. 둘 다 없으면 무음(SetColor 무시).
    static void SetMatColor(Material m, Color c)
    {
        if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, c);
        else m.SetColor(ColorId, c);
    }

    void Update()
    {
        if (!_active) return;
        // ★H-1(리뷰): unscaledDeltaTime — 히트스탑(timeScale 0.05) 중에도 충격파/그을림이 거의 정상 진행(쉐이크 useUnscaledTime과 일관).
        //   scaled를 쓰면 정지 중 충격파가 얼었다가 끝나고 갑자기 점프(연타 중첩 시 끊김). unscaled로 매끄럽게.
        _elapsed += Time.unscaledDeltaTime;

        // 충격파 확장(0→1).
        if (_shockMr != null && _shockMr.enabled)
        {
            float p = Mathf.Clamp01(_elapsed / _shockDuration);
            _shockMat.SetFloat(ProgressId, p);
            if (p >= 1f) _shockMr.enabled = false;
        }

        // 그을림 페이드(알파 1→0).
        if (_scorchMr != null && _scorchMr.enabled)
        {
            float sp = Mathf.Clamp01(_elapsed / _scorchDuration);
            float a = 1f - sp;   // 선형 페이드(빠르게 박혔다 천천히 사라짐)
            Color c = _scorchMat.HasProperty(BaseColorId) ? _scorchMat.GetColor(BaseColorId)
                                                          : _scorchMat.GetColor(ColorId);
            c.a = a * a;   // 제곱 페이드(끝에서 부드럽게)
            SetMatColor(_scorchMat, c);
            if (sp >= 1f) _scorchMr.enabled = false;
        }

        if (_elapsed >= _life)
        {
            Deactivate();
            if (_dust != null) _dust.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (_pool != null) _pool.Return(this);
        }
    }

    void OnDestroy()
    {
        if (_shockMat != null) Destroy(_shockMat);
        if (_scorchMat != null) Destroy(_scorchMat);
        if (_dust != null)
        {
            var pr = _dust.GetComponent<ParticleSystemRenderer>();
            if (pr != null && pr.sharedMaterial != null) Destroy(pr.sharedMaterial);
        }
    }
}
