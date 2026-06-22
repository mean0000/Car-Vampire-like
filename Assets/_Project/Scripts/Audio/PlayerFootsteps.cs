using UnityEngine;

/// <summary>
/// 플레이어 발소리 — 표면별 풀 + 거리 기반 보폭 트리거 + 매 스텝 변주(직전-비반복 랜덤·피치/볼륨 지터).
///
/// 설계 근거(필-볼트):
///  ① 타이밍 2모드:
///     · AnimationEvent(기본) — 클립의 발 디딤 프레임 `OnFootstep` 이벤트로 발 닿는 순간에 정확히 발화.
///       8방향 블렌드트리는 블렌딩 중 여러 클립 이벤트가 겹쳐 터지므로 minStepInterval 디바운스로 병합한다.
///     · Distance(폴백) — XZ 이동거리 누적으로 보폭마다 1스텝(애니 무관, 위상은 어긋남).
///  ② 표면 = 데이터 주도. 발밑 지면 레이캐스트 hit 콜라이더의 SurfaceTag → SurfaceFootstepSet 매칭,
///     실패 시 defaultSet 폴백(현재 _PlayerStackTest Ground는 미태깅이라 기본 표면으로 울린다).
///  ③ 재생 = 자족 2D AudioSource. 내 발소리는 카메라(15m 위)가 움직여도 패닝/거리감쇠되면 안 되므로
///     공간화(3D) 대신 2D — 좀비 그르렁(SfxOneShot 3D)과 채널 정책이 다르다.
///
/// 자족 컴포넌트(PlayerMotor 읽기). PlayerBrain이 Tick 끝에 1줄 호출. 수명주기 메서드 신규 추가 없음.
/// masterEnabled=false면 전체 무음(롤백 경로).
/// </summary>
public class PlayerFootsteps : MonoBehaviour
{
    public enum TriggerMode { AnimationEvent, Distance }

    [Header("Master (롤백 경로)")]
    [Tooltip("끄면 발소리 전체 무음. 기각 시 0으로.")]
    [SerializeField] bool masterEnabled = true;

    [Header("Trigger")]
    [Tooltip("AnimationEvent=클립 발 디딤 프레임(정확한 위상). Distance=이동거리 보폭(애니 무관 폴백).")]
    [SerializeField] TriggerMode triggerMode = TriggerMode.AnimationEvent;
    [Tooltip("AnimationEvent 디바운스(초) — 8방향 블렌드 동시발화를 1스텝으로 병합. 최대 케이던스보다 짧게(런 ~0.37s 간격이라 0.12 안전).")]
    [SerializeField, Range(0f, 0.3f)] float minStepInterval = 0.12f;

    [Header("Surface Sets")]
    [Tooltip("표면 키 → 클립 풀. 발밑 SurfaceTag.surfaceId와 대소문자 무시 매칭.")]
    [SerializeField] SurfaceFootstepSet[] surfaceSets;
    [Tooltip("표면 매칭 실패 시 폴백(도심 기본). 현재 씬 Ground는 미태깅이라 항상 이걸 쓴다.")]
    [SerializeField] SurfaceFootstepSet defaultSet;

    [Header("Stride (보폭 — 거리 기반 발화)")]
    [Tooltip("걷기 보폭(m). 이 거리만큼 이동할 때마다 1스텝. 작을수록 빠른 케이던스.")]
    [SerializeField, Range(0.5f, 3f)] float walkStride = 1.6f;
    [Tooltip("뛰기 보폭(m). 보통 걷기보다 큼(보폭이 늘어남) — 그래도 케이던스는 빨라진다(속도가 더 빠르므로).")]
    [SerializeField, Range(0.5f, 4f)] float runStride = 2.0f;
    [Tooltip("이 속도(m/s) 이상이면 run 풀·runStride 사용. PlayerMotor.Velocity 평면 크기 기준.")]
    [SerializeField, Range(1f, 12f)] float runSpeedThreshold = 5.5f;
    [Tooltip("이 속도(m/s) 미만이면 발소리 정지(거의 정지 상태의 미세 드리프트로 스텝 발화 방지).")]
    [SerializeField, Range(0f, 2f)] float minMoveSpeed = 0.4f;
    [Tooltip("이동 재개 시 첫 스텝 프라이밍(보폭 비율). 1에 가까울수록 출발 즉시 첫 발소리(한 보폭 기다리는 지연 제거). 0=프라이밍 없음.")]
    [SerializeField, Range(0f, 1f)] float firstStepPrime = 0.9f;
    [Tooltip("한 프레임 이동속도가 이 값(m/s)을 넘으면 스폰/리스폰 텔레포트로 보고 적산 스킵(발소리 폭발 방지). 대시 28m/s보다 크게.")]
    [SerializeField, Range(30f, 200f)] float teleportSpeed = 40f;

