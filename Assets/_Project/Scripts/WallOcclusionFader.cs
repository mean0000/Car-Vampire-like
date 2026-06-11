using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 차단벽 디졸브 드라이버 — 카메라↔플레이어 사이를 실제로 가리는 Obstacle 벽만, 가리는 동안만
/// 디더 고스트로 페이드. (2026-06-11 유저 콜: 전역 높이 컷은 "벽이라는 느낌"이 죽음 —
/// 평소엔 풀 높이 질량 유지, 가릴 때만 양보. 디더 잔존 픽셀이 벽 실루엣을 지킨다.)
/// WallCutaway 셰이더의 _Dissolve를 렌더러별 머티리얼 인스턴스로 구동(SRP 배처에서 MPB 비신뢰 — LKP 교훈).
/// Main Camera에 부착.
/// </summary>
public class WallOcclusionFader : MonoBehaviour
{
    [Tooltip("차단 판정 대상 레이어 — 프로젝트 표준 Obstacle(8).")]
    [SerializeField] LayerMask obstacleMask = 1 << 8;
    [Tooltip("차단 판정 스피어캐스트 반경(m) — 플레이어 실루엣 폭을 덮는 크기.")]
    [SerializeField, Min(0.1f)] float castRadius = 1.2f;
    [Tooltip("가림 → 고스트 페이드 시간(초).")]
    [SerializeField, Min(0.01f)] float fadeInTime = 0.12f;
    [Tooltip("해제 → 복원 페이드 시간(초). 복원이 약간 느려야 깜박임이 없다.")]
    [SerializeField, Min(0.01f)] float fadeOutTime = 0.25f;
    [Tooltip("플레이어 기준 판정 끝점 높이(m) — 머리까지 가려짐을 판정.")]
    [SerializeField] float targetHeight = 1.2f;

    [Header("시선 방향 (★상시 콘과 짝 — 바라보는 지점을 가리는 벽도 비킨다)")]
    [Tooltip("플레이어→조준 방향으로 이만큼 떨어진 지점까지 카메라 시야를 확보(m). 정보는 LOS 게이트가 지키므로 치트 아님.")]
    [SerializeField, Min(0f)] float lookDistance = 12f;
    [Tooltip("시선 판정 캐스트 반경(m) — 플레이어 차단 판정보다 좁게(스치는 벽까지 다 빠지면 과함).")]
    [SerializeField, Min(0.1f)] float lookCastRadius = 0.8f;

    static readonly int DissolveId = Shader.PropertyToID("_Dissolve");

    class Faded { public Renderer r; public Material mat; public float v; public bool occluding; }
    readonly Dictionary<Renderer, Faded> _faded = new Dictionary<Renderer, Faded>();
    readonly List<Renderer> _toRemove = new List<Renderer>();
    readonly RaycastHit[] _hits = new RaycastHit[16];
    PlayerCombat _player;

    void LateUpdate()
    {
        if (!Application.isPlaying) return;   // renderer.material 인스턴스화는 플레이 전용(에디터 누수 방지)
        if (_player == null)
        {
            _player = FindObjectOfType<PlayerCombat>();
            if (_player == null) return;
        }

        foreach (var f in _faded.Values) f.occluding = false;

        Vector3 origin = transform.position;

        // 1) 카메라 ↔ 플레이어 — 내 캐릭터를 가리는 벽
        MarkOccluders(origin, _player.transform.position + Vector3.up * targetHeight, castRadius);

        // 2) 카메라 ↔ 시선 지점 — 내가 바라보는 곳을 가리는 벽 (콘=정보, 렌더가 따라간다)
        if (lookDistance > 0f)
        {
            Vector3 aim = _player.AimDirection; aim.y = 0f;
            if (aim.sqrMagnitude > 0.0001f)
            {
                Vector3 lookPoint = _player.transform.position + aim.normalized * lookDistance + Vector3.up * 1f;
                MarkOccluders(origin, lookPoint, lookCastRadius);
            }
        }

        // 크로스페이드 — 가림은 빠르게, 복원은 약간 느리게(경계 깜박임 방지)
        float dt = Time.deltaTime;
        _toRemove.Clear();
        foreach (var kv in _faded)
        {
            var f = kv.Value;
            if (f.r == null || f.mat == null) { _toRemove.Add(kv.Key); continue; }
            float tv = f.occluding ? 1f : 0f;
            f.v = Mathf.MoveTowards(f.v, tv, dt / (f.occluding ? fadeInTime : fadeOutTime));
            f.mat.SetFloat(DissolveId, f.v);
        }
        foreach (var r in _toRemove) _faded.Remove(r);
    }

    void MarkOccluders(Vector3 from, Vector3 to, float radius)
    {
        Vector3 dir = to - from;
        float dist = dir.magnitude;
        if (dist <= radius + 0.01f) return;
        dir /= dist;
        int n = Physics.SphereCastNonAlloc(from, radius, dir, _hits,
            dist - radius, obstacleMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var r = _hits[i].collider.GetComponent<Renderer>();
            // 디졸브 지원 셰이더(WallCutaway)만 — 일반 건물은 통과(아트 전환 시 자동 합류)
            if (r == null || r.sharedMaterial == null || !r.sharedMaterial.HasProperty(DissolveId)) continue;
            if (!_faded.TryGetValue(r, out var f))
            {
                f = new Faded { r = r, mat = r.material };   // 렌더러별 인스턴스(최초 1회)
                _faded[r] = f;
            }
            f.occluding = true;
        }
    }

    void OnDisable()
    {
        // 게이트 꺼짐 → 전부 솔리드 복원(고스트 벽 잔류 방지)
        foreach (var f in _faded.Values)
            if (f.mat != null) f.mat.SetFloat(DissolveId, 0f);
    }
}
