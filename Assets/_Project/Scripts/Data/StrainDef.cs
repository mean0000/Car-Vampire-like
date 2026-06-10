using UnityEngine;

namespace Run
{
    /// <summary>
    /// 정화 산물 strain의 정의. 스켈레톤은 "생체 1성" 1종뿐 — 계열/등급은 Phase 2가 에셋 추가로 분화한다.
    /// 저장 키 = 에셋 이름(StrainDef.name). 따라서 에셋 파일명을 함부로 바꾸면 세이브가 깨진다.
    /// </summary>
    [CreateAssetMenu(menuName = "ZombieCrush/Run/StrainDef", fileName = "StrainDef")]
    public class StrainDef : ScriptableObject
    {
        [Tooltip("계열 이름(스켈레톤='생체'). Phase 2에서 5계열로 분화.")]
        public string family = "생체";

        [Tooltip("등급(성). 스켈레톤=1. Phase 2에서 확률 등급 분화.")]
        [Min(1)] public int grade = 1;

        [Tooltip("UI 표기명(예: '생체 1성'). 비우면 family로 폴백.")]
        public string displayName = "생체 1성";

        /// <summary>UI에 노출할 이름. displayName이 비어 있으면 family를 쓴다.</summary>
        public string DisplayName => string.IsNullOrEmpty(displayName) ? family : displayName;
    }
}
