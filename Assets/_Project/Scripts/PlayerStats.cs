/// <summary>
/// 레벨업 기본 카드가 쌓아 올리는 전역 스탯 보정 레이어.
/// 각 소비처(PlayerCombat·PlayerController·XPManager·XPOrb·PartPickup)가 자기 기본값에
/// 이 배수/가산을 곱해 읽는다. 카드 효과를 한 곳에 모아 시스템을 단순하게 유지한다.
///
/// 정적이므로 씬 재로드 후에도 값이 남는다 → PlayerController.Awake에서 Reset()을 호출해
/// 매 런 깨끗하게 시작한다(런 = 씬 1회).
/// </summary>
public static class PlayerStats
{
    public static int   DamageBonus;        // 강선: 사격 피해 가산 (+1/+2/+3)
    public static float FireRateMult;       // 속사: 연사 속도 배수 (쿨다운 = base / 이 값)
    public static float MoveSpeedMult;      // 경량화: 이동 속도 배수
    public static float MaxHPMult;          // 강화 골격: 최대 체력 배수
    public static float PickupRadiusMult;   // 자력 채집: 부품·XP 흡수 반경 배수
    public static float XPGainMult;         // 효율 흡수: XP 획득량 배수

    static PlayerStats() => Reset();

    /// <summary>매 런 시작 시 기본값(보정 없음)으로 초기화.</summary>
    public static void Reset()
    {
        DamageBonus      = 0;
        FireRateMult     = 1f;
        MoveSpeedMult    = 1f;
        MaxHPMult        = 1f;
        PickupRadiusMult = 1f;
        XPGainMult       = 1f;
    }
}
