// MonsterSignatureAura.cs
// 목적: 몬스터 시그니처 오라 — 변이 핵심부(특정 Renderer)에 레드오렌지 부위 발광.
//       ★전신 발광 ❌ — 머리/눈/등/관절 등 지정 Renderer만.
//       ★등급(LV1~5) = 밝기·강도·크기 차등(색조 아님). LV3 = 중간 밝기.
//       ★평시 = 약한 상시 발광 + 숨(breathing) 펄스. 공격 시 = 확 펄스(AttackPulse).
//
// ════════ 색 규약 (§3 업데이트 2026-06-14) ════════
//   유저 확정: 청록 폐기, 5색 무지개 기각.
//   시그니처 오라 색조 = 레드오렌지(#FF5A2C) 계열 고정.
//   등급 = 밝기/강도/림 크기로만 분화(색조 변경 없음).
//   시안 충돌 원천 소멸(청록 제거로).
//
// ════════ 구현 전략 ════════
//   림 프레넬(ActorRimLit._RimColor + _RimIntensity) + MPB Emissive(_EmissionColor) 조합.
//   신규 셰이더 0 — ActorRimLit의 기존 프로퍼티 직접 조작.
//   DOTween + MaterialPropertyBlock (alloc 0).
//
// ════════ VfxDirector 통합 ════════
//   평시: AuraBreathe() 코루틴 — sin파 미세 진동(idle heartbeat).
//   공격: PulseAttack(duration) 공개 API — VenosaurBrawler.ClawHit이 호출 → 부위가 확 펄스.
//         ★VfxDirector.PulseGlow 패턴 그대로 계승(DOTween Triangle envelope).
//
// ════════ 등급 밝기 곡선 (SSOT) ════════
//   AuraGrade static class에 LV1~5 파라미터 전부 — 이 한 곳만 수정하면 9종 전부 반영.
//   Venosaur(LV3) = Grade.LV3 값으로 초기화.
//
// ★시각 검증 잔무: KatanaController 컴파일 에러 해소 후 플레이모드에서 유저가 판정.
//   색/밝기 수치는 전부 SerializeField 노브 — Inspector에서 실시간 조정 가능.
using System.Collections;
using UnityEngine;
using DG.Tweening;

public class MonsterSignatureAura : MonoBehaviour
{
    // ════════ 등급 밝기 곡선 (SSOT — 9종 공유) ════════
    // 색조 = 레드오렌지 고정(#FF5A2C ≈ (1, 0.353, 0.173)).
    // 등급별 변수: 평시 Emissive HDR 강도 / 림 강도 / 공격 펄스 피크 강도.
    // ★유저가 이 표 한 곳만 수정하면 9종 공통 반영됨.
    public static class AuraGrade
    {
        public struct Params
        {
            /// <summary>평시 상시 발광 HDR 강도 (Emissive _EmissionColor에 곱해짐).</summary>
            public float idleEmissiveIntensity;
            /// <summary>평시 림 강도 (_RimIntensity).</summary>
            public float idleRimIntensity;
            /// <summary>공격 펄스 피크 HDR 강도 (AttackPulse 최대치).</summary>
            public float attackPeakIntensity;
            /// <summary>평시 림 파워 — 낮을수록 림이 넓어짐(LV5 더 넓게).</summary>
            public float rimPower;
            /// <summary>숨 펄스 진폭 (idle 기준치 ± amplitude).</summary>
            public float breathAmplitude;
        }

        // ★LV1 — 작은 눈/관절만. 어둑한 점화.
        public static readonly Params LV1 = new Params
        {
            idleEmissiveIntensity = 0.18f,   // 어둑한 미발광 (눈에 겨우 보임)
            idleRimIntensity      = 0.4f,    // 림 거의 없음
            attackPeakIntensity   = 1.2f,    // 공격 시 잠깐 번쩍
            rimPower              = 5.0f,    // 좁은 림 (가는 선)
            breathAmplitude       = 0.05f,   // 숨 거의 안 보임
        };

        // ★LV2 — 활성화 느낌. 림 약간 보임.
        public static readonly Params LV2 = new Params
        {
            idleEmissiveIntensity = 0.35f,
            idleRimIntensity      = 0.7f,
            attackPeakIntensity   = 1.8f,
            rimPower              = 4.5f,
            breathAmplitude       = 0.08f,
        };

