using UnityEngine;

namespace ZombieCrush.MapGen
{
    /// <summary>
    /// MapGen 이 씬에 박는 동심원 LV 밴드 스폰 마커 — 순수 데이터(런타임 로직 없음).
    /// 스폰 디렉터(Gameplay 후속)가 lvBand 풀·role 출처를 읽어 동적 젠에 쓴다.
    /// ★카운트 ❌(빌드스펙 §디렉터) — 마커는 *어디서 솟나*(role) + *어느 밴드*(lvBand)만.
    ///
    /// 기존 SpawnPointMarker 와 별개: 그건 ContainmentZone 그레이박스 전용(lvBand·FlyerSpawn 없음).
    /// MapGen 데이터(MapSpec.SpawnMarker)와 1:1 대응을 위해 신설.
    /// </summary>
    public class MapGenSpawnMarker : MonoBehaviour
    {
        [Tooltip("동심원 LV 밴드: 1=외곽 / 2~3=중간 / 4~5=코어")]
        public int lvBand = 1;

        [Tooltip("디제틱 젠 출처 종류 — 카운트 아님")]
        public SpawnRole role = SpawnRole.WaveSpawn;

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Color c = role switch
            {
                SpawnRole.EliteSpawn => new Color(1f, 0.3f, 0.1f),   // 엘리트 = 주황
                SpawnRole.FlyerSpawn => new Color(0.7f, 0.4f, 1f),   // 비행 = 보라
                SpawnRole.Ambient    => new Color(0.5f, 0.8f, 1f),   // 앰비언트 = 하늘
                _                    => new Color(1f, 0.85f, 0.2f),  // 웨이브 = 노랑
            };
            Gizmos.color = c;
            Vector3 p = transform.position;
            Gizmos.DrawWireSphere(p, 0.8f);
            Gizmos.DrawLine(p, p + Vector3.up * (1f + lvBand)); // 밴드 높을수록 긴 막대
        }
#endif
    }
}
