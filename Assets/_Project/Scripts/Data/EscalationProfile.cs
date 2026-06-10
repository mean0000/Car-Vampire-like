using UnityEngine;

namespace Run
{
    /// <summary>
    /// 경과 시간(분) → 초당 스폰 수 곡선. ★디렉터 seam: Phase 2 스폰 디렉터가 이걸 입력으로 받아
    /// 구식 스포너를 교체한다(헌장 부록 A). 스켈레톤에선 ZombieSpawner가 직접 평가.
    /// </summary>
    [CreateAssetMenu(menuName = "ZombieCrush/Run/EscalationProfile", fileName = "EscalationProfile")]
    public class EscalationProfile : ScriptableObject
    {
        [Tooltip("X=경과 분, Y=초당 스폰 수. 완만 우상향 — 시간이 갈수록 압박이 커진다.")]
        public AnimationCurve spawnsPerSecondByMinute = new AnimationCurve(
            new Keyframe(0f, 0.4f),
            new Keyframe(5f, 0.8f),
            new Keyframe(10f, 1.4f),
            new Keyframe(15f, 2.2f));

        /// <summary>경과 시간(초)에서의 초당 스폰 수.</summary>
        public float SpawnsPerSecondAt(float elapsedSeconds)
        {
            float minutes = elapsedSeconds / 60f;
            return Mathf.Max(0f, spawnsPerSecondByMinute.Evaluate(minutes));
        }
    }
}
