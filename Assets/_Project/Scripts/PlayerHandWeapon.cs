using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 플레이어 오른손 본에 무기 메시(Synty BR 프리팹)를 소켓 부착하고,
/// PlayerCombat.CurrentGunClass 폴링으로 현재 무기만 켠다.
///
/// 부착은 씬 오브젝트가 아니라 런타임 부트스트랩 — 씬을 더티시키지 않는다(병렬 세션이 씬 소유).
/// Resources/PlayerWeaponSet.prefab(이 컴포넌트 + 무기 3종 자식)을
/// animator.GetBoneTransform(RightHand) 아래로 인스턴스해 본을 그대로 따라간다.
///
/// 폴링 규약: 스탠스 스왑(PlayerLocomotionAnimator.ApplyStance)과 동일 — 이벤트 결합 없음.
/// 그립 오프셋은 클래스별 SerializeField로 노출(초기값 0) — 정밀 정렬은 플레이모드 실측 단계에서.
/// </summary>
public class PlayerHandWeapon : MonoBehaviour
{
    const string PrefabResourceName = "PlayerWeaponSet";

    [Header("Weapon Meshes (GunClass별 — 프리팹 자식 참조)")]
    [SerializeField] GameObject pistolWeapon;
    [SerializeField] GameObject rifleWeapon;
    [SerializeField] GameObject shotgunWeapon;

    [Header("Grip Offsets (오른손 본 로컬 — 그립 정렬 튜닝용)")]
    [SerializeField] Vector3 pistolOffsetPos;
    [SerializeField] Vector3 pistolOffsetEuler;
    [SerializeField] Vector3 rifleOffsetPos;
    [SerializeField] Vector3 rifleOffsetEuler;
    [SerializeField] Vector3 shotgunOffsetPos;
    [SerializeField] Vector3 shotgunOffsetEuler;

    PlayerCombat _combat;       // 부트스트랩이 Bind로 주입 — Awake의 부모 체인 탐색은 폴백
    bool _warnedNoCombat;       // 무음 실패 방지용 1회 경고 플래그

    /// <summary>
    /// 앱 기동 시 1회: 현재 씬에 즉시 부착 시도 + 이후 씬 로드마다 재시도하도록 구독.
    /// AfterSceneLoad는 기동 시 1회만 발화하므로, 런타임 씬 전환(LoadScene)으로 들어온
    /// 전투 씬은 sceneLoaded 핸들러가 커버한다.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        // 도메인 리로드 OFF 환경에서 부트스트랩 재진입 시 정적 이벤트 중복 구독 방지(-= 후 +=).
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;

        TryAttach();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryAttach();

    /// <summary>
    /// PlayerCombat이 있는 씬(전투 씬)에서만 손 무기 소켓을 부착.
    /// 타이틀/오피스 씬엔 PlayerCombat이 없어 조용히 무동작.
    /// </summary>
    static void TryAttach()
    {
        var combat = Object.FindFirstObjectByType<PlayerCombat>();
        if (combat == null) return;   // 타이틀/오피스 씬 가드

        // CharacterVisual의 휴머노이드 Animator — 비주얼 자식에 1개만 존재.
        Animator animator = null;
        foreach (var a in combat.GetComponentsInChildren<Animator>())
            if (a.isHuman) { animator = a; break; }
        if (animator == null) return;

        Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        if (hand == null) return;

        // 중복 부착 가드(sceneLoaded 재호출·도메인 리로드 OFF 재진입에도 1개만) — 재진입 안전망.
        if (hand.GetComponentInChildren<PlayerHandWeapon>() != null) return;

        var prefab = Resources.Load<GameObject>(PrefabResourceName);
        if (prefab == null)
        {
            Debug.LogWarning($"[PlayerHandWeapon] Resources/{PrefabResourceName} 로드 실패 — 손 무기 비표시.");
            return;
        }

        var instance = Object.Instantiate(prefab, hand, false);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;

        // 부트스트랩이 이미 찾아둔 참조를 직접 주입 — 계층 가정(GetComponentInParent)이 깨져도 안전.
        var handWeapon = instance.GetComponent<PlayerHandWeapon>();
        if (handWeapon != null) handWeapon.Bind(combat);
    }

    /// <summary>부트스트랩이 찾아둔 PlayerCombat을 직접 주입(Instantiate 직후 호출).</summary>
    public void Bind(PlayerCombat combat)
    {
        if (combat != null) _combat = combat;
    }

    void Awake()
    {
        // 폴백: Bind 주입 전/외부 경로 인스턴스 대비 — 부모 체인에서 탐색(Instantiate에 부모 지정).
        _combat = GetComponentInParent<PlayerCombat>();
    }

    void Update()
    {
        if (_combat == null)
        {
            // Bind도 폴백도 실패 — 무음 실패 대신 1회 경고(씬 전환/사망 직후 한 프레임도 여기로 오지만 파괴 직전이라 무해).
            if (!_warnedNoCombat)
            {
                _warnedNoCombat = true;
                Debug.LogWarning("[PlayerHandWeapon] PlayerCombat 참조 없음(Bind 미주입 + 부모 체인 탐색 실패) — 손 무기 비활성 상태로 유지.", this);
            }
            return;
        }

        GunSfx.GunClass cls = _combat.CurrentGunClass;

        SetWeaponState(pistolWeapon, cls == GunSfx.GunClass.Pistol, pistolOffsetPos, pistolOffsetEuler);
        SetWeaponState(rifleWeapon, cls == GunSfx.GunClass.Rifle, rifleOffsetPos, rifleOffsetEuler);
        SetWeaponState(shotgunWeapon, cls == GunSfx.GunClass.Shotgun, shotgunOffsetPos, shotgunOffsetEuler);
    }

    /// <summary>활성 무기만 켜고, 켜진 무기에 그립 오프셋을 매 프레임 적용(플레이모드 라이브 튜닝용 — 3개라 비용 무시 가능).</summary>
    static void SetWeaponState(GameObject weapon, bool active, Vector3 offsetPos, Vector3 offsetEuler)
    {
        if (weapon == null) return;
        if (weapon.activeSelf != active) weapon.SetActive(active);
        if (active)
        {
            weapon.transform.localPosition = offsetPos;
            weapon.transform.localEulerAngles = offsetEuler;
        }
    }
}
