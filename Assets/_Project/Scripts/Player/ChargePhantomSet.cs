using UnityEngine;

/// <summary>
/// ★Skill01 차징 팬텀 데이터(통합 SO) — 차징 윈드업 중 전방으로 슉슉슉 방출하는 *베는 잔상*의 정의.
/// <see cref="SkillSet"/>·<see cref="ComboAttackSet"/>과 같은 데이터 주도 규약: 팬텀 셋이 늘면 이 SO를 하나 더 만들어 채우면 끝(코드 무수정).
/// <see cref="ChargePhantomEmitter"/>가 phantomSet으로 참조. ★타이밍(윈드업 경계)은 여기 없다 — 애니가 진실(KatanaWeapon.IsChargeWindup).
///
/// 통합이지만 Inspector에서 접이식 블록(phantoms/emission/ghost/slashVfx)으로 분리돼 보인다.
/// ★phantoms = 이름 붙은 팬텀 애니 라이브러리(각 원소를 이름으로 *구분*) — 방출은 emission이 이 라이브러리를 순서대로(랜덤 아님) 참조.
/// 에셋 네이밍/위치: Assets/_Project/VFX/Katana_Cham_Skill01PhantomSet.asset 등(콤보 규약).
/// ⚠️ 런타임에 이 SO 필드를 쓰지 말 것(공유 에셋 오염) — Emitter는 스폰 시 로컬 사본만 읽는다.
/// </summary>
[CreateAssetMenu(fileName = "Katana_Cham_Skill01PhantomSet", menuName = "ZombieCrush/Charge Phantom Set")]
public class ChargePhantomSet : ScriptableObject
{
    [Tooltip("식별/문서용 이름 — 예: Katana_Skill01_Phantom.")]
    public string setName = "Katana_Skill01_Phantom";

    [Tooltip("★팬텀 애니(베기 플립북) 라이브러리 — 각 원소를 name으로 구분(인스펙터 헤더). 방출 순서/개수는 아래 emission이 결정.")]
    public PhantomAnim[] phantoms;

    [Tooltip("방출(개수·순서·전진·수명).")]
    public EmissionData emission = new EmissionData();
    [Tooltip("고스트(색·알파·페이드).")]
    public GhostData ghost = new GhostData();
    [Tooltip("슬래시 VFX — 차징 중 팬텀마다(onCharge, 각 PhantomAnim의 slash* 오프셋) + 실제 베기(onCut, 아래 cut 오프셋+delay)에 1발. 프리팹/수명/속도는 공용.")]
    public SlashVfxData slashVfx = new SlashVfxData();

    /// <summary>팬텀 애니 1개 = 한 공격(예: Attack01)의 스윙 연속 프레임 + 그 공격 슬래시 VFX 정합값.</summary>
    [System.Serializable]
    public class PhantomAnim
    {
        [Tooltip("★구분용 이름(인스펙터 헤더로 표시) — 예: Attack01. 비우면 'Element N'.")]
        public string name = "Attack";
        [Tooltip("★이 팬텀 애니 사용 여부(boolean 선택) — 끄면 방출에서 제외된다. 켠 것들만 순서대로 순환/방출.")]
        public bool enabled = true;
        [Tooltip("스윙 연속 프레임(순서대로). 한 공격 1개분(에디터 프리베이크 메시).")]
        public Mesh[] frames;
        [Tooltip("★[차징 슬래시 타이밍] 이 공격 플립북의 *슬래시 '딱' 프레임* — VFX가 이 프레임 도달 시 발동(프레임 정확). 0=첫 프레임 … frames개수-1=마지막. Play 보며 베는 순간 프레임에 맞춰라(콤보 AnimationEvent의 플립북판).")]
        [Min(0)] public int slashFrame = 4;
        [Tooltip("★[차징 슬래시 전용] 이 팬텀 동반 슬래시 VFX 방향 정합(deg) — 모션마다 스윙 평면이 달라 각자 맞춘다. 검 앵커 회전 기준. (slashVfx.onCharge일 때 사용)")]
        public Vector3 slashEulerOffset;
        [Tooltip("★[차징 슬래시 전용] 위치 오프셋(검 앵커 로컬).")]
        public Vector3 slashPosOffset;
        [Tooltip("★[차징 슬래시 전용] 스케일(0이면 1).")]
        public float slashScale;
    }

