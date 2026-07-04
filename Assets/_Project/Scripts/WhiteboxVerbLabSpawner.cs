using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ★화이트박스 verb 랩 스포너 — 밀리는 잡몹(<see cref="SwarmChaser"/>)을 목표 밀도로 유지(사망분 재보충).
/// 프리팹 없이 절차 생성(캡슐 프리미티브 + RB + EnemyDamageReceiver + SwarmChaser) = 진짜 화이트박스.
/// 기존 씬(_CombatSlice_ReadAndCut — 커밋 엘리트 보유)에 이 컴포넌트 하나 얹으면 랩 무대 완성.
/// 게이트 질문 G1(자발 전진)/G3(공간 체감)의 무대 — 목표 밀도 40~80(5마리 랩은 거짓말, 랩 스펙 §4).
/// ★2026-07-04 전선형 재구조(유저 플레이 판정 "뚫는다 < 버틴다" + 나·Codex 수렴): 플레이어 중심 360° 링 재중심화가
///   세계를 등방으로 만들어 제자리 버티기가 최적해였다 → 주 스폰=이동 방향 전방 아크, 후방=금지(클리어 포켓 존중),
///   보충=펄스 묶음(연 길이 개별 보충으로 즉시 안 메워지게). "전진이 공간을 만든다"(원칙①)는 스폰이 보장한다.
/// ⚠️랩 전용(빌드 미포함 전제) — 머티리얼은 프리미티브 기본 머티리얼 복제(파이프라인 무관, Shader.Find ❌).
/// </summary>
public class WhiteboxVerbLabSpawner : MonoBehaviour
{
    [Header("밀도")]
    [Tooltip("유지할 잡몹 수 — 랩 스펙 목표 밀도 40~80.")]
    [SerializeField, Min(0)] int targetCount = 60;
    [Tooltip("전방 스폰 안쪽 반경(m) — 플레이어 기준(Codex 스폰 스펙 14~22).")]
    [SerializeField, Min(1f)] float spawnRadiusMin = 14f;
    [Tooltip("전방 스폰 바깥 반경(m).")]
    [SerializeField, Min(2f)] float spawnRadiusMax = 22f;
    [Tooltip("보충 펄스 주기(초) — ★개별 즉시 보충 ❌, 웨이브 단위 묶음(1.5~3 권장). 연 길(클리어 포켓)이 즉시 안 메워지게.")]
    [SerializeField, Min(0.05f)] float respawnInterval = 2f;
    [Tooltip("펄스당 최대 보충 수 — 묶음 크기(스파이크와 트레이드오프).")]
    [SerializeField, Range(1, 30)] int pulseBatchMax = 10;

    [Header("★전선형 스폰 (뚫는 방향이 존재하게 — 2026-07-04)")]
    [Tooltip("전방 아크 폭(도) — 주 스폰이 떨어지는 전선. 플레이어 이동 방향 기준(정지 시 마지막 방향 유지).")]
    [SerializeField, Range(30f, 180f)] float frontArcDeg = 120f;
    [Tooltip("전방 아크에 스폰할 확률(나머지는 측면 밴드). 1=전부 전방. 0.7~0.8 권장.")]
    [SerializeField, Range(0f, 1f)] float frontBias = 0.75f;
    [Tooltip("후방 스폰 금지각(도) — 뚫고 지나온 등 뒤(클리어 포켓)는 비워둔다. 후방엔 실적도 적도 없다(Layer 0: 후퇴=실적 마름).")]
    [SerializeField, Range(0f, 180f)] float rearBlockDeg = 120f;
    [Tooltip("측면 스폰 추가 거리(m) — 측면은 전방보다 멀리서(포위 재형성 지연, Codex 측면 18~26).")]
    [SerializeField, Min(0f)] float sideRadiusBonus = 4f;

    [Header("개체")]
    [Tooltip("잡몹 HP — 가르기 1~2타 감각(읽을 시간은 엘리트가 담당).")]
    [SerializeField, Min(1)] int swarmHp = 2;
    [Tooltip("잡몹 본체 색(화이트박스 회보라 — 플래시 흰/시안과 구별).")]
    [SerializeField] Color swarmColor = new Color(0.45f, 0.42f, 0.52f);
    [Tooltip("적 레이어 — KatanaWeapon.enemyMask 기본(7)과 일치해야 베인다.")]
    [SerializeField, Range(0, 31)] int enemyLayer = 7;

    Transform _player;
    Material _swarmMat;   // 공유 1개(배칭) — 피격 플래시는 MPB라 안 깨짐
    readonly List<GameObject> _alive = new List<GameObject>();
    float _respawnTimer;
    Vector3 _frontDir = Vector3.forward;   // ★전선 방향 — 이동 방향으로 수렴, 정지 시 마지막 방향 유지.
    Vector3 _lastPlayerPos;