    [Header("Surface Raycast")]
    [Tooltip("발밑 표면 감지 레이캐스트 대상 — 지면 레이어만(플레이어/적 콜라이더 오탐 방지). PlayerMotor.groundLayer와 일치시킬 것.")]
    [SerializeField] LayerMask groundLayer = 1 << 6;

    [Header("Variation (변주 — 같은 소리 반복 방지)")]
    [Tooltip("기준 볼륨. ★보수적 시작(캐넌: 첫 볼륨 0.03~0.15). 안 들리면 유저가 올린다.")]
    [SerializeField, Range(0f, 1f)] float volume = 0.10f;
    [Tooltip("볼륨 지터(±). 0.1 = 매 스텝 ±10% 랜덤.")]
    [SerializeField, Range(0f, 0.5f)] float volumeJitter = 0.12f;
    [Tooltip("피치 하한.")]
    [SerializeField, Range(0.7f, 1f)] float pitchMin = 0.92f;
    [Tooltip("피치 상한.")]
    [SerializeField, Range(1f, 1.3f)] float pitchMax = 1.08f;

    [Header("Dash")]
    [Tooltip("켜면 대시 중 일반 발소리 억제(미끄러지는 회피라 발 디딤이 없음).")]
    [SerializeField] bool suppressDuringDash = true;

    [Header("Audio Source")]
    [Tooltip("발소리 재생 소스. 비우면 런타임에 이 GameObject에 2D 소스 생성. 믹서 라우팅은 이 소스에 연결.")]
    [SerializeField] AudioSource source;
    [Tooltip("2D(0)~3D(1). 내 발소리는 2D 권장 — 카메라가 위/멀리 있어 3D면 거리감쇠로 죽는다.")]
    [SerializeField, Range(0f, 1f)] float spatialBlend = 0f;

    PlayerMotor _motor;
    PlayerAnimatorDriver _driver;   // AnimationEvent 모드: 발 디딤 이벤트 출처(비주얼 자식)
    float _distanceAccum;       // 보폭 적산기 — stride 도달 시 1스텝(Distance 모드)
    int _lastIndex = -1;        // 직전 재생 인덱스(no-immediate-repeat)
    int _lastPoolSize = -1;     // 직전 풀 크기 — walk↔run 전환 시 _lastIndex 스테일 방지(Stab M-2)
    bool _wasMoving;            // 직전 프레임 이동 중이었나 — 이동 재개 시 첫 스텝 프라이밍 트리거(Distance 모드)
    float _lastStepTime = -1f;  // 직전 스텝 시각 — AnimationEvent 디바운스
    Vector3 _lastPos;
    bool _havePos;

