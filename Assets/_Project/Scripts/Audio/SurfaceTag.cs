using UnityEngine;

/// <summary>
/// 바닥 콜라이더에 붙여 표면을 선언한다(선택). PlayerFootsteps가 지면 레이캐스트 hit의
/// 콜라이더에서 이 컴포넌트를 읽어 surfaceId를 얻는다 — Unity 태그 슬롯을 오염시키지 않는 표면 식별.
///
/// 없으면 PlayerFootsteps는 defaultSet으로 폴백한다(현재 _PlayerStackTest의 Ground처럼 미태깅 바닥).
/// </summary>
public class SurfaceTag : MonoBehaviour
{
    [Tooltip("이 바닥의 표면 키 — SurfaceFootstepSet.surfaceId와 매칭(대소문자 무시).")]
    public string surfaceId = "DirtyGround";
}
