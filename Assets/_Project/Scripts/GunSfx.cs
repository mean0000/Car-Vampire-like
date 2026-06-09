using UnityEngine;

/// <summary>
/// 총기 발사·재장전 사운드를 Resources에서 로드해 캐싱한다(에셋·씬 와이어링 없이 코드로 해결).
/// 원본은 Feel NiceVibrations 무기 샘플 → Assets/Resources/SFX/Guns/ 로 복사한 것.
/// 무기 분류(권총/라이플/샷건)별로 발사음 1개씩 고정 — 발사마다 동일한 소리로 일관성 유지.
/// 최초 1회만 Resources.Load(이후 캐싱) — 매 발사 로드 시 디스크/GC 압박 회피.
/// </summary>
public static class GunSfx
{
    public enum GunClass { Pistol, Rifle, Shotgun }

    const string Dir = "SFX/Guns/";

    static bool _loaded;
    static AudioClip _pistolShot, _rifleShot, _shotgunShot;
    static AudioClip _pistolReload, _rifleReload, _shotgunReload;
    static float _pistolSkip, _rifleSkip, _shotgunSkip;   // 발사음 재생 시작 오프셋(초) — 완만한 어택 앞부분 건너뛰어 즉발 타격

    // Domain Reload를 끈 에디터 워크플로우(Enter Play Mode Options)에서도 Play 진입마다 캐시를 리셋 — stale 클립 방지.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetCache()
    {
        _loaded = false;
        _pistolShot = _rifleShot = _shotgunShot = null;
        _pistolReload = _rifleReload = _shotgunReload = null;
        _pistolSkip = _rifleSkip = _shotgunSkip = 0f;
    }

    /// <summary>무기 분류에 맞는 발사음(고정). 미발견 시 null.</summary>
    public static AudioClip Shot(GunClass c)
    {
        EnsureLoaded();
        return c switch
        {
            GunClass.Rifle => _rifleShot,
            GunClass.Shotgun => _shotgunShot,
            _ => _pistolShot,
        };
    }

    /// <summary>발사음 재생 시작 오프셋(초). 클립의 완만한 어택 앞부분을 건너뛰어 트리거 즉시 "쾅"이 터지게 한다.</summary>
    public static float ShotSkip(GunClass c)
    {
        EnsureLoaded();
        return c switch
        {
            GunClass.Rifle => _rifleSkip,
            GunClass.Shotgun => _shotgunSkip,
            _ => _pistolSkip,
        };
    }

    /// <summary>무기 분류에 맞는 재장전음. 미발견 시 null.</summary>
    public static AudioClip Reload(GunClass c)
    {
        EnsureLoaded();
        return c switch
        {
            GunClass.Rifle => _rifleReload,
            GunClass.Shotgun => _shotgunReload,
            _ => _pistolReload,
        };
    }

    static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;   // 미발견이어도 재시도 안 함(매 프레임 Resources.Load 폭주 방지)

        _pistolShot = Resources.Load<AudioClip>(Dir + "pistol_fire_1");
        _rifleShot = Resources.Load<AudioClip>(Dir + "rifle_fire_1");
        _shotgunShot = Resources.Load<AudioClip>(Dir + "shotgun_fire_1");

        _pistolReload = Resources.Load<AudioClip>(Dir + "pistol_reload");
        _rifleReload = Resources.Load<AudioClip>(Dir + "rifle_reload");
        _shotgunReload = Resources.Load<AudioClip>(Dir + "shotgun_reload");

        _pistolSkip = ComputeSkip(_pistolShot);
        _rifleSkip = ComputeSkip(_rifleShot);
        _shotgunSkip = ComputeSkip(_shotgunShot);

        if (_pistolShot == null && _rifleShot == null && _shotgunShot == null)
            Debug.LogWarning("[GunSfx] Resources/" + Dir + " 에서 발사음 클립을 찾지 못했습니다. wav 임포트/경로를 확인하세요(무음으로 폴백).");
    }

    /// <summary>
    /// 클립의 "라우드 어택" 시작 지점을 찾아 재생 스킵 오프셋(초)을 계산.
    /// peak 진폭의 45%를 처음 넘는 지점 직전(-4ms 백오프)을 시작점으로 — 완만하게 차오르는 앞부분을 건너뛰어
    /// 트리거 즉시 타격이 들리게 한다. 매직넘버 없이 클립별 자동 산출(클립 교체에도 안전).
    /// </summary>
    static float ComputeSkip(AudioClip clip)
    {
        if (clip == null) return 0f;
        var data = new float[clip.samples * clip.channels];
        if (!clip.GetData(data, 0)) return 0f;

        float peak = 0f;
        for (int i = 0; i < data.Length; i++) { float a = Mathf.Abs(data[i]); if (a > peak) peak = a; }
        if (peak <= 0f) return 0f;

        float thresh = peak * 0.45f;
        int idx = 0;
        for (int i = 0; i < data.Length; i++) if (Mathf.Abs(data[i]) >= thresh) { idx = i; break; }

        float t = (idx / clip.channels) / (float)clip.frequency;
        return Mathf.Max(0f, t - 0.004f);   // 4ms 백오프 — 트랜지언트 엣지 보존(클릭 방지)
    }
}
