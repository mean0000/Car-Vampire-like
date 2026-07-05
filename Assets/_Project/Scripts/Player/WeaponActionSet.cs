using UnityEngine;

/// <summary>
/// ★무기 액션 1개의 데이터(통합 SO) — 판정·타이밍·VFX·사운드·Animator 진입·차징을 <see cref="WeaponActionData"/>
/// 공통 어휘로 묶는다. 스킬/반격/대시베기가 전부 이 한 타입 — "스킬 추가 = 에셋 하나 더 만들어 무기 슬롯에 꽂으면 끝"
/// (2026-07-05 교통정리 — 구 SkillSet(RMB 1슬롯 하드코딩)·KatanaWeapon 인라인 counter*/dashAttack* 필드를 대체).
///
/// 슬롯 바인딩(RMB/E/R/반격/대시베기)은 KatanaWeapon의 SerializeField 5개가 소유(유저 판정 2026-07-05 —
/// Loadout SO는 2번째 무기에서 실제 중복이 보일 때 추출). 런타임 상태(쿨다운·진행·워치독)는 무기 내부
/// ActionRuntime이 들고, ⚠️이 SO 필드에 런타임 기록 금지(공유 에셋 오염).
/// ★타이밍(타격/종료)은 여기 없다 — 클립 AnimationEvent가 소유(애니가 진실). animator 블록은 진입 트리거 이름만.
/// 에셋 위치: Assets/_Project/Data/Combat/Katana_*.asset.
/// </summary>
[CreateAssetMenu(fileName = "Katana_Action", menuName = "ZombieCrush/Weapon Action Set")]
public class WeaponActionSet : ScriptableObject
{
    [Tooltip("식별/문서용 이름 — 예: Katana_ChargeSkill, Katana_Counter.")]
    public string actionId = "Katana_Action";

    [Tooltip("★Animator 진입(트리거 이름) — 애니가 진실, SO는 어느 상태로 들어갈지만 명명.")]
    public WeaponAnimatorData animator = new WeaponAnimatorData();
    [Tooltip("타이밍(쿨다운·워치독).")]
    public WeaponTimingData timing = new WeaponTimingData();
    [Tooltip("판정(히트박스).")]
    public WeaponHitData hit = new WeaponHitData();
    [Tooltip("타격 순간 VFX — 비우면 없음(반격/대시베기는 현재 비움 = 기존 동작 보존).")]
    public WeaponVfxData vfx = new WeaponVfxData();
    [Tooltip("타격 순간 사운드 — 비우면 무음.")]
    public WeaponSfxData sfx = new WeaponSfxData();
    [Tooltip("차징(홀드→릴리스) — RMB 차징스킬만 enabled. 즉발 액션은 꺼둔다.")]
    public WeaponChargeData charge = new WeaponChargeData();
}
