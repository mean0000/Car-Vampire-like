// 룩 랩 카메라 쉐이크 — ★추종 카메라(LabSimpleCamera)가 매 프레임 transform.position을 덮어쓰므로 Feel MMCameraShaker
//   (localPosition 흔들기)와 구조 충돌(추종이 쉐이크를 지움 = Stab H-1/H-2). → 쉐이크를 *오프셋*으로 분리:
//   SmashFeel이 임펄스를 넣고(Add), LabSimpleCamera가 *내부 보존한 추종값 위에* 이 오프셋을 얹는다(추종 SmoothDamp엔 안 섞임).
//   ★XZ 평면만(탑다운 멀미 방지 = 카메라 Y/회전 고정 설계 준수). unscaledTime(히트스탑 timeScale 0.05 중에도 진동 = 쾅+정지 동시).
using UnityEngine;

public static class LabCameraShake
{
    static float _amplitude, _frequency, _duration, _timeLeft, _seedX, _seedZ;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { _amplitude = _frequency = _duration = _timeLeft = 0f; }

    /// <summary>임팩트 쉐이크 임펄스. 더 강한(혹은 첫) 임펄스면 교체+재시드, 약하면 지속만 연장(연타 시 카오스 방지).</summary>
    public static void Add(float duration, float amplitude, float frequency)
    {
        if (amplitude <= 0f || duration <= 0f) return;
        if (amplitude >= _amplitude || _timeLeft <= 0f)
        {
            _amplitude = amplitude;
            _frequency = Mathf.Max(0.01f, frequency);
            _duration  = duration;
            _timeLeft  = duration;
            _seedX = Random.value * 97f;
            _seedZ = Random.value * 53f;
        }
        else _timeLeft = Mathf.Max(_timeLeft, duration);
    }

    /// <summary>LabSimpleCamera가 LateUpdate에서 1회 호출 — 시간 진행 + 현재 XZ 오프셋 반환(ease-out 감쇠 진동).</summary>
    public static Vector3 Evaluate()
    {
        if (_timeLeft <= 0f) return Vector3.zero;
        _timeLeft -= Time.unscaledDeltaTime;
        if (_timeLeft <= 0f) { _amplitude = 0f; return Vector3.zero; }
        float decay = _timeLeft / Mathf.Max(0.0001f, _duration);   // 1→0
        float amp = _amplitude * decay * decay;                    // ease-out(제곱) — 초반 강하게 쾅, 끝 부드럽게
        float t = Time.unscaledTime * _frequency;
        float ox = (Mathf.PerlinNoise(_seedX, t) - 0.5f) * 2f;     // -1~1
        float oz = (Mathf.PerlinNoise(_seedZ, t) - 0.5f) * 2f;
        return new Vector3(ox, 0f, oz) * amp;
    }
}
