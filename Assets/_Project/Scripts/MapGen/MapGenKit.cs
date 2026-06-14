#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ZombieCrush.MapGen
{
    /// <summary>
    /// 치수 인식 배치 DSL — 손으로 던지던 RunCommand 배치를 *재사용 가능·치수 정합* 코드로 승격.
    ///
    /// 헌법(natural-pull-doctrine §3 높이 규칙):
    ///  - 모든 프리팹은 단일 전투 평면. 바닥 정렬 = renderer bounds.min.y 를 y=0 에 맞춤(묻힘/부유 방지).
    ///  - 비균일 스케일 ❌ (회전만, scale 1). 윗면 워크어블 무관(시각만).
    ///
    /// 가족 헌법(asset-driven-map-composition §1):
    ///  - POLYGON BattleRoyale + Construction 단일 가족만. Toon City / PBR / Western / Generic 배제.
    ///  - 프리팹 이름이 여러 가족에 중복되면(예: SM_Env_Road_Straight_01 = BR·Western 둘 다) 가족 폴더로만 해소.
    ///
    /// 에디터 전용(UNITY_EDITOR) — AssetDatabase / PrefabUtility 사용. 플레이모드 의존 없음.
    /// 모든 메서드 static = 메뉴/RunCommand 양쪽에서 호출 가능.
    /// </summary>
    public static class MapGenKit
    {
        // ── 가족 헌법: 이 두 폴더 안에서만 프리팹을 찾는다(다른 가족 동명 프리팹 차단) ──
        static readonly string[] FamilyRoots =
        {
            "Assets/Synty/PolygonBattleRoyale",
            "Assets/Synty/PolygonConstruction",
        };

        // 프리팹 에셋 캐시(이름 → 에셋). null = 조회 실패(로그 1회).
        static readonly Dictionary<string, GameObject> _prefabCache = new Dictionary<string, GameObject>();
        // 풋프린트 캐시(이름 → 로컬 bounds, scale 1·rot 0 기준).
        static readonly Dictionary<string, Bounds> _footprintCache = new Dictionary<string, Bounds>();

        /// <summary>캐시 비우기 — 에셋 리임포트/리프레시 후 stale 방지용(테스트 편의).</summary>
        public static void ClearCaches()
        {
            _prefabCache.Clear();
            _footprintCache.Clear();
        }

        /// <summary>
        /// 문자열 안정 해시(FNV-1a 32bit). string.GetHashCode()는 .NET/Mono에서 프로세스마다 무작위화돼
        /// 세션 간 결정론이 깨진다(Stab H-1/Codex). 시드 분기는 *항상 같은 값*이어야 하므로 이걸 쓴다.
        /// </summary>
        static int StableHash(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            unchecked
            {
                uint h = 2166136261u;
                for (int i = 0; i < s.Length; i++) { h ^= s[i]; h *= 16777619u; }
                return (int)h;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // 프리팹 조회 (가족 폴더 한정 + 캐시)
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// 이름으로 POLYGON 가족(BR+CN) 프리팹 에셋을 찾는다. 못 찾으면 null + 경고 1회.
        /// 동명이 여러 가족에 있으면 가족 폴더 안의 것만 채택(Western/Toon 등 차단).
        /// </summary>
        public static GameObject FindPrefab(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName)) return null;
            if (_prefabCache.TryGetValue(prefabName, out var cached)) return cached;

            GameObject found = null;
            // t:Prefab 검색을 가족 폴더로 한정 → 동명 충돌을 폴더로 해소.
            string[] guids = AssetDatabase.FindAssets($"{prefabName} t:Prefab", FamilyRoots);
            string matchedPath = null;
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                // FindAssets 는 부분일치 → 파일명 정확일치만 채택.
                if (System.IO.Path.GetFileNameWithoutExtension(path) != prefabName) continue;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;
                if (found == null) { found = go; matchedPath = path; }
                else if (path != matchedPath)
                    Debug.LogWarning($"[MapGenKit] '{prefabName}' 가족 폴더 안에서 중복 발견: {matchedPath} / {path} — 첫 번째 채택.");
            }

            if (found == null)
                Debug.LogWarning($"[MapGenKit] 프리팹 미발견(POLYGON BR/CN 가족): '{prefabName}'. 배치 건너뜀. (오타·미임포트·타 가족인지 확인)");

            _prefabCache[prefabName] = found; // null 도 캐시(반복 경고 방지)
            return found;
        }

        /// <summary>
        /// 프리팹의 로컬 풋프린트(scale 1·rot 0 기준 renderer 합산 bounds). 캐시.
        /// bounds.min.y = 바닥 정렬 오프셋 산출에 쓰고, size.x/z = 패킹에 쓴다.
        /// </summary>
        public static Bounds Footprint(string prefabName)
        {
            // null/빈 이름은 캐시 조회(Dictionary.TryGetValue(null)=throw) 전에 막고 fallback 반환(Codex MED).
            if (string.IsNullOrEmpty(prefabName))
                return new Bounds(Vector3.zero, new Vector3(4f, 1f, 4f));
            if (_footprintCache.TryGetValue(prefabName, out var cached)) return cached;

            var prefab = FindPrefab(prefabName);
            Bounds b = new Bounds(Vector3.zero, new Vector3(4f, 1f, 4f)); // fallback
            if (prefab != null)
            {
                // 원점·무회전·scale 1 로 임시 인스턴스 → 로컬 bounds 측정 → 파기.
                // ★H-1: 측정 중 예외가 나도 임시 인스턴스가 활성 씬에 잔존하지 않도록 try/finally 로 파기 보장.
                GameObject temp = null;
                try
                {
                    temp = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    temp.transform.position = Vector3.zero;
                    temp.transform.rotation = Quaternion.identity;
                    temp.transform.localScale = Vector3.one;
                    var rends = temp.GetComponentsInChildren<Renderer>(true);
                    if (rends.Length > 0)
                    {
                        b = rends[0].bounds;
                        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                    }
                    else Debug.LogWarning($"[MapGenKit] '{prefabName}' renderer 없음 → 풋프린트 fallback {b.size}.");
                }
                finally
                {
                    if (temp != null) Object.DestroyImmediate(temp);
                }
            }
            _footprintCache[prefabName] = b;
            return b;
        }

        // ─────────────────────────────────────────────────────────────────
        // Spawn — 치수 인식 단일 배치(바닥 y=0 flush)
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// 프리팹을 (pos.x, _, pos.z)에 yRot 회전으로 배치하고, renderer bounds.min.y 를 y=0 에 맞춘다.
        /// pos.y 는 추가 오프셋(특수 적층용, 보통 0). 비균일 스케일 금지 — scale 1 고정.
        /// 못 찾으면 null 반환(경고는 FindPrefab 이 1회).
        /// </summary>
        public static GameObject Spawn(string prefabName, Vector3 pos, float yRot, Transform parent)
        {
            var prefab = FindPrefab(prefabName);
            if (prefab == null) return null;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent != null ? parent.gameObject.scene : default);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one;       // 비균일/스케일 지터 금지(헌법)
            // ⚠️ pos/yRot 은 *월드* 절대 트랜스폼으로 덮어쓴다(부모 회전 무시). Compound 는 호출측이
            //    이미 월드 XZ·월드 yaw 로 변환해 넘기므로 우연히 정합. local 좌표 직배치로 리팩터하면
            //    부모 회전이 이중 적용되니 주의(이중변환).
            go.transform.rotation = Quaternion.Euler(0f, yRot, 0f);

            // 회전 후의 월드 bounds.min.y 로 바닥 정렬(회전이 높이를 바꿀 수 있으므로 배치 후 측정).
            float minY = WorldMinY(go);
            float baseOffset = pos.y - minY; // min.y → pos.y(보통 0)
            go.transform.position = new Vector3(pos.x, baseOffset, pos.z);
            return go;
        }

        static float WorldMinY(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>(true);
            if (rends.Length == 0)
            {
                // renderer 0개 → 바닥 정렬 기준이 없어 transform.position.y 폴백(묻힘/부유 가능).
                Debug.LogWarning($"[MapGenKit] '{go.name}' renderer 없음 → 바닥 정렬 폴백(transform.position.y). 묻힘/부유 가능.");
                return go.transform.position.y;
            }
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return b.min.y;
        }

        // ─────────────────────────────────────────────────────────────────
        // Road — 굽은 도로(a→b 세그먼트, 방향정렬 타일 적층)
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// a→b 직선 구간을 도로 타일로 적층(표면 y=0 flush). 타일 길이는 풋프린트(긴 축)로 측정해 일반화.
        /// 굽은 폴리라인은 호출측이 세그먼트마다 Road(a,b) 를 연속 호출(RoadPolyline 헬퍼 참고).
        /// </summary>
        public static void Road(Vector3 a, Vector3 b, Transform parent, string tilePrefab = "SM_Env_Road_Straight_01")
        {
            var fp = Footprint(tilePrefab);
            // 도로 타일의 "진행 길이"(travel) = 풋프린트 z. 실측 Road_Straight_01 = 폭20(X)×진행5(Z)
            //   → 진행 5m씩 적층, 폭 20m. (Codex 오해 주의: "20"은 폭이고 진행축은 z=5가 맞음 — 정상.)
            float tileLen = Mathf.Max(fp.size.z, 0.5f);
            Vector3 a0 = new Vector3(a.x, 0f, a.z);
            Vector3 b0 = new Vector3(b.x, 0f, b.z);
            Vector3 seg = b0 - a0;
            float dist = seg.magnitude;
            if (dist < 0.01f) return;
            Vector3 dir = seg / dist;
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg; // +Z 기준 yaw

            // 타일은 네이티브 길이(tileLen)로 적층해 stretch 방지. spacing = tileLen 고정.
            // 마지막 타일은 끝(b)을 넘지 않도록 center 를 clamp(끝 잔여는 허용 — 균등분할로 늘리지 않음).
            int count = Mathf.Max(1, Mathf.RoundToInt(dist / tileLen));
            float half = tileLen * 0.5f;
            for (int i = 0; i < count; i++)
            {
                float along = tileLen * (i + 0.5f);
                // 마지막 타일이 구간을 넘으면 끝에 맞춰 보정(겹침 약간 허용 > 빈틈/stretch).
                if (along + half > dist) along = Mathf.Max(half, dist - half);
                Vector3 center = a0 + dir * along;
                Spawn(tilePrefab, new Vector3(center.x, 0f, center.z), yaw, parent);
            }
        }

        /// <summary>폴리라인(점 배열)을 연속 도로 세그먼트로 깐다 — 굽은 메인 도로/분기용(레거시 폴백).</summary>
        public static void RoadPolyline(IReadOnlyList<Vector2> pts, Transform parent, string tilePrefab = "SM_Env_Road_Straight_01")
        {
            if (pts == null || pts.Count < 2) return;
            for (int i = 1; i < pts.Count; i++)
                Road(new Vector3(pts[i - 1].x, 0f, pts[i - 1].y),
                     new Vector3(pts[i].x, 0f, pts[i].y), parent, tilePrefab);
        }

        // ─────────────────────────────────────────────────────────────────
        // ★E-2 RoadGraph — 노드(degree로 자동분류) + 엣지(5m Straight 채움) + 교차 피스 회전
        // ─────────────────────────────────────────────────────────────────

        /// <summary>그리드 스냅 한 변(m). Synty 도로/지면 = 5m 그리드(synty-kit §1.2).</summary>
        public const float GridStep = 5f;

        // ── 교차 피스 로컬 방향 보정 오프셋(deg). 실측 캡처 후 *이 한 곳*만 고치면 전 회전이 교정된다.
        //    (Synty 피스 로컬 "정면" 방향을 모르는 채 데이터로 짜야 해서, 캡처 루프 1회 보정 전제.)
        //    rotOverride(노드별)가 있으면 그게 우선.
        public static float CornerBaseRot = 0f;  // Corner 피스 굴절 보정
        public static float TBaseRot      = 0f;  // T 피스 스템 보정
        public static float CrossBaseRot  = 0f;  // Cross(90° 대칭이라 영향 작음)
        public static float EndBaseRot    = 0f;  // End 끝막이 보정

        static Vector2 SnapGrid(Vector2 p) =>
            new Vector2(Mathf.Round(p.x / GridStep) * GridStep, Mathf.Round(p.y / GridStep) * GridStep);

        /// <summary>+Z 기준 yaw(deg). XZ 방향 벡터 → Unity yaw.</summary>
        static float YawFromDir(Vector2 d) => Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;

        /// <summary>
        /// 도로 그래프를 배치한다. 노드 degree 로 End/Corner/T/Cross 자동 분류, 각 노드에 올바른
        /// 교차 피스를 그리드 스냅·회전해 놓고, 엣지를 노드 *사이*(피스 반경만큼 안쪽)에 5m Straight 로 채운다.
        /// 폭불일치(20m 간선 ↔ 10m 집산)는 junction inset 으로 흡수(간선 피스가 큰 쪽 기준).
        /// </summary>
        public static void RoadGraph(RoadGraphSpec g, Transform parent)
        {
            if (g == null || !g.HasGraph) return;
            var groot = new GameObject("RoadGraph");
            groot.transform.SetParent(parent, false);

            // 1) 노드 인덱스 + 그리드 스냅 위치.
            var nodeIndex = new Dictionary<string, RoadNodeSpec>();
            var snapped = new Dictionary<string, Vector2>();
            foreach (var n in g.nodes)
            {
                if (n == null || string.IsNullOrEmpty(n.id)) continue;
                if (nodeIndex.ContainsKey(n.id))
                {
                    Debug.LogWarning($"[MapGenKit] RoadGraph 중복 노드 id '{n.id}' — 첫 번째만 채택.");
                    continue;
                }
                nodeIndex[n.id] = n;
                snapped[n.id] = SnapGrid(n.pos);
            }

            // 2) 각 노드의 *나가는 방향*(인접 노드 향) 목록 — degree 산출 + 회전 기준.
            var dirs = new Dictionary<string, List<Vector2>>();
            foreach (var id in nodeIndex.Keys) dirs[id] = new List<Vector2>();
            var validEdges = new List<(string a, string b)>();
            foreach (var e in g.edges)
            {
                if (e == null) continue;
                if (!snapped.ContainsKey(e.nodeA) || !snapped.ContainsKey(e.nodeB))
                {
                    Debug.LogWarning($"[MapGenKit] RoadGraph 엣지 미해소 노드: '{e.nodeA}'-'{e.nodeB}' — 건너뜀.");
                    continue;
                }
                if (e.nodeA == e.nodeB) continue; // self-loop 방지
                Vector2 pa = snapped[e.nodeA], pb = snapped[e.nodeB];
                Vector2 d = pb - pa;
                if (d.sqrMagnitude < 0.01f) continue;
                dirs[e.nodeA].Add(d.normalized);
                dirs[e.nodeB].Add((-d).normalized);
                validEdges.Add((e.nodeA, e.nodeB));
            }

            // 3) 노드별 junction 피스 배치 — degree 로 분류, 회전 계산.
            //    피스가 차지하는 "반경"(엣지 채움 시작점 inset)도 함께 산출.
            var nodeInset = new Dictionary<string, float>();
            foreach (var kv in nodeIndex)
            {
                string id = kv.Key;
                RoadNodeSpec n = kv.Value;
                var dl = dirs[id];
                int degree = dl.Count;

                RoadNodeType t = n.typeHint != RoadNodeType.Auto ? n.typeHint : ClassifyByDegree(degree);
                string piece; float baseRot;
                switch (t)
                {
                    case RoadNodeType.Cross:  piece = g.crossPiece;  baseRot = CrossBaseRot;  break;
                    case RoadNodeType.T:      piece = g.tPiece;      baseRot = TBaseRot;      break;
                    case RoadNodeType.Corner: piece = g.cornerPiece; baseRot = CornerBaseRot; break;
                    case RoadNodeType.End:    piece = g.endPiece;    baseRot = EndBaseRot;    break;
                    default:                  piece = null;          baseRot = 0f;            break; // degree 0/이상치 → 피스 없음
                }

                // 피스 반경 = 풋프린트 긴 축 절반(엣지 직선이 피스 안쪽까지 안 파고들게 inset).
                float inset = 0f;
                if (!string.IsNullOrEmpty(piece))
                {
                    var fp = Footprint(piece);
                    inset = Mathf.Max(fp.size.x, fp.size.z) * 0.5f;
                    float rot = n.rotOverride != 0f ? n.rotOverride
                                                    : ComputeJunctionRot(t, dl) + baseRot;
                    Vector2 sp = snapped[id];
                    Spawn(piece, new Vector3(sp.x, 0f, sp.y), rot, groot.transform);
                }
                nodeInset[id] = inset;
            }

            // 4) 엣지 직선 채움 — 양 노드 inset 만큼 안쪽부터(교차 피스와 겹침 최소화).
            //    위계 = 두 노드 중 더 "넓은"(간선<집산<국지) 쪽 타일.
            foreach (var (aId, bId) in validEdges)
            {
                Vector2 pa = snapped[aId], pb = snapped[bId];
                Vector2 seg = pb - pa;
                float dist = seg.magnitude;
                if (dist < 0.01f) continue;
                Vector2 dir = seg / dist;
                float ia = nodeInset[aId], ib = nodeInset[bId];
                // inset 합이 구간보다 크면(노드 너무 가까움) 최소 한 타일은 깔되 겹침 허용.
                float startD = Mathf.Min(ia, dist * 0.5f);
                float endD = Mathf.Max(dist - ib, startD);
                Vector2 a2 = pa + dir * startD;
                Vector2 b2 = pa + dir * endD;

                RoadTier tier = WiderTier(nodeIndex[aId].tier, nodeIndex[bId].tier);
                string tile = TierTile(g, tier);
                Road(new Vector3(a2.x, 0f, a2.y), new Vector3(b2.x, 0f, b2.y), groot.transform, tile);
            }
        }

        static RoadNodeType ClassifyByDegree(int degree)
        {
            switch (degree)
            {
                case 1:  return RoadNodeType.End;
                case 2:  return RoadNodeType.Corner;
                case 3:  return RoadNodeType.T;
                default: return degree >= 4 ? RoadNodeType.Cross : RoadNodeType.End; // 0=고립(End로 안전 처리)
            }
        }

        /// <summary>
        /// 교차 피스 yaw 계산 — 인접 엣지 방향들로부터 피스의 "정면"을 정한다.
        ///  End: 유일한 엣지의 반대(막다른 쪽이 바깥). Corner: 두 방향 사잇각(bisector).
        ///  T: 세 방향 중 짝 안 맞는 "스템" 방향. Cross: 첫 방향(90° 대칭이라 미세).
        /// (피스 로컬 정면 미상 → *Base 오프셋 상수*로 캡처 후 일괄 보정.)
        /// </summary>
        static float ComputeJunctionRot(RoadNodeType t, List<Vector2> dl)
        {
            if (dl == null || dl.Count == 0) return 0f;
            switch (t)
            {
                case RoadNodeType.End:
                    return YawFromDir(-dl[0]); // 막다른 면이 엣지 반대쪽
                case RoadNodeType.Corner:
                {
                    if (dl.Count < 2) return YawFromDir(dl[0]);
                    Vector2 bis = (dl[0] + dl[1]);
                    if (bis.sqrMagnitude < 0.001f) bis = dl[0]; // 180°(직선) → 한 방향
                    return YawFromDir(bis.normalized);
                }
                case RoadNodeType.T:
                {
                    if (dl.Count < 3) return YawFromDir(dl[0]);
                    // 스템 = 나머지 둘과 가장 안 정렬된 방향(둘이 거의 반대=관통 직선, 셋째가 가지).
                    int stem = FindStemIndex(dl);
                    return YawFromDir(dl[stem]);
                }
                case RoadNodeType.Cross:
                default:
                    return YawFromDir(dl[0]);
            }
        }

        /// <summary>T 노드의 "스템"(가지) 인덱스 — 나머지 두 방향의 합과 가장 같은 방향(관통선 대칭축).</summary>
        static int FindStemIndex(List<Vector2> dl)
        {
            int best = 0; float bestScore = float.NegativeInfinity;
            for (int i = 0; i < dl.Count; i++)
            {
                Vector2 others = Vector2.zero;
                for (int j = 0; j < dl.Count; j++) if (j != i) others += dl[j];
                // 관통선(나머지 둘)이 거의 반대면 others≈0 → 그 둘이 직선, i 가 스템.
                float score = -others.magnitude; // others 작을수록 i 가 스템
                if (score > bestScore) { bestScore = score; best = i; }
            }
            return best;
        }

        static RoadTier WiderTier(RoadTier a, RoadTier b) =>
            (int)a < (int)b ? a : b; // enum 순서 Arterial(0)<Collector(1)<Local(2) = 넓은 쪽

        static string TierTile(RoadGraphSpec g, RoadTier tier)
        {
            switch (tier)
            {
                case RoadTier.Arterial:  return g.arterialTile;
                case RoadTier.Local:     return g.localTile;
                default:                 return g.collectorTile;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // ★E-3 CorridorRow — 컨테이너 줄(어깨맞대기 벽). 두 행이 통과 회랑 형성.
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// a→b 중심선을 따라 컨테이너를 길이축 방향으로 어깨맞대기 적층(NavMesh obstacle 벽).
        /// 색 다양 = 두 프리팹 번갈아. gap=0 이면 빈틈 0. 윗면 워크어블은 NavMesh 베이크가 차단.
        /// </summary>
        public static void CorridorRow(CorridorRowSpec c, Transform parent)
        {
            if (c == null) return;
            string primary = c.containerPrefab;
            if (FindPrefab(primary) == null) return;
            bool alt = !string.IsNullOrEmpty(c.altContainerPrefab) && FindPrefab(c.altContainerPrefab) != null;

            var fp = Footprint(primary);
            // 컨테이너 길이축 = 긴 수평 변. 진행 방향에 그 변을 정렬해 어깨맞대기.
            float unitLen = Mathf.Max(Mathf.Max(fp.size.x, fp.size.z), 0.5f);

            Vector3 a0 = new Vector3(c.a.x, 0f, c.a.y);
            Vector3 b0 = new Vector3(c.b.x, 0f, c.b.y);
            Vector3 seg = b0 - a0;
            float dist = seg.magnitude;
            if (dist < 0.01f) return;
            Vector3 dir = seg / dist;
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;

            float step = unitLen + Mathf.Max(0f, c.gap);
            int count = Mathf.Max(1, Mathf.RoundToInt(dist / step));

            var croot = new GameObject($"Corridor_{c.name}");
            croot.transform.SetParent(parent, false);
            for (int i = 0; i < count; i++)
            {
                float along = step * (i + 0.5f);
                if (along > dist) along = Mathf.Max(unitLen * 0.5f, dist - unitLen * 0.5f);
                Vector3 center = a0 + dir * along;
                string prefab = (alt && (i % 2 == 1)) ? c.altContainerPrefab : primary;
                Spawn(prefab, new Vector3(center.x, 0f, center.z), yaw, croot.transform);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // E-1 GroundPatch — 복합 지면(콘크리트/풀 타일 격자). 단색 quad 위에 덮음.
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// 맵 전체(±mapHalf)를 baseTile 로 빈틈없이 타일링(완전 바닥). + 이음새 backstop quad(미세 틈 보강).
        /// ★틈의 진짜 원인 = 피벗이 모서리인 타일을 회전하면 제각각 밀림. → TilePatch 가 회전 0 + bounds.center
        ///   를 그리드에 정렬해 균일 적층(틈 0). baseTile 비거나 미발견이면 backstop quad 만(폴백).
        /// </summary>
        public static void TiledFloor(float mapHalf, string baseTile, Color backstop, Transform parent)
        {
            var froot = new GameObject("Floor");
            froot.transform.SetParent(parent, false);

            // 이음새 backstop: 단색 quad 를 살짝 아래 깔아 미세 틈도 어둡게.
            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "Floor_Backstop";
            plane.transform.SetParent(froot.transform, false);
            plane.transform.position = new Vector3(0f, -0.12f, 0f);
            float s = mapHalf * 2f * 1.06f / 10f;
            plane.transform.localScale = new Vector3(s, 1f, s);
            var rend = plane.GetComponent<Renderer>();
            if (rend != null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                var mat = new Material(shader);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", backstop);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", backstop);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
                rend.sharedMaterial = mat;
                var owner = parent != null ? parent.GetComponentInParent<MapGenRoot>() : null;
                if (owner != null) owner.RegisterRuntimeMaterial(mat);
            }

            if (string.IsNullOrEmpty(baseTile) || FindPrefab(baseTile) == null) return; // 폴백: backstop만
            TilePatch(baseTile, Vector2.zero, new Vector2(mapHalf * 2f, mapHalf * 2f), 0f, 0.3f, froot.transform);
        }

        /// <summary>
        /// center·size 사각을 tile 로 채움 — ★회전 0 + 타일 bounds.center 를 그리드에 정렬(피벗 모서리 타일도
        /// 균일 적층 → 틈 0) + y 표면 flush. 완전 바닥/구역 공용 내부 헬퍼.
        /// </summary>
        public static void TilePatch(string tile, Vector2 center, Vector2 size, float y, float overlap, Transform parent)
        {
            var prefab = FindPrefab(tile);
            if (prefab == null) return;
            var fp = Footprint(tile);
            float sx = Mathf.Max(fp.size.x, 0.5f), sz = Mathf.Max(fp.size.z, 0.5f);
            // overlap 상한 = 작은 타일변의 90%(step 0 폭주 방지, Stab M-1).
            float ov = Mathf.Clamp(overlap, 0f, Mathf.Min(sx, sz) * 0.9f);
            float stx = Mathf.Max(0.2f, sx - ov), stz = Mathf.Max(0.2f, sz - ov);
            int nx = Mathf.Max(1, Mathf.CeilToInt(size.x / stx)), nz = Mathf.Max(1, Mathf.CeilToInt(size.y / stz));
            float ox = center.x - (nx - 1) * stx * 0.5f, oz = center.y - (nz - 1) * stz * 0.5f;
            for (int ix = 0; ix < nx; ix++)
                for (int iz = 0; iz < nz; iz++)
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent != null ? parent.gameObject.scene : default);
                    go.transform.SetParent(parent, false);
                    go.transform.localScale = Vector3.one;
                    go.transform.rotation = Quaternion.identity; // ★회전 금지(피벗 모서리 타일 틈 방지)
                    var rends = go.GetComponentsInChildren<Renderer>(true);
                    if (rends.Length == 0) { go.transform.position = new Vector3(ox + ix * stx, y, oz + iz * stz); continue; }
                    Bounds bb = rends[0].bounds; for (int i = 1; i < rends.Length; i++) bb.Encapsulate(rends[i].bounds);
                    // 피벗 보정: 타일 bounds.center(XZ)를 그리드점에 맞추고, min.y 를 y 에 flush.
                    float dx = (ox + ix * stx) - bb.center.x, dz = (oz + iz * stz) - bb.center.z;
                    go.transform.position = new Vector3(go.transform.position.x + dx, y - bb.min.y, go.transform.position.z + dz);
                }
        }

        /// <summary>center·size 사각을 타일로 채움(구역 — 콘크리트/풀/흙 패치). TilePatch 로 위임(회전0·피벗보정).</summary>
        public static void GroundPatch(GroundPatchSpec p, Transform parent)
        {
            if (p == null) return;
            if (FindPrefab(p.tilePrefab) == null) return;
            var proot = new GameObject($"GroundPatch_{p.name}");
            proot.transform.SetParent(parent, false);
            TilePatch(p.tilePrefab, p.centerXZ, p.sizeXZ, 0.03f, p.overlap, proot.transform);
        }

        // ─────────────────────────────────────────────────────────────────
        // E-4 VegetationCluster — 나무/덤불 군집(결정론 산포, 반경 안 채움).
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// center·radius 원 안에 species 중에서 count 개를 결정론적으로 산포(같은 seed=같은 결과).
        /// 회전만 무작위(스케일 1). 빈 모래 바닥을 깨고 생기/차폐. cluster.name+seed 로 시드 분기.
        /// </summary>
        public static void VegetationCluster(VegetationClusterSpec v, int seed, Transform parent)
        {
            if (v == null || v.species == null || v.species.Length == 0 || v.count <= 0) return;
            var valid = new List<string>();
            foreach (var s in v.species) if (FindPrefab(s) != null) valid.Add(s);
            if (valid.Count == 0) return;

            var vroot = new GameObject($"Veg_{v.name}");
            vroot.transform.SetParent(parent, false);
            // 군집마다 다른 시드(이름 해시 결합) — 한 시드로 전 군집 동일 패턴 방지하되 결정론 유지.
            var rng = new System.Random(seed ^ StableHash(v.name));
            for (int i = 0; i < v.count; i++)
            {
                string name = valid[rng.Next(valid.Count)];
                // 균등 원 산포(sqrt 보정).
                double ang = rng.NextDouble() * System.Math.PI * 2.0;
                double r = v.radius * System.Math.Sqrt(rng.NextDouble());
                float x = v.centerXZ.x + (float)(System.Math.Cos(ang) * r);
                float z = v.centerXZ.y + (float)(System.Math.Sin(ang) * r);
                float yaw = (float)(rng.NextDouble() * 360.0);
                Spawn(name, new Vector3(x, 0f, z), yaw, vroot.transform);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Compound — 건물 클러스터(풋프린트 패킹, 무겹침, 축정렬)
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// CompoundSpec 1기를 배치한다. 컴파운드 로컬 좌표계에서 건물을 풋프린트로 한 줄/격자 패킹(무겹침),
        /// 컴파운드 회전(rot)을 전체에 적용. 랜덤 회전 ❌(건물은 축정렬, 컴파운드 단위로만 회전).
        /// </summary>
        public static void Compound(CompoundSpec c, Transform mapRoot)
        {
            if (c == null) return;
            var groot = new GameObject($"Compound_{c.type}_{c.name}");
            groot.transform.SetParent(mapRoot, false);
            groot.transform.position = new Vector3(c.centerXZ.x, 0f, c.centerXZ.y);
            groot.transform.rotation = Quaternion.Euler(0f, c.rot, 0f);

            // 건물을 로컬 X축으로 풋프린트만큼 떼어 한 줄 패킹(겹침 0). 중앙 정렬.
            var names = c.buildingPrefabs ?? System.Array.Empty<string>();
            float gap = Mathf.Max(0f, c.buildingGap);

            // 1) 각 건물 폭(x) 측정 → 총 폭 + gap.
            float[] widths = new float[names.Length];
            float[] depths = new float[names.Length];
            float total = 0f;
            for (int i = 0; i < names.Length; i++)
            {
                var fp = Footprint(names[i]);
                widths[i] = fp.size.x;
                depths[i] = fp.size.z;
                total += widths[i];
                if (i > 0) total += gap;
            }

            // 2) 좌측 끝에서 시작해 중앙 정렬, 각 건물을 로컬(x, 0, 0)에 — base flush 는 Spawn 이 처리.
            float cursor = -total * 0.5f;
            for (int i = 0; i < names.Length; i++)
            {
                float localX = cursor + widths[i] * 0.5f;
                // 컴파운드 로컬 → 월드(회전 포함).
                Vector3 localPos = new Vector3(localX, 0f, 0f);
                Vector3 worldXZ = groot.transform.TransformPoint(localPos);
                // 건물 회전 = 컴파운드 회전(축정렬, 개별 랜덤 회전 금지).
                Spawn(names[i], new Vector3(worldXZ.x, 0f, worldXZ.z), c.rot, groot.transform);
                cursor += widths[i] + gap;
            }

            // 3) 프롭(정황) — 컴파운드 둘레 자유 배치(겹침 회피는 단순 링 배치).
            if (c.props != null && c.props.Length > 0)
            {
                float ringR = Mathf.Max(total * 0.5f + 4f, 6f);
                for (int i = 0; i < c.props.Length; i++)
                {
                    float ang = (i / (float)c.props.Length) * Mathf.PI * 2f;
                    Vector3 local = new Vector3(Mathf.Cos(ang) * ringR, 0f, Mathf.Sin(ang) * ringR);
                    Vector3 w = groot.transform.TransformPoint(local);
                    Spawn(c.props[i], new Vector3(w.x, 0f, w.z), c.rot, groot.transform);
                }
            }
        }

        /// <summary>
        /// 컴파운드 1기의 월드 XZ 축정렬 바운딩 Rect(겹침 검수용 근사). 건물 한 줄 패킹 폭/깊이를
        /// 측정해 회전 4꼭짓점을 월드로 변환, 그 AABB 를 돌려준다(프롭 링은 미포함 — 보수적 코어 영역).
        /// </summary>
        public static Rect CompoundFootprint(CompoundSpec c)
        {
            if (c == null) return Rect.zero;
            var names = c.buildingPrefabs ?? System.Array.Empty<string>();
            float gap = Mathf.Max(0f, c.buildingGap);
            float totalW = 0f, maxD = 0f;
            for (int i = 0; i < names.Length; i++)
            {
                var fp = Footprint(names[i]);
                totalW += fp.size.x;
                if (i > 0) totalW += gap;
                maxD = Mathf.Max(maxD, fp.size.z);
            }
            if (names.Length == 0) { totalW = 4f; maxD = 4f; } // 개활(건물 없음) — 최소 박스
            float hw = totalW * 0.5f, hd = maxD * 0.5f;
            // 로컬 사각 4꼭짓점 → 컴파운드 회전 → 월드 AABB.
            float rad = c.rot * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    float lx = sx * hw, lz = sz * hd;
                    float wx = c.centerXZ.x + (lx * cos + lz * sin);
                    float wz = c.centerXZ.y + (-lx * sin + lz * cos);
                    minX = Mathf.Min(minX, wx); maxX = Mathf.Max(maxX, wx);
                    minZ = Mathf.Min(minZ, wz); maxZ = Mathf.Max(maxZ, wz);
                }
            return Rect.MinMaxRect(minX, minZ, maxX, maxZ);
        }

        // ─────────────────────────────────────────────────────────────────
        // Boundary — 봉쇄벽 둘레
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// 사각 둘레(±mapHalf)에 벽 타일을 적층한다(표면 y=0 flush). 모서리는 cornerPrefab 으로 처리.
        /// 벽 타일 길이는 풋프린트(긴 축)로 측정해 일반화.
        /// </summary>
        public static void Boundary(float mapHalf, Transform parent,
            string wallPrefab = "SM_Env_Port_Wall_01", string cornerPrefab = "SM_Env_Port_Wall_Corner_01")
        {
            var broot = new GameObject("Boundary");
            broot.transform.SetParent(parent, false);

            var fp = Footprint(wallPrefab);
            float wallLen = Mathf.Max(Mathf.Max(fp.size.x, fp.size.z), 1f);

            // 네 변. 각 변 길이 = mapHalf*2 − 모서리 여유. 모서리부터 시작해 안쪽으로 적층.
            float side = mapHalf * 2f;
            // 변마다 yaw: +X변(법선 +X)·−X변·+Z변·−Z변.
            // 벽 길이축을 변 방향에 맞춤.
            BuildWallRun(new Vector3(-mapHalf, 0f, +mapHalf), new Vector3(+mapHalf, 0f, +mapHalf), wallLen, wallPrefab, broot.transform); // 북(+Z)
            BuildWallRun(new Vector3(+mapHalf, 0f, -mapHalf), new Vector3(-mapHalf, 0f, -mapHalf), wallLen, wallPrefab, broot.transform); // 남(-Z)
            BuildWallRun(new Vector3(+mapHalf, 0f, +mapHalf), new Vector3(+mapHalf, 0f, -mapHalf), wallLen, wallPrefab, broot.transform); // 동(+X)
            BuildWallRun(new Vector3(-mapHalf, 0f, -mapHalf), new Vector3(-mapHalf, 0f, +mapHalf), wallLen, wallPrefab, broot.transform); // 서(-X)

            // 네 모서리.
            if (FindPrefab(cornerPrefab) != null)
            {
                Spawn(cornerPrefab, new Vector3(+mapHalf, 0f, +mapHalf), 0f, broot.transform);
                Spawn(cornerPrefab, new Vector3(+mapHalf, 0f, -mapHalf), 90f, broot.transform);
                Spawn(cornerPrefab, new Vector3(-mapHalf, 0f, -mapHalf), 180f, broot.transform);
                Spawn(cornerPrefab, new Vector3(-mapHalf, 0f, +mapHalf), 270f, broot.transform);
            }
        }

        static void BuildWallRun(Vector3 a, Vector3 b, float tileLen, string wallPrefab, Transform parent)
        {
            Vector3 seg = b - a;
            float dist = seg.magnitude;
            if (dist < 0.01f) return;
            Vector3 dir = seg / dist;
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            // 모서리 프리팹이 차지하는 양끝을 살짝 비워(겹침 방지) 안쪽만 채운다.
            float inset = tileLen * 0.5f;
            float usable = dist - inset * 2f;
            if (usable <= 0f) return;
            int count = Mathf.Max(1, Mathf.RoundToInt(usable / tileLen));
            float step = usable / count;
            for (int i = 0; i < count; i++)
            {
                Vector3 center = a + dir * (inset + step * (i + 0.5f));
                Spawn(wallPrefab, new Vector3(center.x, 0f, center.z), yaw, parent);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // ScatterRuins — 폐허 산포(결정론 System.Random(seed))
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// area(XZ Rect) 안에 ruinPrefabs 중에서 n 개를 결정론적으로 산포한다(같은 seed=같은 결과).
        /// 회전만 무작위(스케일 1 고정). 폐허 어휘(rubble·damaged road·destroyed car)를 깔아 "초등학생 게임" 방지.
        /// </summary>
        public static void ScatterRuins(Rect area, string[] ruinPrefabs, int n, int seed, Transform parent)
        {
            if (ruinPrefabs == null || ruinPrefabs.Length == 0 || n <= 0) return;
            var valid = new List<string>();
            foreach (var p in ruinPrefabs) if (FindPrefab(p) != null) valid.Add(p);
            if (valid.Count == 0) return;

            var sroot = new GameObject("Ruins");
            sroot.transform.SetParent(parent, false);
            var rng = new System.Random(seed);

            for (int i = 0; i < n; i++)
            {
                string name = valid[rng.Next(valid.Count)];
                float x = area.xMin + (float)rng.NextDouble() * area.width;
                float z = area.yMin + (float)rng.NextDouble() * area.height;
                float yaw = (float)(rng.NextDouble() * 360.0);
                Spawn(name, new Vector3(x, 0f, z), yaw, sroot.transform);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Ground — 베이지 지면(솔리드 색 quad)
        // ─────────────────────────────────────────────────────────────────

        /// <summary>맵 전체를 덮는 단색 지면 quad(y=0, 워크어블 기준면). 색은 spec.groundColor.</summary>
        public static GameObject Ground(float mapHalf, Color color, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.name = "Ground";
            go.transform.SetParent(parent, false);
            go.transform.position = Vector3.zero;
            // Unity Plane = 10×10u @ scale1 → 맵을 덮도록 스케일(여유 5%).
            float s = (mapHalf * 2f * 1.05f) / 10f;
            go.transform.localScale = new Vector3(s, 1f, s);

            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                var mat = new Material(shader);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
                rend.sharedMaterial = mat;
                // ★M-3: new Material 은 GameObject 파괴 시 자동 정리되지 않아 누수 → 루트(MapGenRoot)에
                //   등록해 Clear/재생성 시 함께 파기. 루트가 없으면(독립 호출) 경고.
                var owner = parent != null ? parent.GetComponentInParent<MapGenRoot>() : null;
                if (owner != null) owner.RegisterRuntimeMaterial(mat);
                else Debug.LogWarning("[MapGenKit] Ground 머티리얼을 등록할 MapGenRoot 없음 → 누수 가능(독립 호출).");
            }
            return go;
        }
    }
}
#endif
