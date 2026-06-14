using UnityEngine;

/// <summary>
/// ContainmentZone 그레이박스 스폰 마커 — LevelDesign이 author한 좌표/그룹/롤/스태거를
/// 씬에 박아두는 순수 데이터 컴포넌트. ★런타임 로직 없음(SpawnDirector는 Phase 2).
///
/// 빌드 스펙: docs/02_logs/2026-06-13-containment-zone-graybox-build-spec.md §10
///  - groupTag: 웨이브 그룹 식별(Z0_Tutorial / Z1_AmbushL / Z2_Pack / Z2_Air …)
///  - role: Ambient(상시 배경) / WaveSpawn(트립·proximity 발동) / EliteSpawn(엘리트)
///  - scatter: SpawnDirector가 마커 주변 무작위 분산할 반경(0 = 정확 위치)
///  - staggerDelay: 같은 groupTag 내 순차 깨움 지연(시차쇄도 author, Phase 2 소비)
///  - isNavMeshValid: Editor NavMesh 검증 스탬프(NavMesh.SamplePosition 결과) — 런타임 미사용
/// </summary>
public class SpawnPointMarker : MonoBehaviour
{
    public enum Role { Ambient, WaveSpawn, EliteSpawn }

    [Header("Identity")]
    [Tooltip("웨이브 그룹 식별 태그 (예: Z2_Pack)")]
    public string groupTag = "";

    [Tooltip("스폰 역할 — Ambient/WaveSpawn/EliteSpawn")]
    public Role role = Role.WaveSpawn;

    [Header("Authoring")]
    [Tooltip("SpawnDirector가 마커 주변 분산할 반경(m). 0 = 정확 위치")]
    public float scatter = 0f;

    [Tooltip("같은 groupTag 내 순차 깨움 지연(s) — 시차쇄도 author")]
    public float staggerDelay = 0f;

    [Header("NavMesh 검증 스탬프 (Editor)")]
    [Tooltip("NavMesh.SamplePosition 검증 결과 — Editor 툴이 채움, 런타임 미사용")]
    public bool isNavMeshValid = false;

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Color c = role switch
        {
            Role.EliteSpawn => new Color(1f, 0.3f, 0.1f),   // 엘리트 = 주황
            Role.Ambient    => new Color(0.5f, 0.8f, 1f),   // 앰비언트 = 하늘
            _               => new Color(1f, 0.85f, 0.2f),  // 웨이브 = 노랑
        };
        // NavMesh 무효면 빨강 경고로 덮어쓰기
        if (!isNavMeshValid) c = Color.Lerp(c, Color.red, 0.6f);

        Gizmos.color = c;
        Vector3 p = transform.position;
        Gizmos.DrawWireSphere(p, 0.6f);
        // facing 화살표(forward)
        Gizmos.DrawLine(p, p + transform.forward * 1.5f);
        if (scatter > 0.01f)
        {
            Gizmos.color = new Color(c.r, c.g, c.b, 0.25f);
            Gizmos.DrawWireSphere(p, scatter);
        }
    }
#endif
}
