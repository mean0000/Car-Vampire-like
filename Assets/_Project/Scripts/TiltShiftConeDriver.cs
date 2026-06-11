using UnityEngine;

/// <summary>
/// 시야 콘 틸트시프트 드라이버 — TiltShift.shader의 전역 유니폼(_TS_Cone*)을 매 프레임 주입.
/// 카메라(PlayerCameraRig와 같은 GO)에 부착. 의미: 캐릭터에서 조준 방향으로 뻗는 부채꼴은
/// 선명(Y 밴드가 열림), 바깥은 블러 바닥 + 채도 감소 — "캐릭터의 주위 시야" 연출.
///
/// 가드레일 (브레인스토밍 2026-06-11 합의):
///  - coneBlend 0 = 현행 Y 밴드와 완전 동일(회귀 노브). 드라이버가 꺼져도 셰이더 전역이 0이라 동일.
///  - 조준 방향은 각속도 제한으로 추종 — 마우스가 휙 돌 때 블러 경계가 화면을 쓸며 멀미 유발 방지.
///  - 캐릭터 근접 반경은 방향 무관 무조건 선명(밀착 위협 보호 — 불공정 사망 방지).
///  - 블러 최대치는 셰이더 _MaxBlurRadius 그대로(정보 삭제 수준 금지 — 위치만 바뀌지 세기 불변).
///
/// [ExecuteAlways] — 에디터 캡처 루프로 미적 판정을 하기 위함(verify_topdown_visually).
/// </summary>
[ExecuteAlways]
[DefaultExecutionOrder(-10)]   // PlayerCameraRig(-50) 이후, LKP 실루엣(0) 이전 — _lastUvP 1프레임 스테일 방지(리뷰 H-1)
[RequireComponent(typeof(Camera))]
public class TiltShiftConeDriver : MonoBehaviour
{
    [Header("Cone Shape")]
    [Tooltip("콘 반각(도). 35°≈손전등 외각과 정합. 0.75 블렌드 기준 시작값.")]
    [SerializeField, Range(10f, 80f)] float coneHalfAngle = 35f;
    [Tooltip("경계 그라데이션 폭(도). 좁으면 경계가 날카로워 회전 시 깜박임처럼 보인다.")]
    [SerializeField, Range(5f, 40f)] float coneFalloff = 20f;
    [Tooltip("캐릭터 주변 무조건 선명 반경(uv.y 단위 — 0.1=화면 높이의 10%). 밀착 위협 보호.")]
    [SerializeField, Range(0.02f, 0.25f)] float proximityRadius = 0.10f;
    [Tooltip("콘 기준점 높이(m) — 캐릭터 발 기준 위로. 1.0≈몸통.")]
    [SerializeField] float originHeight = 1.0f;

    [Header("Cone Strength")]
    [Tooltip("★마스터 노브: 0=현행 Y 밴드 그대로, 1=풀 콘. 판정은 캡처+플레이로.")]
    [SerializeField, Range(0f, 1f)] float coneBlend = 0.75f;
    [Tooltip("콘 밖 최소 블러(0~1, _MaxBlurRadius 비율). 밴드 포커스존도 콘 밖이면 이만큼은 흐려진다.")]
    [SerializeField, Range(0f, 1f)] float outsideBlurFloor = 0.6f;
    [Tooltip("콘 밖 채도 감소(0~1). 조준 게이트라 사실상 집중 연출 — 어둠과 함께 '주위 의식 차단'의 절반.")]
    [SerializeField, Range(0f, 0.6f)] float outsideDesat = 0.45f;

    [Header("Aim Follow")]
    [Tooltip("콘 방향 최대 각속도(도/초). 마우스 급회전 시 블러 경계가 화면을 쓸지 않게 제한.")]
    [SerializeField, Min(30f)] float aimMaxDegPerSec = 200f;

    [Header("Aim Focus (주시/조준 — PlayerCameraRig.AimBlend 구동, 지각적 줌)")]
    [Tooltip("조준 시 콘 반각(도) — 터널비전. 35→22가 기본 설계.")]
    [SerializeField, Range(10f, 60f)] float aimConeHalfAngle = 22f;
    [Tooltip("조준 시 콘 밖 어두워짐(0~1). '주위를 의식하지 않는' 집중 — 일시 상태라 가독성 헌법과 양립.")]
    [SerializeField, Range(0f, 0.7f)] float aimOutsideDarken = 0.5f;
    [Tooltip("수렴 완료 시 콘 반각(도) — 콘이 '거의 직선'까지 무너진다. 0°는 금지(방향 정보 소멸) — 레이저와 합쳐져 직선으로 읽힌다.")]
    [SerializeField, Range(3f, 20f)] float convergedHalfAngle = 8f;
    [Tooltip("수렴 완료 시 어둠 추가량 — 마지막 한 단계 더 깊은 집중.")]
    [SerializeField, Range(0f, 0.25f)] float convergedExtraDarken = 0.12f;

