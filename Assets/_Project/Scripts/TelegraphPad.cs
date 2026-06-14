// 텔레그래프 장판 1개 인스턴스 — ThreatArc.shader 쿼드 하나를 들고 채움(_Progress)을 구동한다.
// ★나머지 장판 종(스매시 원·도약 착지원·가스 잔류 링)의 재사용 토대. TelegraphPool이 풀링.
//
// ════════ 플레이어 수용성 = 이 컴포넌트의 존재 이유 (북극성 #6) ════════
//   모양 = 피해 영역 (부채꼴이 실제 클로 타격 범위와 일치 — 거짓 장판 금지).
//   채움 = 타이밍 (_Progress 0→1, 가득 차는 *순간* = 클로가 닿는 프레임).
//   외곽선은 스폰 즉시 상시 표시(예고), 안쪽이 fillDuration 동안 차오른다(카운트다운).
//   → 플레이어가 부채꼴 차오름을 보고 그 전에 빠져나가면 회피 = 공정성.
//
// ════════ 렌더 경로 (동결 — 재발명 금지, 권위: topdown-attack-grammar §5 + StrainHarvestFX 선례) ════════
//   쿼드 = PickupInfo 레이어(13) → URP_HighFidelity_Renderer의 PickupInfoOverlay RenderObjects(이벤트 600,
//   투명 큐) 재드로우 = 시야콘 어둠 면제(공정성 캐넌: 콘 밖 위협도 항상 보인다).
//   _ZTest LEqual(4) → 깊이 살아있어 몬스터/벽이 장판을 가린다("장판은 바닥"). renderQueue 3000(필터=투명 큐).
//   ⚠️오버레이 캔버스/URP Decal로 재발명 금지(시야콘 합성이 월드 투명 VFX를 지운 사고 이력).
//
// ════════ 셰이더 계약 (ThreatArc.shader 실측 — _SizeWorld == 쿼드 localScale 필수) ════════
//   _Style 3=원 5=부채꼴 6=링 / _SizeWorld(x=폭/지름, y=길이) / _Progress 0~1 채움
//   _AngleDeg(부채꼴 전각, ★1~180° 클램프 — 초과 시 SDF 부호반전 통째 소실) / _InnerR01(링 내경비)
//   _Color(레드-오렌지 단색 — 색 캐넌) / _Alpha(마스터) / _FillAlpha / _EdgeAlpha / _EdgeWorld / _ZTest
//   부채꼴 apex = 쿼드 중심, 전방 = 쿼드 로컬 +y. (쿼드를 Euler(90,0,0) 회전 → 로컬+y가 월드+z(전방)로.)
using UnityEngine;

public class TelegraphPad : MonoBehaviour
{
    // ── 셰이더 프로퍼티 ID (TelegraphLab과 동일) ──
    static readonly int StyleId     = Shader.PropertyToID("_Style");
    static readonly int SizeWorldId = Shader.PropertyToID("_SizeWorld");
    static readonly int ColorId     = Shader.PropertyToID("_Color");
    static readonly int AlphaId     = Shader.PropertyToID("_Alpha");
    static readonly int ProgressId  = Shader.PropertyToID("_Progress");
    static readonly int SeedId      = Shader.PropertyToID("_Seed");
    static readonly int ZTestId     = Shader.PropertyToID("_ZTest");
    static readonly int AngleDegId  = Shader.PropertyToID("_AngleDeg");
    static readonly int InnerR01Id  = Shader.PropertyToID("_InnerR01");
    static readonly int EdgeWorldId = Shader.PropertyToID("_EdgeWorld");
    static readonly int FillAlphaId = Shader.PropertyToID("_FillAlpha");
    static readonly int EdgeAlphaId = Shader.PropertyToID("_EdgeAlpha");

    TelegraphPool _pool;
    MeshRenderer _mr;
    Material _mat;          // 인스턴스별 머티리얼(각자 다른 _Progress 구동)
    Transform _quad;        // 회전·스케일 적용 대상

    // 활성 상태(채움 진행)
    bool _active;
    float _fillElapsed;
    float _fillDuration;    // 외곽선 등장 → 가득 참(=히트)까지의 초. 클립 윈드업→컨택 간격과 일치.
    float _holdAfterFull;   // 가득 찬 뒤 잔상 유지(초) — 히트 순간을 눈으로 잡게. 짧게.
    float _holdElapsed;
    int _gen;               // 리스 세대 — Begin마다 증가. 풀 회수로 주인이 바뀐 패드의 stale 조작(ForceFull/Cancel) 차단.

