using UnityEngine;

/// <summary>
/// 근접 타격 사운드를 코드로 절차 생성한다(에셋·씬 와이어링 없이 무기별 차별화).
/// 그레이박스 단계용 — 나중에 실제 AudioClip 에셋으로 교체 가능.
/// 클립은 최초 1회 생성 후 캐싱(매 타격마다 생성 시 GC 압박 → 회피).
/// </summary>
public static class MeleeSfx
{
    const int SampleRate = 44100;

    static AudioClip _thud;    // 방망이: 낮은 둔탁한 타격음
    static AudioClip _clang;   // 쇠지렛대: 금속성 타격음
    static AudioClip _whiff;   // 헛스윙: 공기 가르는 소리

    /// <summary>무기별 죽음/타격 연출에 맞춘 타격음.</summary>
    public static void PlayHit(WeaponLoadout.DeathStyle style, Vector3 pos, float volume = 0.7f)
    {
        AudioClip clip = style == WeaponLoadout.DeathStyle.Crunch ? Clang : Thud;
        AudioSource.PlayClipAtPoint(clip, pos, volume);
    }

    /// <summary>아무것도 못 맞힌 스윙 — 공기 가르는 소리.</summary>
    public static void PlayWhiff(Vector3 pos, float volume = 0.35f)
    {
        AudioSource.PlayClipAtPoint(Whiff, pos, volume);
    }

    /// <summary>둔탁한 "퍽" 클립 공유 접근자 — B-004 명중 thud(PlayerCombat)가 재사용(신규 클립 제작 금지 제약).</summary>
    public static AudioClip ThudClip => Thud;

    static AudioClip Thud => _thud ??= BuildThud();
    static AudioClip Clang => _clang ??= BuildClang();
    static AudioClip Whiff => _whiff ??= BuildWhiff();

    // ── 생성기 ───────────────────────────────────────────────

    /// <summary>낮은 사인(~120Hz) + 노이즈 어택, 빠른 지수 감쇠 = 둔탁한 "퍽".</summary>
    static AudioClip BuildThud()
    {
        int n = (int)(SampleRate * 0.12f);
        var data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float env = Mathf.Exp(-t * 38f);
            float body = Mathf.Sin(2f * Mathf.PI * 120f * t);
            float click = (Random.value * 2f - 1f) * Mathf.Exp(-t * 220f) * 0.5f;   // 초반 어택 노이즈
            data[i] = (body * 0.8f + click) * env;
        }
        return Make("MeleeThud", data);
    }

    /// <summary>비조화 부분음(금속) + 노이즈 버스트, 중간 감쇠 = 금속성 "캉".</summary>
    static AudioClip BuildClang()
    {
        int n = (int)(SampleRate * 0.22f);
        var data = new float[n];
        float[] partials = { 620f, 940f, 1320f, 2100f };
        float[] gains = { 0.5f, 0.35f, 0.25f, 0.15f };
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float env = Mathf.Exp(-t * 16f);
            float body = 0f;
            for (int p = 0; p < partials.Length; p++)
                body += Mathf.Sin(2f * Mathf.PI * partials[p] * t) * gains[p];
            float burst = (Random.value * 2f - 1f) * Mathf.Exp(-t * 160f) * 0.4f;
            data[i] = (body + burst) * env;
        }
        return Make("MeleeClang", data);
    }

    /// <summary>밴드 노이즈를 부드러운 상승-하강 엔벨로프로 = 공기 가르는 "휙".</summary>
    static AudioClip BuildWhiff()
    {
        int n = (int)(SampleRate * 0.16f);
        var data = new float[n];
        float prev = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float u = t / 0.16f;
            float env = Mathf.Sin(u * Mathf.PI);                 // 0→1→0 부드러운 스웰
            float noise = Random.value * 2f - 1f;
            prev = Mathf.Lerp(prev, noise, 0.35f);               // 1차 저역통과 → "쉭" 질감
            data[i] = prev * env * 0.8f;
        }
        return Make("MeleeWhiff", data);
    }

    static AudioClip Make(string name, float[] data)
    {
        var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
