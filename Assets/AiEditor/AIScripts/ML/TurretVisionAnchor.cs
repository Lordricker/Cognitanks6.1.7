using UnityEngine;

/// <summary>
/// Mirrors a turret's world position/rotation onto this GameObject's own transform every
/// LateUpdate, so a RayPerceptionSensorComponent3D attached here tracks the turret's independent
/// rotation.
///
/// Why this exists instead of just parenting the sensor under the turret directly:
/// RayPerceptionSensorComponentBase.GetRayPerceptionInput() hardcodes
/// `rayPerceptionInput.Transform = transform` - it always reads whatever GameObject the sensor
/// script itself sits on, with no field to point it at a different transform. And ML-Agents only
/// auto-discovers sensors that are descendants of the Agent's own GameObject
/// (GetComponentsInChildren, scoped per-Agent - the same reason NavPolicyAgent/TurretPolicyAgent
/// had to be separate GameObjects in the first place). TankMan's turretTransform is a SIBLING of
/// the Nav/Turret Agent helper objects (all children of the tank root), not a descendant of
/// either, so a sensor parented directly under the turret would never be found by either Agent's
/// sensor discovery. This script lets the sensor live in the right place (under an Agent) while
/// still tracking the turret's actual orientation.
///
/// [DefaultExecutionOrder(100)] guarantees this runs after TankMan's own LateUpdate() (the pitch
/// clamp on turretTransform), so it copies the turret's final, settled orientation for the frame,
/// not a pre-clamp value.
/// </summary>
[DefaultExecutionOrder(100)]
public class TurretVisionAnchor : MonoBehaviour
{
    [SerializeField] private Transform turretTransform;

    private void Awake()
    {
        if (turretTransform == null)
        {
            var tankMan = GetComponentInParent<TankMan>();
            if (tankMan != null)
                turretTransform = tankMan.turretPivot;
        }
    }

    private void LateUpdate()
    {
        if (turretTransform == null)
            return;
        transform.SetPositionAndRotation(turretTransform.position, turretTransform.rotation);
    }
}
