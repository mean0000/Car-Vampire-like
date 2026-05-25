using UnityEngine;

public enum CarZone { Front, Rear, Left, Right }

public static class CarZoneHelper
{
    public static CarZone Resolve(Transform car, Vector3 worldPoint)
    {
        Vector3 local = car.InverseTransformPoint(worldPoint);
        local.y = 0f;

        if (Mathf.Abs(local.z) >= Mathf.Abs(local.x))
            return local.z >= 0f ? CarZone.Front : CarZone.Rear;
        return local.x >= 0f ? CarZone.Right : CarZone.Left;
    }
}
