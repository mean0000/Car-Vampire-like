using UnityEngine;

/// <summary>
/// ★원자 테스트 랩(AtomLab) 리그 — 정지 훈련 더미 1기를 반복 타격하며 손맛 채널을 개별 on/off해
/// "정지 한 방" 손맛을 만드는 성분을 격리 판정한다. <c>_AtomLab_OneCut</c> 씬 전용(원본 씬엔 안 붙는다).
///
/// 채널(런타임 키맵, OnGUI HUD로 상태 표시):
///   1=사운드 · 2=플래시 · 3=쉐이크 · 4=히트스탑 · 5=입력측 킥/글라이드
///   6=콤보 스냅 — ★스냅은 리타임된 .anim 클립에 구워져 런타임 클린 토글 불가. 베스트에포트로
///     Animator.speed 1.0↔snapOffSpeed 스왑(러프 A/B). playerAnimator 미배선이면 로그 스텁만.
///   7=★붕괴(방향성 붕괴 — 거합 문법, 2026-07-05 배선) — 더미의 DirectionalCollapse.enabled 토글.
///     off면 죽음이 기존 즉시 소멸(A/B). dummyCollapse 미배선이면 더미에서 자동 탐색.
///   R=더미 리셋/리스폰 · K=1방킬 토글(maxHp 50↔1)
///
/// ★시작 기본=전 채널 ON(현행 풀 손맛이 베이스라인) — 유저가 키로 하나씩 꺼서 제로 베이스라인을 만든다.
/// </summary>
[DisallowMultipleComponent]
public class AtomLabRig : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("정지 훈련 더미의 EnemyDamageReceiver.")]
    [SerializeField] EnemyDamageReceiver dummy;
    [Tooltip("플레이어의 KatanaWeapon.")]
    [SerializeField] KatanaWeapon katana;
    [Tooltip("★콤보 스냅 A/B(6)용 플레이어 Animator. 비우면 '스냅 토글 미배선' 로그만 남는다.")]
    [SerializeField] Animator playerAnimator;
    [Tooltip("★붕괴 채널(7)용 더미의 DirectionalCollapse. 비우면 dummy에서 자동 탐색(Awake).")]
    [SerializeField] DirectionalCollapse dummyCollapse;

    [Header("콤보 스냅 A/B (6) — 베스트에포트")]
    [Tooltip("스냅 OFF 근사 Animator.speed(1.0=원본 유지, 낮출수록 느긋). 클립에 구운 스냅의 완전 재현은 불가 — 러프 대조용.")]
    [SerializeField, Range(0.1f, 1f)] float snapOffSpeed = 0.6f;

    [Header("키맵 (기본값 — 필요시 Inspector에서 재배정)")]
    [SerializeField] KeyCode sfxKey = KeyCode.Alpha1;
    [SerializeField] KeyCode flashKey = KeyCode.Alpha2;
    [SerializeField] KeyCode shakeKey = KeyCode.Alpha3;
    [SerializeField] KeyCode hitStopKey = KeyCode.Alpha4;
    [SerializeField] KeyCode inputFeedbackKey = KeyCode.Alpha5;
    [SerializeField] KeyCode snapKey = KeyCode.Alpha6;
    [SerializeField] KeyCode collapseKey = KeyCode.Alpha7;   // ★붕괴(방향성 — DirectionalCollapse.enabled 토글)
    [SerializeField] KeyCode resetKey = KeyCode.R;
    [SerializeField] KeyCode killToggleKey = KeyCode.K;

    // 채널 on/off 상태 — HUD 표시용. 시작 기본 전부 ON(베이스라인).
    bool _sfxOn = true, _flashOn = true, _shakeOn = true, _hitStopOn = true, _inputFeedbackOn = true;
    bool _snapOn = true;     // 6 — Animator.speed A/B
    bool _collapseOn = true; // 7 — 방향성 붕괴(컴포넌트 enabled 토글). 시작 ON = 리그 컨벤션(전 채널 베이스라인)
    bool _oneShotKill;       // K — maxHp 50↔1 (기본 50 = OFF)

    void Awake()
    {
        if (dummyCollapse == null && dummy != null)
            dummyCollapse = dummy.GetComponent<DirectionalCollapse>();
        if (dummyCollapse != null) dummyCollapse.enabled = _collapseOn;   // 시작 상태 동기화
    }

    void Update()
    {
        if (Input.GetKeyDown(sfxKey)) { _sfxOn = !_sfxOn; katana?.SetSfxEnabled(_sfxOn); }
        if (Input.GetKeyDown(flashKey)) { _flashOn = !_flashOn; dummy?.SetFlashEnabled(_flashOn); }
        if (Input.GetKeyDown(shakeKey)) { _shakeOn = !_shakeOn; dummy?.SetShakeEnabled(_shakeOn); }
        if (Input.GetKeyDown(hitStopKey)) { _hitStopOn = !_hitStopOn; dummy?.SetHitStopEnabled(_hitStopOn); }
        if (Input.GetKeyDown(inputFeedbackKey)) { _inputFeedbackOn = !_inputFeedbackOn; katana?.SetInputFeedbackEnabled(_inputFeedbackOn); }
        if (Input.GetKeyDown(snapKey)) ToggleSnap();
        if (Input.GetKeyDown(collapseKey))
        {
            _collapseOn = !_collapseOn;
            if (dummyCollapse != null) dummyCollapse.enabled = _collapseOn;   // off면 StageDeath 거절 → 기존 즉시 소멸(A/B)
            else Debug.Log("[AtomLabRig] 채널7(붕괴) — 더미에 DirectionalCollapse 미배선.");
        }
        if (Input.GetKeyDown(resetKey)) ResetDummy();
        if (Input.GetKeyDown(killToggleKey)) ToggleOneShotKill();
    }

    void ToggleSnap()
    {
        _snapOn = !_snapOn;
        if (playerAnimator == null)
        {
            Debug.Log("[AtomLabRig] 채널6(콤보 스냅) — snap toggle 미배선(클립에 구움). playerAnimator 참조 비어 로그만 남긴다.");
            return;
        }
        playerAnimator.speed = _snapOn ? 1f : snapOffSpeed;
    }

    void ResetDummy()
    {
        if (dummy == null) return;
        if (!dummy.gameObject.activeSelf) dummy.gameObject.SetActive(true);
        dummy.ResetReceiver();   // ★maxHp는 K 토글이 이미 필드에 반영해뒀으니 그 값으로 부활(1방킬 모드 유지).
    }

    void ToggleOneShotKill()
    {
        _oneShotKill = !_oneShotKill;
        dummy?.SetMaxHp(_oneShotKill ? 1 : 50);
    }

    void OnGUI()
    {
        var title = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
        title.normal.textColor = Color.white;
        GUI.Label(new Rect(12, 10, 480, 26), "=== AtomLab 원자 테스트 랩 — 채널 A/B ===", title);

        float y = 40f;
        DrawChannel(ref y, sfxKey, "사운드", _sfxOn);
        DrawChannel(ref y, flashKey, "플래시", _flashOn);
        DrawChannel(ref y, shakeKey, "쉐이크", _shakeOn);
        DrawChannel(ref y, hitStopKey, "히트스탑", _hitStopOn);
        DrawChannel(ref y, inputFeedbackKey, "입력측 킥/글라이드", _inputFeedbackOn);
        DrawChannel(ref y, snapKey, "콤보 스냅(베스트에포트)", _snapOn);
        DrawChannel(ref y, collapseKey, "붕괴(방향성 — 거합)", _collapseOn);

        var infoStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
        infoStyle.normal.textColor = Color.gray;
        GUI.Label(new Rect(12, y, 480, 22),
            $"[{resetKey}] 더미 리셋/리스폰   [{killToggleKey}] 1방킬 {(_oneShotKill ? "ON(HP1)" : "off(HP50)")}", infoStyle);
        y += 22f;

        if (dummy != null)
            GUI.Label(new Rect(12, y, 480, 22), $"더미 HP: {dummy.Hp}   {(dummy.IsDead ? "DEAD" : "ALIVE")}", infoStyle);
    }

    void DrawChannel(ref float y, KeyCode key, string label, bool on)
    {
        var style = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
        style.normal.textColor = on ? new Color(0.4f, 1f, 0.5f) : Color.gray;
        GUI.Label(new Rect(12, y, 480, 22), $"[{key}] {label}: {(on ? "ON" : "off")}", style);
        y += 22f;
    }
}
