using UnityEngine;

/// <summary>
/// ★화이트박스 잡몹 — 밀리는(pushable) 스웜 추격자. Rigidbody 기반: 호드끼리 콜라이더로 자연 분리되고,
/// 베이면(<see cref="EnemyDamageReceiver.OnKnocked"/>) 임펄스로 *진짜* 밀린다 = 밀어버리기 물리의 상대방
/// (배려 카탈로그 특별조작감 #3 — "베기가 공간을 만든다"의 물리 실체).
///
/// 위치 소유: 루트모션 ❌(순수 코드/물리 이동) — EnemyDamageReceiver.knockbackResponse는 0으로 두고
/// 변위는 전부 이 컴포넌트의 RB가 소유한다(이중소유 없음). 커밋/텔레그래프 없음 — 잡몹은 읽기 대상이
/// 아니다(읽기 슬로모는 고가치 커밋만, Codex 조건 4). 접촉 딜 = 호드 압박의 최소형.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class SwarmChaser : MonoBehaviour
{
    [Header("추격")]
    [Tooltip("추격 속도(m/s). 플레이어 걷기(5)보다 느리게 — 걸으면 벌어지고 멈추면 좁혀지는 압박.")]
    [SerializeField, Min(0f)] float moveSpeed = 3.2f;
    [Tooltip("추격 가속(m/s²) — 속도 강제 대입 대신 수렴. 밀림 임펄스 꼬리·군집 분리(콜라이더) 속도를 매 스텝 지우지 않는다(Codex P2 게이트).")]
    [SerializeField, Min(1f)] float chaseAccel = 25f;

    [Header("밀림 (질량 티어 노브)")]
    [Tooltip("넉백(m/s)에 곱하는 응답 — 몬스터 정합 조항의 '질량 티어'. ★07-04 1→2 상향(3자 수렴 '전선이 접히게') — 넉백 4~5 × 2 = 8~10m/s 임펄스.")]
    [SerializeField, Range(0f, 3f)] float shoveScale = 2f;
    [Tooltip("밀린 뒤 추격 재개까지(초) — 이 동안 물리에 맡겨 밀림이 '먹히는' 시간을 만든다(추격 velocity가 임펄스를 즉시 덮지 않게). 스태거(receiver.IsStaggered)와 별개로 겹쳐 작동.")]
    [SerializeField, Min(0f)] float shoveRecover = 0.35f;

    [Header("★도미노 연쇄 (07-04 — 밀린 놈이 뒤 전열을 접는다)")]
    [Tooltip("연쇄 발동 최소 속도(m/s) — 이보다 빠르게 날아가는 잡몹이 다른 잡몹과 충돌하면 상대를 스태거. 군집 밀착 접촉(저속)은 무시.")]
    [SerializeField, Min(0f)] float dominoSpeedThreshold = 4f;
    [Tooltip("연쇄 스태거 시간(초) — 직격 스태거보다 짧게(Codex 0.25). 0=도미노 끔.")]
    [SerializeField, Min(0f)] float dominoStagger = 0.25f;

    [Header("★스윙 가드 (유저 선택 배려 07-04 — '공격=방어' 최소형)")]
    [Tooltip("플레이어가 콤보 스윙 중이면 전방 아크 안 잡몹의 접촉딜 스킵 — 베는 동안 칼이 닿는 앞에선 안 물린다. 끄면 기존과 동일(A/B 토글).")]
    [SerializeField] bool swingGuardEnabled = true;
    [Tooltip("가드 아크 반각(도) — 70=전방 140°(Codex Rush Guard 전방각 차용). 측후방은 여전히 물린다(포위 위험 보존).")]
    [SerializeField, Range(0f, 180f)] float swingGuardArcHalf = 70f;

    [Header("접촉 딜")]
    [Tooltip("접촉 데미지(플레이어 HP). 잡몹이라 작게 — 압박은 수로.")]
    [SerializeField, Min(0)] int touchDamage = 1;
    [Tooltip("접촉 딜 주기(초).")]
    [SerializeField, Min(0.1f)] float touchInterval = 0.8f;
    [Tooltip("접촉 판정 거리(m, 중심간).")]
    [SerializeField, Min(0.1f)] float touchRange = 1.1f;
    [Tooltip("★크리티컬(커밋 적 타격) 순간부터 전 잡몹 접촉딜 정지 시간(초) — '읽고 벤 직후 파고들기'가 접촉딜에 갉히지 않게(Codex 커밋 처치 유예 0.5 수용). 0=끔. 전역 공유(마지막 활성값 승리 — 스포너 균일 주입 전제).")]
    [SerializeField, Min(0f)] float critTouchGrace = 0.5f;

    // ★전역 접촉 유예 — 크리티컬 엣지에 갱신. 정적(전 잡몹 공유): 유예는 '틈'이지 개체별 상태가 아니다.
    static float s_touchGraceUntil;
    static float s_graceDuration = 0.5f;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ClearStaticState()
    {
        s_touchGraceUntil = 0f;
        EnemyDamageReceiver.AnyCritHit -= OnAnyCritStatic;   // 도메인 리로드 꺼짐 대비 중복 구독 방지
        EnemyDamageReceiver.AnyCritHit += OnAnyCritStatic;   // 정적 핸들러 = 수명 앱 전역(개체 수명 무관, 누수 없음)
    }
    static void OnAnyCritStatic(EnemyDamageReceiver r) => s_touchGraceUntil = Time.time + s_graceDuration;

    Rigidbody _rb;
    EnemyDamageReceiver _receiver;
    Transform _target;
    PlayerHealth _targetHealth;
    KatanaWeapon _weapon;   // ★스윙 가드 판정용(IsSwingActive) — Init서 1회 해석. null=가드 생략.
    float _shoveTimer;
    float _touchTimer;   // 0 = 첫 접촉 즉딜 의도(초기 딜레이 없음, Stab L-5) — 쿨다운은 타격 후 세팅.
    float _dominoCd;     // 도미노 발신 스로틀 만료 시각 — 접촉 수만큼 운동량 증식 방지(Codex P2).

    /// <summary>스포너가 스폰 직후 호출 — 추격 대상 주입(씬 탐색 비용 개체별 중복 방지).</summary>
    public void Init(Transform target)
    {
        _target = target;
        _targetHealth = target != null
            ? (target.GetComponentInParent<PlayerHealth>() ?? target.GetComponentInChildren<PlayerHealth>())
            : null;
        _weapon = target != null ? target.GetComponentInChildren<KatanaWeapon>() : null;
        if (_targetHealth == null && target != null && touchDamage > 0)
            Debug.LogWarning("[SwarmChaser] PlayerHealth 미발견 — 접촉 딜 비활성(무음 소실 방지 경고, Stab M-2).", this);
    }

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.constraints = RigidbodyConstraints.FreezeRotation;   // 넘어짐 방지 — 밀림은 평면 이동만
        _receiver = GetComponent<EnemyDamageReceiver>();
        s_graceDuration = critTouchGrace;   // 전역 노브 갱신(스포너 균일 주입 — 마지막 값 승리)
    }

    void OnEnable()
    {
        if (_receiver == null) return;
        _receiver.OnKnocked -= OnKnocked;   // 중복 구독 방지(재활성 경로)
        _receiver.OnKnocked += OnKnocked;
    }

    void OnDisable()
    {
        if (_receiver != null) _receiver.OnKnocked -= OnKnocked;
    }

    void OnKnocked(Vector3 from, float knockback)
    {
        Vector3 dir = transform.position - from; dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = -transform.forward;
        _rb.AddForce(dir.normalized * knockback * shoveScale, ForceMode.VelocityChange);
        _shoveTimer = shoveRecover;
    }

    void FixedUpdate()
    {
        if (_target == null) return;
        float dt = Time.fixedDeltaTime;

        // ★스태거(피격 경직, receiver 소유 상태) — 추격·접촉딜 정지. "맞으면 행동이 끊긴다"(07-04 3자 수렴의 뿌리).
        //   velocity는 안 건드림 — 밀림 임펄스가 관성으로 코스팅 = 전선이 접히는 그 거리.
        bool staggered = _receiver != null && _receiver.IsStaggered;

        // 밀리는 동안은 물리에 맡긴다 — 추격 velocity 쓰기가 임펄스를 지우지 않게(밀림 체감 보존).
        if (_shoveTimer > 0f) { _shoveTimer -= dt; return; }
        if (staggered) return;   // 경직 중 = 추격 ❌·접촉딜 ❌(아래 전부 스킵). 물리(관성·중력·군집 충돌)만 산다.

        Vector3 to = _target.position - transform.position; to.y = 0f;
        float dist = to.magnitude;
        Vector3 chase = dist > 0.05f ? to / dist * moveSpeed : Vector3.zero;
        // ★가속 수렴(강제 대입 ❌, Codex P2 게이트) — 밀림 임펄스 꼬리·군집 분리 속도가 매 스텝 지워지지 않게.
        Vector3 cur = _rb.linearVelocity;
        Vector3 curFlat = new Vector3(cur.x, 0f, cur.z);
        Vector3 nextFlat = Vector3.MoveTowards(curFlat, chase, chaseAccel * dt);
        _rb.linearVelocity = new Vector3(nextFlat.x, cur.y, nextFlat.z);   // y는 중력에 맡김

        if (touchDamage > 0 && dist <= touchRange && Time.time >= s_touchGraceUntil)   // ★크리 유예 중엔 접촉딜 정지(파고들기 배려)
        {
            // ★스윙 가드(유저 선택 배려 07-04) — 플레이어 콤보 중 전방 아크(±swingGuardArcHalf) 안이면 접촉딜 스킵.
            //   '공격=방어' 최소형: 베는 동안 칼 닿는 앞에선 안 물린다. 측후방은 그대로(포위 위험 보존).
            //   ★기준 방향 = 칼 조준(SwingGuardForward) — 플레이어 루트는 회전 안 함, transform.forward 금지(Codex P1).
            if (swingGuardEnabled && _weapon != null && _weapon.IsSwingActive)
            {
                Vector3 fwd = _weapon.SwingGuardForward; fwd.y = 0f;
                Vector3 toMe = -to;   // 플레이어→나
                if (fwd.sqrMagnitude > 0.0001f && Vector3.Angle(fwd, toMe) <= swingGuardArcHalf)
                    return;
            }
            _touchTimer -= dt;
            if (_touchTimer <= 0f && _targetHealth != null && !_targetHealth.IsDead)
            {
                _touchTimer = touchInterval;
                _targetHealth.TakeHit(touchDamage, transform.position, 0f);
            }
        }
    }

    // (스태거 스쿼시 시각은 제거 — 루트 스케일이 CapsuleCollider까지 줄여 물리 튐(Codex P2). 화이트박스에선
    //  '정지+피격 플래시'가 이미 상태를 읽게 해준다. 전용 경직 시각은 실몹 HitReact 애니 몫.)

    // ★도미노 연쇄(07-04) — 빠르게 밀려 날아가는 잡몹이 다른 잡몹과 충돌하면 상대에게 짧은 스태거+밀림 전달.
    //   "베인 적이 뒤 적을 밀치며 전열이 접힌다"(Codex 카타나 뉘앙스). ★증식 가드(Codex P2): 들이받는 방향만·
    //   발신 스로틀 0.15s·전달 속도 캡 — 접촉 수만큼 운동량이 복제되는 폭주 차단.
    void OnCollisionEnter(Collision c)
    {
        if (dominoStagger <= 0f || Time.time < _dominoCd) return;
        Vector3 v = _rb.linearVelocity; v.y = 0f;
        if (v.sqrMagnitude < dominoSpeedThreshold * dominoSpeedThreshold) return;
        var other = c.collider.GetComponentInParent<SwarmChaser>();
        if (other == null || other == this) return;
        // ★내가 상대 *쪽으로* 실제로 들이받는 경우만(스치기·측후방 접촉 제외).
        Vector3 toOther = other.transform.position - transform.position; toOther.y = 0f;
        if (toOther.sqrMagnitude < 0.0001f || Vector3.Dot(v.normalized, toOther.normalized) < 0.4f) return;
        _dominoCd = Time.time + 0.15f;
        other._receiver?.ApplyStagger(dominoStagger);
        if (other._rb != null)
            other._rb.AddForce(Vector3.ClampMagnitude(v * 0.4f, 4f), ForceMode.VelocityChange);
    }
}
