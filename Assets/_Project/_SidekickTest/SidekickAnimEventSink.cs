using UnityEngine;

/// <summary>
/// throwaway 애니 테스트용 — RPG 카타나 클립에 박힌 'Hit' AnimationEvent를
/// 받아 "no receiver" 경고만 없앤다. 실제 데미지 로직 없음.
/// </summary>
public class SidekickAnimEventSink : MonoBehaviour
{
    public void Hit() { }
    public void Hit(AnimationEvent e) { }
}