    [System.Serializable]
    public class EmissionData
    {
        [Tooltip("★차징당 팬텀 개수(emissionOrder가 비었을 때만). 윈드업 동안 균등 분포. 라이브러리를 순서대로 순환(0,1,2,0,…).")]
        [Min(1)] public int phantomCount = 3;
        [Tooltip("★방출 순서 직접 지정 — phantoms 인덱스를 원하는 순서로(예: 0,2,1,0). 비우면 phantomCount만큼 순서대로 순환. 채우면 개수=이 배열 길이. (랜덤 없음 — 항상 결정적.)")]
        public int[] emissionOrder = System.Array.Empty<int>();
        [Tooltip("윈드업(차징) 길이 추정(초) — 프레임 0→49 @60fps ≈ 0.82. 방출 간격 = 이 시간 / 팬텀 개수.")]
        [Min(0.05f)] public float windupDuration = 0.82f;
        [Tooltip("팬텀 1장 수명(초) — 슉→딱→스르륵 전체. 짧을수록 스냅.")]
        public float lifetime = 0.6f;
        [Tooltip("팬텀 전진 거리(m) — 짧게. 0.5 ≈ 바로 앞.")]
        public float travelRange = 0.5f;
        [Tooltip("전진·슬래시가 완료되는 수명 비율(0~1). 이후 마지막 프레임 유지하며 페이드.")]
        [Range(0.2f, 1f)] public float slashFraction = 0.8f;
        [Tooltip("좌우 산란(±m) — 0이면 일직선.")]
        public float scatter = 0f;
    }

    [System.Serializable]
    public class GhostData
    {
        [Tooltip("페이드 시작 수명 비율(0~1). 이 전까지 풀 알파, 이후 0으로 스르륵.")]
        [Range(0f, 1f)] public float fadeStart = 0.6f;
        [Tooltip("고스트 색(시안=액션 규약, HDR — 블룸 통과).")]
        [ColorUsage(true, true)] public Color color = new Color(0.3f, 1.4f, 1.7f, 1f);
        [Tooltip("고스트 풀 알파(스냅 시작값).")]
        [Range(0f, 1f)] public float startAlpha = 0.85f;
    }

    [System.Serializable]
    public class SlashVfxData
    {
        [Tooltip("슬래시 VFX 프리팹(차징·베기 공용). 비우면 둘 다 없음.")]
        public GameObject prefab;
        [Tooltip("★차징 중 팬텀마다 슬래시 발동(각 PhantomAnim의 slash* 오프셋 + slashFrame 타이밍 사용) — 원래 동작.")]
        public bool onCharge = true;
        [Tooltip("★실제 베기(DoSkillHit='칼 벨 때')에 1발 발동(아래 cut 오프셋+delay 사용).")]
        public bool onCut = true;
        [Tooltip("★[베기 전용] 발동 지연(초) — 베는 타격 기준. 0=정확히 벨 때, >0=조금 늦게(타이밍 조절).")]
        public float delay = 0f;
        [Tooltip("[베기 전용] 방향 정합(deg) — 검(weaponAnchor) 회전 기준(스윙 평면 맞춤).")]
        public Vector3 eulerOffset;
        [Tooltip("[베기 전용] 위치 오프셋 — 검(weaponAnchor) 로컬.")]
        public Vector3 posOffset;
        [Tooltip("[베기 전용] 스케일 배수(0이면 1).")]
        public float scale = 1f;
        [Tooltip("[공용] 슬래시 자동 소멸(초, 0이면 0.5).")]
        public float lifetime = 0.5f;
        [Tooltip("[공용] 재생 속도 배수 — 파티클 simulationSpeed에 곱(0이면 1).")]
        public float playbackSpeed = 1.5f;
        [Tooltip("[베기 전용] 켜면 베기 슬래시를 검(weaponAnchor)에 부모로 붙여 스윙 따라 쓸리게. (차징 슬래시는 팬텀 스폰 기준 고정이라 미적용.)")]
        public bool parentToWeapon = false;
    }
}
