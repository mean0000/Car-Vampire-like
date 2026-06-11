using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 3단 긴장 오디오 디렉터(B-005a, 사운드 프렙 스펙 §2.2의 코드 등가물 — AudioMixer 에셋은 B-005b로 연기).
/// Calm/Alert/Combat 절차 생성 루프 3장을 동시 재생하고 볼륨 크로스페이드로 상태를 표현한다(에셋 0원).
/// 전환 비대칭: 긴장 진입 빠르게(0.5s), 해소 느리게(5s — "여운", 전투 끝의 한숨과 정합).
/// Combat 중 Calm 레이어 −6dB 추가 덕킹 — "세상이 좁아지는" 집중 연출의 음향판.
/// 롤백 = masterEnabled off(전 레이어 0).
/// </summary>
public class TensionAudioDirector : MonoBehaviour
{
    enum Tension { Calm, Alert, Combat }

    [Header("레이어 최대 볼륨")]
    [SerializeField] float calmMaxVolume = 0.03f;   // "황혼의 적막" — 평시는 거의 침묵(유저 게이트: 웅웅거림 과다 판정)
    [SerializeField] float alertMaxVolume = 0.1f;
    [SerializeField] float combatMaxVolume = 0.14f;

    [Header("전환(비대칭 — 진입 빠르게/해소 느리게)")]
    [SerializeField] float attackTime = 0.5f;
    [SerializeField] float releaseTime = 5f;

    [Header("마스터")]
    [SerializeField] bool masterEnabled = true;   // 롤백 = off

    const int SampleRate = 44100;
    const float LoopSeconds = 4f;
    const int FadeSamples = 2048;                 // 루프 경계 크로스페이드(~46ms) — 클릭 방지
    const float PollInterval = 0.25f;             // 상태 판정 폴링
    const float RescanInterval = 1f;              // 좀비 목록 재스캔(ZombieLKPSilhouette 패턴)
    const float CombatCalmDuck = 0.5f;            // −6dB ≈ ×0.5

    AudioSource _calmSrc, _alertSrc, _combatSrc;

    // 정규화 가중치(0~1) — 볼륨 = 가중치 × 레이어 최대
    float _calmW = 1f, _alertW, _combatW;
    float _calmTarget = 1f, _alertTarget, _combatTarget;
    float _fadeTime = 0.5f;                       // 현재 전환에 쓰는 시간(진입/해소 비대칭)

    Tension _tension = Tension.Calm;
    float _pollTimer;
    float _rescanTimer;
    readonly List<ZombieController> _zombies = new List<ZombieController>();

    void Awake()
    {
        _calmSrc = CreateLayerSource("CalmLayer", BuildCalmClip(), calmMaxVolume);
        _alertSrc = CreateLayerSource("AlertLayer", BuildAlertClip(), 0f);
        _combatSrc = CreateLayerSource("CombatLayer", BuildCombatClip(), 0f);
    }

    void Start()
    {
        _calmSrc.Play();
        _alertSrc.Play();
        _combatSrc.Play();
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        _rescanTimer -= dt;
        if (_rescanTimer <= 0f) { Rescan(); _rescanTimer = RescanInterval; }

        _pollTimer -= dt;
        if (_pollTimer <= 0f) { EvaluateTension(); _pollTimer = PollInterval; }

        // 가중치 페이드(매 프레임 — 부드러운 크로스페이드)
        if (_fadeTime > 0.001f)
        {
            float rate = dt / _fadeTime;
            _calmW = Mathf.MoveTowards(_calmW, _calmTarget, rate);
            _alertW = Mathf.MoveTowards(_alertW, _alertTarget, rate);
            _combatW = Mathf.MoveTowards(_combatW, _combatTarget, rate);
        }
        else
        {
            _calmW = _calmTarget; _alertW = _alertTarget; _combatW = _combatTarget;
        }

        float master = masterEnabled ? 1f : 0f;
        _calmSrc.volume = _calmW * calmMaxVolume * master;
        _alertSrc.volume = _alertW * alertMaxVolume * master;
        _combatSrc.volume = _combatW * combatMaxVolume * master;
    }

    // ── 상태 판정: Chase 이상(IsAggro) ≥1 → Combat / Investigate~Alert(IsAlerted) 존재 → Alert / 그 외 Calm ──

