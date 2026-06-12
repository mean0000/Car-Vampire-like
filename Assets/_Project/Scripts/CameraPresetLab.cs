using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 카메라 프리셋 랩 — 유저 인더루프 노브 세션용 실험 장치 (임시).
/// 목적: 플레이 중 F1~F4로 카메라 문법(피치/거리/룩어헤드)을 즉석 전환·판정.
/// 배경 권위: docs/00_authority/2026-06-10-camera-system.md (45°/15m 동결 — 이번 세션 재심사 대상)
///
/// ★ 구조: PlayerCameraRig(단일 권위)를 일절 수정하지 않고 "위에서 값을 덮는" 래퍼.
///   리그는 transform.position을 자기 이전 출력으로 가정하고 SmoothDamp하므로,
///   덮어쓴 채 두면 리그가 교란을 먹고 추종 바이어스가 증폭된다(피드백 루프).
///   → 샌드위치 실행 순서로 해결:
///     1) CameraPresetLabRestorer.Update(-9): 리그의 마지막 순수 출력으로 복원.
///        PlayerCombat.Update(-10) "직후"라 조준 레이(마우스→월드)는 화면에 보이는 포즈로 계산됨.
///     2) PlayerCameraRig.LateUpdate(-50): 평소처럼 자기 출력 기준으로 추종/충격 합성 — 무교란.
///     3) 본 컴포넌트 LateUpdate(-40): 리그 출력을 캐시한 뒤 프리셋 포즈로 덮어쓰기.
///        이후 실행되는 LateUpdate(order 0 — 시야 콘/오클루전/오디오 리그 등)는 화면 포즈를 본다.
///
/// ★ 씬 비부착: 씬 파일 오염 방지를 위해 자가 부트스트랩(런타임 AddComponent)으로만 붙는다.
///   → SerializeField 기본값은 항상 코드가 진실(씬 저장값이 덮는 함정 없음).
///     플레이 중 인스펙터로 실시간 조정 가능하되, 플레이 종료 시 값은 코드 default로 복귀.
/// ★ P1(기준선)은 값 재구성이 아니라 "아무것도 안 함(패시브)" — 현행 동작 보존이 비트 단위로 보장됨.
/// ★ 룩어헤드는 PlayerCombat.AimDirection(기존 조준 계산) 재사용 — 중복 레이캐스트 없음.
/// </summary>
[DefaultExecutionOrder(-40)]   // PlayerCameraRig(-50) 직후, 시야 콘/오디오 등 기본(0) LateUpdate보다 먼저
public class CameraPresetLab : MonoBehaviour
{
    [System.Serializable]
    public class Preset
    {
        [Tooltip("화면 라벨용 이름.")]
        public string label = "프리셋";
        [Tooltip("카메라 오프셋 고도각(도). 기준선 항목에서는 무시(씬 캡처값 사용).")]
        public float pitch = 45f;
        [Tooltip("플레이어→카메라 거리(m). 기준선 항목에서는 무시(씬 캡처값 사용).")]
        public float distance = 15f;
        [Tooltip("조준(마우스) 방향 카메라 타깃 오프셋 최대치(m). 0=비활성.")]
        [Min(0f)] public float lookAhead = 0f;
        [Tooltip("룩어헤드 이동/복귀 댐핑(SmoothDamp 시간, 초).")]
        [Min(0.01f)] public float lookSmoothTime = 0.25f;
        [Tooltip("true면 현행 그대로(씬 캡처 기준선) — pitch/distance 입력값 무시, 완전 패시브로 복귀.")]
        public bool baseline = false;
    }

    [Header("프리셋 (F1~F4 — 플레이 중 인스펙터 실시간 조정 가능)")]
    [SerializeField] Preset[] presets =
    {
        new Preset { label = "기준선 — 현행 그대로",        baseline = true,  pitch = 45f, distance = 15f, lookAhead = 0f },
        new Preset { label = "근접",                        pitch = 40f, distance = 11f, lookAhead = 0f },
        new Preset { label = "근접+룩어헤드",               pitch = 42f, distance = 12f, lookAhead = 2.5f },
        new Preset { label = "저각+룩어헤드",               pitch = 35f, distance = 13f, lookAhead = 3f },
    };

    [Header("전환 댐핑")]
    [Tooltip("프리셋 전환 시 피치/거리 블렌드(SmoothDamp 시간, 초) — 하드 컷 방지.")]
    [SerializeField, Min(0.01f)] float presetBlendTime = 0.3f;

    // ── 기준선 캡처(Start, 리그와 동일 시점) ──
    Vector3 _baseOffset;      // 카메라-플레이어 월드 오프셋 (리그의 _baseOffset과 동일값)
    Vector3 _offsetYawDir;    // 오프셋의 수평 방향(단위) — 피치/거리에서 오프셋 재구성용
    float _basePitch;         // 오프셋 고도각(도) — 씬 기준 ≈45
    float _baseDist;          // 오프셋 길이(m) — 씬 기준 ≈15
    Quaternion _baseRot;      // 씬 회전(리그는 회전을 절대 안 건드림 — 복원 책임은 랩)
    Transform _target;
    PlayerCombat _aim;        // 룩어헤드용 기존 조준 방향 소스(읽기 전용)
    bool _ready;

