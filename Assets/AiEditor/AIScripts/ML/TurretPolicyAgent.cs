using UnityEngine;
using UnityEngine.InputSystem;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Policies;

/// <summary>
/// Turret-branch RL policy (BottomUpAgentPlan.md Section 2.2). Owns turret aiming and firing via
/// TankMan.RotateTurret / TankMan.TryFire - never chassis movement.
///
/// Disabled by default, same handoff contract as NavPolicyAgent - the MLPolicy BT node enables/
/// disables this component; the turret branch's BT tracking/fire actions must not run while it's
/// enabled.
///
/// Phase 1 spike scope: hand-fixed observation/reward set (via TankRewardConfig), not yet
/// configurable through MLGoalNodeAsset (Phase 2).
///
/// Observations deliberately do NOT include TankMan.HasTarget()/currentTarget: that state is set
/// only by the BT's own IfEnemy/IfAny/IfAlly condition handlers, which stop running the moment
/// this branch is under policy control - reading it here would be self-referentially broken
/// (always stale/false).
///
/// Vision is instead computed directly here every tick, mirroring TankMan.UpdateSensorData()'s
/// own technique exactly (Vector3.Angle against the turret's current forward, gated by
/// VisionRange/VisionCone) rather than ML-Agents' RayPerceptionSensorComponent3D - that sensor's
/// rays are a flat horizontal fan (PolarToCartesian3D hardcodes y=0), not a true cone, and since
/// it's anchored to the turret's actual orientation (pitch included), an untrained policy pitching
/// the turret up/down swings the whole fan away from a target that's roughly level. A direct cone
/// check has no such distortion. This is legitimately gated vision, not omniscience (Section 3's
/// concern with a raw position observation) - it only reveals anything when the opponent would
/// actually fall within the tank's real detection envelope, same condition the BT uses. Line-of-
/// sight/occlusion is deliberately not checked yet (TankMan's own CheckLineOfSight is a separate,
/// per-candidate raycast) - add it later if it turns out to matter.
///
/// The REWARD function is different: it's allowed privileged ground-truth access the policy
/// itself never sees, so the `opponent` field below is a direct TankMan reference used both for
/// reward computation (aim accuracy, kill bonus) and to compute the vision-cone observation above
/// - solo/1v1 only, not the general multi-opponent design in Section 3.
///
/// Lives on its own child GameObject under the tank root, NOT on the same GameObject as
/// NavPolicyAgent - see the note on NavPolicyAgent for why (BehaviorParameters/DecisionRequester
/// are strictly one-per-GameObject in ML-Agents).
/// </summary>
[RequireComponent(typeof(BehaviorParameters))]
[RequireComponent(typeof(DecisionRequester))]
public class TurretPolicyAgent : Agent
{
    // 13 self/proprioceptive (see CollectObservations) + 5 vision-cone check
    // (hasVision(1) + localDirection(3) + normalizedDistance(1)).
    private const int VectorObservationSize = 18;

    [SerializeField] private TankMan tankMan;
    [SerializeField] private TankRewardConfig rewardConfig;
    [Tooltip("Solo/1v1 opponent for reward computation (aim accuracy, kill bonus). Not used for observations (Section 3).")]
    [SerializeField] private TankMan opponent;
    private Rigidbody rb;

    // BehaviorParameters must be configured here, not in Initialize() - see the comment on
    // NavPolicyAgent.Awake() for why (InitializeActuators() reads ActionSpec before Initialize()
    // ever runs, so setting the discrete fire branch there is too late and leaves the actuator
    // built with 0 discrete branches, which is exactly what threw the IndexOutOfRangeException
    // on Heuristic()'s DiscreteActions[0] write).
    protected override void Awake()
    {
        base.Awake();

        if (tankMan == null)
            tankMan = GetComponentInParent<TankMan>();
        rb = tankMan != null ? tankMan.GetComponent<Rigidbody>() : null;

        var behaviorParameters = GetComponent<BehaviorParameters>();
        behaviorParameters.BehaviorName = "TurretPolicy";
        behaviorParameters.BrainParameters.VectorObservationSize = VectorObservationSize;
        // 2 continuous (yaw rate, pitch rate) + 1 discrete branch of 2 (fire: 0/1) - Section 4.
        behaviorParameters.BrainParameters.ActionSpec = new ActionSpec(2, new[] { 2 });

        if (tankMan != null)
        {
            tankMan.OnDamageDealt += HandleOwnDamageDealt;
            tankMan.OnDied += HandleOwnDeath;
        }
        if (opponent != null)
        {
            opponent.OnDied += HandleOpponentDeath;
        }
    }

