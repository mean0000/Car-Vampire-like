// 공유 공격 토큰 풀 — 호드가 동시에 안 덤비게 하는 표준 군중 AI 기법(attack token).
//
// 흐름: 스포너가 풀 1개를 만들어 모든 CaniathroxChaser에 같은 참조를 주입한다.
//   - 적이 도착(lungeRange) → TryAcquire() 성공한 적만 Lunge(도약 공격) 발동.
//   - 토큰을 못 얻은 적은 Approach/서성만(접근 유지, 공격 안 함).
//   - 공격 사이클(Lunge→Spit→IdleAngry 복귀) 완료 시 Release() → 다음 적이 획득.
//   maxTokens=1이면 한 마리씩, =2면 두 마리까지 동시 공격. 6마리가 한꺼번에 덤비는 일은 없다.
//
// MonoBehaviour 아님(순수 C# 객체) — 씬/프리팹 구조를 건드리지 않고 스포너가 코드로만 소유·주입.
public class AttackTokenPool
{
    readonly int _maxTokens;
    int _inUse;

    public AttackTokenPool(int maxTokens)
    {
        _maxTokens = Mathf_Max(1, maxTokens);   // 최소 1 보장(0이면 아무도 공격 못 함)
    }

    /// <summary>공격 토큰 1개 획득 시도. 여분이 있으면 점유하고 true, 없으면 false.</summary>
    public bool TryAcquire()
    {
        if (_inUse >= _maxTokens) return false;
        _inUse++;
        return true;
    }

    /// <summary>점유한 토큰 반납. 공격 사이클 완료(IdleAngry 복귀) 시 호출.</summary>
    public void Release()
    {
        if (_inUse > 0) _inUse--;
    }

    // UnityEngine 의존 없이 동작하도록 Max만 로컬 정의(순수 객체 유지).
    static int Mathf_Max(int a, int b) => a > b ? a : b;
}