    void EvaluateTension()
    {
        bool anyAggro = false, anyAlerted = false;
        for (int i = _zombies.Count - 1; i >= 0; i--)
        {
            var z = _zombies[i];
            if (z == null || z.IsDead) { _zombies.RemoveAt(i); continue; }
            if (z.IsAggro) { anyAggro = true; break; }
            if (z.IsAlerted) anyAlerted = true;
        }

        Tension next = anyAggro ? Tension.Combat : anyAlerted ? Tension.Alert : Tension.Calm;
        if (next == _tension) return;

        // 비대칭: 긴장이 오르면 진입 시간, 내리면 해소 시간(여운)
        _fadeTime = next > _tension ? attackTime : releaseTime;
        _tension = next;

        switch (_tension)
        {
            case Tension.Calm:
                _calmTarget = 1f; _alertTarget = 0f; _combatTarget = 0f;
                break;
            case Tension.Alert:   // Calm 위에 얹는 긴장 레이어
                _calmTarget = 1f; _alertTarget = 1f; _combatTarget = 0f;
                break;
            case Tension.Combat:  // Calm −6dB 덕킹 — 세상이 좁아진다
                _calmTarget = CombatCalmDuck; _alertTarget = 0f; _combatTarget = 1f;
                break;
        }
    }

    void Rescan()
    {
        _zombies.Clear();
        _zombies.AddRange(FindObjectsOfType<ZombieController>());
    }

    // ── 절차 생성 루프 클립 ──────────────────────────────────────────
    // 사인 성분은 루프 길이(4s)의 정수배 주기로 맞췄고(55/82.5/110/73.5Hz 모두 4s에 정수 사이클),
    // 노이즈·필터 성분의 경계 불연속은 끝단 크로스페이드 굽기로 제거(MakeLoopClip).

    AudioSource CreateLayerSource(string name, AudioClip clip, float volume)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.loop = true;
        src.spatialBlend = 0f;   // 2D — 긴장 음악은 공간이 아니라 상태
        src.playOnAwake = false;
        src.volume = volume;
        return src;
    }

    /// <summary>Calm: 저역 드론 — 55Hz+82.5Hz 사인 합 + 아주 약한 저역 노이즈. "도시의 적막".</summary>
    AudioClip BuildCalmClip()
    {
        int total = (int)(SampleRate * LoopSeconds) + FadeSamples;
        float[] ext = new float[total];
        var rng = new System.Random(1101);
        float lp = 0f;
        for (int i = 0; i < total; i++)
        {
            float t = i / (float)SampleRate;
            float drone = Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.45f
                        + Mathf.Sin(2f * Mathf.PI * 82.5f * t) * 0.25f;
            float white = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp += 0.02f * (white - lp);   // 원폴 저역 통과 — 멀리서 웅웅대는 도시
            ext[i] = drone + lp * 0.15f;
        }
        return MakeLoopClip("TensionCalm", ext);
    }

    /// <summary>Alert: Calm 위에 얹는 긴장 레이어 — 110Hz 사인 펄스(1.5Hz 진폭 변조) + 가는 고역 노이즈 쉬머.</summary>
    AudioClip BuildAlertClip()
    {
        int total = (int)(SampleRate * LoopSeconds) + FadeSamples;
        float[] ext = new float[total];
        var rng = new System.Random(1102);
        float lp = 0f;
        for (int i = 0; i < total; i++)
        {
            float t = i / (float)SampleRate;
            float pulse = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 1.5f * t);
            float tone = Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.4f * pulse;
            float white = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp += 0.3f * (white - lp);
            float shimmer = (white - lp) * 0.06f;   // 고역만 남긴 가는 쉬머
            ext[i] = tone + shimmer;
        }
        return MakeLoopClip("TensionAlert", ext);
    }

    /// <summary>Combat: 더 단단한 레이어 — 73.5Hz(≈D2, 4s 정수 사이클로 스냅) 사인 + 2Hz 펄스 + 진폭 변조 노이즈 퍼커션.</summary>
    AudioClip BuildCombatClip()
    {
        int total = (int)(SampleRate * LoopSeconds) + FadeSamples;
        float[] ext = new float[total];
        var rng = new System.Random(1103);
        for (int i = 0; i < total; i++)
        {
            float t = i / (float)SampleRate;
            float raw = Mathf.Sin(2f * Mathf.PI * 2f * t);
            float pulse = raw > 0f ? raw * raw : 0f;   // 반파 제곱 — 단단한 박동
            float tone = Mathf.Sin(2f * Mathf.PI * 73.5f * t) * (0.25f + 0.45f * pulse);
            float white = (float)(rng.NextDouble() * 2.0 - 1.0);
            float perc = white * pulse * pulse * 0.2f;   // 펄스에 묶인 노이즈 — 퍼커션 느낌
            ext[i] = tone + perc;
        }
        return MakeLoopClip("TensionCombat", ext);
    }

    /// <summary>
    /// 확장 버퍼(루프 길이 + FadeSamples)를 받아 루프 경계가 이어지는 클립을 만든다.
    /// 머리 구간을 "꼬리의 자연 연속(ext[L..L+N))"에서 원래 머리로 크로스페이드 —
    /// 마지막 샘플 ext[L-1] → 첫 샘플이 ext[L]에서 시작하므로 경계 클릭이 없다.
    /// </summary>
    static AudioClip MakeLoopClip(string name, float[] ext)
    {
        int loopLen = ext.Length - FadeSamples;
        float[] data = new float[loopLen];
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
}
