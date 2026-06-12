using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 좀비 보컬 디렉터(위협 패스 2026-06-12, 세션 1 임무 3-②) — 런타임 부착자 패턴.
/// 자가 부트스트랩 DDOL 매니저가 씬의 ZombieController를 폴링 스캔해 AudioSource를 AddComponent —
/// 프리팹·씬 무수정. 좀비 상태는 공개 프로퍼티(IsAggro/IsDead) 폴링으로만 읽는다(파일권 제약).
///
/// 설계 의도(브리핑 §0 위협 테제):
///  ① 평시 으르렁 = 시야 밖 존재 알림. 3D 정위(spatialBlend 1, Log 감쇠)라 화면 밖 좀비의
///     으르렁이 곧 후방 압박 정보가 된다(RearThreatHint 시각 힌트의 청각 원본).
///  ② 발각 절규 = 개전 선언. IsAggro 상승 엣지에서 1회 즉발 — "들켰다"의 음향판.
///
/// ★캐넌 준수:
///  - 첫 볼륨 보수적(으르렁 0.10 / 절규 0.15).
///  - 3D 감쇠 = 소리사다리 정합: SfxOneShot과 동일 1.5/25m Logarithmic(발견 밴드 8~11m 안쪽에서 가청).
///  - 동시 으르렁 한도(4) — 호드 폭주 방지. 절규는 좀비당 소스 1개가 자연 상한.
///  - 절차 합성 = 플레이스홀더(클립명 placeholder 표기). 실음원 입고 시 교체 전제.
///  - timeScale=0이면 신규 발화 금지(진행 중 원샷 ≤1.3s는 자연 소멸).
///
/// 노브: 플레이 중 하이어라키 "ZombieVocalDirector" 인스펙터에서 라이브 조정(런타임 생성 —
/// 씬 직렬화 함정 없음). 확정값은 코드에 기입. 롤백 = masterEnabled off.
/// </summary>
public class ZombieVocalDirector : MonoBehaviour
{
    [Header("볼륨 노브 (제0원칙: 첫 볼륨 보수적)")]
    [SerializeField, Range(0f, 0.5f)] float growlVolume = 0.10f;
    [SerializeField, Range(0f, 0.5f)] float screamVolume = 0.15f;

    [Header("으르렁 간격(초) — 좀비별 랜덤 [min, max]")]
    [SerializeField, Range(2f, 20f)] float growlIntervalMin = 5f;
    [SerializeField, Range(4f, 30f)] float growlIntervalMax = 11f;

    [Header("3D 감쇠 — 소리사다리 정합(SfxOneShot 1.5/25 Log). 신규 부착분부터 적용")]
    [SerializeField, Range(0.5f, 5f)] float minDistance = 1.5f;
    [SerializeField, Range(10f, 40f)] float maxDistance = 25f;

    [Header("동시 으르렁 한도 — 호드 폭주 방지(초과분은 짧게 재예약)")]
    [SerializeField, Range(1, 12)] int maxConcurrentGrowls = 4;

    [Header("마스터(롤백 = off)")]
    [SerializeField] bool masterEnabled = true;

    const float RescanInterval = 1f;
    const float PitchVarMin = 0.85f;   // 좀비별 고정 기본 피치 — 개체 음색 다양성(공짜)
    const float PitchVarMax = 1.15f;
    const int GrowlPriority = 160;     // Unity 기본 128보다 낮은 우선권 — 전투음에 양보
    const int ScreamPriority = 100;    // 절규는 정보 — 기본보다 높은 우선권

    class Voice
    {
        public ZombieController z;
        public AudioSource src;
        public float nextGrowl;
        public float growlEndTime;   // 으르렁 종료 예정 시각 — PlayOneShot은 src.clip을 안 채우므로 시간으로 추적
        public bool prevAggro;
        public float basePitch;
    }

    static ZombieVocalDirector s_instance;

    readonly List<Voice> _voices = new List<Voice>();
    float _rescanTimer;

