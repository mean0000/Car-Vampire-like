using UnityEngine;

/// <summary>
/// 풀링 3D 원샷 헬퍼(B-005a, 사운드 프렙 스펙 §2.3) — AudioSource.PlayClipAtPoint 전면 대체.
/// PlayClipAtPoint는 매 호출 GO를 생성·파괴하고 rolloff가 기본값(Linear) — 소리사다리 감쇠 설계가 안 먹힘.
/// 풀 GO는 씬 로컬(DDOL 아님) — 씬 전환 시 파괴되고 다음 Play에서 자동 재생성.
/// </summary>
public static class SfxOneShot
{
    const int PoolSize = 12;
    const int MaxSameClip = 3;          // 같은 클립 동시 발성 한도 — 호드 그르렁 폭주 방지(초과 드롭)
    const float MinDistance = 1.5f;     // 소리사다리 정합(§3.5): 좀비류 1.5/25, Logarithmic
    const float MaxDistance = 25f;

    static GameObject _root;
    static AudioSource[] _pool;
    static float[] _startTimes;

    /// <summary>3D 원샷 재생. 피치 = 1 ± pitchJitter 랜덤(기계적 반복 제거 — 공짜 다양성).</summary>
    public static void Play(AudioClip clip, Vector3 pos, float volume = 1f, float pitchJitter = 0.05f)
    {
        if (clip == null) return;
        EnsurePool();

        // 같은 클립 동시 발성 카운트 — 한도 초과분은 드롭(가장 싼 클램프)
        int sameClip = 0;
        for (int i = 0; i < PoolSize; i++)
            if (_pool[i].isPlaying && _pool[i].clip == clip) sameClip++;
        if (sameClip >= MaxSameClip) return;

        // 빈 소스 우선, 풀 고갈 시 가장 오래된 소스 강제 재사용
        int idx = -1;
        float oldest = float.MaxValue;
        for (int i = 0; i < PoolSize; i++)
        {
            if (!_pool[i].isPlaying) { idx = i; break; }
            if (_startTimes[i] < oldest) { oldest = _startTimes[i]; idx = i; }
        }

        var src = _pool[idx];
        src.transform.position = pos;
        src.clip = clip;
        src.volume = volume;
        src.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        src.Play();
        _startTimes[idx] = Time.unscaledTime;
    }

    static void EnsurePool()
    {
        if (_root != null) return;   // Unity 오버로드 == — 씬 전환으로 파괴됐으면 재생성

        _root = new GameObject("SfxOneShotPool");
        _pool = new AudioSource[PoolSize];
        _startTimes = new float[PoolSize];
        for (int i = 0; i < PoolSize; i++)
        {
            var go = new GameObject($"SfxOneShot_{i}");
            go.transform.SetParent(_root.transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 1f;                              // 완전 3D — 리스너 분리(AudioListenerRig)가 전제
            src.minDistance = MinDistance;
            src.maxDistance = MaxDistance;
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            _pool[i] = src;
        }
    }
}