    void Start()
    {
        var motor = FindFirstObjectByType<PlayerMotor>();
        if (motor == null)
        {
            Debug.LogWarning("[WhiteboxVerbLab] PlayerMotor 미발견 — 스폰 중단.", this);
            enabled = false;
            return;
        }
        _player = motor.transform;
        // ★전선 초기화 — 첫 이동 전엔 플레이어 시선이 전선.
        Vector3 f = _player.forward; f.y = 0f;
        _frontDir = f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward;
        _lastPlayerPos = _player.position;
        // ★첫 프레임 스파이크 방지(Stab M-3) — 워밍업 일부만 즉시, 나머지는 Update 보충 루프가 배치로 채움.
        int warmup = Mathf.Min(targetCount, 12);
        for (int i = 0; i < warmup; i++) Spawn();
    }

    void Update()
    {
        // ★전선 방향 추적 — 이동 중(속도 >1.5m/s)이면 이동 방향으로 부드럽게 수렴(미세 떨림·정지 시 마지막 방향 유지).
        if (_player != null)
        {
            float dt = Time.deltaTime;
            Vector3 delta = _player.position - _lastPlayerPos; delta.y = 0f;
            _lastPlayerPos = _player.position;
            if (dt > 0f && delta.sqrMagnitude / (dt * dt) > 2.25f)
                _frontDir = Vector3.Slerp(_frontDir, delta.normalized, Mathf.Clamp01(6f * dt));
        }

        // 사망(비활성) 정리 — EnemyDamageReceiver.Die()가 SetActive(false)로 마무리한 개체를 파괴 후 보충.
        for (int i = _alive.Count - 1; i >= 0; i--)
        {
            var go = _alive[i];
            if (go == null) { _alive.RemoveAt(i); continue; }
            if (!go.activeInHierarchy) { Destroy(go); _alive.RemoveAt(i); }
        }
        if (_alive.Count < targetCount)
        {
            _respawnTimer -= Time.deltaTime;
            if (_respawnTimer <= 0f)
            {
                _respawnTimer = respawnInterval;
                // ★펄스 보충(웨이브 묶음) — 연 길이 개별 스트림 보충으로 즉시 안 메워지게. 램프업(12→60)은 수 펄스에 찬다.
                int batch = Mathf.Min(pulseBatchMax, targetCount - _alive.Count);
                for (int b = 0; b < batch; b++) Spawn();
            }
        }
        else _respawnTimer = 0f;
    }

    void Spawn()
    {
        if (_player == null) return;
        // ★전선형 각 선택 — 전방 아크(주 전선) / 측면 밴드(빈도 낮게·더 멀리) / 후방 금지각(클리어 포켓 존중).
        //   각도는 명시 산출(구 Codex P3 (0,0) 엣지 게이트 계승 — 정규화 의존 없음).
        float frontHalf = frontArcDeg * 0.5f;
        float sideMax = 180f - Mathf.Min(rearBlockDeg * 0.5f, 179f);   // 측면 밴드 상한 = 후방 금지 경계
        bool front = Random.value < frontBias;
        if (sideMax <= frontHalf) front = true;   // 파라미터 퇴화(밴드 소멸) 가드 — 전방 폴백
        float offsetDeg = front
            ? Random.Range(-frontHalf, frontHalf)
            : (Random.value < 0.5f ? -1f : 1f) * Random.Range(frontHalf, sideMax);
        float rad = Random.Range(spawnRadiusMin, Mathf.Max(spawnRadiusMin, spawnRadiusMax));
        if (!front) rad += sideRadiusBonus;
        Vector3 dir = Quaternion.AngleAxis(offsetDeg, Vector3.up) * _frontDir;
        Vector3 pos = _player.position + dir * rad;

        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = "SwarmWB";
        go.layer = enemyLayer;
        go.transform.position = pos + Vector3.up * 1.0f;   // 캡슐 중심(높이2) — 중력이 지면에 정착

        // 머티리얼 — 프리미티브 기본 머티리얼 복제 후 틴트(파이프라인 무관, Shader.Find 스트립 함정 회피).
        var rend = go.GetComponent<Renderer>();
        if (_swarmMat == null)
        {
            _swarmMat = new Material(rend.sharedMaterial);
            if (_swarmMat.HasProperty("_BaseColor")) _swarmMat.SetColor("_BaseColor", swarmColor);
            else _swarmMat.color = swarmColor;
        }
        rend.sharedMaterial = _swarmMat;

        var rb = go.AddComponent<Rigidbody>();
        rb.mass = 1f;

        var recv = go.AddComponent<EnemyDamageReceiver>();   // HP·플래시·크리 시안·OnKnocked 통지(넉백 오프셋은 기본 0)
        recv.SetMaxHp(swarmHp);
        recv.SetStaggerOnHit(true);   // ★피격 스태거(억제 상태, 07-04) — RB 잡몹 전용(루트모션 몹은 꺼진 기본 유지)

        var chaser = go.AddComponent<SwarmChaser>();          // RB 추격 + 임펄스 밀림 + 접촉 딜
        chaser.Init(_player);

        _alive.Add(go);
    }
}