    // ── 시스템 검증용 공개 디버그 접근자(unity-mcp 리플렉션 함정 회피 패턴) — 게임 로직 사용 금지 ──
    public static int DebugGrowlsFired { get; private set; }
    public static int DebugScreamsFired { get; private set; }
    public static int DebugVoiceCount => s_instance != null ? s_instance._voices.Count : -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (s_instance != null) return;
        var go = new GameObject("ZombieVocalDirector");
        go.AddComponent<ZombieVocalDirector>();
    }

    void Awake()
    {
        if (s_instance != null && s_instance != this) { Destroy(gameObject); return; }
        s_instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable() { UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded; }
    void OnDisable() { UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded; }

    void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m)
    {
        // Additive 로드는 기존 좀비가 생존 — Clear하면 다음 리스캔에서 AudioSource 중복 부착.
        if (m != UnityEngine.SceneManagement.LoadSceneMode.Single) return;
        _voices.Clear();    // 이전 씬 좀비 참조는 전부 사망 — 다음 리스캔에서 재구축
        _rescanTimer = 0f;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        _rescanTimer -= dt;
        if (_rescanTimer <= 0f) { Rescan(); _rescanTimer = RescanInterval; }

        var pc = PlayerController.Instance;
        Vector3 playerPos = pc != null ? pc.transform.position : Vector3.zero;
        bool worldRunning = Time.timeScale > 0f && pc != null;
        float now = Time.time;   // scaled — 정산 정지 중엔 예약 시계도 멎는다(의도)

        // 동시 으르렁 카운트(절규 제외 — 절규는 정보라 무제한, 좀비당 소스 1개가 자연 상한).
        // ⚠️PlayOneShot은 src.clip을 설정하지 않음 — isPlaying+clip 검사 불가. 종료 예정 시각으로 추적.
        int growling = 0;
        for (int i = 0; i < _voices.Count; i++)
            if (_voices[i].growlEndTime > now) growling++;

        for (int i = _voices.Count - 1; i >= 0; i--)
        {
            var v = _voices[i];
            if (v.z == null || v.z.IsDead || v.src == null)
            {
                if (v.src != null) v.src.Stop();   // 사망 즉시 으르렁 절단 — 킬 정적 캐넌(숨·정적→한 발 해소) 보존
                _voices.RemoveAt(i); continue;
            }

            // 라이브 노브 반영(감쇠) — 매 프레임 대입은 값 동일 시 사실상 무비용
            v.src.minDistance = minDistance;
            v.src.maxDistance = maxDistance;

            // ② 발각 절규 — IsAggro 상승 엣지(폴링). 으르렁 중이어도 끊고 즉발(개전이 우선).
            bool aggro = v.z.IsAggro;
            if (aggro && !v.prevAggro && masterEnabled && worldRunning)
            {
                float dist = (v.z.transform.position - playerPos).magnitude;
                if (dist <= maxDistance)   // 어차피 안 들릴 거리면 보이스 낭비 금지
                {
                    v.src.Stop();            // 으르렁 진행 중이면 끊는다 — 개전이 우선
                    v.growlEndTime = 0f;
                    v.src.priority = ScreamPriority;
                    v.src.pitch = v.basePitch * Random.Range(0.95f, 1.05f);
                    v.src.PlayOneShot(ZombieVocalClips.RandomScream(), screamVolume);
                    DebugScreamsFired++;
                    v.nextGrowl = now + Random.Range(growlIntervalMin, growlIntervalMax);
                }
            }
            v.prevAggro = aggro;

            // ① 평시 으르렁 — 좀비별 랜덤 간격 루프
            if (now < v.nextGrowl) continue;
            if (!masterEnabled || !worldRunning)
            {
                v.nextGrowl = now + Random.Range(1f, 2f);   // 게이트 닫힘 — 짧게 재시도
                continue;
            }
            float d = (v.z.transform.position - playerPos).magnitude;
            if (d > maxDistance + 5f)
            {
                v.nextGrowl = now + Random.Range(2f, 4f);   // 가청권 밖 — 들어오면 곧 발화하게 짧은 재예약
                continue;
            }
            if (growling >= maxConcurrentGrowls)
            {
                v.nextGrowl = now + Random.Range(0.5f, 1.5f);   // 한도 초과 — 슬롯 나면 발화
                continue;
            }

            v.src.priority = GrowlPriority;
            v.src.pitch = v.basePitch * Random.Range(0.93f, 1.07f);
            var growl = ZombieVocalClips.RandomGrowl();
            v.src.PlayOneShot(growl, growlVolume);
            DebugGrowlsFired++;
            v.growlEndTime = now + growl.length / Mathf.Max(v.src.pitch, 0.1f);
            growling++;
            v.nextGrowl = now + Random.Range(growlIntervalMin, growlIntervalMax);
        }
    }

    void Rescan()
    {
        var all = FindObjectsOfType<ZombieController>();
        foreach (var z in all)
        {
            if (z.IsDead) continue;
            bool known = false;
            for (int i = 0; i < _voices.Count; i++)
                if (_voices[i].z == z) { known = true; break; }
            if (known) continue;

            // 런타임 부착 — 프리팹 무수정. 좀비 파괴 시 소스도 함께 파괴(정리 불필요).
            var src = z.gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 1f;                          // 완전 3D — 으르렁 = 방위 정보
            src.minDistance = minDistance;
            src.maxDistance = maxDistance;
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.dopplerLevel = 0f;                          // 부감 카메라에서 도플러는 멀미만 남김

            _voices.Add(new Voice
            {
                z = z,
                src = src,
                // 첫 으르렁 분산(0~max) — 스폰 직후 일제 합창 방지.
                nextGrowl = Time.time + Random.Range(0f, growlIntervalMax),
                // 이미 어그로 상태로 발견된 좀비는 절규 엣지 미발화(RearThreatHint와 동일 정책).
                prevAggro = z.IsAggro,
                basePitch = Random.Range(PitchVarMin, PitchVarMax),
            });
        }
    }

    void OnDestroy()
    {
        if (s_instance == this) s_instance = null;
    }
}

