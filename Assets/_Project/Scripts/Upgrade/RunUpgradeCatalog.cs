using System.Collections.Generic;
using UnityEngine;

namespace Upgrade
{
    /// <summary>
    /// 인-런 카드 풀 + 드로우 가중. LevelUpChoiceUI가 하드코딩 배열 대신 이걸 읽는다(Phase 2 데이터 훅).
    /// ※카드 *내용* 권위 = docs/00_authority/2026-05-31-levelup-cards-catalog.md.
    ///   드로우 알고리즘(가중 추첨·만렙 제외·진화 유도)은 Phase 2 구현 — 본 SO는 데이터 모양 + 진입점만.
    /// </summary>
    [CreateAssetMenu(menuName = "ZombieCrush/Run/RunUpgradeCatalog", fileName = "RunUpgradeCatalog")]
    public class RunUpgradeCatalog : ScriptableObject
    {
        [Tooltip("기본축 카드(catalog §기본 8). 초반 가중이 높다.")]
        public RunUpgradeDef[] basics;

        [Tooltip("동사축 카드(catalog §동사 10). 후반 가중이 높다. 시그니처도 여기 섞인다.")]
        public RunUpgradeDef[] verbs;

        [Tooltip("레벨대별 동사 비중(X=플레이어 레벨, Y=동사 가중 0~1). catalog: Lv1~3=0.25, Lv4~6=0.45, Lv7+=0.60.")]
        public AnimationCurve verbWeightByLevel = new AnimationCurve(
            new Keyframe(1f, 0.25f),
            new Keyframe(4f, 0.45f),
            new Keyframe(7f, 0.60f));

        /// <summary>
        /// 레벨업 시 count장 드로우(만렙 카드 제외, 가중 적용).
        /// [판정 필요 — Phase 2] 가중 추첨/만렙 제외/티어 희귀도 보정/진화 유도 알고리즘.
        /// 스펙 §5.2가 본문을 의도적으로 비워둠("Phase 2 구현"). 임의 알고리즘을 지어내지 않고
        /// 컴파일 가능한 빈 결과를 반환한다 — Phase 2 착수 시 가중 로직 합의 후 구현.
        /// </summary>
        public RunUpgradeDef[] Draw(int count, int playerLevel, IDictionary<string, int> ownedLevels)
        {
            // INTEGRATION (Phase 2): 가중 추첨 로직 구현 지점.
            return System.Array.Empty<RunUpgradeDef>();
        }
    }
}