    static readonly int ConePosId    = Shader.PropertyToID("_TS_ConePos");
    static readonly int ConeParamsId = Shader.PropertyToID("_TS_ConeParams");
    static readonly int ConeExtraId  = Shader.PropertyToID("_TS_ConeExtra");
    static readonly int ConeFxId     = Shader.PropertyToID("_TS_ConeFx");

    /// <summary>콘 판정을 공유하는 시스템(LKP 실루엣 등)용 싱글턴.</summary>
    public static TiltShiftConeDriver Instance { get; private set; }

    Camera _cam;
    PlayerCombat _combat;
    PlayerCameraRig _rig;              // 같은 GO — AimBlend로 콘 수축·어두움 구동
    Vector2 _smoothDir = Vector2.up;   // 스크린 공간(aspect 보정) 콘 방향 — 각속도 제한 추종 상태
    Vector2 _lastUvP = new Vector2(0.5f, 0.5f);   // 마지막 주입한 플레이어 uv(IsWorldPosInCone 공유)
    float _curHalfAngle = 35f;         // 이번 프레임 실효 콘 반각(조준 수축 반영) — 판정·셰이더 공유
    float _curBlend;                   // 이번 프레임 실효 블렌드(조준 게이트 반영) — ConeActive 판정 공유
    float _prevConv;                   // 수렴 완료 순간 감지(0.95 교차)용
    float _convPulseT = -10f;          // 완료 플래시 시작 시각(unscaled)

    /// <summary>콘이 실제로 화면에 적용 중인가(조준 게이트 반영). false면 "전부 보이는 상태".</summary>
    public bool ConeActive => _curBlend > 0.01f;

    /// <summary>월드 좌표가 현재 시야 콘 안(선명 영역)인가 — 셰이더와 동일한 스크린스페이스 판정.
    /// 콘 꺼짐/투영 불가 시 true(전부 보임)로 폴백.</summary>
    public bool IsWorldPosInCone(Vector3 worldPos)
    {
        if (!ConeActive || _cam == null) return true;
        Vector3 vp = _cam.WorldToViewportPoint(worldPos);
        if (vp.z <= 0f) return false;   // 카메라 뒤 — 화면 밖
        float aspect = _cam.aspect;
        Vector2 toPix = new Vector2((vp.x - _lastUvP.x) * aspect, vp.y - _lastUvP.y);
        float d = toPix.magnitude;
        if (d < proximityRadius) return true;   // 근접 선명 반경
        float ca = Vector2.Dot(toPix / Mathf.Max(d, 1e-5f), _smoothDir);
        return ca >= Mathf.Cos((_curHalfAngle + coneFalloff * 0.5f) * Mathf.Deg2Rad);   // 조준 수축 반영
    }

    void OnEnable()
    {
        _cam = GetComponent<Camera>();
        // 플레이 중엔 항상 게임 카메라가 권위. 에디터(ExecuteAlways)에선 빈 슬롯만 차지(씬뷰 경합 방지 — 리뷰 H-2).
        if (Application.isPlaying || Instance == null) Instance = this;
    }

    void OnDisable()
    {
        Shader.SetGlobalVector(ConeParamsId, Vector4.zero);   // blend 0 → 효과 완전 해제
        Shader.SetGlobalVector(ConeFxId, Vector4.zero);       // 수렴 플래시 잔류 방지(리뷰 L-4)
        if (Instance == this) Instance = null;                // 비활성 드라이버의 stale 콘 판정 차단(리뷰 M-4)
    }
    void OnDestroy()                                                           // 도메인 리로드 시 OnDisable 미보장(리뷰 M-2)
    {
        Shader.SetGlobalVector(ConeParamsId, Vector4.zero);
        Shader.SetGlobalVector(ConeFxId, Vector4.zero);
        if (Instance == this) Instance = null;
    }

