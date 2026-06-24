using UnityEngine;

/// <summary>
/// ★맞받음(클래시) 실험 더미 — 정지 표적이 텔레그래프→타격을 반복(또는 [T]키 발동)한다.
///
/// 목적 2가지:
///   ① 호드 카오스 없이 *클래시 타이밍을 격리 실험* — 느리고 또렷한 1:1 표적에서 "그 순간 베기"를 연습/튜닝.
///   ② *진단* — 더미는 애니 컨트롤러 상태명에 의존하지 않고 자체 코드 타이머로 IAttackCommit.IsStriking을 구동한다.
///      → 더미에서 클래시가 *뜨면* 메커니즘(KatanaWeapon→IsStriking→ParrySlowMotion.Clash)은 정상 = 진짜 Caniathrox
///        문제는 '타이밍 창/도달성' 또는 '애니 상태명 불일치'. 더미에서도 *안 뜨면* 메커니즘 자체 버그.
///
/// 사용: 빈 GameObject에 이 컴포넌트만 붙이고 플레이어 앞 ~3m에 둔다(콜라이더·리시버·비주얼 자동 부트스트랩).
///   격리하려면 CombatSliceSpawner를 비활성화(또는 swarmSize 낮춤). 화면 좌상단 디버그 라벨로 상태/판정 확인.
/// </summary>
[DisallowMultipleComponent]
public class ClashTestDummy : MonoBehaviour, IAttackCommit
{
    [Header("공격 사이클 (초) — 타이밍 튜닝")]
    [Tooltip("휴지(다음 공격 전 대기).")]
    [SerializeField, Min(0f)] float restDuration = 1.5f;
    [Tooltip("윈드업(회→주황 차오름). 길수록 예고가 여유롭다.")]
    [SerializeField, Min(0.05f)] float windupDuration = 0.8f;
    [Tooltip("★타격 창(= 클래시 가능 시간). 핵심 튜닝값 — 진짜 적에서 '전혀 안 뜨면' 창이 너무 짧았던 것. 넉넉히 시작.")]
    [SerializeField, Min(0.05f)] float strikeWindow = 0.4f;
    [Tooltip("회수(타격 후).")]
    [SerializeField, Min(0f)] float recoverDuration = 0.6f;

    [Header("발동 방식")]
    [Tooltip("켜면 자동 반복, 끄면 [발동 키]로만.")]
    [SerializeField] bool autoLoop = true;
    [Tooltip("수동 발동 키(autoLoop 끌 때).")]
    [SerializeField] KeyCode triggerKey = KeyCode.T;

    [Header("표적 셋업 (자동 부트스트랩)")]
    [Tooltip("HP — 안 죽고 반복 실험하도록 크게.")]
    [SerializeField] int hp = 99999;
    [SerializeField, Min(0.1f)] float colliderRadius = 0.9f;
    [SerializeField, Min(0.2f)] float colliderHeight = 2f;
    [Tooltip("화면 좌상단 디버그 라벨(상태·마지막 판정) 표시.")]
    [SerializeField] bool showDebugLabel = true;

    enum Phase { Rest, Windup, Strike, Recover }
    Phase _phase = Phase.Rest;
    float _t;
    EnemyDamageReceiver _receiver;

    // ── IAttackCommit (자체 타이머 구동 — 애니 무관) ──
    public bool IsCommittingAttack => _phase == Phase.Windup || _phase == Phase.Strike;
    public bool IsStriking => _phase == Phase.Strike;   // ★클래시 창

    // 디버그 표시
    string _lastResult = "—";
    float _resultFlash;

