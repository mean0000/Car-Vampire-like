using UnityEngine;

/// <summary>
/// 무기 판정이 때릴 수 있는 대상 — 적(좀비/몬스터)이 구현한다. 무기는 구체 적 클래스를 모르고
/// 이 인터페이스로만 타격해, 적 시스템 재정비(뱀서 피벗)와 무기 코드를 디커플한다.
/// </summary>
public interface IDamageable
{
    /// <summary>타격 적용. from = 가해 위치(넉백 방향 산출 기준), knockback = 초기 속도(m/s).</summary>
    void TakeHit(int damage, Vector3 from, float knockback);
}
