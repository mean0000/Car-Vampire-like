using UnityEngine;

/// <summary>지면 감지. 다른 차량 컴포넌트가 참조해 씀.</summary>
[RequireComponent(typeof(Collider))]
public class CarGroundSensor : MonoBehaviour
{
    [SerializeField] private float checkDistance = 0.35f;
    [SerializeField] private LayerMask groundLayer = -1;

    public bool IsGrounded { get; private set; }
    public Vector3 GroundNormal { get; private set; } = Vector3.up;

    private Collider _col;
    private Collider[] _selfColliders;

    private void Awake()
    {
        _col = GetComponent<Collider>();
        _selfColliders = GetComponentsInChildren<Collider>();
        if (_col == null)
        {
            Debug.LogError($"[CarGroundSensor] No Collider on {gameObject.name}. Disabling.", this);
            enabled = false;
        }
    }

    /// <summary>즉시 지면 감지 수행. CarMovement에서 호출하여 실행 순서 보장.</summary>
    public void Probe()
    {
        Vector3 origin = _col.bounds.center;
        Vector3 downDir = -transform.up;
        float castDist = _col.bounds.extents.y + checkDistance;
        if (Physics.SphereCast(origin, 0.2f, downDir, out RaycastHit hit, castDist,
                               groundLayer, QueryTriggerInteraction.Ignore)
            && !IsSelfCollider(hit.collider))
        {
            IsGrounded = true;
            GroundNormal = hit.normal;
        }
        else
        {
            IsGrounded = false;
            GroundNormal = Vector3.up;
        }
    }

    // Probe()는 CarMovement.FixedUpdate에서 직접 호출하여 실행 순서 보장.
    // FixedUpdate 제거 — 중복 SphereCast 방지.

    private bool IsSelfCollider(Collider c)
    {
        for (int i = 0; i < _selfColliders.Length; i++)
            if (_selfColliders[i] == c) return true;
        return false;
    }
}