    /// <summary>현재 리스의 세대 id — 드라이버가 스폰 직후 저장해, 재활용으로 주인이 바뀌면 stale 조작을 막는다.</summary>
    public int Gen => _gen;

    /// <summary>풀이 생성 시 1회 바인딩. 쿼드 메시·렌더러·인스턴스 머티리얼을 받는다.</summary>
    public void Bind(TelegraphPool pool, Transform quad, MeshRenderer mr, Material mat)
    {
        _pool = pool;
        _quad = quad;
        _mr = mr;
        _mat = mat;
        Deactivate();
    }

    void Deactivate()
    {
        _active = false;
        if (_mr != null) _mr.enabled = false;
    }

    /// <summary>풀이 강제 회수 시 호출(RecycleOldest 경로 전용) — 즉시 비활성.
    /// Pool.Return은 호출하지 않는다(Acquire 측이 이미 _live에서 꺼냈음 — 이중 반납 방지).
    /// SmashImpactFX.ForceDeactivate()와 대칭.</summary>
    public void ForceDeactivate()
    {
        _active = false;
        if (_mr != null) _mr.enabled = false;
    }

    /// <summary>
    /// 부채꼴 장판 1개를 스폰해 채움 개시. 시전자 위치·전방에 정렬.
    /// origin=시전자(부채꼴 apex), forward=전방(평면화), radius=사거리(m), angleDeg=전각(1~180 클램프),
    /// fillDuration=외곽선→가득참까지 초(= 클립 윈드업→컨택), color/alpha=톤.
    /// </summary>
    public void SpawnFan(Vector3 origin, Vector3 forward, float radius, float angleDeg,
                         float fillDuration, float holdAfterFull, Color color,
                         float masterAlpha, float fillAlpha, float edgeAlpha, float edgeWorld)
    {
        radius = Mathf.Max(0.05f, radius);   // 재사용 공개 API 방어: 0/음수 radius = degenerate 쿼드·셰이더 0除
        float diameter = radius * 2f;   // _SizeWorld.x = 지름(부채꼴 R = SizeWorld.x*0.5)
        ConfigureCommon(5f, new Vector2(diameter, diameter), color, masterAlpha, fillAlpha, edgeAlpha, edgeWorld);
        _mat.SetFloat(AngleDegId, Mathf.Clamp(angleDeg, 1f, 180f));   // ★180° 초과 = 부호반전 사고
        PlaceOrientedToForward(origin, forward);
        Begin(fillDuration, holdAfterFull);
    }

    /// <summary>원형 장판(스매시·착지원 등 재사용). origin 중심, radius 사거리.</summary>
    public void SpawnCircle(Vector3 origin, float radius, float fillDuration, float holdAfterFull,
                            Color color, float masterAlpha, float fillAlpha, float edgeAlpha, float edgeWorld)
    {
        radius = Mathf.Max(0.05f, radius);
        float diameter = radius * 2f;
        ConfigureCommon(3f, new Vector2(diameter, diameter), color, masterAlpha, fillAlpha, edgeAlpha, edgeWorld);
        PlaceFlat(origin);
        Begin(fillDuration, holdAfterFull);
    }

    /// <summary>링 장판(자기중심 광역·스톰프 등 재사용). origin 중심, radius 외경, inner01 내경비.</summary>
    public void SpawnRing(Vector3 origin, float radius, float inner01, float fillDuration, float holdAfterFull,
                          Color color, float masterAlpha, float fillAlpha, float edgeAlpha, float edgeWorld)
    {
        radius = Mathf.Max(0.05f, radius);
        float diameter = radius * 2f;
        ConfigureCommon(6f, new Vector2(diameter, diameter), color, masterAlpha, fillAlpha, edgeAlpha, edgeWorld);
        _mat.SetFloat(InnerR01Id, Mathf.Clamp01(inner01));
        PlaceFlat(origin);
        Begin(fillDuration, holdAfterFull);
    }

