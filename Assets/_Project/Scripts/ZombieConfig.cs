using UnityEngine;   // ★ 필수 — ScriptableObject/CreateAssetMenu/Header

public enum ZombieType { General, Signal }

[CreateAssetMenu(menuName = "ZombieCrush/ZombieConfig")]
public class ZombieConfig : ScriptableObject
{
    [Header("Identity")]
    public ZombieType zombieType;

    [Header("Movement")]
    public float moveSpeed = 3f;
    public float acceleration = 6f;

    [Header("Health")]
    public int maxHP = 3;

    [Header("Detection - Sight")]
    public float sightRange = 12f;
    public float sightHalfAngle = 60f;

    [Header("Detection - Hearing")]
    public float hearingMultiplier = 1f;

    [Header("Combat")]
    public float attackRange = 1.5f;
    public float attackCooldown = 1.5f;
    public float attackDamage = 20f;   // ★ infection-noise §5.4 확정: 좀비 타격 20 (플레이어 100HP=5대 사망)

    [Header("Investigation")]
    public float investigateLookTime = 3f;
    public float investigateTimeout = 8f;

    [Header("Perception — 게이트0 확률 누적 인식 (combat-texture-foundation §2)")]
    [Tooltip("인식 판정 주기(초). 매 프레임이 아니라 틱 단위 — 개체별 위상 분산(스태거드).")]
    public float perceptionTickInterval = 0.2f;
    [Tooltip("시야 원뿔 안일 때 틱당 발각 확률(근거리 기준). 거리·플레이어 행동으로 스케일.")]
    [Range(0f, 1f)] public float detectBaseChance = 0.45f;
    [Tooltip("발각 성공 1틱당 게이지 증가량. 1.0 도달 = Chase 확정.")]
    public float detectGaugePerTick = 0.34f;
    [Tooltip("게이지가 이 값 이상이면 Alert(멈춰 응시 — '쟤가 날 봤나?' 텔레그래프).")]
    [Range(0f, 1f)] public float alertThreshold = 0.34f;
    [Tooltip("시야를 잃었을 때 게이지 감쇠(초당).")]
    public float detectDecayPerSec = 0.4f;
    [Tooltip("Alert 진입 후 멈춰 응시하는 시간(초). 1~2초 — 플레이어가 반응할 창.")]
    public float alertStareTime = 1.5f;
    [Tooltip("응시 후 느린 접근 속도 배수.")]
    public float alertApproachSpeedMult = 0.35f;
    [Tooltip("시야·청각 개체 분산(±). 같은 무리에도 눈 밝은 놈/장님이 섞인다(PZ Random).")]
    [Range(0f, 0.5f)] public float senseVariance = 0.3f;
    [Tooltip("Chase 진입 시 그르렁 전파 반경(m). 듣는 좀비는 Investigate로만 승급(떼어내기 보장).")]
    public float gruntRadius = 8f;

    [Header("Hit Reaction — 피격 사다리 (§6.2)")]
    [Tooltip("flinch: 모든 풀히트가 보장하는 짧은 경직(초). 한 발 = 적 행동 1회 거부(윈드업 취소).")]
    public float flinchDuration = 0.15f;
    [Tooltip("stagger: 사다리 2단 정지(초). 런지도 취소된다.")]
    public float staggerDuration = 0.4f;
    [Tooltip("knockdown: 넘어져 있는 시간(초). 다운 중 받는 피해 증가.")]
    public float knockdownDuration = 1.5f;
    [Tooltip("누적 윈도우(초) 안에서 풀히트 N번째에 stagger 발동.")]
    public int staggerAtHit = 3;
    [Tooltip("누적 윈도우 안에서 풀히트 N번째에 knockdown 발동.")]
    public int knockdownAtHit = 6;
    [Tooltip("연속 히트 누적이 유지되는 윈도우(초). 비우면 리셋.")]
    public float poiseWindow = 2.5f;
    [Tooltip("knockdown 발동마다 다음 사다리 문턱 배수(스턴락 방지 내성). 1 미만이면 내성이 거꾸로 사다리를 가속하므로 금지.")]
    [Min(1f)] public float resistGrowth = 1.5f;
    [Tooltip("이 시간(초) 동안 안 맞으면 내성이 1로 복귀.")]
    public float resistDecayTime = 4f;
    [Tooltip("다운 중 받는 피해 배수(다운 추가타 보상).")]
    public float downedDamageMult = 1.5f;

    [Header("Lunge & Grapple — 게이트0 ③ (§6.5)")]
    [Tooltip("이 거리(m) 안이면 윈드업 시작.")]
    public float lungeRange = 2.6f;
    [Tooltip("윈드업(초) — 상체 젖힘 + 사운드 큐. 0.4~0.6. 이 동안 맞으면 취소(한 발의 거부권).")]
    public float lungeWindup = 0.5f;
    [Tooltip("런지 돌진 속도(m/s). 방향은 윈드업 종료 순간 고정(커밋 — 회피 보상).")]
    public float lungeSpeed = 9f;
    [Tooltip("런지 지속(초). speed × duration ≈ 돌진 거리.")]
    public float lungeDuration = 0.35f;
    [Tooltip("런지가 빗나간 뒤 후딜(초) — 회피 성공의 보상 창.")]
    public float lungeRecover = 0.6f;
    [Tooltip("런지 중 이 거리(m) 안에 플레이어가 들어오면 접촉 = grapple.")]
    public float grappleContactRadius = 1.0f;
    [Tooltip("grapple 최대 홀드(초). 탈출 못 하면 추가 물기 후 놓아준다.")]
    public float grappleHold = 2.5f;
    [Tooltip("탈출 성공 시 좀비가 밀쳐지는 경직(초).")]
    public float grappleEscapeStagger = 0.8f;
    [Tooltip("탈출 성공 시 좀비 넉백(m/s).")]
    public float grappleEscapeKnockback = 4f;

    [Header("Signal")]
    public bool isSignalZombie;
    public float signalRadius = 15f;
    public float signalDelay = 3f;
    public int signalSummonCount = 4;
    public float signalCooldown = 15f;   // ★ 재소환 쿨다운 (하드코딩 제거)

    [Header("XP Drop")]
    public int xpOrbCountMin = 3;
    public int xpOrbCountMax = 5;

    [Header("Strain Drop (익스트랙션 루프)")]
    [Tooltip("사망 시 수확되는 strain. 비우면 RunManager 기본(생체 1성) 사용.")]
    public Run.StrainDef strainDrop;
}
