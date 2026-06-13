// 산성 글롭 투사체 — 직사 글롭 1발. ProjectilePool이 소유·재활용하는 풀링 단위.
//
// ════════ 역할 경계 (애니 헌법과의 관계) ════════
//   이 투사체는 "발사된 위협"이다 — 몬스터의 정체성 동작(사격 모션)이 아니라, 그 모션의
//   AnimationEvent가 스폰한 독립 오브젝트. 즉 클립과 무관하게 자기 직선 궤적을 산다(직사).
//   "발사된 모든 위협은 시야콘 게이트 면제"(공격 문법 §2 공정성 캐넌) — 항상 보인다.
//
//   ★예측 없음(조준탄 철학): 발사 시점의 방향으로 고정 직사. 날아가는 동안 유도/재조준 0.
//     플레이어가 멈춰 있으면 정통으로 맞고, 옆으로 움직이면 빗나간다 = "정지 사격의 대가".
//     이 처벌의 공정성은 탄속(피하기 가능선)이 만든다 — 코드가 아니라 노브(speed).
//
//   비행 중 위치는 코드가 직선으로 만든다 — 이건 헌법 위반이 아니다(투사체는 *애니메이션 클립이
//   아니다*. 헌법 "코드가 위치/포즈 안 만듦"은 캐릭터 본체의 모션에 대한 것이다). 글롭엔 클립이 없다.
using UnityEngine;

public class AcidGlob : MonoBehaviour
{
    ProjectilePool _pool;       // 소멸 시 반납할 풀
    Vector3 _velocity;          // 발사 시 고정(유도 없음)
    float _life;                // 남은 수명(초)
    float _maxRange;            // 발사점에서 이 거리 넘으면 소멸
    Vector3 _origin;            // 발사 위치(사거리 판정 기준)
    bool _active;

    Renderer _renderer;
    TrailRenderer _trail;

    // 풀이 생성 직후 1회 셋업(비주얼 컴포넌트 캐시).
    public void Bind(ProjectilePool pool, Renderer rend, TrailRenderer trail)
    {
        _pool = pool; _renderer = rend; _trail = trail;
        gameObject.SetActive(false);
    }

    // 발사 — origin에서 dir 방향으로 speed로 직선 비행. range/lifetime에 소멸.
    public void Launch(Vector3 origin, Vector3 dir, float speed, float range, float lifetime)
    {
        _origin = origin;
        transform.position = origin;
        Vector3 d = dir; d.y = 0f;               // 탑뷰 평면 직사(높이 성분 제거 — 바닥 평면 위빙)
        if (d.sqrMagnitude < 0.0001f) d = transform.forward;
        d.Normalize();
        _velocity = d * speed;
        transform.rotation = Quaternion.LookRotation(d, Vector3.up);
        _maxRange = range;
        _life = lifetime;
        _active = true;

        gameObject.SetActive(true);
        if (_trail != null) { _trail.Clear(); }   // 재활용 시 이전 궤적 잔상 제거
    }

    void Update()
    {
        if (!_active) return;
        float dt = Time.deltaTime;
        transform.position += _velocity * dt;

        _life -= dt;
        // 사거리 초과 또는 수명 종료 → 반납. (랩엔 HP 없음 — 명중 데미지는 게임플레이 단계 소관)
        Vector3 flat = transform.position - _origin; flat.y = 0f;
        if (_life <= 0f || flat.sqrMagnitude >= _maxRange * _maxRange)
            Despawn();
    }

    void Despawn()
    {
        _active = false;
        if (_trail != null) _trail.Clear();
        gameObject.SetActive(false);
        if (_pool != null) _pool.Return(this);
    }
}
