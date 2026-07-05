using UnityEngine;

/// <summary>
/// ★사망 연출 소유 계약 — <see cref="EnemyDamageReceiver.Die"/>가 즉시 SetActive(false) 하는 대신,
/// 이 계약이 붙어 있고 스테이징을 수락하면(반환 true) *비활성화를 연출이 소유*한다(연출 끝에 스스로 끔).
/// 죽음 연출 티어(06-19 스펙: 잡몹 싼죽음/엘리트 디졸브/보스 파쇄)의 공용 이음새 — 첫 구현 = 방향성 붕괴
/// (<see cref="DirectionalCollapse"/>, 거합 문법). 거절(반환 false — 비활성/미배선)이면 기존 즉시 소멸 폴백.
/// </summary>
public interface IDeathStager
{
    /// <summary>사망 순간 호출. hitFrom = 치명타의 가해 원점(붕괴 방향 = 몸 − hitFrom).
    /// true = 연출이 시체를 인수(수신기는 GO를 끄지 않는다) / false = 거절(수신기가 즉시 비활성).</summary>
    bool StageDeath(Vector3 hitFrom);
}
