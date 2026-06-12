using UnityEngine;

/// <summary>
/// 앰비언트 베드 1겹 — 저역 드론(위협 패스 2026-06-12, 세션 1 임무 3-①). "침묵 제거"가 1차 가치.
/// 자가 부트스트랩 DDOL(RearThreatHint 패턴) — 씬 배치·씬 저장 불필요, 어떤 씬에서든 존재.
///
/// ★캐넌 준수:
///  - 저역 사인은 1개 층 + 옥타브 간격(49/98/196Hz — 인접 주파수 0개)으로만 적층. 맥놀이(웅웅거림) 원천 차단.
///    움직임은 두 번째 사인이 아니라 LFO 진폭 변조(루프당 정수 사이클)로 만든다.
///  - 첫 볼륨 보수적(0.05) — "안 들려"가 "고막 때림"보다 싸다.
///  - timeScale=0(정산/사무실)에서 AudioSource는 안 멈춤 → 명시적 페이드아웃 게이트.
///  - 절차 합성 = 시스템 검증용 플레이스홀더(클립명에 placeholder 표기). 실음원 입고 시 교체 전제.
///
/// 노브: 플레이 중 하이어라키의 "AmbientBedDrone" GO 인스펙터에서 라이브 조정(런타임 생성이라
/// 씬 직렬화 함정 없음 — 코드 default가 항상 진실). 확정값은 코드에 기입.
/// 롤백 = masterEnabled off.
/// </summary>
public class AmbientBedDrone : MonoBehaviour
{
    [Header("볼륨 노브 (제0원칙: 첫 볼륨 보수적 0.03~0.15)")]
    [SerializeField, Range(0f, 0.3f)] float bedVolume = 0.05f;

    [Header("페이드(초) — 진입 느리게(적막이 스며듦) / 이탈 빠르게(정산 컷)")]
    [SerializeField, Range(0.1f, 10f)] float fadeInTime = 3f;
    [SerializeField, Range(0.05f, 5f)] float fadeOutTime = 0.8f;

    [Header("마스터(롤백 = off)")]
    [SerializeField] bool masterEnabled = true;

    const int SampleRate = 44100;
    const float LoopSeconds = 8f;     // 49/98/196Hz·LFO 0.125/0.25Hz 전부 8s에 정수 사이클 — 경계 무클릭
    const int FadeSamples = 2048;     // 노이즈 성분 루프 경계 크로스페이드(~46ms)

    static AmbientBedDrone s_instance;

    AudioSource _src;

    // 2026-06-12 유저 판정: 합성 드론 톤 기각("아무 상황 없을 때 우우우웅 거슬림"). 실음원 확보 시 true로 재기동.
    const bool kAutoStart = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
#pragma warning disable 0162   // kAutoStart=false 동안 아래 코드는 의도된 unreachable — 재기동 대비 보존
        if (!kAutoStart) return;
        if (s_instance != null) return;
        var go = new GameObject("AmbientBedDrone");
        go.AddComponent<AmbientBedDrone>();
#pragma warning restore 0162
    }

    void Awake()
    {
        if (s_instance != null && s_instance != this) { Destroy(gameObject); return; }
        s_instance = this;
        DontDestroyOnLoad(gameObject);

        _src = gameObject.AddComponent<AudioSource>();
        _src.clip = BuildBedClip();
        _src.loop = true;
        _src.playOnAwake = false;
        _src.spatialBlend = 0f;   // 2D — 베드는 공간이 아니라 공기
        _src.volume = 0f;         // 페이드 인으로만 차오른다
        _src.Play();
    }

    void Update()
    {
        if (_src == null) return;   // Awake 중복-파괴 경로: Destroy는 프레임 끝 — 같은 프레임 Update NRE 방지

        // 게이트: 마스터 on + 미션 중(플레이어 존재) + 세계가 흐르는 중(정산/사무실 timeScale=0 제외).
        bool active = masterEnabled && Time.timeScale > 0f && PlayerController.Instance != null;
        float target = active ? bedVolume : 0f;
        float fade = target > _src.volume ? fadeInTime : fadeOutTime;
        float rate = Mathf.Max(bedVolume, 0.01f) / Mathf.Max(fade, 0.01f);
        _src.volume = Mathf.MoveTowards(_src.volume, target, rate * Time.unscaledDeltaTime);
    }

    // ── 절차 생성: 어둡고 짓누르는 저역 드론 ─────────────────────────
    // 구성: 49Hz(G1) 기둥 + 98/196Hz 옥타브(소형 스피커 가청 보장) + 짙은 저역 노이즈(도시의 공기).
    // 호흡 = LFO 0.125Hz(노이즈)·0.25Hz(196Hz층) 진폭 변조 — 사인 추가 없이 정적인 드론에 미세한 맥.

    static AudioClip BuildBedClip()
    {
        int total = (int)(SampleRate * LoopSeconds) + FadeSamples;
        var ext = new float[total];
        var rng = new System.Random(2026);
        float lp = 0f;
        for (int i = 0; i < total; i++)
        {
            float t = i / (float)SampleRate;
            float breathe = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 0.125f * t);   // 루프당 1사이클
            float shimmer = 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 0.25f * t);    // 루프당 2사이클

            float drone = Mathf.Sin(2f * Mathf.PI * 49f * t) * 0.45f
                        + Mathf.Sin(2f * Mathf.PI * 98f * t) * 0.30f
                        + Mathf.Sin(2f * Mathf.PI * 196f * t) * 0.12f * shimmer;

            float white = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp += 0.015f * (white - lp);   // 강한 저역 통과 — 멀리서 짓누르는 공기

            ext[i] = drone + lp * 0.20f * breathe;
        }
        return MakeLoopClip("AmbientBed_LowDrone_placeholder", ext);
    }

    /// <summary>확장 버퍼 머리를 꼬리의 자연 연속에서 크로스페이드 — 루프 경계 클릭 제거(TensionAudioDirector와 동일 기법).</summary>
    static AudioClip MakeLoopClip(string name, float[] ext)
    {
        int loopLen = ext.Length - FadeSamples;
        var data = new float[loopLen];
        for (int k = 0; k < FadeSamples; k++)
        {
            float blend = k / (float)FadeSamples;
            data[k] = Mathf.Lerp(ext[loopLen + k], ext[k], blend);
        }
        for (int i = FadeSamples; i < loopLen; i++)
            data[i] = ext[i];

        var clip = AudioClip.Create(name, loopLen, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    void OnDestroy()
    {
        if (s_instance == this) s_instance = null;
    }
}