        // ★LV3 (Venosaur 기본) — 중간 밝기. 부위가 뚜렷이 빛남.
        public static readonly Params LV3 = new Params
        {
            idleEmissiveIntensity = 0.65f,   // 이 씬 조명에서 눈에 띄게 빛남
            idleRimIntensity      = 1.2f,    // 림 확실히 보임
            attackPeakIntensity   = 3.0f,    // 공격 시 화끈하게 터짐
            rimPower              = 3.5f,    // 중간 폭 림
            breathAmplitude       = 0.12f,   // 숨 살짝 보임
        };

        // ★LV4 — 활활. 위협적.
        public static readonly Params LV4 = new Params
        {
            idleEmissiveIntensity = 1.1f,
            idleRimIntensity      = 2.0f,
            attackPeakIntensity   = 4.5f,
            rimPower              = 3.0f,    // 더 넓은 림
            breathAmplitude       = 0.18f,
        };

        // ★LV5 — 활활 + 검은 입자(파티클은 범위 밖, 이번 릴리즈 제외). 눈이 부심.
        public static readonly Params LV5 = new Params
        {
            idleEmissiveIntensity = 1.8f,
            idleRimIntensity      = 3.2f,
            attackPeakIntensity   = 7.0f,
            rimPower              = 2.5f,    // 넓은 림 (LV5 특유 "온몸이 타오르는" 느낌)
            breathAmplitude       = 0.25f,
        };

        public static Params Get(int level)
        {
            return level switch
            {
                1 => LV1,
                2 => LV2,
                3 => LV3,
                4 => LV4,
                5 => LV5,
                _ => LV3,   // 범위 밖은 LV3 폴백
            };
        }
    }

    // ── Inspector 노브 ──

    [Header("등급 설정")]
    [Tooltip("몬스터 등급(LV1~5). AuraGrade 표에서 밝기/강도/크기 파라미터를 읽어옴.")]
    [SerializeField, Range(1, 5)] int monsterLevel = 3;

    [Header("색조 노브 (레드오렌지 고정 — 수치만 튜닝)")]
    [Tooltip("오라 기본 색조(레드오렌지). HDR 강도 배율은 등급 파라미터로. 색조를 바꾸지 않는 것이 설계 의도.")]
    [SerializeField] Color auraBaseColor = new Color(1f, 0.353f, 0.173f, 1f);   // #FF5A2C

    [Header("발광 부위 렌더러 (특정 부위만 — 전신 금지)")]
    [Tooltip("변이 핵심부 Renderer 목록. 머리/눈/등/관절 등 지정. 비워두면 첫 번째 SkinnedMeshRenderer 전체 폴백(비권장).")]
    [SerializeField] Renderer[] auraRenderers;

    [Header("숨(Breathing) 노브")]
    [Tooltip("숨 사이클 주기(초). 길수록 느린 맥박.")]
    [SerializeField, Min(0.5f)] float breathPeriod = 2.8f;

    [Header("공격 펄스 노브")]
    [Tooltip("공격 펄스 총 지속시간(초). Triangle envelope (0→peak→0).")]
    [SerializeField, Min(0.05f)] float attackPulseDuration = 0.35f;

    [Header("디버그")]
    [Tooltip("오라 켜기/끄기 (Inspector에서 실시간 테스트).")]
    [SerializeField] bool auraEnabled = true;

    // ── 런타임 상태 ──
    AuraGrade.Params _grade;
    // ★Awake에서 생성 — 필드 이니셜라이저(MonoBehaviour 생성자 시점) new MaterialPropertyBlock()은 Unity가 막는다(런타임 UnityException).
    MaterialPropertyBlock _mpb;

    // MPB 프로퍼티 ID 캐시
    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    static readonly int RimIntensityId  = Shader.PropertyToID("_RimIntensity");
    static readonly int RimPowerId      = Shader.PropertyToID("_RimPower");

    // 현재 idle Emissive 강도 (Breathing이 이 값 ± amplitude로 진동)
    float _currentIdleIntensity;
    // 공격 펄스 진행 중인지 (중복 펄스 트윈 누적 방지)
    bool _attackPulsing;

    // ── 공격 펄스 트윈 진행 변수 ──
    float _pulseT;
    // ★DOTween SetId/Kill은 동일한 object 참조로 해야 함 — 문자열 ID 사용(boxing 불일치 회피)
    string _pulseTweenId;