    void LateUpdate()
    {
        // PlayerCameraRig(-50)의 LateUpdate 이후(기본 순서 0) — 이 프레임 최종 카메라 위치 기준으로 투영.
        if (_combat == null)
        {
            _combat = FindObjectOfType<PlayerCombat>();
            if (_combat == null) { Shader.SetGlobalVector(ConeParamsId, Vector4.zero); return; }
        }
        if (_cam == null) return;

        Vector3 origin = _combat.transform.position + Vector3.up * originHeight;
        Vector3 aim = _combat.AimDirection; aim.y = 0f;
        if (aim.sqrMagnitude < 0.0001f) aim = Vector3.forward;

        // 플레이어 스크린 uv (커서 리드로 가장자리에 갈 때 콘 중심이 화면 밖으로 나가 클램프 샘플
        // 아티팩트가 생기지 않게 소프트 클램프).
        Vector3 vpP = _cam.WorldToViewportPoint(origin);
        // 카메라 뒤(z≤0)면 WorldToViewportPoint가 좌표를 미러해 콘이 엉뚱한 곳에 뜬다 — 효과 해제(리뷰 H-2).
        if (vpP.z <= 0f) { Shader.SetGlobalVector(ConeParamsId, Vector4.zero); return; }
        Vector2 uvP = new Vector2(Mathf.Clamp(vpP.x, 0.1f, 0.9f), Mathf.Clamp(vpP.y, 0.1f, 0.9f));
        _lastUvP = uvP;

        // 조준 방향의 스크린 투영(45° 부감의 상하 원근 압축을 투영이 자연 처리) → aspect 보정 공간.
        Vector3 vpA = _cam.WorldToViewportPoint(origin + aim * 3f);
        Vector2 target = new Vector2((vpA.x - vpP.x) * _cam.aspect, vpA.y - vpP.y);
        if (target.sqrMagnitude > 1e-6f) target.Normalize(); else target = _smoothDir;

        // 각속도 제한 추종 — 경계 쓸림 멀미 방지. 에디터(비플레이)에선 즉시 반영.
        float dt = Application.isPlaying ? Time.unscaledDeltaTime : 1f;
        float ang = Vector2.SignedAngle(_smoothDir, target);
        float step = aimMaxDegPerSec * dt;
        float rot = Mathf.Clamp(ang, -step, step) * Mathf.Deg2Rad;
        float c = Mathf.Cos(rot), s = Mathf.Sin(rot);
        _smoothDir = new Vector2(c * _smoothDir.x - s * _smoothDir.y,
                                 s * _smoothDir.x + c * _smoothDir.y).normalized;

        // 주시/조준 블렌드 — 콘 수축(터널비전) + 콘 밖 어두움 + 포커스 풀(셰이더 w 채널).
        // ★2026-06-11 "평시 틸트시프트 포기"(유저 콜): 콘 스택 전체가 조준 게이트 — 평시 화면은 100% 깨끗,
        //   우클릭 홀드에서만 집중 세계(줌+콘+어둠+블러+탈색)가 들어온다.
        if (_rig == null) _rig = GetComponent<PlayerCameraRig>();
        float aimF = (_rig != null && Application.isPlaying) ? _rig.AimBlend : 0f;
        float conv = (_rig != null && Application.isPlaying) ? _rig.AimConvergence : 0f;
        // 1단계(블렌드): 35°→22° 수축, 2단계(수렴): 22°→8° 붕괴 — 콘 자체가 수렴 게이지(UI 불필요).
        _curHalfAngle = Mathf.Lerp(Mathf.Lerp(coneHalfAngle, aimConeHalfAngle, aimF), convergedHalfAngle, conv);
        _curBlend = coneBlend * aimF;
        float darken = (aimOutsideDarken + convergedExtraDarken * conv) * aimF;

        float cosInner = Mathf.Cos(_curHalfAngle * Mathf.Deg2Rad);
        float cosOuter = Mathf.Cos((_curHalfAngle + coneFalloff) * Mathf.Deg2Rad);

        Shader.SetGlobalVector(ConePosId,    new Vector4(uvP.x, uvP.y, _smoothDir.x, _smoothDir.y));
        Shader.SetGlobalVector(ConeParamsId, new Vector4(cosInner, cosOuter, proximityRadius, _curBlend));
        Shader.SetGlobalVector(ConeExtraId,  new Vector4(outsideBlurFloor, outsideDesat, darken, aimF));

        // 수렴 신호 — 0.95 교차 순간 경계 플래시("지금이다"), 이후 은은한 유지(셰이더 ×0.15).
        if (conv >= 0.95f && _prevConv < 0.95f) _convPulseT = Time.unscaledTime;
        float flash = conv >= 0.95f ? Mathf.Exp(-(Time.unscaledTime - _convPulseT) * 14f) : 0f;
        _prevConv = conv;
        Shader.SetGlobalVector(ConeFxId, new Vector4(conv, flash, 0f, 0f));
    }
}