/// <summary>
/// 좀비 보컬 절차 합성 클립(플레이스홀더 — 클립명에 placeholder 표기, 실음원 입고 시 교체).
/// 으르렁 3변형 + 절규 2변형, 최초 1회 생성 후 캐싱(MeleeSfx 패턴).
/// </summary>
public static class ZombieVocalClips
{
    const int SampleRate = 44100;

    static AudioClip[] _growls;
    static AudioClip[] _screams;

    public static AudioClip RandomGrowl()
    {
        EnsureBuilt();
        return _growls[Random.Range(0, _growls.Length)];
    }

    public static AudioClip RandomScream()
    {
        EnsureBuilt();
        return _screams[Random.Range(0, _screams.Length)];
    }

    static void EnsureBuilt()
    {
        // 배열 비-null만으로는 부족 — 도메인 리로드 off 환경에서 정적 배열은 살고 클립만 파괴(가짜 null)될 수 있다.
        if (_growls != null && _growls[0] != null) return;
        _growls = new[]
        {
            BuildGrowl("ZombieGrowl_placeholder_1", 11, 68f, 24f),
            BuildGrowl("ZombieGrowl_placeholder_2", 23, 76f, 21f),
            BuildGrowl("ZombieGrowl_placeholder_3", 37, 85f, 27f),
        };
        _screams = new[]
        {
            BuildScream("ZombieScream_placeholder_1", 51, 170f, 640f),
            BuildScream("ZombieScream_placeholder_2", 67, 210f, 760f),
        };
    }

    // ── 으르렁: 저역 톤(배음 3개) × 거친 진폭 변조(rasp) + 숨 노이즈, 완만한 스웰 ──
    static AudioClip BuildGrowl(string name, int seed, float baseFreq, float raspHz)
    {
        float dur = 1.3f;
        int n = (int)(SampleRate * dur);
        var data = new float[n];
        var rng = new System.Random(seed);
        float lp = 0f, phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            // 엔벨로프: 0.2s 스웰 → 끝 0.35s 릴리즈
            float env = Mathf.Clamp01(t / 0.2f) * Mathf.Clamp01((dur - t) / 0.35f);
            env *= env * (3f - 2f * env);   // smoothstep — 모서리 제거

            // 피치: 미세한 흔들림 + 끝으로 갈수록 살짝 하강(짐승의 날숨)
            float f = baseFreq * (1f + 0.05f * Mathf.Sin(2f * Mathf.PI * 0.9f * t)) * (1f - 0.06f * t / dur);
            phase += 2f * Mathf.PI * f / SampleRate;
            float body = Mathf.Sin(phase) * 0.55f + Mathf.Sin(2f * phase) * 0.3f + Mathf.Sin(3f * phase) * 0.18f;

            // rasp: 반파 정류된 저주파 변조 — 목구멍의 거친 떨림
            float rasp = 0.5f + 0.5f * Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * raspHz * t + 0.8f * Mathf.Sin(2f * Mathf.PI * 3f * t)));

            float white = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp += 0.12f * (white - lp);   // 숨 — 저역 통과 노이즈

            float x = (body * rasp + lp * 0.45f * rasp) * env * 1.6f;
            data[i] = x / (1f + Mathf.Abs(x));   // 소프트 클립 — 그르렁의 포화 질감
        }
        return Make(name, data);
    }

    // ── 절규: 지수 상승 글라이드(비브라토) + 고역 노이즈 찢김, 즉발 어택 ──
    static AudioClip BuildScream(string name, int seed, float f0, float f1)
    {
        float dur = 0.85f;
        int n = (int)(SampleRate * dur);
        var data = new float[n];
        var rng = new System.Random(seed);
        float lp = 0f, phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float u = t / dur;
            float env = Mathf.Clamp01(t / 0.02f) * Mathf.Clamp01((dur - t) / 0.3f);

            // 지수 글라이드 f0→f1 + 9Hz 비브라토 — 목이 찢어지는 상승
            float f = f0 * Mathf.Pow(f1 / f0, u) * (1f + 0.05f * Mathf.Sin(2f * Mathf.PI * 9f * t));
            phase += 2f * Mathf.PI * f / SampleRate;
            float body = Mathf.Sin(phase) * 0.5f + Mathf.Sin(2f * phase) * 0.35f + Mathf.Sin(3f * phase) * 0.2f;

            float white = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp += 0.25f * (white - lp);
            float shred = (white - lp) * 0.45f * (0.4f + 0.6f * u);   // 고역만 — 갈수록 찢김 증가

            float x = (body + shred) * env * 2.2f;
            data[i] = x / (1f + Mathf.Abs(x));   // 강한 포화 — 절규의 디스토션
        }
        return Make(name, data);
    }

    static AudioClip Make(string name, float[] data)
    {
        var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
