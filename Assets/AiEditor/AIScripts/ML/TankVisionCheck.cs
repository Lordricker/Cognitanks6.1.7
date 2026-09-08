using UnityEngine;

/// <summary>
/// Shared vision-cone check for ML observations, mirroring TankMan.UpdateSensorData()'s own
/// technique exactly (Vector3.Angle against the turret's current forward, gated by
/// VisionRange/VisionCone) rather than a raycast-fan sensor. Kept as one shared static method
/// so NavPolicyAgent and TurretPolicyAgent's observations of "can this tank currently see the
/// opponent" can never drift out of sync with each other - the turret owns the vision stats and
/// look direction, both branches read the same result (BottomUpAgentPlan.md Section 2.2/2.4:
/// "Nav needs to read [the turret's] data").
///
/// Legitimately gated, not omniscient (Section 3): direction/distance are only ever non-zero when
/// the opponent actually falls within the tank's real detection envelope, the same condition the
/// BT uses. Line-of-sight/occlusion is deliberately not checked here (TankMan's own
/// CheckLineOfSight is a separate, per-candidate raycast) - add it later if it turns out to matter.
/// </summary>
public static class TankVisionCheck
{
    /// <summary>
    /// Checks whether `opponent` currently falls within `tankMan`'s turret vision cone/range.
    /// Returns local (turret-space) direction and normalized distance when detected; both are
    /// zero when not detected, so "not seen right now" is unambiguous to whatever reads them.
    /// </summary>
    public static bool TryDetect(TankMan tankMan, TankMan opponent, out Vector3 localDirection, out float normalizedDistance)
    {
        localDirection = Vector3.zero;
        normalizedDistance = 0f;

        if (tankMan == null || opponent == null || tankMan.turretPivot == null)
            return false;

        Vector3 toOpponent = opponent.transform.position - tankMan.turretPivot.position;
        float distance = toOpponent.magnitude;
        if (distance > tankMan.VisionRange)
            return false;

        float angle = Vector3.Angle(tankMan.turretPivot.forward, toOpponent);
        if (angle > tankMan.VisionCone * 0.5f)
            return false;

        localDirection = tankMan.turretPivot.InverseTransformDirection(toOpponent.normalized);
        normalizedDistance = Mathf.Clamp01(distance / tankMan.VisionRange);
        return true;
    }
}
