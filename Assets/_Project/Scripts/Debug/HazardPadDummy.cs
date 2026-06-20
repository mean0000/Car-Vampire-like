using UnityEngine;

/// <summary>
/// ★임시 테스트 더미(throwaway) — 무적 프레임(i-frame) 검증용 장판(AoE) 공격기.
/// 주기적으로: 텔레그래프(빨강이 중심→가장자리로 차오름) → 가장자리 닿는 순간 타격(흰 플래시, 1회 피해) → 쿨다운.
/// "언제 치는지"를 색 밝기가 아니라 ★차오르는 채움(fill)의 도달로 보여준다 — 타이밍이 명확.
///
/// 타격 시 반경 내 Player 레이어 IDamageable에 피해. PlayerHealth가 i-frame이면 무효 → 대시로 빠지면 회피.
/// 타격마다 HIT/DODGED를 콘솔에 찍어 i-frame이 먹었는지 명시. 정식 적 시스템 붙으면 삭제.
/// </summary>
public class HazardPadDummy : MonoBehaviour
{
    [Header("장판")]
    [SerializeField] float radius = 3f;
    [SerializeField] int damage = 20;
    [Tooltip("텔레그래프 시간(초) — 채움이 중심에서 가장자리까지 차는 시간. 끝에 타격.")]
    [SerializeField] float telegraphTime = 1.3f;
    [Tooltip("타격 플래시 시간(초).")]
    [SerializeField] float strikeFlash = 0.22f;
    [Tooltip("타격 후 쿨다운(초).")]
    [SerializeField] float cooldown = 1.2f;
    [Tooltip("피해 대상 레이어 — 플레이어.")]
    [SerializeField] LayerMask targetMask = 1 << 3;

    [Header("색")]
    [SerializeField] Color zoneColor = new Color(0.9f, 0.15f, 0.08f, 0.16f);   // 위험 구역 테두리(항상)
    [SerializeField] Color fillStart = new Color(1f, 0.45f, 0.1f, 0.30f);      // 채움 시작(중심)
    [SerializeField] Color fillEnd = new Color(1f, 0.12f, 0.04f, 0.72f);       // 채움 끝(가장자리=임박)
    [SerializeField] Color strikeColor = new Color(1f, 1f, 1f, 0.95f);         // 타격 플래시

    enum Phase { Telegraph, Strike, Cooldown }
    Phase _phase = Phase.Telegraph;
    float _timer;

    Renderer _zone, _fill;
    MaterialPropertyBlock _mpb;
    static readonly int ColorID = Shader.PropertyToID("_BaseColor");
    readonly Collider[] _overlap = new Collider[16];

    void Awake()
    {
        _timer = telegraphTime;
        _mpb = new MaterialPropertyBlock();
        _zone = BuildDisc("__Zone", radius * 2f, 0.02f);   // 항상 보이는 위험 구역
        _fill = BuildDisc("__Fill", 0f, 0.035f);           // 차오르는 채움(중심→가장자리)
        SetColor(_zone, zoneColor);
        SetColor(_fill, fillStart);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        _timer -= dt;
        SetColor(_zone, _phase == Phase.Strike ? strikeColor : zoneColor);

        switch (_phase)
        {
            case Phase.Telegraph:
            {
                float fill = telegraphTime > 0f ? 1f - Mathf.Clamp01(_timer / telegraphTime) : 1f;
                SetFill(fill);
                SetColor(_fill, Color.Lerp(fillStart, fillEnd, fill));
                if (_timer <= 0f) { Strike(); _phase = Phase.Strike; _timer = strikeFlash; SetFill(1f); SetColor(_fill, strikeColor); }
                break;
            }
            case Phase.Strike:
                if (_timer <= 0f) { _phase = Phase.Cooldown; _timer = cooldown; SetFill(0f); }
                break;
            case Phase.Cooldown:
                if (_timer <= 0f) { _phase = Phase.Telegraph; _timer = telegraphTime; }
                break;
        }
    }

    void Strike()
    {
        int n = Physics.OverlapSphereNonAlloc(transform.position, radius, _overlap, targetMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < n; i++)
        {
            var dmg = _overlap[i].GetComponentInParent<IDamageable>();
            if (dmg == null) continue;

            // i-frame 검증 로그: 때리기 전 무적 상태를 읽어 HIT/DODGED 명시.
            var ph = _overlap[i].GetComponentInParent<PlayerHealth>();
            bool invuln = ph != null && ph.IsInvulnerable;
            dmg.TakeHit(damage, transform.position, 0f);
            Debug.Log(invuln
                ? $"[HazardPad] {name}: 타격 → DODGED (i-frame 무적) ✓"
                : $"[HazardPad] {name}: 타격 → HIT {damage}", this);
        }
    }

    // 채움 디스크 지름을 fill01(0~1)에 맞춰 스케일 — 중심→가장자리로 차오름.
    void SetFill(float fill01)
    {
        if (_fill == null) return;
        float d = radius * 2f * Mathf.Clamp01(fill01);
        _fill.transform.localScale = new Vector3(d, 0.01f, d);
        _fill.enabled = fill01 > 0.001f;
    }

    void SetColor(Renderer r, Color c)
    {
        if (r == null) return;
        r.GetPropertyBlock(_mpb);
        _mpb.SetColor(ColorID, c);
        r.SetPropertyBlock(_mpb);
    }

    // 바닥 디스크 — 납작 실린더(콜라이더 제거) + URP 언릿 투명. 색은 MPB 구동.
    Renderer BuildDisc(string nm, float diameter, float y)
    {
        var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = nm;
        var col = disc.GetComponent<Collider>();
        if (col != null) Destroy(col);
        disc.transform.SetParent(transform, false);
        disc.transform.localPosition = new Vector3(0f, y, 0f);
        disc.transform.localScale = new Vector3(diameter, 0.01f, diameter);

        var rend = disc.GetComponent<Renderer>();
        var sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh != null)
        {
            var m = new Material(sh);
            m.SetFloat("_Surface", 1f);   // Transparent
            m.SetFloat("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            rend.sharedMaterial = m;
        }
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
        return rend;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
