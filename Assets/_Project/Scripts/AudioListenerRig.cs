using UnityEngine;

/// <summary>
/// AudioListener 분리 리그(B-005a, 사운드 프렙 스펙 §2.1) — 탑다운 제1 함정 해소.
/// 리스너가 카메라(15m 상공)에 있으면 모든 3D 소리가 멀고 정위가 카메라 기준으로 뒤틀림 —
/// 귀는 캐릭터의 것(탑뷰 삼위일체): 위치 = 플레이어, 회전 = 카메라 yaw만.
/// ⚠️ 전용 GO에만 붙일 것([[runloop-pitfalls]] — 기존 GO에 얹기 금지).
/// </summary>
public class AudioListenerRig : MonoBehaviour
{
    Camera _cam;   // Camera.main 캐시 — 매 프레임 태그 검색 방지

    void LateUpdate()
    {
        if (_cam == null) _cam = Camera.main;

        var player = PlayerController.Instance;
        if (player != null)
            transform.position = player.transform.position;
        else if (_cam != null)
            transform.position = _cam.transform.position;   // 플레이어 부재 폴백(메뉴/관전 등)

        // 회전은 카메라 yaw만 — 피치(58°)까지 따라가면 좌우 정위가 위아래로 샌다.
        if (_cam != null)
            transform.rotation = Quaternion.Euler(0f, _cam.transform.eulerAngles.y, 0f);
    }
}
