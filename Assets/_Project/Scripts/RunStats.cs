using UnityEngine;

/// <summary>
/// 한 런(=씬 1회) 동안의 집계 — 경과 시간(런 타이머)과 처치 수(킬 카운터).
/// HUD가 폴링한다(이벤트 불필요 — 값이 매 프레임 바뀌는 타이머 + 단순 정수 카운터).
/// 정적 상태가 아니라 MonoBehaviour 싱글톤이므로 씬 재로드마다 새 인스턴스 = 암묵적 리셋.
/// </summary>
public class RunStats : MonoBehaviour
{
    public static RunStats Instance { get; private set; }

    /// <summary>런 시작부터의 누적 경과 시간(초). HUD에서 MM:SS로 포맷.</summary>
    public float ElapsedTime { get; private set; }

    /// <summary>이번 런의 좀비 처치 누적 수.</summary>
    public int Kills { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        ElapsedTime += Time.deltaTime;
    }

    /// <summary>좀비가 죽을 때 ZombieController가 호출. 처치 수 +1.</summary>
    public void AddKill()
    {
        Kills++;
    }
}