    void Awake()
    {
        _mpb = new MaterialPropertyBlock();   // ★MPB는 여기서 생성(필드 이니셜라이저 금지 — OnEnable/Start보다 먼저 준비돼야 함)
        _grade = AuraGrade.Get(monsterLevel);
        _currentIdleIntensity = _grade.idleEmissiveIntensity;
        _pulseTweenId = $"AuraPulse_{GetInstanceID()}";

        // 렌더러 미지정 시 첫 번째 SkinnedMeshRenderer 폴백(전신 발광 — 비권장, 경고 출력)
        if (auraRenderers == null || auraRenderers.Length == 0)
        {
            var smr = GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr != null)
            {
                auraRenderers = new Renderer[] { smr };
                Debug.LogWarning($"[MonsterSignatureAura] '{gameObject.name}': auraRenderers 미지정 — 첫 번째 SkinnedMeshRenderer({smr.name}) 전신 폴백. Inspector에서 변이 핵심부만 지정 권장.");
            }
            else
            {
                Debug.LogError($"[MonsterSignatureAura] '{gameObject.name}': auraRenderers 미지정, SkinnedMeshRenderer도 미발견. 오라 비활성.");
                enabled = false;
                return;
            }
        }
    }

    void OnEnable()
    {
        if (!auraEnabled) return;
        StopAllCoroutines();   // ★재활성 시 AuraBreathe 중복 시작 방지(sin 위상 2개 flicker) — Stab H-1
        ApplyIdleGlow(_grade.idleEmissiveIntensity);
        StartCoroutine(AuraBreathe());
    }

    void OnDisable()
    {
        // 코루틴 + 트윈 정리
        StopAllCoroutines();
        if (_pulseTweenId != null) DOTween.Kill(_pulseTweenId);
        _attackPulsing = false;   // ★상태 리셋 — 펄스 중 비활성 시 _attackPulsing이 true로 남아 재활성 후 숨이 영영 막히는 것 방지(아래 가드의 짝)
        ApplyIdleGlow(0f);   // 비활성 시 발광 꺼짐
    }

    // ════════ 평시 숨 펄스 (AuraBreathe) ════════
    // sin파로 미세 진동 — 살아있음 표현. 공격 펄스 중에도 기저로 계속 돌지만,
    // AttackPulse가 훨씬 강하므로 사실상 묻힘 (별도 코루틴이라 중단 불필요).
    IEnumerator AuraBreathe()
    {
        float t = Random.Range(0f, Mathf.PI * 2f);   // 인스턴스마다 위상 어긋남 — 여러 마리가 동기화되지 않게
        while (true)
        {
            // ★공격 펄스 중엔 평시 숨 write를 멈춘다 — 숨·펄스 둘 다 _EmissionColor를 쓰면 숨이 펄스를 매 프레임 덮어
            //   컨택 "확 번쩍"이 무력화됨(Codex H). _attackPulsing은 OnComplete/OnDisable에서 false로 풀림.
            if (!auraEnabled || _attackPulsing) { yield return null; continue; }

            t += Time.deltaTime * (Mathf.PI * 2f / breathPeriod);
            float sine = Mathf.Sin(t);   // -1 ~ 1
            // 기준 intensity ± amplitude (하단 클램프 0 — 음수 발광 방지)
            float intensity = Mathf.Max(0f, _currentIdleIntensity + sine * _grade.breathAmplitude);
            ApplyIdleGlow(intensity);
            yield return null;
        }
    }

    // ════════ 공격 펄스 (외부 호출 API) ════════
    // VenosaurBrawler.ClawHit → 이 메서드를 호출 → 해당 부위가 확 터짐.
    // VfxDirector.PulseGlow와 동일한 Triangle envelope 패턴.
    // ★공격 중 연속 ClawHit 시 이전 트윈을 Kill하고 재시작(누적 방지, M-2 패턴 계승).
    public void PulseAttack()
    {
        if (!isActiveAndEnabled) return;   // ★비활성/파괴된 컴포넌트는 펄스 안 함(풀링·사망 프레임 방어) — Codex M
        if (auraRenderers == null || auraRenderers.Length == 0) return;
        if (!auraEnabled) return;
        if (attackPulseDuration <= 0f) return;

        // 기존 공격 펄스 트윈 Kill (DOTween 누적 방지 — M-2 패턴)
        DOTween.Kill(_pulseTweenId);

        float peak = _grade.attackPeakIntensity;
        _pulseT = 0f;

        DOTween.To(() => _pulseT, v => _pulseT = v, attackPulseDuration, attackPulseDuration)
            .SetEase(Ease.Linear)
            .SetId(_pulseTweenId)   // 문자열 ID — 인스턴스별 고유 (Kill 가능하게)
            .OnUpdate(() =>
            {
                if (this == null || !auraEnabled) return;   // ★대상 파괴 후에도 트윈이 살아 파괴된 멤버/렌더러 접근하는 것 방어 — Stab H-2
                float norm = _pulseT / attackPulseDuration;
                // Triangle: 0→0.4 올라가고 0.4→1.0 내려감 (앞쪽이 빠른 스냅, 뒤쪽이 느린 페이드아웃)
                float envelope = norm < 0.4f
                    ? norm / 0.4f
                    : 1f - (norm - 0.4f) / 0.6f;
                float intensity = Mathf.Lerp(_currentIdleIntensity, peak, envelope);
                ApplyEmissive(intensity);
            })
            .OnComplete(() =>
            {
                if (this == null) return;   // ★파괴 후 OnComplete 방어 — Stab H-2
                // 펄스 종료 → 평시 idle 강도로 복귀
                ApplyEmissive(_currentIdleIntensity);
                _attackPulsing = false;
            });

        _attackPulsing = true;
    }

    // ════════ 내부 MPB 적용 ════════

    // 숨 펄스용 — Emissive만 (림은 Awake에서 1회 설정)
    void ApplyIdleGlow(float emissiveIntensity)
    {
        if (auraRenderers == null) return;
        Color c = auraBaseColor * emissiveIntensity;
        foreach (var r in auraRenderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(EmissionColorId, c);
            r.SetPropertyBlock(_mpb);
        }
    }

    // 공격 펄스용 — Emissive만 변조 (림은 평시 고정)
    void ApplyEmissive(float intensity)
    {
        if (auraRenderers == null) return;
        Color c = auraBaseColor * intensity;
        foreach (var r in auraRenderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(EmissionColorId, c);
            r.SetPropertyBlock(_mpb);
        }
    }

    // ════════ 초기화 (Awake에서 1회 — 림 강도/파워 적용) ════════
    void Start()
    {
        // 림 강도/파워는 평시 고정값 — 한 번만 적용.
        // ★Emissive는 Breathing 코루틴이 매 프레임 덮어쓰므로 여기선 림만.
        if (auraRenderers == null) return;
        foreach (var r in auraRenderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(_mpb);
            _mpb.SetFloat(RimIntensityId, _grade.idleRimIntensity);
            _mpb.SetFloat(RimPowerId,     _grade.rimPower);
            r.SetPropertyBlock(_mpb);
        }
    }

    // ════════ 등급 변경 API (런타임 — 미래 보스 페이즈 등급 강화 등) ════════
    /// <summary>등급을 런타임에 변경. 다음 프레임부터 새 밝기 곡선이 적용됨.</summary>
    public void SetLevel(int level)
    {
        monsterLevel = Mathf.Clamp(level, 1, 5);
        _grade = AuraGrade.Get(monsterLevel);
        _currentIdleIntensity = _grade.idleEmissiveIntensity;
        // 림 재적용
        if (auraRenderers != null)
        {
            foreach (var r in auraRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetFloat(RimIntensityId, _grade.idleRimIntensity);
                _mpb.SetFloat(RimPowerId,     _grade.rimPower);
                r.SetPropertyBlock(_mpb);
            }
        }
    }

#if UNITY_EDITOR
    // Inspector에서 auraEnabled 토글 시 즉시 반영 (에디터 루프 편의)
    void OnValidate()
    {
        if (!Application.isPlaying) return;
        _grade = AuraGrade.Get(monsterLevel);
        _currentIdleIntensity = _grade.idleEmissiveIntensity;
        if (!auraEnabled)
            ApplyIdleGlow(0f);
        // 림 파라미터 즉시 반영
        if (auraRenderers != null)
        {
            foreach (var r in auraRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetFloat(RimIntensityId, _grade.idleRimIntensity);
                _mpb.SetFloat(RimPowerId,     _grade.rimPower);
                r.SetPropertyBlock(_mpb);
            }
        }
    }
#endif
}