    // ── 런타임 상태 ──
    int _active;              // 활성 프리셋 인덱스(0=기준선)
    float _curPitch, _curDist;
    float _pitchVel, _distVel;
    Vector3 _look, _lookVel;  // 룩어헤드 오프셋(XZ) + SmoothDamp 속도 상태
    Vector3 _rigPos;          // 이번 프레임 리그의 순수 출력(복원용 캐시)
    bool _overriding;         // 지금 카메라 포즈를 덮어쓴 상태인가 (Restorer가 참조)

    /// <summary>Restorer 전용 — 덮어쓰기 중이면 리그 실행 전에 이 포즈로 복원해야 한다.</summary>
    internal bool Overriding => _overriding;
    internal Vector3 RigPos => _rigPos;
    internal Quaternion BaseRot => _baseRot;

    void Start()
    {
        // PlayerCameraRig(-50)의 Start가 먼저 실행돼 카메라는 아직 씬 포즈 그대로 — 동일 기준선 캡처.
        var pc = FindObjectOfType<PlayerController>();
        if (pc == null) return;
        _target = pc.transform;
        _aim = pc.GetComponent<PlayerCombat>();   // 없으면 룩어헤드만 비활성

        _baseOffset = transform.position - _target.position;
        Vector3 flat = new Vector3(_baseOffset.x, 0f, _baseOffset.z);
        _baseDist = _baseOffset.magnitude;
        if (_baseDist < 0.01f || flat.sqrMagnitude < 0.0001f) return;   // 수직 직하 카메라 등 — 랩 비활성
        _offsetYawDir = flat.normalized;
        _basePitch = Mathf.Atan2(_baseOffset.y, flat.magnitude) * Mathf.Rad2Deg;
        _baseRot = transform.rotation;

        _curPitch = _basePitch;
        _curDist = _baseDist;
        _ready = true;
    }

    void OnDisable()
    {
        // 컴포넌트가 꺼져도 Restorer가 살아 스테일 _rigPos로 매 프레임 스냅백하는 고착 방지 —
        // 덮어쓴 포즈를 마지막 리그 출력으로 복원하고 상태를 기준선으로 리셋.
        if (_overriding)
        {
            transform.SetPositionAndRotation(_rigPos, _baseRot);
            _overriding = false;
        }
        _look = Vector3.zero; _lookVel = Vector3.zero;
        _pitchVel = 0f; _distVel = 0f;
        _curPitch = _basePitch; _curDist = _baseDist;
        _active = 0;
    }

    void Update()
    {
        // 프리셋 전환 키 — F1~F4 (Scripts 전수 검색: F키 충돌 없음. CarBoost의 F는 폐기 차량 전용)
        if (!_ready) return;
        for (int i = 0; i < presets.Length && i < 4; i++)
            if (Input.GetKeyDown(KeyCode.F1 + i)) _active = i;
    }

