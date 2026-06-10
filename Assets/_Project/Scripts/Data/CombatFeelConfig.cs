using UnityEngine;

/// <summary>
/// 전투 질감 파운데이션(게이트0) 플레이어 측 수치 — 3계층 탄 판정 + 히트스탑.
/// 권위: docs/00_authority/2026-06-10-combat-texture-foundation.md §6.2.
/// 로직(불변)과 수치(가변)를 분리한다(생산 헌장 §1.3) — 랩 튜닝은 이 에셋만 만진다.
/// </summary>
[CreateAssetMenu(menuName = "ZombieCrush/CombatFeelConfig")]
public class CombatFeelConfig : ScriptableObject
{
    [Header("3계층 탄 판정 — 탄도선↔좀비 축 최단 수평거리 d (m)")]
    [Tooltip("d ≤ 이 값 = 풀히트. 풀데미지 + 넉백 + 피격 사다리. 탄 정지.")]
    [Min(0.01f)] public float fullHitRadius = 0.4f;
    [Tooltip("풀히트 < d ≤ 이 값 = 스침. 데미지 50% + flinch만(넉백·사다리 없음). 탄 정지.")]
    [Min(0.01f)] public float grazeRadius = 0.7f;
    [Tooltip("스침 < d ≤ 이 값 = 그레이즈. 소량 데미지 + 비틀 확률. ★탄은 계속 날아간다(미스에 가까운 스치기).")]
    [Min(0.01f)] public float nearMissRadius = 1.0f;
    [Tooltip("스침 데미지 배수.")]
    [Min(0f)] public float grazeDamageMult = 0.5f;
    [Tooltip("그레이즈 데미지 배수(최소 1 보장).")]
    [Min(0f)] public float nearMissDamageMult = 0.2f;
    [Tooltip("그레이즈가 flinch(비틀)를 일으킬 확률(0~1).")]
    [Range(0f, 1f)] public float nearMissFlinchChance = 0.35f;

    [Header("히트스탑 — 피격자만 정지(전역 timeScale 금지)")]
    [Tooltip("일반 히트: 피격자 애니·이동 정지 시간(초). 30~50ms.")]
    public float hitStopNormal = 0.04f;
    [Tooltip("킬샷: 시체가 이 시간 동안 프리즈된 뒤 죽음 연출(초). 80~120ms.")]
    public float hitStopKill = 0.1f;

    [Header("킬 피드백 클램프 — 대량 정화 시 카메라 발작·오디오 폭주 방지")]
    [Tooltip("킬 사운드·킬 펀치를 이 시간창(초)당 1회로 클램프. 초과분 병합.")]
    public float killFeedbackWindow = 0.15f;

    void OnValidate()
    {
        // 판정 반경의 순서(풀히트 ≤ 스침 ≤ 그레이즈)가 무너지면 계층이 사라진다 — 에디터에서 강제.
        grazeRadius = Mathf.Max(grazeRadius, fullHitRadius);
        nearMissRadius = Mathf.Max(nearMissRadius, grazeRadius);
    }
}
