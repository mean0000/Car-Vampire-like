using UnityEngine;

/// <summary>
/// 한 표면(흙·타일·금속…)의 발소리 풀. 데이터 주도 — 표면 추가 = 에셋 1개 생성(코드 무수정).
/// 걷기/뛰기 풀 분리(케이던스·체중감이 다름). 임포트한 Footsteps - Essentials의 *_Walk_*/*_Run_* 클립을 채운다.
///
/// surfaceId = 바닥 콜라이더 식별자(태그 or SurfaceTag 컴포넌트)와 매칭되는 키.
/// 매칭 실패 시 PlayerFootsteps의 defaultSet으로 폴백.
/// </summary>
[CreateAssetMenu(fileName = "Surface_", menuName = "ZombieCrush/Audio/Surface Footstep Set", order = 0)]
public class SurfaceFootstepSet : ScriptableObject
{
    [Tooltip("이 표면을 가리키는 키. 바닥의 SurfaceTag.surfaceId 또는 태그와 대소문자 무시 매칭. 예: DirtyGround, Tile, Metal.")]
    public string surfaceId = "DirtyGround";

    [Tooltip("걷기(저속) 스텝 풀. 매 스텝 직전-비반복 랜덤으로 1개 뽑는다.")]
    public AudioClip[] walkClips;

    [Tooltip("뛰기(고속) 스텝 풀. 비면 walkClips로 폴백.")]
    public AudioClip[] runClips;

    /// <summary>run 풀이 비어 있으면 walk로 폴백(부분 채움 안전).</summary>
    public AudioClip[] Pool(bool running)
    {
        if (running && runClips != null && runClips.Length > 0) return runClips;
        return walkClips;
    }
}
