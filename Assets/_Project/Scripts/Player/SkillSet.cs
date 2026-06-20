using UnityEngine;

/// <summary>
/// ★스킬 1개의 데이터(통합 SO) — 판정·타이밍·슬래시/VFX·사운드를 한 에셋에 묶되, 중첩 그룹으로 **구분**한다.
/// <see cref="ComboAttackSet"/>과 같은 데이터 주도 규약: 스킬이 늘면 이 SO를 하나 더 만들어 채우면 끝(코드 무수정).
/// KatanaWeapon이 skillSet으로 참조. ★타이밍(타격/종료)은 여기 없다 — 클립 AnimationEvent가 소유(애니가 진실).
///
/// 통합이지만 Inspector에서 4개 접이식 블록(hit/timing/vfx/sfx)으로 분리돼 보인다. vfx가 구 SlashSet에 해당.
/// 에셋 네이밍/위치: Assets/_Project/VFX/Katana_Cham_Skill01Set.asset 등(콤보 규약).
/// </summary>
[CreateAssetMenu(fileName = "Katana_Cham_SkillSet", menuName = "ZombieCrush/Skill Set")]
public class SkillSet : ScriptableObject
{
    [Tooltip("식별/문서용 이름 — 예: Katana_Skill01.")]
    public string skillName = "Katana_Skill01";

    [Tooltip("판정(히트박스).")]
    public HitData hit = new HitData();
    [Tooltip("타이밍(쿨다운·워치독).")]
    public TimingData timing = new TimingData();
    [Tooltip("★슬래시/VFX (타격 순간) — 구 SlashSet에 해당하는 부분.")]
    public VfxData vfx = new VfxData();
    [Tooltip("사운드 (타격 순간).")]
    public SfxData sfx = new SfxData();

    [System.Serializable]
    public class HitData
    {
        [Tooltip("사거리(m).")] public float range = 3.5f;
        [Tooltip("부채꼴 半각(deg).")] public float arcHalfAngle = 80f;
        [Tooltip("히트존 원점 전진(조준 방향 m).")] public float forwardOffset = 0.5f;
        [Tooltip("데미지.")] public int damage = 20;
        [Tooltip("넉백(m/s).")] public float knockback = 7f;
    }

    [System.Serializable]
    public class TimingData
    {
        [Tooltip("쿨다운(초). 0이면 애니가 허용하는 한 즉시 재사용(테스트용) — 밸런스 시 올린다.")]
        public float cooldown = 0f;
        [Tooltip("안전 워치독(초) — 클립 길이+여유. OnComboEnd 누락 시 강제 종료(반 소프트락 방지).")]
        public float maxDuration = 3.5f;
    }

    /// <summary>스킬 VFX 정합 기준 — 불렛(플레이어 전방 발사)과 슬래시(무기 휘두름)는 완전히 다른 정합이 필요.</summary>
    public enum VfxBasis { Player, Weapon }

    [System.Serializable]
    public class VfxData
    {
        [Tooltip("★기준 — Player: 플레이어 위치+조준(불렛/전방 발사, z=앞) / Weapon: 무기(칼) 앵커(슬래시 — 휘두름 따라, 콤보 슬래시와 동일).")]
        public VfxBasis basis = VfxBasis.Player;
        [Tooltip("VFX 프리팹 — 비우면 없음. basis 기준으로 스폰.")]
        public GameObject prefab;
        [Tooltip("기준(basis) 회전 기준 미세 정합(deg).")]
        public Vector3 eulerOffset;
        [Tooltip("기준-로컬 위치 오프셋(m). Player=조준 프레임(z=앞·x=우·y=위) / Weapon=칼 로컬.")]
        public Vector3 posOffset;
        [Tooltip("스케일 배수(0이면 1).")] public float scale = 1f;
        [Tooltip("재생 속도 배수 — 파티클 simulationSpeed에 곱(0이면 1).")] public float playbackSpeed = 1f;
        [Tooltip("자동 소멸(초, 0이면 1.5).")] public float lifetime = 1.5f;
    }

    [System.Serializable]
    public class SfxData
    {
        [Tooltip("사운드 클립(2D, 거리감쇠 없음) — 비우면 무음.")]
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 0.7f;
    }
}
