using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// ★Skill01 차징 팬텀 — 차징 윈드업(<see cref="KatanaWeapon.IsChargeWindup"/>) 중 *베는 슬래시 잔상*을 전방으로 슉슉슉 방출한다(텔레그래프).
/// 모든 데이터(팬텀 애니 라이브러리·방출·고스트·슬래시 VFX)는 <see cref="ChargePhantomSet"/> SO가 소유(SkillSet/ComboAttackSet과 동형).
/// 이 컴포넌트는 씬 참조(weapon/ghostShader) + SO 참조만 들고, 매 차징 SO를 읽어 *결정적*으로(랜덤 아님) 잔상을 방출한다.
///
/// 각 팬텀은 phantomSet.phantoms 중 emission 순서(<see cref="ChargePhantomSet.EmissionData.emissionOrder"/>가 있으면 그대로,
/// 없으면 phantomCount만큼 0,1,2,…로 순환)대로 *지정된*(켜진 것만) 베기 플립북을 재생: 슉!(스냅 전진) → 딱!(슬래시 플립북) → 스르륵(페이드).
/// (애니 *선택*은 결정적 — 좌우 위치 산란(scatter)만 랜덤이다.)
///
/// ★슬래시 VFX는 두 갈래(둘 다 토글):
///   - slashVfx.onCharge: 차징 중 팬텀마다 동반(각 PhantomAnim의 slash* 오프셋, 스폰 시점 검 앵커 정합).
///   - slashVfx.onCut: 실제 베기(<see cref="KatanaWeapon.SkillHitSeq"/>=DoSkillHit='칼 벨 때') 증가를 폴링해 slashVfx.delay 후 1발(cut 오프셋).
///   프리팹/수명/속도는 공용.
///
/// 프레임 메시는 캐릭터(Visual) 로컬 공간에 구워져 있어, 스폰 시 플레이어 위치 + ChargeAimDir 방향 + Visual 스케일로 배치.
/// Player/Visual(SkinnedMeshRenderer 루트이자 Animator)에 붙인다. GC≈0 — 고스트 풀·프리워밍·공유 프레임 메시.
/// ⚠️ phantoms 프레임/오프셋은 *공유 SO 에셋* — 런타임에 SO를 건드리지 않고, 프레임은 정제 사본, 슬래시 오프셋은 스폰 시 라이브 재조회(튜닝 반영).
/// ※데미지는 별도 비트(몬스터 필요 + 전투 게이트) — KatanaWeapon.DoSkillHit이 소유. 이 컴포넌트는 순수 비주얼.
/// </summary>
public class ChargePhantomEmitter : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("차징 상태 소스. 비우면 부모/루트에서 KatanaWeapon 탐색.")]
    [SerializeField] KatanaWeapon weapon;
    [Tooltip("고스트 셰이더(비우면 ZombieCrush/AfterimageGhost 자동 탐색).")]
    [SerializeField] Shader ghostShader;
    [Tooltip("★팬텀 데이터 SO(ChargePhantomSet) — 라이브러리·방출·고스트·슬래시 VFX 전부. 비우면 비활성. Assets/_Project/VFX/…PhantomSet.asset.")]
    [SerializeField] ChargePhantomSet phantomSet;

    Material _ghostMatTemplate;
    static readonly int AlphaID = Shader.PropertyToID("_Alpha");
    static readonly int ColorID = Shader.PropertyToID("_BaseColor");

    readonly List<Phantom> _pool = new List<Phantom>();
    readonly List<LiveAnim> _anims = new List<LiveAnim>();   // ★켜진 팬텀들의 정제 프레임 + 원본 인덱스(SO 비변형)
    int[] _liveOfOriginal = System.Array.Empty<int>();        // 원본 phantoms 인덱스 → _anims 인덱스(-1=꺼짐/프레임없어 드롭)
    readonly List<int> _plan = new List<int>();               // 이번 차징 방출 순서(=_anims 인덱스 나열)
    int _emitSeq;            // 이번 차징 누적 방출 수(plan을 순환 인덱싱)
    float _spawnTimer;
    float _spawnInterval = 0.27f;   // = windupDuration / plan수 (윈드업 시작마다 산출)
    Vector3 _chargeOrigin;          // ★윈드업 시작 시 1회 잠근 출발점 — 모든 팬텀이 같은 위치에서 출발/소멸
    bool _wasWindup;

    // ── 실제 베기(DoSkillHit) 슬래시 VFX(onCut) 발동 상태 ──
    int _lastHitSeq;        // 마지막으로 본 weapon.SkillHitSeq — 증가하면 '벴다'
    bool _slashPending;     // delay 대기 중
    float _slashTimer;      // delay 카운트다운
    Vector3 _slashDir;      // 벨 때 잠근 전방(앵커 없을 때 폴백 방향)

    /// <summary>한 팬텀 애니의 런타임 사본 — 정제 프레임 + 원본 인덱스(슬래시 오프셋 라이브 재조회용). 공유 SO 비변형.</summary>
    class LiveAnim
    {
        public Mesh[] frames;
        public int origIndex;
    }

    class Phantom
    {
        public GameObject go;
        public MeshFilter mf;
        public Material mat;
        public float age;
        public bool active;
        public Vector3 pos0;     // 스폰 위치(플레이어)
        public Vector3 dir;      // 전진 방향(스폰 시 ChargeAimDir 잠금)
        public Vector3 lateral;  // 좌우 산란 오프셋(스폰 고정)
        public float range;      // 이 팬텀의 전진 상한(스폰 시 캡처)
        public Mesh[] frames;    // ★이 팬텀이 재생 중인 플립북(스폰 시 plan에서 선택)
        public int origIndex;    // 원본 phantoms 인덱스(per-phantom 슬래시 오프셋 조회)
        public bool slashFired;  // ★차징 슬래시 VFX 1회 발동됨(모션의 슬래시 순간)
    }

    void Awake()
    {
        if (weapon == null) weapon = GetComponentInParent<KatanaWeapon>();
        if (weapon == null && transform.root != null) weapon = transform.root.GetComponentInChildren<KatanaWeapon>(true);
        if (weapon == null) { Debug.LogError("[ChargePhantomEmitter] KatanaWeapon 미연결 — 비활성. Inspector/계층 확인.", this); enabled = false; return; }

        if (phantomSet == null)
        {
            Debug.LogWarning("[ChargePhantomEmitter] phantomSet(ChargePhantomSet SO) 미할당 — 비활성. Assets/_Project/VFX/…PhantomSet.asset을 연결하라.", this);
            enabled = false; return;
        }

        BuildLiveAnims();   // ★SO 비변형 — 정제 프레임 사본 + 인덱스 맵
        if (_anims.Count == 0)
        {
            Debug.LogWarning("[ChargePhantomEmitter] phantomSet.phantoms에 켜진(enabled) 유효 프레임이 없음 — 비활성. 팬텀 애니/Enabled 확인.", this);
            enabled = false; return;
        }

        _ghostMatTemplate = CreateGhostMaterial();
        if (_ghostMatTemplate == null) { enabled = false; return; }

        // 풀 프리워밍 — 방출 간격 추정으로 산정(plan은 윈드업 시작마다 확정, 풀은 부족 시 동적 증가).
        var em = phantomSet.emission;
        int provCount = (em.emissionOrder != null && em.emissionOrder.Length > 0) ? em.emissionOrder.Length : Mathf.Max(1, em.phantomCount);
        float provInterval = Mathf.Max(0.01f, em.windupDuration / provCount);
        int warm = Mathf.CeilToInt(Mathf.Max(0.0001f, em.lifetime) / provInterval) + 1;
        for (int i = 0; i < warm; i++) CreatePhantom();
    }

    void OnEnable()
    {
        // ★베기 카운터 재동기화(첫 활성·재활성 공통) — 비활성 중 발생한 베기를 새 것으로 오인해 헛 슬래시 방지(Stab L-1 / Codex). pending도 클리어.
        if (weapon != null) _lastHitSeq = weapon.SkillHitSeq;
        _slashPending = false;
    }

    /// <summary>켜진 phantoms를 null 프레임 제거해 _anims로 복사(SO 비변형) + 원본→로컬 인덱스 맵 구축.</summary>
    void BuildLiveAnims()
    {
        _anims.Clear();
        var src = phantomSet.phantoms;
        int n = src != null ? src.Length : 0;
        _liveOfOriginal = n > 0 ? new int[n] : System.Array.Empty<int>();
        for (int i = 0; i < n; i++)
        {
            _liveOfOriginal[i] = -1;
            var pa = src[i];
            if (pa == null || !pa.enabled || pa.frames == null) continue;   // ★enabled 끄면 방출 제외(boolean 선택)
            var lf = new List<Mesh>(pa.frames.Length);
            foreach (var m in pa.frames) if (m != null) lf.Add(m);
            if (lf.Count == 0) continue;
            _liveOfOriginal[i] = _anims.Count;
            _anims.Add(new LiveAnim { frames = lf.ToArray(), origIndex = i });
        }
    }

    /// <summary>이번 차징의 방출 순서 확정 — emissionOrder가 있으면 그 인덱스대로(드롭/범위밖은 순환 폴백), 없으면 phantomCount만큼 0,1,2,… 순환.</summary>
    void BuildPlan()
    {
        _plan.Clear();
        if (_anims.Count == 0) return;   // 계약 방어 — 아래 나머지 연산 DivideByZero 차단(Stab M-1)
        var em = phantomSet.emission;
        var order = em.emissionOrder;
        if (order != null && order.Length > 0)
        {
            for (int k = 0; k < order.Length; k++)
            {
                int oi = order[k];
                int li = (oi >= 0 && oi < _liveOfOriginal.Length) ? _liveOfOriginal[oi] : -1;
                if (li < 0) li = _plan.Count % _anims.Count;   // 드롭/범위밖 → 순환 폴백
                _plan.Add(li);
            }
        }
        else
        {
            int count = Mathf.Max(1, em.phantomCount);
            for (int i = 0; i < count; i++) _plan.Add(i % _anims.Count);
        }
    }

    void OnDisable()
    {
        foreach (var p in _pool) { p.active = false; if (p.go != null) p.go.SetActive(false); }
        _spawnTimer = 0f;
        _wasWindup = false;
        _slashPending = false;
    }

    void OnDestroy()
    {
        if (_ghostMatTemplate != null) Destroy(_ghostMatTemplate);
        foreach (var p in _pool)
        {
            if (p.mat != null) Destroy(p.mat);
            if (p.go != null) Destroy(p.go);
        }
        _pool.Clear();
        // ★phantoms 프레임 메시는 공유 에셋(.asset) — 파괴 금지(_anims는 참조만 들고 있다).
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (phantomSet == null) return;   // ★라이브 null 가드 — Play 중 SO 필드 비워도 NRE 방지(Codex)
        var em = phantomSet.emission;
        float life = Mathf.Max(0.0001f, em.lifetime);
        float slashF = Mathf.Clamp(em.slashFraction, 0.05f, 1f);
        float fadeStart = phantomSet.ghost.fadeStart;
        float startAlpha = phantomSet.ghost.startAlpha;

        // 1) 살아있는 팬텀: (제 플립북) 재생 + 전진(이즈아웃, 사거리 상한) + 페이드
        for (int i = 0; i < _pool.Count; i++)
        {
            var p = _pool[i];
            if (!p.active) continue;
            p.age += dt;
            float t = p.age / life;
            if (t >= 1f)
            {
                // ★만료 직전 마지막 1회 — slashFrame 도달 전 대형 dt로 fi 구간을 건너뛰어도 차징 슬래시 누락 방지(Stab/Codex).
                var csvEnd = phantomSet.slashVfx;
                if (csvEnd.onCharge && csvEnd.prefab != null && !p.slashFired)
                {
                    p.slashFired = true;
                    SpawnChargeSlashVfxAt(p.go != null ? p.go.transform.position : p.pos0, p.origIndex, p.dir);
                }
                p.active = false; if (p.go != null) p.go.SetActive(false); continue;
            }

            var frames = p.frames;
            if (frames == null || frames.Length == 0) { p.active = false; if (p.go != null) p.go.SetActive(false); continue; }
            int frameCount = frames.Length;
            float u = Mathf.Clamp01(t / slashF);            // 슬래시·전진 진행(0~1, slashFraction 도달 후 1 유지)
            int fi = Mathf.Clamp(Mathf.FloorToInt(u * frameCount), 0, frameCount - 1);
            p.mf.sharedMesh = frames[fi];                    // 딱! — 슬래시 플립북

            float ease = 1f - (1f - u) * (1f - u);           // 슉! — 이즈아웃(빠르게 나갔다 사거리서 감속)
            if (p.go != null) p.go.transform.position = p.pos0 + p.dir * (p.range * ease) + p.lateral;

            float a = (t < fadeStart)                        // 스르륵 — 풀 유지 후 끝에서 페이드
                ? startAlpha
                : Mathf.Lerp(startAlpha, 0f, (t - fadeStart) / Mathf.Max(0.0001f, 1f - fadeStart));
            p.mat.SetFloat(AlphaID, a);

            // ★차징 슬래시 VFX — 각 공격 플립북의 *슬래시 프레임*(per-phantom slashFrame) 도달 시 1회, 팬텀 현재 위치서(프레임 정확 — 콤보 AnimationEvent의 플립북판).
            var csv = phantomSet.slashVfx;
            if (csv.onCharge && csv.prefab != null && !p.slashFired)
            {
                var sphs = phantomSet.phantoms;
                int sf = (sphs != null && p.origIndex >= 0 && p.origIndex < sphs.Length && sphs[p.origIndex] != null)
                    ? Mathf.Clamp(sphs[p.origIndex].slashFrame, 0, frameCount - 1) : 0;
                if (fi >= sf)
                {
                    p.slashFired = true;
                    SpawnChargeSlashVfxAt(p.go != null ? p.go.transform.position : p.pos0, p.origIndex, p.dir);
                }
            }
        }

        // 2) ★실제 베기(DoSkillHit) 감지 → (delay 후) 슬래시 VFX 1발(onCut). 윈드업과 무관 — 항상 검사.
        UpdateCutSlash(dt);

        // 3) ★윈드업(Skill01Charge)일 때만 잔상 방출 — 홀드/베기엔 X(애니에 결속). ChargeAimDir도 이때 유효.
        if (weapon == null || !weapon.IsChargeWindup) { _spawnTimer = 0f; _wasWindup = false; return; }
        if (!_wasWindup)
        {
            _chargeOrigin = transform.position;   // ★출발점 1회 잠금(윈드업 시작) — 모든 팬텀 같은 위치
            BuildPlan();                          // ★개수·순서 노브 → 방출 계획(차징마다 라이브 반영)
            _spawnInterval = Mathf.Max(0.01f, em.windupDuration / Mathf.Max(1, _plan.Count));
            _emitSeq = 0;
            _wasWindup = true;
        }
        _spawnTimer -= dt;
        if (_spawnTimer > 0f) return;
        _spawnTimer = _spawnInterval;
        SpawnPhantom();
    }

    /// <summary>★실제 베기(weapon.SkillHitSeq 증가) 감지 → onCut이면 slashVfx.delay 후 슬래시 VFX 1발. 윈드업과 독립. _lastHitSeq는 항상 동기화.</summary>
    void UpdateCutSlash(float dt)
    {
        if (weapon == null) return;
        var sv = phantomSet.slashVfx;
        int seq = weapon.SkillHitSeq;
        if (seq != _lastHitSeq)   // ★'벴다' — 이번 프레임 감지
        {
            _lastHitSeq = seq;
            if (sv.onCut && sv.prefab != null)
            {
                _slashDir = SlashForward();
                float d = Mathf.Max(0f, sv.delay);
                if (d <= 0f) { _slashPending = false; FireCutSlashVfx(_slashDir); }   // delay 0 = 정확히 벨 때(같은 프레임). 연속 베기 시 최신 슬래시로 replace(의도).
                else { _slashPending = true; _slashTimer = d; }
            }
        }
        else if (_slashPending)   // ★감지 프레임엔 카운트 안 함 — delay>0가 1프레임 일찍 발동하던 것 정밀화(Codex)
        {
            _slashTimer -= dt;
            if (_slashTimer <= 0f)
            {
                _slashPending = false;
                if (sv.onCut && sv.prefab != null) FireCutSlashVfx(_slashDir);   // ★발동 직전 토글 재확인 — 런타임 onCut off 반영(Stab M-1/Codex)
            }
        }
    }

    Vector3 SlashForward()
    {
        Vector3 fwd = weapon.ChargeAimDir; fwd.y = 0f;
        if (fwd.sqrMagnitude < 1e-4f) fwd = transform.forward;
        return fwd.normalized;
    }

    void SpawnPhantom()
    {
        if (_plan.Count == 0 || _anims.Count == 0) return;
        Vector3 fwd = weapon.ChargeAimDir; fwd.y = 0f;
        if (fwd.sqrMagnitude < 1e-4f) fwd = transform.forward;
        fwd.Normalize();

        var a = _anims[_plan[_emitSeq]];   // ★지정 순서대로(랜덤 아님)
        _emitSeq = (_emitSeq + 1) % _plan.Count;   // 항상 [0, plan수) — int 오버플로 음수모듈로 방지(Codex LOW)
        if (a.frames == null || a.frames.Length == 0) return;

        var em = phantomSet.emission;
        var p = GetPhantom();
        p.age = 0f;
        p.active = true;
        p.pos0 = _chargeOrigin;               // ★윈드업 시작 시 잠근 출발점 — 모든 팬텀 동일 위치 출발/소멸
        p.dir = fwd;
        p.range = em.travelRange;
        p.lateral = (em.scatter > 0f)
            ? Vector3.Cross(Vector3.up, fwd) * Random.Range(-em.scatter, em.scatter)
            : Vector3.zero;
        p.frames = a.frames;
        p.origIndex = a.origIndex;            // per-phantom 슬래시 오프셋 조회용
        p.slashFired = false;                 // ★차징 슬래시는 모션 중(slashFrame) 1회 — 스폰선 안 쏨

        p.mf.sharedMesh = p.frames[0];        // 첫 프레임
        if (p.go != null)
        {
            p.go.transform.SetPositionAndRotation(p.pos0, Quaternion.LookRotation(fwd, Vector3.up));
            p.go.transform.localScale = transform.lossyScale;   // Visual 스케일 정합
            p.go.SetActive(true);
        }
        p.mat.SetColor(ColorID, phantomSet.ghost.color);
        p.mat.SetFloat(AlphaID, phantomSet.ghost.startAlpha);   // 스냅 — 확 나옴(페이드인 없음)
        // ★차징 슬래시 VFX는 스폰서 안 쏜다 — LateUpdate 루프가 모션의 슬래시 프레임(slashFrame)에 발동.
    }

    /// <summary>★차징 슬래시 VFX — 팬텀의 *현재 위치*(슬래시 순간)에 per-phantom 오프셋으로 스폰. 검 앵커 안 씀(부감 정합·순서 무관 일관). SO 라이브 재조회.</summary>
    void SpawnChargeSlashVfxAt(Vector3 atPos, int origIndex, Vector3 fwd)
    {
        var sv = phantomSet.slashVfx;
        if (sv.prefab == null) return;
        var phs = phantomSet.phantoms;
        if (phs == null || origIndex < 0 || origIndex >= phs.Length) return;
        var pa = phs[origIndex];   // ★라이브 — Play 중 오프셋 튜닝 즉시 반영
        if (pa == null) return;
        Quaternion baseRot = Quaternion.LookRotation(fwd, Vector3.up);
        Quaternion rot = baseRot * Quaternion.Euler(pa.slashEulerOffset);
        Vector3 pos = atPos + baseRot * pa.slashPosOffset;   // ★팬텀 현재 위치 기준(모션 따라감)
        SpawnSlashGo(pos, rot, null, pa.slashScale);   // anchor=null — parentToWeapon 비적용
    }

    /// <summary>★실제 베기 순간(+delay)의 슬래시 VFX 1발 — slashVfx의 cut 오프셋으로 검 앵커 정합 스폰.</summary>
    void FireCutSlashVfx(Vector3 fwd)
    {
        var sv = phantomSet.slashVfx;
        if (sv.prefab == null) return;
        var anchor = weapon != null ? weapon.WeaponAnchor : null;
        Vector3 pos; Quaternion rot;
        if (anchor != null)
        {
            pos = anchor.TransformPoint(sv.posOffset);
            rot = anchor.rotation * Quaternion.Euler(sv.eulerOffset);
        }
        else   // 폴백: 검 앵커 미연결 시 플레이어 위치 + 전방
        {
            rot = Quaternion.LookRotation(fwd, Vector3.up) * Quaternion.Euler(sv.eulerOffset);
            pos = transform.position + rot * sv.posOffset;
        }
        SpawnSlashGo(pos, rot, anchor, sv.scale);
    }

    /// <summary>슬래시 VFX 프리팹 1개 인스턴스화 공통 경로 — 차징/베기 공유. 콤보 슬래시(PlayerAttackVfx)와 동일 규약
    /// (임베디드 사운드 차단, PlayOnAwake=false 프리팹도 강제 Play, 스케일/속도 0폴백, lifetime 후 소멸).</summary>
    void SpawnSlashGo(Vector3 pos, Quaternion rot, Transform anchor, float scale)
    {
        var sv = phantomSet.slashVfx;
        var go = Instantiate(sv.prefab, pos, rot);
        if (sv.parentToWeapon && anchor != null) go.transform.SetParent(anchor, true);
        PlayerAttackVfx.StripEmbeddedAudio(go);
        float s = scale > 0f ? scale : 1f;
        if (!Mathf.Approximately(s, 1f)) go.transform.localScale *= s;
        float spd = sv.playbackSpeed > 0f ? sv.playbackSpeed : 1f;
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>())
        {
            if (!Mathf.Approximately(spd, 1f)) { var main = ps.main; main.simulationSpeed *= spd; }
            ps.Play(false);
        }
        Destroy(go, sv.lifetime > 0f ? sv.lifetime : 0.5f);
    }

    Phantom GetPhantom()
    {
        for (int i = 0; i < _pool.Count; i++)
            if (!_pool[i].active) return _pool[i];
        return CreatePhantom();
    }

    Phantom CreatePhantom()
    {
        var go = new GameObject("ChargePhantom") { hideFlags = HideFlags.DontSave };
        go.transform.SetParent(null, false);
        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = LightProbeUsage.Off;
        mr.reflectionProbeUsage = ReflectionProbeUsage.Off;

        var mat = new Material(_ghostMatTemplate);
        mr.sharedMaterial = mat;

        var p = new Phantom { go = go, mf = mf, mat = mat, active = false };
        go.SetActive(false);
        _pool.Add(p);
        return p;
    }

    Material CreateGhostMaterial()
    {
        var sh = ghostShader != null ? ghostShader : Shader.Find("ZombieCrush/AfterimageGhost");
        if (sh == null)
        {
            Debug.LogWarning("[ChargePhantomEmitter] ZombieCrush/AfterimageGhost 셰이더 미발견 — 비활성.");
            return null;
        }
        var m = new Material(sh);
        m.SetColor(ColorID, phantomSet.ghost.color);
        m.SetFloat(AlphaID, phantomSet.ghost.startAlpha);
        return m;
    }
}
