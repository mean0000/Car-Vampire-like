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

    [Header("죽음의 스펙터클 — 터짐·쓰러짐·와해 (디자인 나침반 §3.1)")]
    [Tooltip("머리/모자 팝 비산 거리(m). 20m 카메라에서 읽히려면 과장 필요(아트 검토: 3~4m).")]
    public float headPopDistance = 3f;
    [Tooltip("시체 잔류 시간(초) — 쓰러진 채 남아 '무게'를 만든다. 이후 나노봇 와해로 정화.")]
    public float corpseLinger = 6f;
    [Tooltip("동시 시체 상한 — 초과하면 오래된 시체부터 와해(호드 성능 보호).")]
    public int corpseMax = 18;
    [Tooltip("와해 연출 시간(초) — 시안 버스트 + 축소.")]
    public float dissolveTime = 0.3f;
    [Tooltip("킬 링 펄스 최대 지름(m) — '죽였다'의 원거리 확인 신호(시안).")]
    public float killRingSize = 1.8f;

    // ── B-004 트랜지언트의 행렬 (2026-06-12-b004-rapidfire-spec) ──
    // 연사가 균질 루프(매 발 동일 플래시·소리·잔광)면 무게가 죽는다 — 발당 개별성 + 주기적 박자.
    // ★기존 .asset에 없는 신규 필드 — 이니셜라이저 값으로 디시리얼라이즈된다(0으로 안 떨어짐).
    [Header("B-004 트랜지언트의 행렬 — 마스터 토글(C1~C3 일괄 롤백)")]
    [Tooltip("끄면 C1(트레이서 박자)·C2(머즐 변조)·C3(사운드 행렬)이 전부 꺼져 볼트 이전 연사로 롤백.")]
    public bool transientMatrixEnabled = true;

    [Header("C1 — 트레이서 박자 (N발당 1발 강조)")]
    [Tooltip("N발당 1발이 강조발(굵은 글로우 궤적 + 머즐 강조 동기). 1이면 매 발 강조 = 박자 소멸.")]
    [Min(1)] public int tracerCadence = 4;
    [Tooltip("강조발 궤적 폭(m) — 굵은 가산 글로우(예광탄).")]
    [Min(0.001f)] public float tracerEmphasisWidth = 0.07f;
    [Tooltip("일반발 궤적 폭(m) — 얇은 잔광(유저 픽 2026-06-11 값의 재배치).")]
    [Min(0.001f)] public float tracerNormalWidth = 0.015f;

    [Header("C2 — 머즐 변조 + 광원 스트로브")]
    [Tooltip("머즐 플래시 크기 발당 지터(±비율).")]
    [Range(0f, 1f)] public float flashSizeJitter = 0.2f;
    [Tooltip("머즐 라이트 강도 발당 지터(±비율) — 부감에선 바닥에 깜빡이는 광원이 본체.")]
    [Range(0f, 1f)] public float lightIntensityJitter = 0.25f;
    [Tooltip("강조발(C1과 같은 발) 머즐 크기·광량 배수.")]
    [Min(1f)] public float emphasisMuzzleMult = 1.3f;

    [Header("C3 — 사운드 행렬")]
    [Tooltip("발사음 발당 피치 지터(±비율) — 기계적 반복 제거(SfxOneShot과 같은 문법).")]
    [Range(0f, 0.5f)] public float shotPitchJitter = 0.05f;
    [Tooltip("연사 종료 테일(잔향) 볼륨 — '연사가 끝났다'의 마침표. 0이면 끔.")]
    [Range(0f, 1f)] public float tailVolume = 0.08f;
    [Tooltip("명중 thud 볼륨 — 발사음과 분리된 확인 채널(CoD 히트마커 문법). 보수적(못 듣는 원칙). 0이면 끔.")]
    [Range(0f, 1f)] public float thudVolume = 0.06f;

    // ── Feel 레이어 — 카메라 킥·쉐이크·전역 히트스탑 (2026-06-12 채널맵 docs/03_reference/2026-06-12-feel-layer-channel-map.md) ──
    // 킥 예약제: 카메라 킥은 ①킬(펀치인—ZombieController 소관) ②수렴샷 ③차지샷만. 연사 매발 킥 기각(스펙 §5).
    // ★기존 .asset에 없는 신규 필드 — 이니셜라이저가 진실. 씬 SerializeField 시절 값 0 덮어쓰기(히트스탑 사망)를 이것으로 해독.
    [Header("Feel — 카메라 킥·쉐이크 (예약제)")]
    [Tooltip("발사 시 카메라 쉐이크 세기(m). 작게 — 연사 시 잔잔한 럼블. 샷건은 자동 2배.")]
    [Min(0f)] public float fireShake = 0.06f;
    [Tooltip("발사 시 카메라 킥(m) — 반동 반대 방향으로 밀렸다 복귀. 묵직함의 주력 채널. 샷건은 자동 1.8배.")]
    [Min(0f)] public float fireKick = 0.1f;
    [Tooltip("카메라 충격 예약제: 발사간격이 이 값(초) 이상인 무거운 단발(또는 산탄)만 카메라를 친다. " +
             "연사류는 카메라 충격 0 — 손맛은 히트스탑·머즐·사운드 채널 담당(Hades/DOOM 모델). " +
             "충격 유무 자체가 무기 계열의 촉각 언어(묵직=킥/속사=무킥).")]
    [Min(0f)] public float heavyKickMinCooldown = 0.3f;
    [Tooltip("수렴샷 카메라 킥 배수 — 예약제 무관, 의도된 한 발은 항상 묵직.")]
    [Min(1f)] public float convergedKickMult = 1.4f;
    [Tooltip("차지샷 카메라 킥(m) — 킥 예약 ③. charge01 비례 적용(최소차지 0.2배 약킥 → 풀차지 전량). " +
             "위계: 샷건 0.18 > 풀차지 0.16 > 수렴 0.14 > 무거운 단발 0.1.")]
    [Min(0f)] public float chargeKick = 0.16f;

    // ⚠️ 전역 히트스탑 = 기본 비활성(0). 2026-06-13: SO 이주로 부활(0.06/0.11)시켰더니 InMission에서
    // timeScale 0.05가 잔류해 "거의 멈춤"(마우스만 정상) 회귀 발생. HitStop은 이 게임에서 씬이 0으로
    // 덮어써 한 번도 작동한 적 없던 미검증 경로 — B-004 §5 "글로벌 히트스탑 기각 유지" 정합. 재활성은
    // 노브 세션 판정 + HitStop.cs 잔류 가드 견고화 후. 필드·Range는 노브로 보존(0이면 HitStop.Do 무시).
    [Header("Feel — 전역 히트스탑 (기본 OFF — 노브 판정 대기, 시간의 사다리: 수렴 60ms < Zone 0.2s < 산데)")]
    [Tooltip("수렴샷 '풀히트' 순간 전역 히트스탑(초). 0=OFF(현행). 연사엔 절대 안 걸림 — 시간의 사다리 최하단.")]
    [Range(0f, 0.15f)] public float convergedHitStop = 0f;
    [Tooltip("수렴샷 '킬' 히트스탑(초). 0=OFF(현행). 켜면 일반 명중과 차별되는 '이 킬은 달랐다'.")]
    [Range(0f, 0.2f)] public float convergedKillHitStop = 0f;

    [Header("Feel — 발사 사운드 볼륨 (씬 0.65가 라이브 값 — 구 코드 default 0.185는 씬에 가려 들린 적 없음)")]
    [Tooltip("발사 사운드 볼륨.")]
    [Range(0f, 1f)] public float shotVolume = 0.65f;

    void OnValidate()
    {
        // 판정 반경의 순서(풀히트 ≤ 스침 ≤ 그레이즈)가 무너지면 계층이 사라진다 — 에디터에서 강제.
        grazeRadius = Mathf.Max(grazeRadius, fullHitRadius);
        nearMissRadius = Mathf.Max(nearMissRadius, grazeRadius);
    }
}
