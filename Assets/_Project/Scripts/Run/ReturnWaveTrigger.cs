using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Run
{
    /// <summary>
    /// E-001 판돈 볼트온: 회수 불확실성(스펙 §요소4).
    /// 탈출 지점 반경(~8m) 진입 시 1회성 귀로 웨이브 — C구역 출현 포인트(맨홀/창문) 경유
    /// 스태거드 스폰(0.6s 간격). 스폰 좀비는 RegisterImmediate로 시야 게이트에 즉시 편입한다.
    /// ExtractionPoint와 같은 GO에 붙는다(트리거 중심 = 탈출 지점).
    /// </summary>
    public class ReturnWaveTrigger : MonoBehaviour
    {
        [Header("트리거")]
        [Tooltip("이 반경 진입 시 웨이브 발동(런당 1회).")]
        [SerializeField] float triggerRadius = 8f;

        [Header("웨이브")]
        [SerializeField] GameObject zombiePrefab;
        [Tooltip("출현 포인트(맨홀 C / 창문 C). 순환하며 사용.")]
        [SerializeField] Transform[] spawnPoints;
        [Tooltip("마커별 스폰 위치 보정(월드 기준, spawnPoints와 인덱스 대응). 창문 마커=벽 비주얼 슬랩이라 랩 안쪽 +x 1m 이격 — 벽 평면 겹침 디페네트레이션이 좀비를 랩 밖으로 밀어내는 사고 방지. y는 코드에서 바닥(0)으로 스냅.")]
        [SerializeField] Vector3[] spawnOffsets;
        [SerializeField] int waveCount = 4;
        [SerializeField] float spawnStagger = 0.6f;

        [Header("스폰 공정성 (코너랩 스펙 §5.2)")]
        [Tooltip("플레이어로부터 이 거리(m) 미만인 포인트는 피한다. 전 포인트가 미만이면 0.5s 후 재시도(최대 3회), 그래도 안 되면 그냥 스폰.")]
        [SerializeField] float minSpawnDistance = 8f;

        bool _fired;
        Transform _player;
        float _minSpawnDistEffective;   // TTC 가드 반영 실효 최소 스폰 거리 — SpawnWave에서 산출
        readonly List<GameObject> _spawned = new List<GameObject>();   // 스폰 클론 추적(향후 정리 핸들 — 지금은 추적만)

        void Update()
        {
            if (_fired) return;

            // RunPhase 게이트 — InMission/Extracting에서만 발동(사무실/정산 무시).
            if (!PhaseAllows()) return;

            if (_player == null)
            {
                var pc = PlayerController.Instance;
                if (pc == null) return;
                _player = pc.transform;
            }

            Vector3 d = _player.position - transform.position; d.y = 0f;
            if (d.sqrMagnitude > triggerRadius * triggerRadius) return;

            _fired = true;
            StartCoroutine(SpawnWave());
        }

        /// <summary>런 진행 중인가. RunManager 부재(룩데브 씬)는 허용 — Update 게이트와 동일 기준.</summary>
        static bool PhaseAllows()
        {
            var rm = RunManager.Instance;
            return rm == null ||
                   rm.Phase == RunManager.RunPhase.InMission ||
                   rm.Phase == RunManager.RunPhase.Extracting;
        }

        IEnumerator SpawnWave()
        {
            if (zombiePrefab == null || spawnPoints == null || spawnPoints.Length == 0)
            {
                Debug.LogError("[ReturnWaveTrigger] zombiePrefab/spawnPoints 미설정 — 귀로 웨이브 불발.", this);
                yield break;
            }

            // 스폰 가드 TTC 확장(2026-06-12 후방 힌트 짝 규칙) — 도달 3초 이내 지점 스폰 금지.
            // 가청 힌트가 "거짓말"하지 않으려면 힌트 반응 창 안쪽에서 좀비가 물질화되면 안 된다.
            var prefabZombie = zombiePrefab.GetComponent<ZombieController>();
            _minSpawnDistEffective = Mathf.Max(minSpawnDistance, (prefabZombie != null ? prefabZombie.MoveSpeed : 3f) * 3f);

            for (int i = 0; i < waveCount; i++)
            {
                // 페이즈 가드 — 탈출 정산/사망 후 잔여 스폰 차단.
                if (!PhaseAllows()) yield break;

                int preferred = i % spawnPoints.Length;
                int idx = PickFairIndex(preferred);
                for (int retry = 0; idx < 0 && retry < 3; retry++)
                {
                    yield return new WaitForSeconds(0.5f);
                    if (!PhaseAllows()) yield break;
                    idx = PickFairIndex(preferred);
                }
                if (idx < 0) idx = preferred;   // 3회 재시도 후에도 전부 가까우면 그냥 스폰

                if (spawnPoints[idx] != null) SpawnOne(SpawnPosFor(idx));
                yield return new WaitForSeconds(spawnStagger);
            }
        }

        /// <summary>preferred 포인트가 플레이어로부터 minSpawnDistance 이상이면 그대로,
        /// 미만이면 다른 포인트를 순회 탐색. 전부 미만이면 -1.</summary>
        int PickFairIndex(int preferred)
        {
            if (_player == null) return preferred;
            float eff = _minSpawnDistEffective > 0f ? _minSpawnDistEffective : minSpawnDistance;
            float minSqr = eff * eff;
            for (int k = 0; k < spawnPoints.Length; k++)
            {
                int idx = (preferred + k) % spawnPoints.Length;
                if (spawnPoints[idx] == null) continue;
                Vector3 d = SpawnPosFor(idx) - _player.position; d.y = 0f;
                if (d.sqrMagnitude >= minSqr) return idx;
            }
            return -1;
        }

        /// <summary>마커 위치 + 보정 벡터, y는 바닥(0) 스냅 — 공중/벽면 마커여도 랩 바닥 안쪽에서 출현.</summary>
        Vector3 SpawnPosFor(int index)
        {
            Vector3 pos = spawnPoints[index].position;
            if (spawnOffsets != null && index < spawnOffsets.Length) pos += spawnOffsets[index];
            pos.y = 0f;
            return pos;
        }

        void SpawnOne(Vector3 pos)
        {
            var obj = Instantiate(zombiePrefab, pos, Quaternion.identity);
            var zombie = obj.GetComponent<ZombieController>();
            if (zombie == null)
            {
                Debug.LogError("[ReturnWaveTrigger] zombiePrefab에 ZombieController 없음 — 스폰 파기.", this);
                Destroy(obj);
                return;
            }

            _spawned.Add(obj);

            // 시야 게이트 즉시 편입 — LKP 리스캔(0.3s 주기)까지의 노출 공백 차단.
            var lkp = ZombieLKPSilhouette.Instance;
            if (lkp != null) lkp.RegisterImmediate(zombie);
            RearThreatHint.RegisterImmediate(zombie);   // 후방 힌트도 즉시 편입 — 어그로 스폰 힌트 공백 차단

            if (_player == null) return;
            zombie.Init(_player, pos);
            zombie.PlaySpawnAnimation(SpawnAnimation.RiseFromGround);   // 맨홀/창문 = "숨어 있었다" 알리바이
            zombie.AlertTo(_player.position);                           // 귀로 웨이브 = 즉시 압박(배회 아님)
        }
    }
}
