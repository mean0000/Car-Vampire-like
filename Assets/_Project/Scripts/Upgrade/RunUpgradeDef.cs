using UnityEngine;

namespace Upgrade
{
    /// <summary>
    /// 인-런 레벨업 카드 1장. levelup_catalog 구조의 익스트랙션 적응.
    /// 효과는 PlayerStats mod 레이어에 적용(기존 카드와 동일 경로) — 신규 스탯 훅은 effectKind로 분기.
    /// ※카드 *내용*(수치·문구) 권위 = docs/00_authority/2026-05-31-levelup-cards-catalog.md.
    ///   Phase 2가 그 ~23장을 이 SO 에셋으로 입력. 본 클래스는 데이터 *모양*만 정의한다.
    /// </summary>
    [CreateAssetMenu(menuName = "ZombieCrush/Run/RunUpgradeDef", fileName = "RunUpgradeDef")]
    public class RunUpgradeDef : ScriptableObject
    {
        [Tooltip("카드 식별자(드로우·소유 추적 키). 카탈로그 내 고유.")]
        public string id;

        [Tooltip("표시 이름.")]
        public string displayName;

        [Tooltip("Lv1~maxLevel 청크 설명(기존 AbilityDescByLevel 구조).")]
        [TextArea] public string[] descByLevel;

        [Tooltip("이 카드의 최대 레벨(만렙). catalog 기준 카드당 Lv3.")]
        public int maxLevel = 3;

        /// <summary>자원축(진화 공명용). catalog §자원축 = 소음/시계/킬/기동.</summary>
        public enum ResourceAxis { Noise, Clock, Kill, Mobility, None }

        [Tooltip("자원축 — 같은 축 동사 2개 + 만렙 시 진화 게이트(Phase 2).")]
        public ResourceAxis axis = ResourceAxis.None;

        /// <summary>효과 종류 — PlayerStats 어느 mod 레이어를 건드리는지(Phase 2 확장 가능).</summary>
        public enum EffectKind { FireRate, Damage, MaxHP, MoveSpeed, PickupRadius, XPGain }

        [Tooltip("효과 종류. PlayerStats 적용 분기 키.")]
        public EffectKind effectKind;

        [Tooltip("레벨별 누적 최종값(델타 아님 — 기존 *ByLevel 배열과 동일 규약). 인덱스 0=Lv1.")]
        public float[] valueByLevel;

        [Header("진화 (post-MVP 훅 — catalog §진화 게이트)")]
        [Tooltip("시그니처 카드 여부(동사 슬롯 가중). 선택 무기 장착 시에만 등장.")]
        public bool isSignature;

        [Tooltip("진화 결과 카드. Lv 만렙 + 같은 axis 동사 2개 → 진화(Phase 2).")]
        public RunUpgradeDef evolvesInto;
    }
}