    private void OnDestroy()
    {
        if (tankMan != null)
        {
            tankMan.OnDamageDealt -= HandleOwnDamageDealt;
            tankMan.OnDied -= HandleOwnDeath;
        }
        if (opponent != null)
        {
            opponent.OnDied -= HandleOpponentDeath;
        }
    }

    // Per-branch BT/ML mutex - see the matching comment on NavPolicyAgent.OnEnable/OnDisable
    // (including why these must be `override` + base.OnEnable()/OnDisable(), not plain Unity
    // messages - Agent declares them `protected virtual`).
    protected override void OnEnable()
    {
        base.OnEnable();
        if (tankMan != null) tankMan.SetTurretAIEnabled(false);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (tankMan != null) tankMan.SetTurretAIEnabled(true);
    }

    private void HandleOwnDamageDealt(float damage)
    {
        if (rewardConfig != null)
            AddReward(rewardConfig.damageDealtWeight * damage);
    }

    private void HandleOwnDeath()
    {
        if (rewardConfig != null)
            AddReward(rewardConfig.deathPenalty);
    }

    private void HandleOpponentDeath()
    {
        if (rewardConfig != null)
            AddReward(rewardConfig.killBonus);
    }

    public override void OnEpisodeBegin()
    {
        // Total episode reset is the training arena's job (Section 8), not this Agent's.
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (tankMan == null)
        {
            for (int i = 0; i < VectorObservationSize; i++)
                sensor.AddObservation(0f);
            return;
        }

        // Self / proprioceptive (Section 3)
        sensor.AddObservation(tankMan.TotalHP > 0 ? tankMan.CurrentHealth / tankMan.TotalHP : 0f); // 1
        sensor.AddObservation(tankMan.ReloadProgress01); // 1

        Vector3 turretLocalEuler = tankMan.turretPivot != null ? tankMan.turretPivot.localEulerAngles : Vector3.zero;
        sensor.AddObservation(NormalizeAngle(turretLocalEuler.y) / 180f); // 1
        sensor.AddObservation(NormalizeAngle(turretLocalEuler.x) / 180f); // 1

        sensor.AddObservation(tankMan.Range); // 1
        sensor.AddObservation(tankMan.VisionRange); // 1
        sensor.AddObservation(tankMan.VisionCone); // 1

        // Cross-branch: chassis motion (Section 3 - "Turret policy observes: chassis linear/
        // angular velocity, heading"). Same Rigidbody as the Nav branch actuates - the split is
        // about which policy issues commands, not separate physics bodies.
        sensor.AddObservation(tankMan.transform.InverseTransformDirection(rb.linearVelocity)); // 3
        sensor.AddObservation(tankMan.transform.InverseTransformDirection(rb.angularVelocity)); // 3

        // Vision-cone check (Section 3) - see TankVisionCheck for why this replaces
        // RayPerceptionSensorComponent3D. Direction/distance are zero when not currently detected.
        bool hasVision = TankVisionCheck.TryDetect(tankMan, opponent, out Vector3 localDirection, out float normalizedDistance);
        sensor.AddObservation(hasVision); // 1
        sensor.AddObservation(localDirection); // 3
        sensor.AddObservation(normalizedDistance); // 1
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (tankMan == null)
            return;

        float yawIntensity = actions.ContinuousActions[0];
        float pitchIntensity = actions.ContinuousActions[1];
        tankMan.RotateTurret(yawIntensity, pitchIntensity);

        // No penalty for firing (Section 5 revision, 2026-09-01): the only real downside of
        // spamming fire is a wasted cooldown window if an enemy shows up mid-reload, a minor
        // consequence not worth shaping against directly - and a flat per-shot cost was creating
        // a "never fire" cold-start trap while hit rate was still near zero. Reload gating alone
        // (TryFire's own IsReloadReady check) already caps how often this can even fire.
        int fire = actions.DiscreteActions[0];
        if (fire == 1)
        {
            tankMan.TryFire();
        }

        // Computed and checked ahead of the rewardConfig null-return below on purpose - release
        // checking must keep working in the real gameplay use case (an MLPolicy BT node, Section
        // 2.4), where rewardConfig will typically be unassigned entirely since no training/reward
        // computation happens at play time (Section 2.4/10c: "inert at play time").
        bool hasVision = opponent != null && tankMan.turretPivot != null
            && TankVisionCheck.TryDetect(tankMan, opponent, out _, out _);
        CheckReleaseCondition(hasVision);

        if (rewardConfig == null || opponent == null || tankMan.turretPivot == null)
            return;

        // Dense aim-accuracy term (Section 5) - damage-dealt/death/kill stay the significant,
        // sparser combat-outcome rewards (event-driven, above); this is purely a dense per-tick
        // guidance signal so there's gradient to follow even before the turret's precisely
        // on-target. 1.0 when dead-on the opponent's center, 0.0 at 180 degrees off.
        Vector3 directionToOpponent = (opponent.transform.position - tankMan.turretPivot.position).normalized;
        float angleToOpponent = Vector3.Angle(tankMan.turretPivot.forward, directionToOpponent);
        float accuracy01 = 1f - Mathf.Clamp01(angleToOpponent / 180f);
        AddReward(rewardConfig.aimAccuracyWeight * accuracy01);

        // Raycast bonus: does the turret's CURRENT aim actually connect with the opponent's real
        // hitbox right now - not just its center point? Angle-to-center alone scores a shot aimed
        // at the target's edge the same as one aimed at empty air just past it; this doesn't.
        // Deliberately computed every tick rather than waiting for an actual fired shot to resolve
        // (the bullet-impact miss-distance idea, shelved - reload-gated, too sparse relative to
        // what this already gives for free every decision).
        // Deliberately NOT penalizing ground hits separately here anymore: it created an
        // asymmetric incentive (only downward-pointing was punished) that made "point straight up,
        // hit nothing" a cheap way to dodge the penalty without ever attempting to aim at the
        // opponent - easier for random exploration to discover than genuine tracking. The
        // angle-based term above already symmetrically scores every off-target direction (up
        // included) the same way, with no such loophole.
        if (Physics.Raycast(tankMan.turretPivot.position, tankMan.turretPivot.forward, out RaycastHit hit, tankMan.Range)
            && hit.collider.GetComponentInParent<TankMan>() == opponent)
        {
            AddReward(rewardConfig.aimHitWeight);
        }
    }

