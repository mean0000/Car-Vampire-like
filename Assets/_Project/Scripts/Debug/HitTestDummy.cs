using UnityEngine;

/// <summary>
/// ★임시 테스트 더미(throwaway) — 카타나 히트박스 검증 전용. 정식 적이 IDamageable을 구현하기 전,
/// 히트박스가 실제로 적중하는지(각도/사거리/오프셋/중복가드) 객관 확인하기 위한 표적.
/// 피격 시: 콘솔 로그 + 빨강 플래시(MaterialPropertyBlock) + 약한 넉백 + HP0이면 잠깐 후 부활(반복 테스트).
/// 정식 적 시스템 붙으면 삭제.
/// </summary>
[RequireComponent(typeof(Collider))]
public class HitTestDummy : MonoBehaviour, IDamageable
{
    [SerializeField] int maxHp = 12;
    [SerializeField] float flashTime = 0.12f;
    [SerializeField] float reviveDelay = 1.0f;

    int _hp;
    Renderer _rend;
    MaterialPropertyBlock _mpb;
    float _flashTimer;
    float _reviveTimer;
    Vector3 _spawnPos;
    Vector3 _knockVel;
    bool _dead;
    static readonly int ColorID = Shader.PropertyToID("_BaseColor");

    void Awake()
    {
        _hp = maxHp;
        _spawnPos = transform.position;
        _rend = GetComponentInChildren<Renderer>();
        _mpb = new MaterialPropertyBlock();
    }

    public void TakeHit(int damage, Vector3 from, float knockback)
    {
        if (_dead) return;
        _hp -= damage;
        _flashTimer = flashTime;
        Vector3 dir = transform.position - from; dir.y = 0f;
        _knockVel = (dir.sqrMagnitude > 0.0001f ? dir.normalized : transform.forward) * knockback;
        Debug.Log($"[HitDummy] {name} took {damage} (hp {Mathf.Max(_hp,0)}/{maxHp}) kb {knockback:F1}", this);
        if (_hp <= 0) { _dead = true; _reviveTimer = reviveDelay; }
    }

    void Update()
    {
        float dt = Time.deltaTime;

        if (_knockVel.sqrMagnitude > 0.0001f)
        {
            transform.position += _knockVel * dt;
            _knockVel = Vector3.MoveTowards(_knockVel, Vector3.zero, 12f * dt);
        }

        if (_dead)
        {
            _reviveTimer -= dt;
            if (_reviveTimer <= 0f) { _dead = false; _hp = maxHp; transform.position = _spawnPos; _knockVel = Vector3.zero; }
        }

        if (_rend != null)
        {
            if (_flashTimer > 0f) _flashTimer -= dt;
            Color c = _dead ? new Color(0.2f, 0.2f, 0.2f)
                    : _flashTimer > 0f ? Color.red
                    : new Color(0.8f, 0.5f, 0.5f);
            _rend.GetPropertyBlock(_mpb);
            _mpb.SetColor(ColorID, c);
            _rend.SetPropertyBlock(_mpb);
        }
    }
}
