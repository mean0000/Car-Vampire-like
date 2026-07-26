using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// ★대시 잔상(afterimage) — 하이브리드: 기존 성능 골격(풀링·버퍼 재사용·GC≈0·속도선) +
/// 시안 프레넬 단색 비주얼(ZombieCrush/AfterimageGhost). 스킨드 메시 루트(CharacterVisual)에 붙인다.
///
/// 원리: 플레이어는 여러 SkinnedMeshRenderer로 구성된다. 매 interval마다 각 SMR의 현재 포즈를
/// BakeMesh로 굽고(재사용 버퍼), CombineMeshes로 한 메시로 합쳐 그 자리에 고스트 1장을 띄운다.
/// 몸은 대시로 빠져나가고 고스트는 월드 고정으로 남아 페이드 → 촘촘한 "스슥" 잔상.
///
/// ★성능(기존 보존): BakeMesh 버퍼·CombineInstance 배열을 영속 재사용하고 고스트를 풀링한다.
///   프리워밍으로 첫 대시 생성 스파이크를 시작 1회로 옮긴다. 스트릭은 ParticleSystem 내부 풀 +
///   EmitParams 재사용으로 할당 0. 정상상태 힙 할당 ≈ 0.
/// ★비주얼(신규): 마젠타 위상분리 폐기 → 시안 단색 프레넬 림 고스트(실루엣 살리고 바디 옅게, 가산).
///   색 팔레트 규약 시안=액션. 알파는 머티리얼 _Alpha로 페이드.
///
/// PlayerMotor.IsDashing을 폴링해 대시 중에만 스폰 — 트리거/타이밍은 모터가 소유. 진행방향은
/// 위치 델타로 추정(스트릭 방향용).
/// </summary>
public class PlayerAfterimage : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("대시 상태 소스. 비우면 부모/씬에서 탐색.")]
    [SerializeField] PlayerMotor motor;
    [Tooltip("고스트 셰이더(비우면 ZombieCrush/AfterimageGhost 자동 탐색).")]
    [SerializeField] Shader ghostShader;

    [Header("Spawn")]
    [Tooltip("잔상 1장 간격(초). 작을수록 촘촘. 대시 0.10s에 0.012면 ~8장 + lifetime 동안 잔존.")]
    [SerializeField] float interval = 0.012f;

    [Header("Ghost")]
    [Tooltip("잔상 1장 페이드 시간(초).")]
    [SerializeField] float lifetime = 0.28f;

    [Header("★시작 실루엣 (R11 — 붉은사막식: 대시 트레일 대신 출발점에 실루엣만 잠깐)")]
    [Tooltip("대시 동안 연속 트레일 스폰(구 방식). OFF=시작 실루엣만 — 유저 지시(07-11 R11, 붉은사막 레퍼).")]
    [SerializeField] bool trailDuringDash = false;
    [Tooltip("시작 실루엣 장수(0=끔, 2=살짝 이중상).")]
    [SerializeField, Range(0, 3)] int startSilhouetteCount = 1;
    [Tooltip("시작 실루엣 페이드 시간(초) — 트레일보다 길게 남아 '방금 여기 있었다'를 판다.")]
    [SerializeField] float startSilhouetteLifetime = 0.45f;
    [Tooltip("고스트 색(시안=액션 규약). HDR — 다크월드 블룸 통과.")]
    [ColorUsage(true, true)]
    [SerializeField] Color ghostColor = new Color(0.3f, 1.4f, 1.7f, 1f);
    [Tooltip("고스트 시작 알파(알파블렌드 솔리드 필 — 낮추면 겹침이 비쳐 부드럽고, 높이면 진한 시안 덩어리).")]
    [SerializeField, Range(0f, 1f)] float startAlpha = 0.7f;

    [Header("Streaks (속도선)")]
    [Tooltip("대시 방향 속도선 켜기.")]
    [SerializeField] bool enableStreaks = true;

    // ─── ★대시 연출 스타일 A/B (07-11 R8 — 유저 "맛대가리 없음" 판정. 킥오프/착지 비트, J키 순환) ───
    public enum DashFxStyle { Off, Glitch, Physical }
    [Header("★대시 연출 스타일 A/B (07-11 R8 — 킥오프·착지 비트. 플레이 중 J키 순환)")]
    [Tooltip("Glitch=위상 스텝(마젠타 붕괴 팝→시안 재조립 — RUINER 북극성·신호붕괴 캐넌) / Physical=먼지·무게(데스도어) / Off=이동 잔상만(기존). ★R14(07-13 유저): 글리치 끔 — 시작 실루엣(스타일 무관)만 남김.")]
    [SerializeField] DashFxStyle dashStyle = DashFxStyle.Off;
    [Tooltip("스타일 순환 키(비교 하니스 — 판정 후 제거).")]
    [SerializeField] KeyCode cycleStyleKey = KeyCode.J;
    [Tooltip("대시 시작 카메라 킥(m) — ★R9: 대시 *반대* 방향으로 당긴다(캐릭터가 프레임 앞으로 튀어나감 = 속도감. " +
             "진행 방향으로 밀면 캐릭터가 화면 중앙 고정돼 속도감이 죽는다). 0=끔. 양 스타일 공통.")]
    [SerializeField] float dashCamKick = 0.22f;
    [Tooltip("★대시 순간 FOV 서지 펀치(도, R9) — 짧은 수축→복귀로 '확!' 가속감. 0=끔. 기존 줌펀치 채널 재사용(밀도 억제 포함).")]
    [SerializeField, Min(0f)] float dashFovPunch = 1.5f;

    [Header("Glitch 노브 (위상 스텝)")]
    [Tooltip("킥오프 팝 색(마젠타=신호붕괴 캐넌). HDR.")]
    [ColorUsage(true, true)] [SerializeField] Color glitchPopColor = new Color(1.8f, 0.25f, 1.6f, 0.9f);
    [Tooltip("착지 재조립 색(시안=액션 캐넌). HDR.")]
    [ColorUsage(true, true)] [SerializeField] Color reassembleColor = new Color(0.3f, 1.4f, 1.7f, 0.9f);
    [Tooltip("킥오프/착지 샤드 수 — 많을수록 진한 붕괴/재조립.")]
    [SerializeField, Range(4, 40)] int glitchShardCount = 16;
    [Tooltip("i-frame 동안 옅은 고스트 유지(무적 꼬리 '위상 상태' 가독 — Glitch 전용). ★R11 기본 OFF — 시작 실루엣 문법과 충돌(잔상 최소화 지시).")]
    [SerializeField] bool iframeGhosting = false;
    [Tooltip("★킥오프 실루엣 RGB 분리(m) — 본체 실루엣 양옆에 마젠타/시안 고스트 1장씩 수직축 오프셋(색수차 = 신호가 찢어진 출발점). 0=끔.")]
    [SerializeField, Range(0f, 0.5f)] float chromaSplit = 0.14f;
    [Tooltip("RGB 분리 고스트 수명(초) — 본체 실루엣(0.45s)보다 짧게 죽어 '신호가 수렴'하는 읽기.")]
    [SerializeField] float chromaLifetime = 0.18f;

    [Header("Physical 노브 (먼지·무게)")]
    [Tooltip("킥오프/착지 먼지 입자 수.")]
    [SerializeField, Range(4, 30)] int dustCount = 12;
    [Tooltip("착지 스쿼시 배율(Y) — 1=끔. 낮을수록 쿵 눌림(무게). 착지 프레임에 눌렀다 landSquashTime에 복원.")]
    [SerializeField, Range(0.7f, 1f)] float landSquash = 0.92f;
    [Tooltip("스쿼시 복원 시간(초).")]
    [SerializeField] float landSquashTime = 0.12f;
    [Tooltip("★킥오프 스트레치(진행축 Z 배율) — 착지 스쿼시의 대칭(차고 나감). 1=끔. dashFaceMotion ON이라 몸=대시 방향=로컬 Z.")]
    [SerializeField, Range(1f, 1.3f)] float kickoffStretch = 1.1f;
    [Tooltip("스트레치 복원 시간(초).")]
    [SerializeField] float kickoffStretchTime = 0.1f;
    [Tooltip("★출구 슬라이드 드래그 먼지 — 착지 후 속도 비례 발끝 먼지 방출률(개/초, 풀속도 기준). '미끄러지며 정착'을 판다. 0=끔.")]
    [SerializeField, Range(0f, 60f)] float slideDustRate = 30f;
    [Tooltip("드래그 먼지 하한 속도(m/s) — 이 밑이면 정착으로 보고 중단.")]
    [SerializeField] float slideDustMinSpeed = 1.5f;

    [Header("★대시 사운드 (임시 2D 플레이스홀더 — 음색은 유저 귀 판정. 비우면 무음)")]
    [Tooltip("킥오프 whoosh — 캐넌 첫 볼륨 0.03~0.15.")]
    [SerializeField] AudioClip whooshClip;
    [SerializeField, Range(0f, 1f)] float whooshVolume = 0.12f;
    [Tooltip("착지 스커프(발 끌림) — 물리 착지의 절반은 소리다.")]
    [SerializeField] AudioClip scuffClip;
    [SerializeField, Range(0f, 1f)] float scuffVolume = 0.10f;

    const float StreakRate = 40f;     // 대시 중 초당 방출 수
    static readonly Color StreakColor = new Color(0.7f, 0.95f, 1f, 0.8f);

    // --- 재사용 버퍼(영속) ---
    SkinnedMeshRenderer[] _renderers;
    Mesh[] _bakeBuffers;
    int[] _combineCount;          // SMR별 서브메시 수(고정, sharedMesh 기준)
    CombineInstance[] _combine;   // 활성 SMR만 — 멤버십 변할 때만 재구성(GC-제로 유지)
    bool[] _prevActive;           // 직전 스폰의 SMR별 활성 상태(변화 감지)
    bool _layoutDirty = true;

    Material _ghostMatTemplate;
    static readonly int AlphaID = Shader.PropertyToID("_Alpha");
    static readonly int ColorID = Shader.PropertyToID("_BaseColor");

    readonly List<Ghost> _pool = new List<Ghost>();
    float _spawnTimer;
    float _burstUntil;   // ★외부 요청 잔상 창(스킬 런지 등) — 대시가 아니어도 이 시각까지 스폰 유지(scaled time, 클래스 전체와 정렬)

    // --- 진행방향 추정(스트릭용) ---
    Vector3 _prevPos;
    bool _hasPrevPos;
    Vector3 _moveDir = Vector3.forward;
    Vector3 _lastDelta;   // ★이번 프레임 변위(R11) — 대시 시작 프레임엔 모터가 이미 첫 스텝을 옮긴 뒤라, 실루엣을 이만큼 되돌려 출발점에 정렬

    // --- 스피드 스트릭 ---
    ParticleSystem _streakPS;
    Material _streakMat;
    float _streakTimer;
    ParticleSystem.EmitParams _streakEmit;

    // --- ★대시 비트(R8) ---
    ParticleSystem _shardPS;      // 가산·스트레치 — Glitch 팝/재조립 샤드
    Material _shardMat;
    ParticleSystem _dustPS;       // 알파·빌보드 — Physical 먼지
    Material _dustMat;
    Texture2D _dustTex;           // 절차 소프트 원(내장 리소스 런타임 접근 불가 대체)
    ParticleSystem.EmitParams _beatEmit;   // 재사용(할당 0 정책 유지)
    AudioSource _audio;           // 2D 플레이스홀더(무기 SFX와 동일 정책)
    PlayerCameraFollow _camFollow;
    Vector3 _baseScale;
    bool _prevDashing;            // 착지(폴링 엣지) 감지
    float _slideDustUntil;        // ★슬라이드 드래그 먼지 창(Physical 심화) — 착지 시각+상한, 속도 하한으로 조기 종료
    float _slideDustTimer;

    class Ghost
    {
        public GameObject go;
        public MeshFilter mf;
        public Material mat;
        public Mesh mesh;
        public float age;
        public float life;   // ★개별 수명(R11) — 트레일(lifetime)과 시작 실루엣(startSilhouetteLifetime)이 다른 페이드를 가짐
        public bool active;
    }

    void Awake()
    {
        interval = Mathf.Max(0.01f, interval);
        if (motor == null) motor = GetComponentInParent<PlayerMotor>();   // 부모 체인만 — FindObjectOfType 제거(임의 픽업 방지)
        if (motor == null) { Debug.LogError("[PlayerAfterimage] PlayerMotor 미연결 — 잔상 비활성. Inspector/부모 배선 확인.", this); enabled = false; return; }

        var all = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        var live = new List<SkinnedMeshRenderer>(all.Length);
        foreach (var s in all)
            if (s != null && s.sharedMesh != null) live.Add(s);   // 활성 여부는 스폰 시 판단(런타임 토글 대응, Stab 권고-4)
        _renderers = live.ToArray();

        if (_renderers.Length == 0) { Debug.LogWarning("[PlayerAfterimage] SkinnedMeshRenderer 미발견.", this); }

        _bakeBuffers = new Mesh[_renderers.Length];
        _combineCount = new int[_renderers.Length];
        _prevActive = new bool[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
        {
            _bakeBuffers[i] = new Mesh();
            _combineCount[i] = _renderers[i].sharedMesh.subMeshCount;
        }
        // _combine은 첫 스폰 시 활성 멤버십에 맞춰 구성(_layoutDirty=true).

        _ghostMatTemplate = CreateGhostMaterial();
        if (_ghostMatTemplate == null) { enabled = false; return; }   // 셰이더 전멸 — 안전 무효화

        // 프리워밍: 동시 생존 고스트 수(≈lifetime/interval)만큼 미리 생성.
        int warm = Mathf.CeilToInt(Mathf.Max(0.0001f, lifetime) / interval) + 1;
        for (int i = 0; i < warm; i++) CreateGhost();

        if (enableStreaks)
        {
            _streakMat = CreateStreakMaterial();   // null(셰이더 전멸)이면 PS 미생성 — 사용처는 전부 _streakPS 널 가드
            if (_streakMat != null)
            {
                _streakPS = CreateStreakPS();
                _streakPS.Play();
            }
        }

        // ★대시 비트(R8) — 샤드(가산)/먼지(알파) PS 절차 생성(기존 스트릭과 동일 이디엄), 2D 오디오, 카메라 킥 참조.
        _shardMat = CreateStreakMaterial();   // 가산·버텍스컬러 — 색은 EmitParams가 입힘(스트릭과 재질 규격 공유)
        if (_shardMat != null) _shardPS = CreateBeatPS("DashShardPS", _shardMat, stretch: true);
        _dustMat = CreateDustMaterial();
        if (_dustMat != null) _dustPS = CreateBeatPS("DashDustPS", _dustMat, stretch: false);
        _audio = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake = false; _audio.spatialBlend = 0f;
        _camFollow = FindFirstObjectByType<PlayerCameraFollow>();   // 없어도 무해(널 가드)
        _baseScale = transform.localScale;
    }

    void OnDisable()
    {
        transform.DOKill(complete: true);   // ★진행 중 스쿼시 트윈 완료 처리 — 눌린/늘어난 스케일 잔존 방지(OnDestroy와 대칭)
        foreach (var g in _pool) { g.active = false; if (g.go != null) g.go.SetActive(false); }
        if (_streakPS != null) _streakPS.Clear();
        _spawnTimer = 0f;
        _streakTimer = 0f;
        _burstUntil = 0f;
        _slideDustUntil = 0f;
    }

    /// <summary>★대시 외 순간 전진(스킬 런지 등)의 잔상 창 — duration 동안 대시와 동일 문법(고스트+속도선).
    /// 중첩 요청은 더 긴 쪽 유지. 트리거는 호출자(무기) 소유 — 이 컴포넌트는 대시와 마찬가지로 폴링만.</summary>
    public void EmitBurst(float duration)
    {
        if (duration <= 0f || !isActiveAndEnabled) return;
        _burstUntil = Mathf.Max(_burstUntil, Time.time + duration);
    }

    void OnDestroy()
    {
        transform.DOKill();   // ★스쿼시 트윈 잔존 방지(R8)
        if (_ghostMatTemplate != null) Destroy(_ghostMatTemplate);
        if (_streakMat != null) Destroy(_streakMat);
        if (_shardMat != null) Destroy(_shardMat);
        if (_dustMat != null) Destroy(_dustMat);
        if (_dustTex != null) Destroy(_dustTex);
        if (_bakeBuffers != null)
            foreach (var m in _bakeBuffers) if (m != null) Destroy(m);
        foreach (var g in _pool)
        {
            if (g.mesh != null) Destroy(g.mesh);
            if (g.mat != null) Destroy(g.mat);
            if (g.go != null) Destroy(g.go);
        }
        _pool.Clear();
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        float life = Mathf.Max(0.0001f, lifetime);

        // 1) 살아있는 고스트 페이드(_Alpha만 — 메시 안 건드림). ★R11: 개별 수명(g.life) 기준.
        for (int i = 0; i < _pool.Count; i++)
        {
            var g = _pool[i];
            if (!g.active) continue;
            g.age += dt;
            float t = g.age / Mathf.Max(0.0001f, g.life);
            if (t >= 1f) { g.active = false; if (g.go != null) g.go.SetActive(false); continue; }
            g.mat.SetFloat(AlphaID, startAlpha * (1f - t));
        }

        // 2) 진행방향 추정(스트릭 방향) + 이번 프레임 변위 기록(시작 실루엣 원점 정렬용, R11).
        _lastDelta = Vector3.zero;
        if (motor != null)
        {
            Vector3 pos = motor.transform.position;
            if (_hasPrevPos)
            {
                Vector3 d = pos - _prevPos; d.y = 0f;
                _lastDelta = d;
                if (d.sqrMagnitude > 1e-6f) _moveDir = d.normalized;
            }
            _prevPos = pos;
            _hasPrevPos = true;
        }

        // 2.5) ★대시 비트(R8) — 스타일 순환(비교 하니스) + 킥오프/착지 엣지.
#if UNITY_EDITOR
        if (Input.GetKeyDown(cycleStyleKey))
            dashStyle = (DashFxStyle)(((int)dashStyle + 1) % 3);
#endif
        if (motor != null)
        {
            if (motor.DashStartedThisFrame) OnDashKickoff();
            else if (_prevDashing && !motor.IsDashing) OnDashLanding();
            _prevDashing = motor.IsDashing;
        }

        // 2.7) ★슬라이드 드래그 먼지(Physical 심화) — 출구 슬라이드 동안 속도 비례로 발끝 먼지를 끈다("미끄러지며 정착").
        if (Time.time < _slideDustUntil && motor != null && _dustPS != null)
        {
            Vector3 v = motor.Velocity; v.y = 0f;
            float spd = v.magnitude;
            if (spd < slideDustMinSpeed || motor.IsDashing) _slideDustUntil = 0f;   // 정착 완료 또는 연속 대시 — 창 닫음
            else
            {
                float perSec = slideDustRate * Mathf.Clamp01(spd / 8f);   // 8 = 현행 dashExitSpeed(R9) 기준 풀 강도
                _slideDustTimer -= dt;
                int safety = 6;
                while (_slideDustTimer <= 0f && perSec > 0f && safety-- > 0)
                {
                    _slideDustTimer += 1f / perSec;
                    EmitDragPuff(motor.transform.position, v / spd);
                }
            }
        }

        // 3) 트레일 스폰 — ★R11: 대시는 기본적으로 트레일 없음(시작 실루엣은 OnDashKickoff가 별도 스폰).
        //    trailDuringDash(구 방식 A/B)·버스트 창(스킬 런지)·위상 꼬리(Glitch, 기본 꺼짐)만 연속 스폰.
        bool bursting = Time.time < _burstUntil;
        bool dashTrail = motor != null && motor.IsDashing && trailDuringDash;
        bool phaseGhost = dashStyle == DashFxStyle.Glitch && iframeGhosting
                          && motor != null && motor.IsInvulnerable;
        if (motor == null || (!dashTrail && !bursting && !phaseGhost)) { _spawnTimer = 0f; _streakTimer = 0f; return; }

        if (enableStreaks && _streakPS != null && (dashTrail || bursting)) EmitStreaks(dt);

        _spawnTimer -= dt;
        if (_spawnTimer > 0f) return;
        _spawnTimer = (dashTrail || bursting) ? interval : interval * 3f;   // 위상 꼬리는 성긴 밀도
        SpawnGhost(lifetime);
    }

    // ─── ★대시 비트(R8) — 킥오프/착지. 두 스타일이 같은 훅을 공유하고 내용물만 다르다 ───

    void OnDashKickoff()
    {
        // 공통: 카메라를 대시 *반대*로 당김(캐릭터가 프레임 앞으로 돌출 = 속도감, R9) + FOV 서지 + whoosh.
        if (_camFollow != null)
        {
            if (dashCamKick > 0f) _camFollow.AddKick(-_moveDir, dashCamKick);
            if (dashFovPunch > 0f) _camFollow.AddFovPunch(dashFovPunch);
        }
        PlayBeatSfx(whooshClip, whooshVolume);

        // ★시작 실루엣(R11 — 붉은사막 문법): 출발점에 몸 실루엣이 잠깐 남는다. 스타일 무관(대시 판독의 코어).
        //   이 프레임 모터가 이미 첫 스텝을 옮긴 뒤라(LateUpdate) 고스트 GO를 -이번프레임변위로 되돌려 원점 정렬
        //   (메시는 월드 베이크 — GO 위치가 곧 전체 오프셋).
        for (int i = 0; i < startSilhouetteCount; i++)
        {
            var g = SpawnGhost(startSilhouetteLifetime);
            if (g != null) g.go.transform.position = -_lastDelta;
        }
        Vector3 feet = motor.transform.position;

        if (dashStyle == DashFxStyle.Glitch)
        {
            // ★심화: 출발 실루엣 RGB 분리 — 대시 수직축 양옆 마젠타/시안 고스트(색수차 = 신호가 찢어진 출발점).
            // ★R13(07-12 유저): 킥오프 샤드 팝 제거 — 잔광이 대시 중반까지 남아 "중간의 빛나는 효과"로 지적. 회피 모션 가독 우선.
            if (chromaSplit > 0f)
            {
                Vector3 dashDir = motor.DashDir.sqrMagnitude > 0.001f ? motor.DashDir : _moveDir;
                Vector3 side = Vector3.Cross(Vector3.up, dashDir).normalized;
                SpawnTintedGhost(-_lastDelta + side * chromaSplit, glitchPopColor, chromaLifetime);
                SpawnTintedGhost(-_lastDelta - side * chromaSplit, reassembleColor, chromaLifetime);
            }
        }
        else if (dashStyle == DashFxStyle.Physical)
        {
            EmitDust(feet, -_moveDir);                                             // 뒤로 차는 먼지 킥
            if (kickoffStretch > 1f && kickoffStretchTime > 0f)                    // ★심화: 진행축 스트레치 — 착지 스쿼시의 대칭("차고 나감")
            {
                transform.DOKill();
                transform.localScale = new Vector3(_baseScale.x * 0.97f, _baseScale.y * 0.96f, _baseScale.z * kickoffStretch);
                transform.DOScale(_baseScale, kickoffStretchTime).SetEase(Ease.OutQuad);
            }
        }
    }

    void OnDashLanding()
    {
        Vector3 feet = motor.transform.position;
        if (dashStyle == DashFxStyle.Glitch)
        {
            // ★R13(07-12 유저): 착지 재조립 샤드·포즈 플래시 제거 — "끝부분의 빛나는 효과" 지적. 착지는 애니(Evade 리타임)가 판다.
            PlayBeatSfx(scuffClip, scuffVolume * 0.7f);                            // 재조립은 스커프 약하게(디지털 착지)
        }
        else if (dashStyle == DashFxStyle.Physical)
        {
            EmitDust(feet, _moveDir * 0.7f);                                       // 진행 관성으로 앞으로 쏠리는 착지 먼지(★심화: 0.4→0.7 관성 강조)
            _slideDustUntil = Time.time + 0.6f;                                    // ★심화: 슬라이드 드래그 먼지 창 — 속도 하한이 조기 종료
            PlayBeatSfx(scuffClip, scuffVolume);
            if (landSquash < 1f && landSquashTime > 0f)                            // 쿵 눌림(무게) — DOTween, 스케일만(회전/포즈 불가침)
            {
                transform.DOKill();
                transform.localScale = new Vector3(_baseScale.x * 1.04f, _baseScale.y * landSquash, _baseScale.z * 1.04f);
                transform.DOScale(_baseScale, landSquashTime).SetEase(Ease.OutQuad);
            }
        }
    }

    /// <summary>Glitch 샤드 — outward=붕괴 팝(터져나감), inward=재조립(모여듦). 가산·스트레치 파티클.</summary>
    void EmitShards(Vector3 center, Color color, bool inward)
    {
        if (_shardPS == null) return;
        for (int i = 0; i < glitchShardCount; i++)
        {
            Vector3 dir = Random.onUnitSphere; dir.y *= 0.35f;   // 납작한 방사(쿼터뷰 가독)
            if (dir.sqrMagnitude < 0.001f) dir = Vector3.forward;
            dir.Normalize();
            float speed = Random.Range(4f, 8f);
            Vector3 body = center + Vector3.up * Random.Range(-0.7f, 0.7f);   // ★심화: 몸통 세로 분포 — 한 점이 아니라 '몸이' 찢어지고 재조립되는 읽기
            _beatEmit.position = inward ? body + dir * Random.Range(0.5f, 0.9f) : body + dir * 0.1f;
            _beatEmit.velocity = (inward ? -dir : dir) * speed;
            _beatEmit.startColor = color;
            _beatEmit.startSize = Random.Range(0.05f, 0.12f);
            _beatEmit.startLifetime = Random.Range(0.1f, 0.18f);
            _shardPS.Emit(_beatEmit, 1);
        }
    }

    /// <summary>Physical 먼지 — biasDir 쪽으로 낮게 퍼지는 흙먼지 퍼프(발 높이). 알파·빌보드.</summary>
    void EmitDust(Vector3 feet, Vector3 biasDir)
    {
        if (_dustPS == null) return;
        for (int i = 0; i < dustCount; i++)
        {
            Vector2 c = Random.insideUnitCircle * 0.25f;
            Vector3 dir = new Vector3(c.x, 0f, c.y) + biasDir;
            _beatEmit.position = feet + new Vector3(c.x, 0.06f, c.y);
            _beatEmit.velocity = dir.normalized * Random.Range(0.8f, 1.8f) + Vector3.up * Random.Range(0.3f, 0.9f);
            _beatEmit.startColor = new Color(0.62f, 0.58f, 0.52f, 0.45f);   // 채도 억제 흙빛(월드 팔레트)
            _beatEmit.startSize = Random.Range(0.16f, 0.34f);
            _beatEmit.startLifetime = Random.Range(0.3f, 0.55f);
            _dustPS.Emit(_beatEmit, 1);
        }
    }

    /// <summary>★슬라이드 드래그 퍼프(Physical 심화) — 발끝에서 진행 반대로 끌리는 낮고 옅은 먼지 1개.</summary>
    void EmitDragPuff(Vector3 feet, Vector3 moveDir)
    {
        Vector2 c = Random.insideUnitCircle * 0.12f;
        _beatEmit.position = feet + new Vector3(c.x, 0.05f, c.y);
        _beatEmit.velocity = -moveDir * Random.Range(0.6f, 1.2f) + Vector3.up * Random.Range(0.2f, 0.5f);
        _beatEmit.startColor = new Color(0.62f, 0.58f, 0.52f, 0.35f);   // 착지 퍼프보다 옅게(끌림은 배경)
        _beatEmit.startSize = Random.Range(0.12f, 0.24f);
        _beatEmit.startLifetime = Random.Range(0.25f, 0.4f);
        _dustPS.Emit(_beatEmit, 1);
    }

    /// <summary>★색 오버라이드 고스트(Glitch 심화) — RGB 분리·재조립 플래시용. 페이드는 공용 루프(_Alpha)가 처리, 색은 수명 동안 유지.</summary>
    void SpawnTintedGhost(Vector3 offset, Color color, float life)
    {
        var g = SpawnGhost(life);
        if (g == null) return;
        g.go.transform.position = offset;
        g.mat.SetColor(ColorID, color);
    }

    void PlayBeatSfx(AudioClip clip, float volume)
    {
        if (clip == null || _audio == null || volume <= 0f) return;
        _audio.pitch = 1f + Random.Range(-0.05f, 0.05f);
        _audio.PlayOneShot(clip, volume);
    }

    static bool IsActive(SkinnedMeshRenderer s) => s != null && s.enabled && s.gameObject.activeInHierarchy;

    /// <summary>활성 SMR 멤버십이 바뀌었을 때만 _combine 레이아웃 재구성(GC-제로 유지, Stab 권고-4).</summary>
    void RebuildCombineIfNeeded()
    {
        bool changed = _layoutDirty;
        for (int i = 0; i < _renderers.Length && !changed; i++)
            if (IsActive(_renderers[i]) != _prevActive[i]) changed = true;
        if (!changed) return;

        int total = 0;
        for (int i = 0; i < _renderers.Length; i++)
        {
            _prevActive[i] = IsActive(_renderers[i]);
            if (_prevActive[i]) total += _combineCount[i];
        }
        _combine = new CombineInstance[total];   // 멤버십 변화 시에만 할당(평시 0)
        int w = 0;
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (!_prevActive[i]) continue;
            for (int s = 0; s < _combineCount[i]; s++)
                _combine[w++] = new CombineInstance { mesh = _bakeBuffers[i], subMeshIndex = s };
        }
        _layoutDirty = false;
    }

    Ghost SpawnGhost(float ghostLife)
    {
        if (_renderers.Length == 0) return null;
        RebuildCombineIfNeeded();
        if (_combine == null || _combine.Length == 0) return null;   // 활성 SMR 없음

        // 활성 SMR만 베이크 + 월드 변환 갱신(_combine 레이아웃과 동일 순서).
        int w = 0;
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (!_prevActive[i]) continue;
            var smr = _renderers[i];
            smr.BakeMesh(_bakeBuffers[i]);
            Matrix4x4 toWorld = smr.transform.localToWorldMatrix;
            for (int s = 0; s < _combineCount[i]; s++)
                _combine[w++].transform = toWorld;
        }

        var ghost = GetGhost();
        ghost.mesh.Clear();
        ghost.mesh.indexFormat = IndexFormat.UInt32;
        ghost.mesh.CombineMeshes(_combine, true, true);   // 호출 시점 복사 → 버퍼 재사용 안전, 할당 0
        ghost.mf.sharedMesh = ghost.mesh;
        ghost.age = 0f;
        ghost.life = ghostLife;
        ghost.active = true;
        ghost.go.transform.position = Vector3.zero;   // 메시에 월드 좌표가 구워짐 → GO는 원점(호출자가 오프셋 가능)

        ghost.mat.SetColor(ColorID, ghostColor);
        ghost.mat.SetFloat(AlphaID, startAlpha);
        if (ghost.go != null) ghost.go.SetActive(true);
        return ghost;
    }

    Ghost GetGhost()
    {
        for (int i = 0; i < _pool.Count; i++)
            if (!_pool[i].active) return _pool[i];
        return CreateGhost();
    }

    Ghost CreateGhost()
    {
        var go = new GameObject("DashGhost") { hideFlags = HideFlags.DontSave };   // 씬 저장 제외 — 에디터 정지 시 고아 GO 잔류 방지
        go.transform.SetParent(null, false);
        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = LightProbeUsage.Off;
        mr.reflectionProbeUsage = ReflectionProbeUsage.Off;

        var mat = new Material(_ghostMatTemplate);
        mr.sharedMaterial = mat;

        var mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
        mesh.MarkDynamic();

        var g = new Ghost { go = go, mf = mf, mat = mat, mesh = mesh, active = false };
        go.SetActive(false);
        _pool.Add(g);
        return g;
    }

    Material CreateGhostMaterial()
    {
        var sh = ghostShader != null ? ghostShader : Shader.Find("ZombieCrush/AfterimageGhost");
        if (sh == null)
        {
            Debug.LogWarning("[PlayerAfterimage] ZombieCrush/AfterimageGhost 셰이더 미발견 — 잔상 비활성. 빌드 스트립 의심.");
            return null;
        }
        var m = new Material(sh);
        m.SetColor(ColorID, ghostColor);
        m.SetFloat(AlphaID, startAlpha);
        return m;
    }

    // ──────────── 스피드 스트릭 ────────────

    void EmitStreaks(float dt)
    {
        _streakTimer -= dt;
        int safety = 8;
        while (_streakTimer <= 0f && safety-- > 0)
        {
            _streakTimer += 1f / StreakRate;
            _streakEmit.position = motor.transform.position + Vector3.up * 0.9f + Random.insideUnitSphere * 0.35f;
            _streakEmit.velocity = -_moveDir * Random.Range(1.5f, 3f);
            _streakPS.Emit(_streakEmit, 1);
        }
    }

    ParticleSystem CreateStreakPS()
    {
        var go = new GameObject("DashStreakPS");
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop();
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.25f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
        main.startColor = StreakColor;
        main.maxParticles = 64;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission; em.enabled = false;
        var shp = ps.shape; shp.enabled = false;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.renderMode = ParticleSystemRenderMode.Stretch;
        psr.lengthScale = 4f;
        psr.velocityScale = 0.08f;
        psr.shadowCastingMode = ShadowCastingMode.Off;
        psr.receiveShadows = false;
        psr.sharedMaterial = _streakMat;
        return ps;
    }

    // ─── ★대시 비트(R8) 생성부 ───

    /// <summary>비트용 PS — 방출/셰이프 끔(전부 EmitParams 수동), 월드 시뮬. stretch=샤드(글리치), 아니면 빌보드(먼지).</summary>
    ParticleSystem CreateBeatPS(string name, Material mat, bool stretch)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop();
        var main = ps.main;
        main.startSpeed = 0f;
        main.maxParticles = 128;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission; em.enabled = false;
        var shp = ps.shape; shp.enabled = false;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;

        var psr = go.GetComponent<ParticleSystemRenderer>();
        if (stretch) { psr.renderMode = ParticleSystemRenderMode.Stretch; psr.lengthScale = 5f; psr.velocityScale = 0.05f; }
        psr.shadowCastingMode = ShadowCastingMode.Off;
        psr.receiveShadows = false;
        psr.sharedMaterial = mat;
        ps.Play();
        return ps;
    }

    /// <summary>소프트 원 텍스처 절차 생성(64², smoothstep 가장자리) — 내장 Default-Particle은 이 Unity 버전에서
    /// 런타임 GetBuiltinResource 불가(콘솔 에러, 07-11 유저 보고)라 직접 만든다. 에셋 의존 0 유지.</summary>
    Texture2D CreateSoftCircleTex()
    {
        const int N = 64;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false)
        { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        var px = new Color32[N * N];
        float half = (N - 1) * 0.5f;
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = (x - half) / half, dy = (y - half) / half;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                a = a * a * (3f - 2f * a);   // smoothstep — 부드러운 가장자리
                px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return tex;
    }

    /// <summary>먼지 재질 — 알파 블렌드(가산 ❌: 흙먼지는 빛나면 안 됨) + 절차 소프트 원 텍스처.</summary>
    Material CreateDustMaterial()
    {
        var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        if (sh == null) { Debug.LogWarning("[PlayerAfterimage] 먼지 셰이더 미발견(빌드 스트립?) — 대시 먼지 비활성."); return null; }
        var mat = new Material(sh);
        _dustTex = CreateSoftCircleTex();
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", _dustTex);
        else if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", _dustTex);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)RenderQueue.Transparent;
        return mat;
    }

#if UNITY_EDITOR
    // 비교 하니스 HUD(드라이버 라인 아래) — 판정 후 제거. 에디터 전용(빌드 노출 차단).
    void OnGUI()
    {
        var style = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
        style.normal.textColor = Color.magenta;
        GUI.Label(new Rect(14f, 44f, 900f, 40f), $"[{cycleStyleKey}] 대시연출: {dashStyle}  (Off / Glitch위상 / Physical먼지 순환)", style);
    }
#endif

    Material CreateStreakMaterial()
    {
        var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        if (sh == null) { Debug.LogWarning("[PlayerAfterimage] 스트릭 셰이더 미발견(빌드 스트립?) — 속도선/샤드 비활성."); return null; }
        var mat = new Material(sh);
        Color hdr = StreakColor * 2.5f; hdr.a = StreakColor.a;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", hdr);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", hdr);
        mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)BlendMode.One);
        mat.SetInt("_ZWrite", 0);
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 2f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)RenderQueue.Transparent;
        return mat;
    }
}