    void Awake()
    {
        _motor = GetComponentInParent<PlayerMotor>();
        if (_motor == null)
            Debug.LogError("[PlayerFootsteps] PlayerMotor를 부모 체인에서 못 찾음 — 발소리가 울리지 않는다.", this);
        _driver = GetComponentInChildren<PlayerAnimatorDriver>();
        // 모드 무관 경고 — triggerMode는 씬 저장값/런타임 전환으로 바뀔 수 있어, AnimationEvent 전환 시 무음을 막으려 항상 알린다(Stab M-1).
        if (_driver == null)
            Debug.LogWarning("[PlayerFootsteps] PlayerAnimatorDriver 미발견 — AnimationEvent 모드 불가(Distance 모드로 폴백 권장).", this);

        if (source == null)
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
        }
        source.spatialBlend = spatialBlend;
    }

    void OnEnable()
    {
        // 활성화 지점에서 기준 리셋 — 비활성 동안의 위치 점프가 가짜 거리로 적산되는 것 방지.
        _havePos = false;
        _distanceAccum = 0f;
        if (_driver != null) _driver.Footstep += HandleAnimFootstep;
    }

    void OnDisable()
    {
        if (_driver != null) _driver.Footstep -= HandleAnimFootstep;
    }

    /// <summary>AnimationEvent 모드: 클립 발 디딤 프레임에서 PlayerAnimatorDriver.Footstep 경유로 호출.
    /// 8방향 블렌드 동시발화는 minStepInterval 디바운스로 1스텝 병합. 정지/대시 중엔 무음.</summary>
    void HandleAnimFootstep()
    {
        if (triggerMode != TriggerMode.AnimationEvent || !masterEnabled || _motor == null) return;
        if (suppressDuringDash && _motor.IsDashing) return;
        if (Time.time - _lastStepTime < minStepInterval) return;   // 블렌드 동시발화 병합

        Vector3 vel = _motor.Velocity; vel.y = 0f;
        float speed = vel.magnitude;
        if (speed < minMoveSpeed) return;

        _lastStepTime = Time.time;
        PlayStep(speed >= runSpeedThreshold);
    }

    /// <summary>PlayerBrain이 매 프레임 끝에 호출. Distance 모드에서만 거리 적산 → 보폭 도달 시 1스텝.
    /// AnimationEvent 모드면 트리거는 <see cref="HandleAnimFootstep"/>가 담당하므로 여기선 무시.</summary>
    public void Tick()
    {
        if (triggerMode != TriggerMode.Distance) return;
        if (!masterEnabled || _motor == null) return;

        Vector3 pos = _motor.transform.position;
        if (!_havePos) { _lastPos = pos; _havePos = true; return; }

        // XZ 이동거리만 — 지면 추종 y 변화는 보폭과 무관.
        Vector3 delta = pos - _lastPos; delta.y = 0f;
        _lastPos = pos;
        float dist = delta.magnitude;

        // 텔레포트 가드(Stab H-1): 한 프레임 속도가 대시보다 크게 넘으면 스폰/리스폰 점프 → 적산 스킵·기준만 갱신.
        float dt = Time.deltaTime;
        if (dt > 0f && dist / dt > teleportSpeed) { _distanceAccum = 0f; return; }

        // 대시 억제 — 적산도 멈춰 대시 직후 스텝 폭발 방지.
        if (suppressDuringDash && _motor.IsDashing) { _distanceAccum = 0f; return; }

        // 속도 게이트: 거의 정지면 미세 드리프트로 스텝이 새지 않게.
        Vector3 vel = _motor.Velocity; vel.y = 0f;
        float speed = vel.magnitude;
        if (speed < minMoveSpeed) { _distanceAccum = 0f; _wasMoving = false; return; }

        bool running = speed >= runSpeedThreshold;
        float stride = running ? runStride : walkStride;

        // 이동 재개 첫 프레임: 적산을 미리 채워 첫 스텝이 한 보폭 뒤로 밀리지 않게(출발 즉시 발소리).
        if (!_wasMoving) _distanceAccum = stride * firstStepPrime;
        _wasMoving = true;

        _distanceAccum += dist;
        if (_distanceAccum >= stride)
        {
            _distanceAccum -= stride;        // 잔여 보존(프레임 큰 점프에도 케이던스 유지)
            PlayStep(running);
        }
    }

    void PlayStep(bool running)
    {
        SurfaceFootstepSet set = ResolveSurfaceSet();
        if (set == null) return;
        AudioClip[] pool = set.Pool(running);
        if (pool == null || pool.Length == 0) return;

        // 풀이 바뀌면(walk↔run·표면 전환) 직전 인덱스가 새 풀 범위와 무관해지므로 리셋(반복방지 정합).
        if (pool.Length != _lastPoolSize) { _lastIndex = -1; _lastPoolSize = pool.Length; }

        // 직전-비반복 랜덤: 풀이 1개면 어쩔 수 없이 반복, 2개 이상이면 직전과 다른 것 보장.
        int idx;
        if (pool.Length == 1) idx = 0;
        else
        {
            idx = Random.Range(0, pool.Length);
            if (idx == _lastIndex) idx = (idx + 1) % pool.Length;
        }
        _lastIndex = idx;

        AudioClip clip = pool[idx];
        if (clip == null) return;

        source.pitch = Random.Range(pitchMin, pitchMax);
        float v = volume * (1f + Random.Range(-volumeJitter, volumeJitter));
        source.PlayOneShot(clip, Mathf.Max(0f, v));
    }

    /// <summary>발밑 지면 레이캐스트 → SurfaceTag → 매칭 SO. 실패 시 defaultSet.</summary>
    SurfaceFootstepSet ResolveSurfaceSet()
    {
        if (surfaceSets != null && surfaceSets.Length > 0)
        {
            Vector3 origin = _motor.transform.position + Vector3.up * 1f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 3f,
                    groundLayer, QueryTriggerInteraction.Ignore))
            {
                var tag = hit.collider.GetComponentInParent<SurfaceTag>();
                if (tag != null && !string.IsNullOrEmpty(tag.surfaceId))
                {
                    for (int i = 0; i < surfaceSets.Length; i++)
                        if (surfaceSets[i] != null &&
                            string.Equals(surfaceSets[i].surfaceId, tag.surfaceId,
                                System.StringComparison.OrdinalIgnoreCase))
                            return surfaceSets[i];
                }
            }
        }
        return defaultSet;
    }
}