    /// <summary>
    /// MLPolicy node release condition (Section 2.4) - see the matching comment on
    /// NavPolicyAgent.CheckReleaseCondition for the full reasoning (grace period against vision
    /// flicker, EpisodeInterrupted vs EndEpisode, off by default for training-arena use).
    /// </summary>
    [Header("MLPolicy Node (Section 2.4) - only relevant when BT-driven, not the training arena")]
    [Tooltip("When true, this Agent voluntarily releases control back to the BT node that enabled it once the opponent has been out of vision continuously for releaseGraceSeconds. Leave false for training-arena use (TrainingEpisodeManager owns episode boundaries there).")]
    [SerializeField] private bool releaseControlOnLostVision = false;
    [SerializeField] private float releaseGraceSeconds = 2f;
    private float timeWithoutVision;

    private void CheckReleaseCondition(bool hasVision)
    {
        if (!releaseControlOnLostVision)
            return;

        if (hasVision)
        {
            timeWithoutVision = 0f;
            return;
        }

        timeWithoutVision += Time.fixedDeltaTime;
        if (timeWithoutVision >= releaseGraceSeconds)
        {
            EpisodeInterrupted();
            enabled = false;
        }
    }

    // TEMP DEBUG - manual actuator smoke test only (arrows to aim, Space to fire). Remove once
    // RotateTurret/TryFire are confirmed working; this isn't a real control scheme, the turret
    // branch has no intended human-playable heuristic (Section 3 - perception isn't wired up yet).
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuous = actionsOut.ContinuousActions;
        var discrete = actionsOut.DiscreteActions;
        var keyboard = Keyboard.current;
        if (keyboard == null)
        {
            continuous[0] = 0f;
            continuous[1] = 0f;
            discrete[0] = 0;
            return;
        }

        float yaw = 0f;
        if (keyboard.rightArrowKey.isPressed) yaw += 1f;
        if (keyboard.leftArrowKey.isPressed) yaw -= 1f;

        float pitch = 0f;
        if (keyboard.upArrowKey.isPressed) pitch += 1f;
        if (keyboard.downArrowKey.isPressed) pitch -= 1f;

        continuous[0] = yaw;
        continuous[1] = pitch;
        discrete[0] = keyboard.spaceKey.isPressed ? 1 : 0;
    }

    private static float NormalizeAngle(float angle)
    {
        if (angle > 180f) angle -= 360f;
        return angle;
    }
}
