using UnityEngine;

namespace Run
{
    /// <summary>
    /// E-001 판돈 볼트온: 깊이 가중 strain 판정(스펙 §요소1).
    /// 랩 로컬 z(월드 z - 랩 원점 z)로 구역 판정 — z&lt;28=A(+1), 28~64=B(+2), &gt;64=C(+3).
    /// 씬 싱글톤. ZombieController.HarvestStrain이 WeightAt으로 조회한다.
    /// 컴포넌트가 씬에 없으면(다른 씬 등) 가중 1로 폴백 — 기존 동작 그대로.
    /// </summary>
    public class LabDepthStrain : MonoBehaviour
    {
        public static LabDepthStrain Instance { get; private set; }

        [Header("랩 좌표 (CornerLab_B008 원점 = world (60, 0, -45))")]
        [Tooltip("랩 원점의 월드 z. 로컬 z = 월드 z - 이 값.")]
        [SerializeField] float labOriginZ = -45f;

        [Header("구역 경계 (랩 로컬 z)")]
        [SerializeField] float zoneBMinLocalZ = 28f;
        [SerializeField] float zoneCMinLocalZ = 64f;

        [Header("랩 footprint x 경계 (월드 x) — 밖이면 앰비언트 취급, 가중 1")]
        [SerializeField] float labMinX = 59f;
        [SerializeField] float labMaxX = 92f;

        [Header("구역별 strain 가중")]
        [SerializeField] int weightA = 1;
        [SerializeField] int weightB = 2;
        [SerializeField] int weightC = 3;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>월드 위치의 strain 가중(A=1/B=2/C=3). 판정기 부재 시 1.</summary>
        public static int WeightAt(Vector3 worldPos)
        {
            var i = Instance;
            if (i == null) return 1;
            // 랩 footprint 밖(앰비언트 좀비)은 깊이 가중 없음 — z만 보면 랩 밖 고-z 좀비가 +3 받는 구멍.
            if (worldPos.x < i.labMinX || worldPos.x > i.labMaxX) return 1;
            float localZ = worldPos.z - i.labOriginZ;
            if (localZ > i.zoneCMinLocalZ) return i.weightC;
            if (localZ >= i.zoneBMinLocalZ) return i.weightB;
            return i.weightA;
        }
    }
}
