// 거대 브루트(Crassorrid) 동시 슬램 조율자 — "몇 기가 치냐" 게이트가 아니라 "어느 각·박자로 치냐" 분산.
//   권위 = docs/02_logs/2026-06-14-large-enemy-ai-research.md §C (유저 확정 Option 1).
//
// ★문제: AttackTokenPool로 동시 슬램 *수*를 막으니, 토큰 못 잡은 브루트가 기웃기웃 맴돌아 어색.
//   소수 거구라 동시 슬램은 OK — 막을 건 "같은 각 / 같은 순간 슬램"뿐.
//
// ★3 규칙(리서치 §C):
//   1) 각 분산(Aztez "같은 각에서 둘 안 옴") — 이미 슬램 중인 피어와 플레이어 기준 방위 차 < angleSpread면 지금 슬램 금지.
//   2) 미세 스태거 0.2~0.4s — 마지막 슬램 커밋부터 staggerMin~Max 내면 잠깐 대기(완전 동시 내려찍기=회피불가 방지, 동시감은 유지).
//   3) 막힌 브루트는 맴돌지 말고 *가장 열린 방위*(플랭크)로 재배치 — 굼뜬 거구라 이동 자체가 위협적.
//
// MonoBehaviour 아님(순수 static) — AttackTokenPool과 같은 결. 씬/프리팹 안 건드림.
//   ★수명: 활성 슬램 방위/마지막 커밋 시각은 정적 상태 → [RuntimeInitializeOnLoadMethod]로 플레이마다 리셋(Roster 패턴).
//   ★stale 누수 차단(제일 위험): 슬램 종료(Recovery 진입/OnDisable)에 반드시 Unregister — 죽은 브루트 방위가 남아 산 브루트를 영영 막는 사고 금지.
using System.Collections.Generic;
using UnityEngine;

public static class BruteSlamCoordinator
{
    // ── 활성 슬램 등록부: 소유자(브루트) → 그 브루트의 플레이어 기준 방위(도, -180~180) ──
    //   값 = "커밋 시점의" 방위. 슬램 중엔 회전 0(Strike/Recovery)이라 방위가 거의 안 변함 → 커밋 시각 스냅샷으로 충분.
    static readonly Dictionary<object, float> ActiveSlamAzimuths = new Dictionary<object, float>();

    // ── 마지막 슬램 커밋 시각(공유) — 미세 스태거 판정. Time.time 기준. ──
    static float _lastCommitTime = -999f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ClearStaticState()
    {
        ActiveSlamAzimuths.Clear();
        _lastCommitTime = -999f;
    }

    /// <summary>플레이어→브루트 방향의 평면 방위(도, atan2). XZ만. (-180,180].</summary>
    public static float AzimuthOf(Vector3 brutePos, Vector3 playerPos)
    {
        float dx = brutePos.x - playerPos.x;
        float dz = brutePos.z - playerPos.z;
        if (dx * dx + dz * dz < 1e-6f) return 0f;   // 동일 위치 = 방위 무의미, 0
        return Mathf.Atan2(dx, dz) * Mathf.Rad2Deg; // atan2(x,z) = +Z 기준 시계 방향 — 일관성만 있으면 기준축 무관
    }

    /// <summary>두 방위 사이 최단 각차(0~180, 절대값). wrap-around 처리.</summary>
    public static float AngleDelta(float a, float b)
    {
        return Mathf.Abs(Mathf.DeltaAngle(a, b));
    }

    /// <summary>
    /// 지금 이 방위로 슬램을 커밋해도 되나? (self는 자기 자신 — 등록부에서 제외).
    /// 막히면 false: ①angleSpread 내 피어가 이미 슬램 중(각 충돌) ②마지막 커밋부터 staggerMin 미만(박자 충돌).
    /// 혼자(피어 0)면 각 충돌 0 → 즉시 true(조율 무의미).
    /// </summary>
    public static bool CanSlamNow(object self, float myAzimuth, float angleSpread, float staggerMin)
    {
        // 미세 스태거: 직전 커밋과 너무 가까우면 박자 충돌(완전 동시 내려찍기 방지).
        //   ★self가 이미 등록돼 있으면(=내가 방금 커밋한 본인) 스태거 자기 차단 금지 — 자기 커밋이 자길 막는 모순.
        if (!ActiveSlamAzimuths.ContainsKey(self) && Time.time - _lastCommitTime < staggerMin)
            return false;

        // 각 분산: angleSpread 내에 슬램 중인 *다른* 피어가 있으면 충돌.
        foreach (var kv in ActiveSlamAzimuths)
        {
            if (ReferenceEquals(kv.Key, self)) continue;
            if (AngleDelta(myAzimuth, kv.Value) < angleSpread)
                return false;
        }
        return true;
    }

    /// <summary>슬램 커밋 등록 — Windup 진입(커밋 확정)에 호출. 방위 스냅샷 + 커밋 시각 갱신.</summary>
    public static void RegisterSlam(object self, float myAzimuth)
    {
        ActiveSlamAzimuths[self] = myAzimuth;
        _lastCommitTime = Time.time;
    }

    /// <summary>슬램 종료/비활성 — Recovery 진입·OnDisable에 호출. ★stale 방위 누수 차단(누락 시 산 브루트 영영 차단 사고).</summary>
    public static void Unregister(object self)
    {
        ActiveSlamAzimuths.Remove(self);
    }

    /// <summary>
    /// 막힌 브루트가 이동할 *가장 열린 방위*(플랭크). 슬램 중인 피어 방위들에서 각거리가 최대인 각을 찾는다.
    /// 피어 0이면(=막힐 이유 없음) 현재 방위 그대로(이동 불필요 — 호출측이 즉시 슬램 가능).
    /// 반환 = 플레이어 기준 절대 방위(도). 호출측이 (이 방위 - 내 현재 방위)를 slotAngleDeg로 변환해 surround 스티어가 데려감.
    /// </summary>
    public static float OpenestAzimuth(object self, float myAzimuth)
    {
        // 슬램 중인 피어 방위 수집(self 제외).
        var peers = new List<float>();
        foreach (var kv in ActiveSlamAzimuths)
        {
            if (ReferenceEquals(kv.Key, self)) continue;
            peers.Add(kv.Value);
        }
        if (peers.Count == 0) return myAzimuth;   // 막힐 이유 없음 — 제자리

        // 후보 방위를 촘촘히 스캔(15° 간격, 24샘플)해 피어들과의 최소 각거리가 최대가 되는 각 선택.
        //   = 모든 슬램 각에서 가장 먼 "빈 틈". 내 현재 방위를 기준에 포함해 동률 시 덜 움직이게 가중.
        float bestAzimuth = myAzimuth;
        float bestClearance = -1f;
        for (int i = 0; i < 24; i++)
        {
            float cand = i * 15f;   // 0~345
            float minClear = 360f;
            for (int p = 0; p < peers.Count; p++)
                minClear = Mathf.Min(minClear, AngleDelta(cand, peers[p]));

            // 동률 타이브레이크: 현재 방위에 가까운 후보 선호(불필요한 큰 우회 방지 — 플랭킹은 "의도적 짧은 이동").
            if (minClear > bestClearance + 0.5f ||
                (Mathf.Abs(minClear - bestClearance) <= 0.5f &&
                 AngleDelta(cand, myAzimuth) < AngleDelta(bestAzimuth, myAzimuth)))
            {
                bestClearance = minClear;
                bestAzimuth = cand;
            }
        }
        return bestAzimuth;
    }
}