    void LateUpdate()
    {
        if (!_ready) return;

        // 플레이어(타깃) 파괴·비활성 가드 — 오버라이드 중단(회전 복원 후 패시브로).
        // 위치는 직전 -50에서 리그가 자기 출력으로 갱신했으므로 회전만 복귀하면 된다.
        if (_target == null || !_target.gameObject.activeInHierarchy)
        {
            if (_overriding)
            {
                transform.rotation = _baseRot;
                _overriding = false;
            }
            return;
        }

        // 플레이 중 인스펙터에서 presets를 비우면 Clamp(_active, 0, -1) = -1 → IndexOutOfRange 방지.
        if (presets == null || presets.Length == 0) return;

        // 리그와 동일 규율: unscaled + 스파이크 클램프(슬로모/일시정지 중에도 실시간 반응).
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        if (dt <= 0f) return;

        Preset p = presets[Mathf.Clamp(_active, 0, presets.Length - 1)];

        // 목표값 — 기준선 프리셋은 씬 캡처값으로 복귀(입력 숫자 무시 → 항상 정확히 현행).
        float tgtPitch = p.baseline ? _basePitch : p.pitch;
        float tgtDist  = p.baseline ? _baseDist  : p.distance;
        _curPitch = Mathf.SmoothDamp(_curPitch, tgtPitch, ref _pitchVel, presetBlendTime, Mathf.Infinity, dt);
        _curDist  = Mathf.SmoothDamp(_curDist,  tgtDist,  ref _distVel,  presetBlendTime, Mathf.Infinity, dt);

        // 룩어헤드 — 기존 조준 방향(AimDirection) 재사용. 기준선/비활성 프리셋은 0으로 복귀.
        Vector3 lookTgt = Vector3.zero;
        if (!p.baseline && p.lookAhead > 0f && _aim != null)
        {
            Vector3 d = _aim.AimDirection; d.y = 0f;
            if (d.sqrMagnitude > 0.0001f) lookTgt = d.normalized * p.lookAhead;
        }
        _look = Vector3.SmoothDamp(_look, lookTgt, ref _lookVel, p.lookSmoothTime, Mathf.Infinity, dt);

        // 기준선으로 수렴 완료 → 완전 패시브 전환(리그 출력에 손 안 댐 = 현행 동작 비트 단위 보존).
        bool atBase = p.baseline
            && Mathf.Abs(_curPitch - _basePitch) < 0.01f
            && Mathf.Abs(_curDist - _baseDist) < 0.005f
            && _look.sqrMagnitude < 0.0001f;
        if (atBase)
        {
            if (_overriding)
            {
                transform.rotation = _baseRot;   // 리그는 회전을 안 만지므로 복귀는 랩 책임
                _curPitch = _basePitch; _curDist = _baseDist;
                _look = Vector3.zero; _lookVel = Vector3.zero;
                _pitchVel = 0f; _distVel = 0f;
                _overriding = false;
            }
            return;   // 위치는 리그 출력 그대로
        }

        // ── 덮어쓰기: 리그 출력에서 초점(추종+리드+충격 합성 결과)을 역산 → 프리셋 오프셋으로 재배치 ──
        _rigPos = transform.position;                    // 리그의 순수 출력 캐시(다음 프레임 복원용)
        Vector3 focus = _rigPos - _baseOffset;           // 리그가 프레이밍 중인 지면 초점
        Vector3 offset = _offsetYawDir * (Mathf.Cos(_curPitch * Mathf.Deg2Rad) * _curDist)
                       + Vector3.up * (Mathf.Sin(_curPitch * Mathf.Deg2Rad) * _curDist);
        // 회전은 씬 회전에 피치 델타만 가산 — 기준선 값에서 씬 포즈와 정확히 일치(스냅 없음).
        Vector3 e = _baseRot.eulerAngles;
        transform.SetPositionAndRotation(
            focus + _look + offset,
            Quaternion.Euler(e.x + (_curPitch - _basePitch), e.y, e.z));
        _overriding = true;
    }

    void OnGUI()
    {
        if (!_ready) return;

        const float x = 10f, y = 10f, w = 380f, line = 20f;
        GUI.Box(new Rect(x - 4f, y - 4f, w, line * (presets.Length + 1) + 8f), GUIContent.none);
        GUI.Label(new Rect(x, y, w, line), "<b>CAMERA PRESET LAB</b>  [F1~F4 전환]", RichLabel());
        for (int i = 0; i < presets.Length && i < 4; i++)
        {
            Preset p = presets[i];
            string vals = p.baseline
                ? $"{_basePitch:0.#}°/{_baseDist:0.#}m (씬 캡처)"
                : $"{p.pitch:0.#}°/{p.distance:0.#}m" + (p.lookAhead > 0f ? $" 룩{p.lookAhead:0.#}m" : "");
            string row = $"F{i + 1}  {p.label} — {vals}";
            if (i == _active) row = $"<color=#7CFC00>▶ {row}</color>";
            else row = $"   {row}";
            GUI.Label(new Rect(x, y + line * (i + 1), w, line), row, RichLabel());
        }
    }

    static GUIStyle _richLabel;
    static GUIStyle RichLabel()
    {
        if (_richLabel == null)
            _richLabel = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 13 };
        return _richLabel;
    }
}

/// <summary>
/// 복원 헬퍼 — PlayerCombat.Update(-10) "직후", 모든 LateUpdate "이전"에 카메라를
/// 리그의 마지막 순수 출력으로 되돌린다. (Update 페이즈는 LateUpdate 페이즈 전체보다 항상 먼저)
/// → 리그의 SmoothDamp는 자기 출력만 보고(무교란), 조준 레이는 화면 포즈로 계산된다.
/// </summary>
[DefaultExecutionOrder(-9)]
public class CameraPresetLabRestorer : MonoBehaviour
{
    internal CameraPresetLab lab;

    void Update()
    {
        if (lab != null && lab.isActiveAndEnabled && lab.Overriding)   // 랩이 꺼지면 복원도 중단(방어 이중화)
            transform.SetPositionAndRotation(lab.RigPos, lab.BaseRot);
    }
}

/// <summary>
/// 자가 부트스트랩 — 씬 파일을 건드리지 않고 PlayerCameraRig가 있는 씬에서만 런타임 부착.
/// sceneLoaded는 Awake/OnEnable 후·Start 전에 호출되므로, 랩의 Start 캡처가 리그와 같은 프레임에 성립.
/// </summary>
static class CameraPresetLabBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;   // 도메인 리로드 비활성 환경에서 중복 구독 방지
        SceneManager.sceneLoaded += OnSceneLoaded;
        Attach();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Attach();

    static void Attach()
    {
        var rig = Object.FindObjectOfType<PlayerCameraRig>();
        if (rig == null || rig.GetComponent<CameraPresetLab>() != null) return;
        var lab = rig.gameObject.AddComponent<CameraPresetLab>();
        rig.gameObject.AddComponent<CameraPresetLabRestorer>().lab = lab;
    }
}