    void ConfigureCommon(float style, Vector2 sizeWorld, Color color, float masterAlpha,
                         float fillAlpha, float edgeAlpha, float edgeWorld)
    {
        _mat.SetFloat(StyleId, style);
        _mat.SetVector(SizeWorldId, new Vector4(sizeWorld.x, sizeWorld.y, 0f, 0f));
        _mat.SetColor(ColorId, color);
        _mat.SetFloat(AlphaId, masterAlpha);
        _mat.SetFloat(FillAlphaId, fillAlpha);
        _mat.SetFloat(EdgeAlphaId, edgeAlpha);
        _mat.SetFloat(EdgeWorldId, edgeWorld);
        _mat.SetFloat(SeedId, Random.value * 37f);
        _mat.SetFloat(ZTestId, 4f);                    // LEqual — 바닥 깊이 존중
        _mat.renderQueue = VfxLayers.TelegraphFloor;  // PickupInfoOverlay 필터 = 투명 큐
        // ★전제: 쿼드 localScale == _SizeWorld.xy (셰이더 월드미터 SDF 계산)
        _quad.localScale = new Vector3(sizeWorld.x, sizeWorld.y, 1f);
    }

    // 부채꼴/레인: 쿼드 로컬+y가 forward(월드 평면)를 향하게. Euler(90,0,0)으로 눕힌 뒤 yaw로 방향.
    void PlaceOrientedToForward(Vector3 origin, Vector3 forward)
    {
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();
        float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;   // +z 기준 yaw
        // X+90: 쿼드 평면을 바닥에 눕힘(로컬+y → 월드 수평). 그 다음 Y(yaw)로 전방 정렬.
        _quad.SetPositionAndRotation(origin + Vector3.up * 0.05f,
                                     Quaternion.Euler(90f, yaw, 0f));
    }

    // 원/링: 방향 무관, 바닥에 평평하게.
    void PlaceFlat(Vector3 origin)
    {
        _quad.SetPositionAndRotation(origin + Vector3.up * 0.05f, Quaternion.Euler(90f, 0f, 0f));
    }

    void Begin(float fillDuration, float holdAfterFull)
    {
        _gen++;                          // 새 리스 — 이전(stale) 참조를 무효화
        _fillDuration = Mathf.Max(0.01f, fillDuration);
        _holdAfterFull = Mathf.Max(0f, holdAfterFull);
        _fillElapsed = 0f;
        _holdElapsed = 0f;
        _active = true;
        if (_mr != null) _mr.enabled = true;
        _mat.SetFloat(ProgressId, 0f);
    }

    /// <summary>가득 찬 순간 강제 동기(애니메이션 이벤트가 히트 프레임에 호출 — 클립과 채움 어긋남 방지).
    /// ★gen 보호: 이 패드가 풀 회수로 다른 주인에게 넘어갔으면(세대 불일치) 무시 — 남의 장판 오발 방지(H-2).</summary>
    public void ForceFull(int gen)
    {
        if (!_active || gen != _gen) return;
        _fillElapsed = _fillDuration;
        if (_mat != null) _mat.SetFloat(ProgressId, 1f);
    }

    /// <summary>시전자가 콤보 중 죽거나 비활성되면 호출 — 채움을 멈추고 즉시 풀로 반납.
    /// 안 그러면 시체 위에서 장판이 끝까지 차올라 "닿는다"고 거짓말한다(북극성 #6 공정성).
    /// ★gen 보호: 이미 회수돼 남의 것이 됐으면 무시.</summary>
    public void CancelImmediate(int gen)
    {
        if (!_active || gen != _gen) return;
        Deactivate();
        if (_pool != null) _pool.Return(this);
    }

    void Update()
    {
        if (!_active) return;

        if (_fillElapsed < _fillDuration)
        {
            _fillElapsed += Time.deltaTime;
            float p = Mathf.Clamp01(_fillElapsed / _fillDuration);
            _mat.SetFloat(ProgressId, p);
        }
        else
        {
            // 가득 참 — 잔상 유지 후 풀로 반납.
            _holdElapsed += Time.deltaTime;
            if (_holdElapsed >= _holdAfterFull)
            {
                Deactivate();
                if (_pool != null) _pool.Return(this);
            }
        }
    }

    void OnDestroy()
    {
        if (_mat != null) Destroy(_mat);
    }
}