    void Awake()
    {
        // 레이어 7(Zombie) — KatanaWeapon.enemyMask 정합(★못 맞추면 영원히 못 때림).
        int layer = LayerMask.NameToLayer("Zombie");
        if (layer >= 0) gameObject.layer = layer;
        else Debug.LogWarning("[ClashTestDummy] 'Zombie' 레이어 미존재 — KatanaWeapon이 못 때릴 수 있음.");

        // 비주얼(렌더러 없으면 캡슐 생성 — 커밋 글로우/플래시가 보이게).
        if (GetComponentInChildren<Renderer>() == null)
        {
            var vis = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            vis.name = "DummyVisual";
            vis.transform.SetParent(transform, false);
            vis.transform.localPosition = new Vector3(0f, colliderHeight * 0.5f, 0f);
            var visCol = vis.GetComponent<Collider>();
            if (visCol != null) Destroy(visCol);   // 시각만 — 판정 콜라이더는 루트에 따로.
        }

        // 판정 콜라이더(트리거) + kinematic RB.
        if (GetComponent<Collider>() == null)
        {
            var col = gameObject.AddComponent<CapsuleCollider>();
            col.radius = colliderRadius;
            col.height = colliderHeight;
            col.center = new Vector3(0f, colliderHeight * 0.5f, 0f);
            col.isTrigger = true;
        }
        if (GetComponent<Rigidbody>() == null)
        {
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // 피격 리시버(커밋 글로우·플래시·클래시 시안 구동) — 자동 부착.
        _receiver = GetComponent<EnemyDamageReceiver>();
        if (_receiver == null) _receiver = gameObject.AddComponent<EnemyDamageReceiver>();
        _receiver.SetMaxHp(hp);
        _receiver.OnDamaged += OnDamaged;

        _phase = Phase.Rest;
        _t = restDuration;
    }

    void OnDestroy()
    {
        if (_receiver != null) _receiver.OnDamaged -= OnDamaged;
    }

    void Update()
    {
        // 수동 발동: 휴지 중 키 → 즉시 윈드업.
        if (!autoLoop && _phase == Phase.Rest && Input.GetKeyDown(triggerKey))
        {
            _phase = Phase.Windup;
            _t = windupDuration;
        }

        if (_resultFlash > 0f) _resultFlash -= Time.unscaledDeltaTime;
        _t -= Time.deltaTime;

        switch (_phase)
        {
            case Phase.Rest:
                _receiver?.DriveCommit(0f, false);
                if (autoLoop && _t <= 0f) { _phase = Phase.Windup; _t = windupDuration; }
                break;

            case Phase.Windup:   // 회→주황 차오름(0→1)
                _receiver?.DriveCommit(1f - Mathf.Clamp01(_t / Mathf.Max(0.01f, windupDuration)), false);
                if (_t <= 0f) { _phase = Phase.Strike; _t = strikeWindow; }
                break;

            case Phase.Strike:   // ★NOW 펄스 + IsStriking=true (클래시 가능)
                _receiver?.DriveCommit(1f, true);
                if (_t <= 0f) { _phase = Phase.Recover; _t = recoverDuration; }
                break;

            case Phase.Recover:
                _receiver?.DriveCommit(0f, false);
                if (_t <= 0f) { _phase = Phase.Rest; _t = restDuration; }
                break;
        }
    }

    // 타격이 들어온 순간의 페이즈로 클래시 성패 진단.
    void OnDamaged(int dmg, Vector3 from)
    {
        bool clash = _phase == Phase.Strike;
        _lastResult = clash ? "★CLASH (Strike 창 명중)" : $"일반 타격 (phase={_phase})";
        _resultFlash = 1.2f;
        Debug.Log($"[ClashTestDummy] {_lastResult} / dmg={dmg}");
    }

    void OnGUI()
    {
        if (!showDebugLabel) return;
        var style = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
        style.normal.textColor = _phase == Phase.Strike ? Color.white
            : _phase == Phase.Windup ? new Color(1f, 0.6f, 0.2f) : Color.gray;
        GUI.Label(new Rect(12, 10, 600, 30), $"[ClashDummy] phase = {_phase}   (Strike=클래시 창)", style);

        var rstyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
        rstyle.normal.textColor = _resultFlash > 0f
            ? (_lastResult.StartsWith("★") ? new Color(0.3f, 1f, 1f) : new Color(1f, 0.5f, 0.5f))
            : Color.gray;
        GUI.Label(new Rect(12, 36, 600, 30), $"마지막 타격: {_lastResult}", rstyle);
        GUI.Label(new Rect(12, 62, 600, 24), autoLoop ? "자동 반복" : $"[{triggerKey}] 키로 발동", style);
    }
}
