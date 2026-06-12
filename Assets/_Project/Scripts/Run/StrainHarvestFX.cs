using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Run
{
    /// <summary>
    /// strain 수확 연출 매니저 — 좀비 처리 순간 strain을 결정 파편으로 떨궈 플레이어가 줍게 한다(PhysicalDrop).
    ///   (A/B 판정 완료 2026-06-12: PhysicalDrop 승리 — AutoAbsorb 즉시입금 모드는 제거됨.)
    ///
    /// 동작: 처리 → 0.05s 후 회전하는 시안 결정 파편(팔면체) 드랍 + 지면 시안 마커.
    ///   플레이어가 픽업 반경에 들면 입금 + 흡수 쿼드 수렴 연출. 안 주우면 수명 만료로 소실(수확 소실).
    ///
    /// 세계관: strain="메모리"(나노봇 잔해). 현장에 흩어진 잔해를 직접 줍는 거친 회수.
    ///   색은 시안(정상 신호 채널, 스캔펄스 캐넌). 결정 파편의 Y 회전은 부감에서 글린트(실루엣 폭 변화)로 읽힌다.
    ///
    /// 패턴: PurgeSnapshotFX의 자가 부트스트랩 싱글톤 + PlayerAfterimage의 가산 머티리얼/풀/폴백 체인 재사용.
    ///   씬 배치 불필요 — 첫 호출 시 GameObject를 만들고, 자원은 전부 코드 생성, OnDestroy/OnDisable에서 정리.
    ///
    /// ★페일세이프(최우선): 어떤 경로가 막혀도 strain은 증발하지 않는다.
    ///   - 드랍 스폰이 실패하면 즉시 Add로 폴백(잃지 않는다).
    ///   - OnDisable 시 살아있는 드랍은 즉시 입금하고 회수(증발 금지).
    ///   - RunHarvest.Instance가 null이면 입금 대상 자체가 없으니 조용히 버린다(정상).
    ///   - 의도된 유일한 손실 = 드랍의 수명 만료 소실(기회비용).
    /// </summary>
    public class StrainHarvestFX : MonoBehaviour
    {
        // ════════════ 튜닝 레버(핑퐁 대비 한곳에) ════════════

        // ── 흡수 FX(양 모드 공용) ──
        const int AbsorbQuadCount = 4;        // 소스→플레이어로 수렴하는 빌보드 쿼드 수
        const float AbsorbDuration = 0.35f;   // 수렴 시간(초)
        const float AbsorbStartSize = 0.35f;  // 출발 시 쿼드 한 변(m)
        const float AbsorbEndSize = 0.06f;    // 도착 시 쿼드 한 변(m) — 빨려들며 작아짐
        const float AbsorbSpawnHeight = 0.3f; // 소스 +높이(m) — 발밑이 아니라 살짝 띄움
        const float AbsorbArcAmp = 0.45f;     // 수직 sin 호 진폭 기준(m) — 쿼드별 분산
        const float AbsorbHDR = 2.5f;         // HDR 색 배율(블룸 임계 통과)

        // ── 도착 링 펄스 ──
        const float RingDuration = 0.12f;     // 링 확장+페이드 시간(초)
        const float RingStartRadius = 0.4f;   // 링 시작 반경(m)
        const float RingEndRadius = 0.9f;     // 링 끝 반경(m)
        const float RingHeight = 0.05f;       // 지면 z-fighting 회피용 높이(m)

        // ── 드랍 결정 파편 ──
        const float DropDelay = 0.05f;        // 킬 신호와 분리하는 스폰 딜레이(초)
        const float DropSpawnHeight = 0.35f;  // 사망 위치 +높이(m) — 하단 꼭짓점(−0.27)이 지면 위 0.08m
        const float DropScatterMin = 0.25f;   // 다중 드랍 XZ 산란 최소 반경(m) — 호드 밀집 겹침 방지
        const float DropScatterMax = 0.45f;   // 다중 드랍 XZ 산란 최대 반경(m)
        const float DropPickupRadius = 1.3f;  // 플레이어 픽업 반경(m)
        const float DropLifetime = 10f;       // 드랍 수명(초, scaled)
        const float DropBlinkWindow = 2f;     // 만료 전 점멸 구간(초)
        const float DropBlinkHzStart = 2f;    // 점멸 시작 주파수(Hz) — 만료 임박할수록 가속
        const float DropBlinkHzEnd = 8f;      // 점멸 끝 주파수(Hz) — 만료 직전
        const float DropFadeOut = 0.45f;      // 만료 "소독 회수" 연출 시간(초)
        const float DropHDR = 2.2f;           // 드랍 HDR 배율

        // ── 만료 소독 회수(시안→무채 lerp + 회백색 산란 쿼드) ──
        const int PurgeShardCount = 3;        // 흩어지는 회백색 쿼드 수
        const float PurgeShardDuration = 0.4f;// 산란 쿼드 부유 소멸 시간(초)
        const float PurgeShardRise = 0.4f;    // 산란 쿼드 상승 거리(m)
        const float PurgeShardSpread = 0.35f; // 산란 쿼드 측면 분산(m)
        const float PurgeShardSize = 0.12f;   // 산란 쿼드 한 변(m)
        const float PurgeShardHDR = 1.2f;     // 산란 쿼드 HDR 배율(회백 — 차가운 정리 톤)
        // 무채 그레이(루마 보존) — 시안 틴트가 만료 시 여기로 수렴해 "신호 죽음"을 읽힌다.
        static readonly Color PurgeGrayBase = new Color(0.6f, 0.62f, 0.64f, 1f);

        // ── 결정 파편 형태(코드 생성 세로 길쭉 팔면체 — 가산 언릿이라 실루엣이 전부) ──
        const float CrystalTopY = 0.48f;      // 상단 꼭짓점 +높이(m)
        const float CrystalBottomY = -0.27f;  // 하단 꼭짓점 −깊이(m)
        const float CrystalEquatorR = 0.20f;  // 적도 사각형 반경(m)
        const float CrystalSpinSpeed = 50f;   // Y축 연속 회전 속도(°/s, scaled) — 글린트용

        // ── 바닥 마커(드랍마다 지면에 깔리는 희미한 시안 디스크 — 위치 단서) ──
        const float MarkerHeight = 0.03f;     // 지면 +높이(m, z-fight 회피)
        const float MarkerDiameter = 0.8f;    // 마커 지름(m)
        const float MarkerIntensity = 0.35f;  // 마커 색 강도(가산이라 알파 대신 색을 어둡게)

        // ── 정보 레이어 승격(조준 탈색·어둠 면제) ──
        // 드랍/마커/흡수/링/산란 GO를 이 레이어로 보내 AfterPost RenderObjects 피처가 재드로우 → TiltShift 게이트 면제.
        const string PickupInfoLayerName = "PickupInfo";

        // ── 풀 워밍업 ──
        const int AbsorbWarm = 32;            // 흡수 쿼드 프리워밍 수
        const int DropWarm = 24;              // 드랍 구체 프리워밍 수

        // 시안 — 정상 신호 채널(스캔펄스 캐넌). PlayerAfterimage ghostColorCyan과 동일 계열.
        static readonly Color CyanBase = new Color(0.35f, 0.75f, 0.85f, 1f);

        // 셰이더 프로퍼티 ID 캐시 — SetTint가 매 프레임 string 조회(HasProperty(string))하지 않도록.
        // PlayerAfterimage가 _baseColorId를 캐싱한 패턴과 동일. _absorbMat 생성 시 어느 쪽이 유효한지 1회 결정.
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        int _tintId = -1;   // 머티리얼이 받는 색 프로퍼티 ID(-1=둘 다 없음, 틴트 생략)

        // 도메인 리로드 off(Enter Play Mode Options) 에디터에서 static이 이전 세션 값으로 잔존하는 함정 방어.
        // s_instance가 파괴된 GO를 가리키는 것을 차단.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_instance = null;
        }

        // ════════════ 자가 부트스트랩 싱글톤 ════════════

        static StrainHarvestFX s_instance;

        static StrainHarvestFX Get()
        {
            if (s_instance == null)
            {
                var go = new GameObject("StrainHarvestFX");
                s_instance = go.AddComponent<StrainHarvestFX>();
            }
            return s_instance;
        }

        /// <summary>좀비 처리 순간 1회 호출 — 결정 파편을 떨군다(픽업 시 입금).</summary>
        public static void OnZombiePurged(StrainDef def, int weight, Vector3 worldPos)
        {
            if (def == null || weight <= 0) return;   // 입금할 게 없음
            Get().Handle(def, weight, worldPos);
        }

        // ════════════ 런타임 자원 ════════════

        Material _absorbMat;     // 시안 가산(쿼드·링 공용 템플릿 — 인스턴스 색은 풀 슬롯이 따로 가짐)
        Mesh _quadMesh;          // 1×1 평면 쿼드(흡수·링 공용)
        Mesh _crystalMesh;       // 세로 길쭉 팔면체(드랍 결정 파편 — 1회 생성 공유)
        Mesh _discMesh;          // 지름 1 원형 디스크(바닥 마커 — 원이라 부모 Y스핀이 보이지 않음)
        bool _safe;              // 셰이더 폴백까지 전멸하면 false — FX 생략, 입금 폴백은 유지

        readonly List<AbsorbQuad> _absorbPool = new List<AbsorbQuad>();
        readonly List<Ring> _ringPool = new List<Ring>();
        readonly List<Drop> _dropPool = new List<Drop>();

        Camera _cam;             // 빌보드용 — null 가드

        int _pickupLayer = -1;   // PickupInfo 레이어 인덱스(Awake 1회 해석, 미존재 시 0=Default 폴백)

        // ── 흡수 쿼드 1슬롯: 소스→플레이어 호밍 수렴(픽업) 또는 회백 산란 부유(만료 소독) ──
        // 한 풀을 두 모드로 공유 — scatter=true면 호밍 대신 고정 드리프트 부유 후 소멸(제로GC 유지).
        class AbsorbQuad
        {
            public GameObject go;
            public MeshRenderer mr;
            public Material mat;
            public bool active;
            public float age;
            public Vector3 source;     // 출발(사망) 위치 — 호밍 모드: 매 프레임 플레이어로 보간 / 산란 모드: 부유 기준점
            public float arcPhase;     // sin 호 위상(쿼드별 분산)
            public float arcAmp;       // sin 호 진폭(쿼드별 분산)
            // ── 산란 모드 전용(만료 소독 회수) ──
            public bool scatter;       // true=회백 산란 부유 모드(호밍 끔)
            public Vector3 driftDir;   // 부유 방향(위+측면 랜덤, 정규화 안 함 — 길이가 속도)
        }

        // ── 도착 링 1슬롯: 플레이어 발밑 펄스 ──
        class Ring
        {
            public GameObject go;
            public MeshRenderer mr;
            public Material mat;
            public bool active;
            public float age;
            public Vector3 center;     // 도착 시점 플레이어 위치에 고정
        }

        // ── 드랍 결정 파편 1슬롯 ──
        class Drop
        {
            public GameObject go;       // 결정 파편 본체(회전·보빙)
            public MeshRenderer mr;
            public Material mat;
            public GameObject marker;   // 지면 고정 시안 디스크(go의 자식 — 생성/회수 일체화)
            public MeshRenderer markerMr;
            public Material markerMat;
            public bool active;
            public float age;          // 스폰 후 경과(딜레이 포함)
            public bool live;          // 딜레이 끝나 픽업 가능한 상태인가
            public bool purged;        // 만료 페이드 진입 시 회백 산란 쿼드를 1회만 뿌리도록
            public Vector3 basePos;    // 정지 부유 위치(보빙 폐지 — 톤 교정으로 결정은 가만히 떠 있음)
            public StrainDef def;      // 보유 strain
            public int weight;         // 보유 가중
        }

        void Awake()
        {
            if (s_instance != null && s_instance != this) { Destroy(gameObject); return; }
            s_instance = this;

            _cam = Camera.main;

            // PickupInfo 레이어 1회 해석 — AfterPost RenderObjects 피처가 이 레이어를 재드로우해 조준 탈색·어둠을 면제한다.
            // 미존재 시 0(Default) 폴백 + 경고 1회(FX는 그려지되 정보 레이어 승격만 무효 — 페일세이프 우선).
            _pickupLayer = LayerMask.NameToLayer(PickupInfoLayerName);
            if (_pickupLayer < 0)
            {
                _pickupLayer = 0;
                Debug.LogWarning($"[StrainHarvestFX] '{PickupInfoLayerName}' 레이어 미존재 — 드랍이 조준 탈색·어둠 게이트를 면제받지 못함(Default 폴백). TagManager에 레이어를 추가하라.");
            }

            _quadMesh = CreateQuadMesh();
            _crystalMesh = CreateCrystalMesh();
            _discMesh = CreateDiscMesh();
            _absorbMat = CreateAdditiveMaterial(CyanBase, AbsorbHDR);
            _safe = _absorbMat != null;
            if (_safe)   // 틴트 프로퍼티 1회 결정 — 풀 머티리얼은 _absorbMat 복제라 동일 셰이더
                _tintId = _absorbMat.HasProperty(BaseColorId) ? BaseColorId
                        : _absorbMat.HasProperty(ColorId) ? ColorId : -1;
            if (!_safe)
            {
                Debug.LogWarning("[StrainHarvestFX] 가산 셰이더 미발견(URP/Unlit·Sprites/Default 모두) — 흡수/드랍 FX 비활성. 입금 폴백은 유지. 빌드 스트립 의심.");
                return;
            }

            // 프리워밍 — 첫 처리 러시의 생성 스파이크를 게임 시작 1회 비용으로 옮긴다.
            for (int i = 0; i < AbsorbWarm; i++) CreateAbsorbQuad();
            for (int i = 0; i < AbsorbWarm / 4 + 1; i++) CreateRing();   // 링은 처리당 1개라 적게
            for (int i = 0; i < DropWarm; i++) CreateDrop();
        }

        void OnDisable()
        {
            // 페일세이프 — GO 비활성 시 활성 FX가 가산 HDR로 월드에 영구 잔존하지 않도록 전부 끈다.
            foreach (var q in _absorbPool) { q.active = false; if (q.go != null) q.go.SetActive(false); }
            foreach (var r in _ringPool) { r.active = false; if (r.go != null) r.go.SetActive(false); }
            // 드랍은 strain을 보유하므로 비활성 시 페일세이프로 즉시 입금하고 회수(증발 금지).
            // ★active만 보면 됨 — strain은 SpawnDrop에서 active와 함께 확정되므로 딜레이(live 이전) 구간 드랍도 입금해야 한다.
            foreach (var d in _dropPool)
            {
                if (d.active && d.def != null && RunHarvest.Instance != null)
                    RunHarvest.Instance.Add(d.def, d.weight);
                d.active = false; d.live = false; d.def = null;
                if (d.go != null) d.go.SetActive(false);
            }
        }

        void OnDestroy()
        {
            if (s_instance == this) s_instance = null;

            // 풀 슬롯은 부모 없는 독립 GO라 자동 파괴되지 않는다 — 메시/머티리얼/오브젝트 명시 정리.
            if (_absorbMat != null) Destroy(_absorbMat);
            if (_quadMesh != null) Destroy(_quadMesh);
            if (_crystalMesh != null) Destroy(_crystalMesh);
            if (_discMesh != null) Destroy(_discMesh);
            foreach (var q in _absorbPool) { if (q.mat != null) Destroy(q.mat); if (q.go != null) Destroy(q.go); }
            foreach (var r in _ringPool) { if (r.mat != null) Destroy(r.mat); if (r.go != null) Destroy(r.go); }
            // 드랍: 마커 머티리얼은 별도 인스턴스라 명시 파괴(마커 GO는 d.go 자식이라 d.go 파괴 시 함께 사라짐).
            foreach (var d in _dropPool) { if (d.mat != null) Destroy(d.mat); if (d.markerMat != null) Destroy(d.markerMat); if (d.go != null) Destroy(d.go); }
            _absorbPool.Clear(); _ringPool.Clear(); _dropPool.Clear();
        }

        // ════════════ 진입 처리 ════════════

        void Handle(StrainDef def, int weight, Vector3 worldPos)
        {
            // 입금 보류 — 드랍을 떨군다. 스폰 실패 시 즉시 폴백 입금(증발 금지).
            bool spawned = _safe && SpawnDrop(def, weight, worldPos);
            if (!spawned && RunHarvest.Instance != null) RunHarvest.Instance.Add(def, weight);
        }

        // ════════════ 매 프레임 갱신(전부 scaled — 히트스탑 시 세계와 함께 멎음) ════════════

        void LateUpdate()
        {
            if (!_safe) return;
            float dt = Time.deltaTime;
            if (_cam == null) _cam = Camera.main;   // 카메라 교체 대비 재획득

            var pc = PlayerController.Instance;
            Vector3 playerPos = pc != null ? pc.transform.position : Vector3.zero;
            bool hasPlayer = pc != null;

            UpdateAbsorb(dt, playerPos, hasPlayer);
            UpdateRings(dt);
            UpdateDrops(dt, playerPos, hasPlayer);
        }

        // ── 흡수 쿼드: 소스→플레이어 호밍 보간(플레이어가 움직이므로 매 프레임 목표 갱신) ──
        void UpdateAbsorb(float dt, Vector3 playerPos, bool hasPlayer)
        {
            for (int i = 0; i < _absorbPool.Count; i++)
            {
                var q = _absorbPool[i];
                if (!q.active) continue;
                q.age += dt;

                // ── 산란 모드(만료 소독 회수): 호밍/링 없이 고정 드리프트 부유 + 페이드 후 회수 ──
                if (q.scatter)
                {
                    float st = q.age / PurgeShardDuration;
                    if (st >= 1f)
                    {
                        q.active = false; q.scatter = false;
                        if (q.go != null) q.go.SetActive(false);
                        continue;
                    }
                    // ease-out 부유(끝에서 느려지며 잦아듦) + 선형 페이드.
                    float se = 1f - (1f - st) * (1f - st);
                    if (q.go != null)
                    {
                        q.go.transform.position = q.source + q.driftDir * se;
                        FaceCamera(q.go.transform);
                    }
                    SetTint(q.mat, PurgeGrayBase, PurgeShardHDR * (1f - st));
                    continue;
                }

                float t = q.age / AbsorbDuration;
                if (t >= 1f || !hasPlayer)
                {
                    // 도착(또는 플레이어 소멸) — 링 1회 발사하고 회수.
                    if (hasPlayer) SpawnRing(playerPos);
                    q.active = false;
                    if (q.go != null) q.go.SetActive(false);
                    continue;
                }

                // ease-in(t^2)으로 끝에서 빨려드는 가속감. 목표는 매 프레임의 플레이어 위치(호밍).
                float e = t * t;
                Vector3 target = playerPos + Vector3.up * AbsorbSpawnHeight;
                Vector3 pos = Vector3.Lerp(q.source, target, e);
                // 수직 sin 호 — 직선 4개가 똑같이 날면 밋밋하다. 위상/진폭은 쿼드별 분산.
                pos.y += Mathf.Sin(t * Mathf.PI + q.arcPhase) * q.arcAmp * (1f - t);

                q.go.transform.position = pos;
                FaceCamera(q.go.transform);
                float size = Mathf.Lerp(AbsorbStartSize, AbsorbEndSize, e);
                q.go.transform.localScale = new Vector3(size, size, size);
            }
        }

        // ── 도착 링: 반경 확장 + 페이드 ──
        void UpdateRings(float dt)
        {
            for (int i = 0; i < _ringPool.Count; i++)
            {
                var r = _ringPool[i];
                if (!r.active) continue;
                r.age += dt;
                float t = r.age / RingDuration;
                if (t >= 1f)
                {
                    r.active = false;
                    if (r.go != null) r.go.SetActive(false);
                    continue;
                }
                float radius = Mathf.Lerp(RingStartRadius, RingEndRadius, t);
                r.go.transform.localScale = new Vector3(radius * 2f, radius * 2f, radius * 2f);
                SetTint(r.mat, CyanBase, AbsorbHDR * (1f - t));   // 확장하며 페이드
            }
        }

        // ── 드랍 결정 파편: 딜레이→회전·부유→픽업/만료(한 곳에서 리스트 순회, 드랍별 Update 금지) ──
        void UpdateDrops(float dt, Vector3 playerPos, bool hasPlayer)
        {
            for (int i = 0; i < _dropPool.Count; i++)
            {
                var d = _dropPool[i];
                if (!d.active) continue;
                d.age += dt;

                // 스폰 딜레이 — 킬 신호와 시간 분리. 딜레이 동안은 숨김.
                if (d.age < DropDelay) continue;
                if (!d.live)
                {
                    d.live = true;
                    if (d.go != null) d.go.SetActive(true);
                }

                float life = d.age - DropDelay;

                // 만료 = "소독 회수" 연출 — DropFadeOut 동안 시안→무채 lerp 페이드 + 회백 산란 쿼드 1회.
                // 디제틱: 못 주운 메모리는 광역 소독이 회수한다(따뜻한 흡수 아닌 차가운 정리). Add 호출 안 함 = 기회비용 손실.
                if (life >= DropLifetime)
                {
                    if (!d.purged)
                    {
                        d.purged = true;
                        SpawnPurgeShards(d.basePos);   // 회백 쿼드 산란(absorb 풀 재사용, scatter 모드)
                    }
                    float fade = (life - DropLifetime) / DropFadeOut;
                    if (fade >= 1f)
                    {
                        d.active = false; d.live = false; d.def = null;
                        if (d.go != null) d.go.SetActive(false);
                        continue;
                    }
                    // 시안→무채 그레이로 색을 끌고 가며 동시에 페이드 — "신호 죽음".
                    Color tint = Color.Lerp(CyanBase, PurgeGrayBase, fade);
                    SetDropTintColor(d, tint, DropHDR * (1f - fade));
                    PoseDrop(d, life);
                    continue;
                }

                // 픽업 — 한 곳에서 거리 체크. 픽업 시 입금 + 그 자리에서 흡수 FX(시안 수렴 — 불변).
                if (hasPlayer)
                {
                    Vector3 flat = d.basePos - playerPos; flat.y = 0f;
                    if (flat.sqrMagnitude <= DropPickupRadius * DropPickupRadius)
                    {
                        if (RunHarvest.Instance != null) RunHarvest.Instance.Add(d.def, d.weight);
                        SpawnAbsorb(d.basePos);   // 줍는 순간 흡수 연출
                        d.active = false; d.live = false; d.def = null;
                        if (d.go != null) d.go.SetActive(false);
                        continue;
                    }
                }

                // 마지막 2s 가속 점멸 — 주파수 2Hz→8Hz 선형 램프(만료 임박 신호).
                // ★위상 누적: 주파수를 시간에 직접 곱하면(life*f) f가 변할 때 위상이 점프한다(흔한 버그).
                //   대신 ramp 구간 진입 후 경과 u에 대해 위상을 적분: φ(u)=f0·u + (f1−f0)·u²/(2W). 연속·제로상태.
                float remain = DropLifetime - life;
                if (remain <= DropBlinkWindow)
                {
                    float u = DropBlinkWindow - remain;   // ramp 진입 후 경과(0→W)
                    float phase = DropBlinkHzStart * u
                                + (DropBlinkHzEnd - DropBlinkHzStart) * u * u / (2f * DropBlinkWindow);
                    bool on = Mathf.Repeat(phase, 1f) < 0.5f;
                    SetDropTint(d, on ? DropHDR : DropHDR * 0.15f);
                }
                else
                {
                    SetDropTint(d, DropHDR);
                }
                PoseDrop(d, life);
            }
        }

        // 정지 부유(보빙 폐지) + Y축 연속 회전. 결정은 basePos에 가만히 떠 있고, 마커만 지면에 고정.
        void PoseDrop(Drop d, float life)
        {
            if (d.go == null) return;   // 다른 d.go 접근부와 동일하게 방어
            d.go.transform.position = d.basePos;   // 정지 부유 — 상하 사인 운동 제거(톤 교정)
            // 빌보드 대신 Y축 스핀 — 부감에서 실루엣 폭이 주기적으로 변해 글린트.
            d.go.transform.rotation = Quaternion.Euler(0f, life * CrystalSpinSpeed, 0f);
            // 마커는 지면 고정: basePos가 DropSpawnHeight만큼 떠 있으므로 로컬로 끌어내려 지면 마커 높이에 둔다(보빙 상쇄항 제거).
            if (d.marker != null)
                d.marker.transform.localPosition = new Vector3(0f, -DropSpawnHeight + MarkerHeight, 0f);
        }

        // 드랍 색 적용: 크리스탈은 hdr, 마커는 hdr×강도(가산이라 색을 어둡게 — 만료 점멸·페이드를 함께 탄다).
        void SetDropTint(Drop d, float hdr)
        {
            SetDropTintColor(d, CyanBase, hdr);
        }

        // 색 지정 가능 버전 — 만료 "소독 회수"에서 시안→무채 lerp 색을 흘려보내기 위함.
        void SetDropTintColor(Drop d, Color baseColor, float hdr)
        {
            SetTint(d.mat, baseColor, hdr);
            // MarkerIntensity는 절대 강도(0.35) — DropHDR(2.2)를 다시 곱하면 0.77로 떠서 흰색처럼 날아간다(캡처 검증).
            // hdr 인자는 점멸·페이드 비율로만 쓰도록 DropHDR로 정규화.
            if (d.markerMat != null) SetTint(d.markerMat, baseColor, hdr * (MarkerIntensity / DropHDR));
        }

        // ════════════ 스폰 ════════════

        void SpawnAbsorb(Vector3 worldPos)
        {
            Vector3 src = worldPos + Vector3.up * AbsorbSpawnHeight;
            for (int k = 0; k < AbsorbQuadCount; k++)
            {
                var q = GetAbsorbQuad();
                q.active = true;
                q.scatter = false;   // 호밍 모드(산란 모드 재사용 시 리셋)
                q.age = 0f;
                // 출발점을 살짝 흩어 4개가 한 점에서 안 나오게.
                q.source = src + new Vector3(Random.Range(-0.15f, 0.15f), Random.Range(0f, 0.2f), Random.Range(-0.15f, 0.15f));
                q.arcPhase = (k / (float)AbsorbQuadCount) * Mathf.PI * 2f + Random.Range(-0.3f, 0.3f);
                q.arcAmp = AbsorbArcAmp * Random.Range(0.5f, 1.2f) * (k % 2 == 0 ? 1f : -1f);  // 위아래 분산
                q.go.transform.position = q.source;
                q.go.transform.localScale = Vector3.one * AbsorbStartSize;
                FaceCamera(q.go.transform);
                SetTint(q.mat, CyanBase, AbsorbHDR);
                q.go.SetActive(true);
            }
        }

        // 만료 소독 회수 — 결정 위치에서 회백 쿼드 N개가 위+측면 랜덤으로 떠올라 흩어져 소멸.
        // absorb 풀을 scatter 모드로 재사용(제로GC) — 호밍 대신 고정 드리프트 부유.
        void SpawnPurgeShards(Vector3 worldPos)
        {
            Vector3 src = worldPos;   // basePos(이미 떠 있음) 기준
            for (int k = 0; k < PurgeShardCount; k++)
            {
                var q = GetAbsorbQuad();
                q.active = true;
                q.scatter = true;
                q.age = 0f;
                q.source = src + new Vector3(Random.Range(-0.1f, 0.1f), Random.Range(-0.05f, 0.05f), Random.Range(-0.1f, 0.1f));
                // 부유 방향 = 위 보장 + 측면 랜덤. 길이가 곧 이동량(UpdateAbsorb에서 정규화 보간 t로 적용).
                q.driftDir = new Vector3(
                    Random.Range(-PurgeShardSpread, PurgeShardSpread),
                    PurgeShardRise,
                    Random.Range(-PurgeShardSpread, PurgeShardSpread));
                q.go.transform.position = q.source;
                q.go.transform.localScale = Vector3.one * PurgeShardSize;
                FaceCamera(q.go.transform);
                SetTint(q.mat, PurgeGrayBase, PurgeShardHDR);   // 회백 — 차가운 정리 톤
                q.go.SetActive(true);
            }
        }

        void SpawnRing(Vector3 center)
        {
            var r = GetRing();
            r.active = true;
            r.age = 0f;
            r.center = center;
            r.go.transform.position = center + Vector3.up * RingHeight;
            r.go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // 수평으로 눕힘(지면 평행)
            r.go.transform.localScale = new Vector3(RingStartRadius * 2f, RingStartRadius * 2f, RingStartRadius * 2f);
            SetTint(r.mat, CyanBase, AbsorbHDR);
            r.go.SetActive(true);
        }

        bool SpawnDrop(StrainDef def, int weight, Vector3 worldPos)
        {
            var d = GetDrop();
            if (d == null) return false;   // 풀 확장 실패(이론상 없음) → 폴백 입금 유도
            d.active = true;
            d.live = false;       // 딜레이 동안 숨김
            d.purged = false;     // 만료 산란 1회 가드 리셋
            d.age = 0f;
            d.def = def;
            d.weight = weight;
            // 다중 드랍 산란 — 호드 밀집 사망 시 결정이 한 점에 겹쳐 못 읽히는 걸 방지(XZ 링 오프셋).
            float ang = Random.value * Mathf.PI * 2f;
            float rad = Random.Range(DropScatterMin, DropScatterMax);
            Vector3 scatter = new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);
            d.basePos = worldPos + scatter + Vector3.up * DropSpawnHeight;
            d.go.transform.position = d.basePos;
            d.go.transform.rotation = Quaternion.identity;
            // 크리스탈 메시는 실치수로 생성됐으므로 스케일 1(DropDiameter 폐지). 마커는 자식이 자체 스케일로 처리.
            d.go.transform.localScale = Vector3.one;
            SetDropTint(d, DropHDR);   // 크리스탈+마커 초기 색
            d.go.SetActive(false);   // 딜레이 끝나면 UpdateDrops가 켠다(마커는 자식이라 함께 켜짐)
            return true;
        }

        // ════════════ 풀 ════════════

        AbsorbQuad GetAbsorbQuad()
        {
            for (int i = 0; i < _absorbPool.Count; i++)
                if (!_absorbPool[i].active) return _absorbPool[i];
            return CreateAbsorbQuad();
        }

        AbsorbQuad CreateAbsorbQuad()
        {
            var go = new GameObject("AbsorbQuad");
            go.layer = _pickupLayer;   // 정보 레이어 승격(조준 탈색·어둠 면제)
            go.transform.SetParent(null, false);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = _quadMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var mat = new Material(_absorbMat);
            mr.sharedMaterial = mat;
            var q = new AbsorbQuad { go = go, mr = mr, mat = mat, active = false };
            go.SetActive(false);
            _absorbPool.Add(q);
            return q;
        }

        Ring GetRing()
        {
            for (int i = 0; i < _ringPool.Count; i++)
                if (!_ringPool[i].active) return _ringPool[i];
            return CreateRing();
        }

        Ring CreateRing()
        {
            var go = new GameObject("AbsorbRing");
            go.layer = _pickupLayer;   // 정보 레이어 승격(조준 탈색·어둠 면제)
            go.transform.SetParent(null, false);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = _quadMesh;   // 평면 쿼드를 눕혀 링처럼 펄스(가장 싼 방법 — 가산 디스크)
            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var mat = new Material(_absorbMat);
            mr.sharedMaterial = mat;
            var r = new Ring { go = go, mr = mr, mat = mat, active = false };
            go.SetActive(false);
            _ringPool.Add(r);
            return r;
        }

        Drop GetDrop()
        {
            for (int i = 0; i < _dropPool.Count; i++)
                if (!_dropPool[i].active) return _dropPool[i];
            return CreateDrop();
        }

        Drop CreateDrop()
        {
            // 본체 = 세로 길쭉 팔면체 결정 파편(Y 스핀 — 부감 글린트).
            var go = new GameObject("StrainDrop");
            go.layer = _pickupLayer;   // 정보 레이어 승격(조준 탈색·어둠 면제)
            go.transform.SetParent(null, false);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = _crystalMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var mat = new Material(_absorbMat);
            mr.sharedMaterial = mat;

            // 바닥 마커 = 지면에 눕힌 희미한 시안 디스크(쿼드 재사용). go의 자식 — 생성/회수/표시 일체화.
            var marker = new GameObject("StrainDropMarker");
            marker.layer = _pickupLayer;   // 정보 레이어 승격(자식이라도 명시 — 레이어는 상속 안 됨)
            marker.transform.SetParent(go.transform, false);
            marker.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // 수평으로 눕힘(지면 평행)
            marker.transform.localScale = Vector3.one * MarkerDiameter;       // 부모 스케일 1이라 곧 월드 지름
            var markerMf = marker.AddComponent<MeshFilter>();
            markerMf.sharedMesh = _discMesh;   // 원형 — 정사각 쿼드는 부모 Y스핀에 따라 도는 게 노출됐음(캡처 검증)
            var markerMr = marker.AddComponent<MeshRenderer>();
            markerMr.shadowCastingMode = ShadowCastingMode.Off;
            markerMr.receiveShadows = false;
            var markerMat = new Material(_absorbMat);
            markerMr.sharedMaterial = markerMat;

            var d = new Drop { go = go, mr = mr, mat = mat, marker = marker, markerMr = markerMr, markerMat = markerMat, active = false };
            go.SetActive(false);
            _dropPool.Add(d);
            return d;
        }

        // ════════════ 유틸 ════════════

        // 빌보드 — 쿼드가 카메라를 향하도록. 카메라 null이면 회전 생략(쿼드는 기본 방향).
        void FaceCamera(Transform t)
        {
            if (_cam == null) return;
            t.rotation = Quaternion.LookRotation(t.position - _cam.transform.position, Vector3.up);
        }

        // 가산 색 적용: RGB는 HDR 배율, 알파는 1(가산이라 알파 기여 보조). PlayerAfterimage.GhostTint 패턴.
        void SetTint(Material mat, Color baseColor, float hdr)
        {
            if (_tintId < 0) return;   // 틴트 프로퍼티 없는 셰이더 — 생략(매 프레임 string 조회 안 함)
            Color c = baseColor * hdr;
            c.a = baseColor.a;
            mat.SetColor(_tintId, c);
        }

        // 1×1 중심 평면 쿼드(XY 평면, +Z 노멀). 빌보드는 transform 회전으로 처리.
        Mesh CreateQuadMesh()
        {
            var mesh = new Mesh { name = "StrainFXQuad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3( 0.5f, -0.5f, 0f),
                new Vector3( 0.5f,  0.5f, 0f),
                new Vector3(-0.5f,  0.5f, 0f),
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(1f, 1f), new Vector2(0f, 1f),
            };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            return mesh;
        }

        // 지름 1 원형 디스크(XY 평면, +Z 노멀 — 쿼드와 같은 기준이라 기존 눕히기 회전(90,0,0) 그대로).
        // 바닥 마커용: 원은 부모 Y스핀에 시각 불변이라 회전 상쇄가 필요 없다. 16분할 팬.
        Mesh CreateDiscMesh()
        {
            const int Segments = 16;
            var mesh = new Mesh { name = "StrainFXDisc" };
            var verts = new Vector3[Segments + 1];
            var tris = new int[Segments * 6];   // 양면(두 와인딩) — 크리스탈과 같은 이유로 컬링 무관하게 보이도록
            verts[0] = Vector3.zero;
            for (int i = 0; i < Segments; i++)
            {
                float a = i / (float)Segments * 2f * Mathf.PI;
                verts[i + 1] = new Vector3(Mathf.Cos(a) * 0.5f, Mathf.Sin(a) * 0.5f, 0f);
                int next = (i + 1) % Segments + 1;
                tris[i * 6] = 0; tris[i * 6 + 1] = i + 1; tris[i * 6 + 2] = next;
                tris[i * 6 + 3] = 0; tris[i * 6 + 4] = next; tris[i * 6 + 5] = i + 1;
            }
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        // 세로 길쭉 팔면체 결정 파편(실치수). 상단 꼭짓점·하단 꼭짓점 + 적도 사각형 4점 → 8삼각형.
        // 가산 언릿이라 노멀 무관 — 실루엣이 전부. 와인딩은 무시 가능(양면처럼 보이도록 두 갈래 모두 채움).
        Mesh CreateCrystalMesh()
        {
            var mesh = new Mesh { name = "StrainCrystalShard" };
            float r = CrystalEquatorR;
            // 0=상단, 1=하단, 2~5=적도 사각형(+X,+Z,−X,−Z)
            mesh.vertices = new[]
            {
                new Vector3(0f, CrystalTopY, 0f),     // 0 상단 꼭짓점
                new Vector3(0f, CrystalBottomY, 0f),  // 1 하단 꼭짓점
                new Vector3( r, 0f,  0f),             // 2 적도 +X
                new Vector3( 0f, 0f,  r),             // 3 적도 +Z
                new Vector3(-r, 0f,  0f),             // 4 적도 −X
                new Vector3( 0f, 0f, -r),             // 5 적도 −Z
            };
            // 상단 4삼각형(꼭짓점→적도 변) + 하단 4삼각형. 적도는 2-3-4-5 순환.
            mesh.triangles = new[]
            {
                0, 2, 3,  0, 3, 4,  0, 4, 5,  0, 5, 2,   // 상단 팬
                1, 3, 2,  1, 4, 3,  1, 5, 4,  1, 2, 5,   // 하단 팬(역와인딩)
            };
            mesh.RecalculateBounds();
            return mesh;
        }

        // 가산 언릿 — PlayerAfterimage.CreateGhostMaterial 폴백 체인. 전멸 시 null(호출부가 입금 폴백 유지).
        Material CreateAdditiveMaterial(Color tint, float hdr)
        {
            var sh = Shader.Find("Universal Render Pipeline/Unlit");
            Material m;
            if (sh != null)
            {
                m = new Material(sh);
                m.SetFloat("_Surface", 1f);   // Transparent
                m.SetFloat("_Blend", 2f);     // Additive
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.One);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.DisableKeyword("_ALPHATEST_ON");
                m.renderQueue = (int)RenderQueue.Transparent;
            }
            else
            {
                var fallback = Shader.Find("Sprites/Default");
                if (fallback == null) return null;   // 최종 폴백도 없음 — 안전 무효화
                m = new Material(fallback);
            }
            Color c = tint * hdr; c.a = tint.a;
            if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, c);
            if (m.HasProperty(ColorId)) m.SetColor(ColorId, c);
            return m;
        }
    }
}
