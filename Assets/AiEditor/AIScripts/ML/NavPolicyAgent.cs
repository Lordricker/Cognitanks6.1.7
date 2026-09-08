using UnityEngine;
using UnityEngine.InputSystem;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Policies;

/// <summary>
/// Nav-branch RL policy (BottomUpAgentPlan.md Section 2.2). Owns chassis movement
/// (throttle, turn) via TankMan.SetMovementInput - never the turret.
///
/// Disabled by default. The MLPolicy BT node (Section 2.4, not yet built) is responsible for
/// enabling this component to hand control of the Nav branch to the trained policy, and
/// disabling it again when the release condition fires. The Nav branch's BT movement actions
/// must not run while this component is enabled - there is no mutex here, that invariant is the
/// BT node's job.
///
/// Phase 1 spike scope: this is a hand-fixed observation/reward set (via TankRewardConfig), not
/// yet configurable through MLGoalNodeAsset (Phase 2). Solo/1v1 only - the opponent field is a
/// direct reference to one BT-controlled tank, not the general multi-opponent perception design
/// in Section 3 (deferred until team/BufferSensor work). It's used both for reward computation and
/// for the vision-cone observation (TankVisionCheck) - the turret owns the vision stats/look
/// direction, Nav reads the same detection result the turret branch does.
///
/// Lives on its own child GameObject under the tank root, NOT on the same GameObject as
/// TurretPolicyAgent. BehaviorParameters/DecisionRequester are strictly one-per-GameObject in
/// ML-Agents (this script's Awake() configures the single BehaviorParameters via GetComponent,
/// which only ever finds the first one) - stacking both policy agents on one GameObject makes them
/// silently overwrite each other's VectorObservationSize/ActionSpec and produces
/// "more observations made than vector observation size" warnings/truncation.
/// </summary>
[RequireComponent(typeof(BehaviorParameters))]
[RequireComponent(typeof(DecisionRequester))]
public class NavPolicyAgent : Agent
{
    // 19 = HP%(1) + reload(1) + linVel(3) + angVel(3) + isGrounded(1) + usingComs(1)
    //    + turnRamp(1) + normTurnPower(1) + turretYaw(1) + turretPitch(1)
    //    + vision-cone check: hasVision(1) + localDirection(3) + normalizedDistance(1).
    // Must match CollectObservations exactly or ML-Agents pads/truncates.
    private const int VectorObservationSize = 19;

    [SerializeField] private TankMan tankMan;
    [SerializeField] private TankRewardConfig rewardConfig;
    [Tooltip("Solo/1v1 opponent for reward computation (distance-band, flanking, kill bonus). Not used for observations - those stay limited to what TankMan's public getters expose (Section 3).")]
    [SerializeField] private TankMan opponent;
    private Rigidbody rb;

    // BehaviorParameters must be configured here, not in Initialize(): Agent.LazyInitialize()
    // calls InitializeActuators() (which reads BrainParameters.ActionSpec to build the actuator)
    // BEFORE it calls Initialize(), so setting ActionSpec there is one step too late - the
    // actuator's already built with the default (0 discrete branches) spec by then. Awake() is
    // documented by the base class as running before OnEnable()/LazyInitialize(), so it's the
    // correct hook.
    protected override void Awake()
    {
        base.Awake();

        if (tankMan == null)
            tankMan = GetComponentInParent<TankMan>();
        rb = tankMan != null ? tankMan.GetComponent<Rigidbody>() : null;

        var behaviorParameters = GetComponent<BehaviorParameters>();
        behaviorParameters.BehaviorName = "NavPolicy";
        behaviorParameters.BrainParameters.VectorObservationSize = VectorObservationSize;
        behaviorParameters.BrainParameters.ActionSpec = ActionSpec.MakeContinuous(2);

        if (tankMan != null)
        {
            tankMan.OnDamageTaken += HandleOwnDamageTaken;
            // Weak secondary signal (Section 5 explicitly allows this: "Damage dealt" is
            // Turret-primary but can be enabled on Nav as a secondary). Without this, Nav's only
            // incentive is purely defensive (avoid damage, hold position) with nothing pushing it
            // toward staying somewhere Turret can actually connect - risks Nav's evasion starving
            // Turret of engagement opportunities entirely (the co-adaptation dynamic, Risk 8).
            // Combined with damageTakenWeight below, the net incentive becomes "somewhere hittable,
            // not somewhere gettable-hit" instead of pure flight.
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
            tankMan.OnDamageTaken -= HandleOwnDamageTaken;
            tankMan.OnDamageDealt -= HandleOwnDamageDealt;
            tankMan.OnDied -= HandleOwnDeath;
        }
        if (opponent != null)
        {
            opponent.OnDied -= HandleOpponentDeath;
        }
    }

    // Per-branch BT/ML mutex (TankMan.SetNavAIEnabled) - lets this Agent take over just the Nav
    // branch while Turret stays BT-driven (or vice versa on TurretPolicyAgent), enabling curriculum
    // training: train one branch against a competently-behaving BT partner instead of both branches
    // co-adapting from scratch simultaneously. Declared as a separate OnEnable/OnDisable (not part
    // of Awake/OnDestroy) so it also works if a future MLPolicy BT node (Section 2.4) toggles this
    // component on/off at runtime rather than it being enabled for a whole training run.
    // Agent.OnEnable/OnDisable are `protected virtual`, not plain Unity messages - must use
    // override + base.OnEnable()/OnDisable() (same reasoning as Awake() above) or ML-Agents' own
    // LazyInitialize()/registration in the base class would silently never run.
    protected override void OnEnable()
    {
        base.OnEnable();
        if (tankMan != null) tankMan.SetNavAIEnabled(false);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (tankMan != null) tankMan.SetNavAIEnabled(true);
    }

    private void HandleOwnDamageTaken(float damage)
    {
        if (rewardConfig != null)
            AddReward(rewardConfig.damageTakenWeight * damage);
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
        // Total episode reset (position/velocity/HP/AllyTargetList/etc.) is the training arena's
        // job (Section 8), not this Agent's - it only resets what it owns.
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
        sensor.AddObservation(tankMan.transform.InverseTransformDirection(rb.linearVelocity)); // 3
        sensor.AddObservation(tankMan.transform.InverseTransformDirection(rb.angularVelocity)); // 3

        // Hidden actuator state - required for the MDP to be Markov (Section 3)
        sensor.AddObservation(tankMan.IsGrounded); // 1
        sensor.AddObservation(tankMan.IsCurrentlyUsingComs); // 1
        sensor.AddObservation(tankMan.TurnRampProgress01); // 1
        sensor.AddObservation(tankMan.NormalizedTurningPower); // 1

        // Cross-branch: turret state (Section 3). Turret local yaw/pitch is read straight off the
        // Transform, so it's always live regardless of who's driving that branch.
        Vector3 turretLocalEuler = tankMan.turretPivot != null ? tankMan.turretPivot.localEulerAngles : Vector3.zero;
        sensor.AddObservation(NormalizeAngle(turretLocalEuler.y) / 180f); // 1
        sensor.AddObservation(NormalizeAngle(turretLocalEuler.x) / 180f); // 1

        // Vision-cone check (Section 3) - see TankVisionCheck. This replaced TankMan.HasTarget()/
        // TimeSinceLastShot, which were only meaningful while the turret branch was BT-driven
        // (currentTarget is set exclusively by the BT's IfEnemy/IfAny/IfAlly condition handlers -
        // Risk 8, branch co-adaptation, went stale the moment the turret was ALSO under MLPolicy
        // control). This check is computed fresh every tick directly off the turret's live
        // orientation, so it stays correct regardless of which branch is driving the turret.
        bool hasVision = TankVisionCheck.TryDetect(tankMan, opponent, out Vector3 localDirection, out float normalizedDistance);
        sensor.AddObservation(hasVision); // 1
        sensor.AddObservation(localDirection); // 3
        sensor.AddObservation(normalizedDistance); // 1
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (tankMan == null)
            return;

        float move = actions.ContinuousActions[0];
        float turn = actions.ContinuousActions[1];
        tankMan.SetMovementInput(move, turn);

        if (rewardConfig == null)
            return;

        // Dense per-decision terms (Section 5) - damage-taken/death/kill are event-driven above.
        AddReward(rewardConfig.survivalWeight);

        // Anti-idling nudge - reward actual forward chassis speed, not the raw move action, so a
        // tank that commands throttle but is stuck/blocked doesn't get credit for motion that
        // isn't happening. Not part of Section 5's palette - see TankRewardConfig.movementWeight.
        if (rb != null && tankMan.MoveSpeed > 0f)
        {
            float forwardSpeed = Vector3.Dot(rb.linearVelocity, tankMan.transform.forward);
            AddReward(rewardConfig.movementWeight * Mathf.Clamp01(forwardSpeed / tankMan.MoveSpeed));
        }

        // Distance-band and flanking are gated on the tank's OWN actual current vision of the
        // opponent (TankVisionCheck - same check CollectObservations uses), not just proximity.
        // Section 5 revision 2026-09-01: this used to be ungated, rewarding Nav for holding a good
        // distance/flank position relative to an opponent it could not currently perceive through
        // any observed channel. Nav's observations never include raw opponent position - only the
        // vision-gated result - so the network had no legitimate way to learn to produce that
        // outcome; any apparent improvement from the ungated version could only have been
        // exploiting incidental structure of this specific training arena (fixed spawn geometry,
        // deterministic BT movement), not a real positioning skill, and would not have held up
        // against a real/unpredictable opponent. Gating on hasVision ties the reward to the same
        // information channel the policy actually has. The tradeoff is a harder bootstrap - Nav
        // can't earn either term until Turret's aim is competent enough to keep the opponent in
        // view at all - but that coupling is intentional: it forces Nav to learn positioning that
        // actually supports keeping the opponent visible, not positioning in a vacuum.
        // survivalWeight/movementWeight above are both purely self-referential (no opponent info
        // needed) and stay ungated, so this isn't a total cold start.
        bool hasVision = opponent != null && TankVisionCheck.TryDetect(tankMan, opponent, out _, out _);
        if (hasVision)
        {
            Vector3 selfPos = tankMan.transform.position;
            Vector3 opponentPos = opponent.transform.position;
            float distance = Vector3.Distance(selfPos, opponentPos);

            // Band is relative to the tank's own live weapon Range (varies per turret loadout),
            // not a fixed world-unit distance - see TankRewardConfig.distanceBandMinRatio/MaxRatio.
            float minDistance = tankMan.Range * rewardConfig.distanceBandMinRatio;
            float maxDistance = tankMan.Range * rewardConfig.distanceBandMaxRatio;
            if (distance >= minDistance && distance <= maxDistance)
                AddReward(rewardConfig.distanceBandWeight);

            // Flanking: opponent is visible to self (checked above), but self is outside the
            // opponent's own vision cone - the ideal "I see them, they don't see me" position.
            // (opponent.VisionRange is no longer checked separately here - hasVision above already
            // bounds distance by the tank's own VisionRange, which is what actually matters for
            // "is this flank position tactically relevant right now".)
            Vector3 opponentToSelf = selfPos - opponentPos;
            float angleFromOpponentForward = Vector3.Angle(opponent.transform.forward, opponentToSelf);
            bool outsideOpponentVisionCone = angleFromOpponentForward > opponent.VisionCone * 0.5f;
            if (outsideOpponentVisionCone)
                AddReward(rewardConfig.flankingWeight);
        }

        CheckReleaseCondition(hasVision);
    }

    /// <summary>
    /// MLPolicy node release condition (Section 2.4) - hands the branch back to BT once the
    /// opponent has been continuously out of vision for releaseGraceSeconds. Off by default
    /// (releaseControlOnLostVision = false) so the Phase 1 training arena's behavior is completely
    /// unaffected - TrainingEpisodeManager owns episode boundaries there (death/timeout), and this
    /// Agent has no business self-disabling mid-episode just because the target is momentarily out
    /// of view (that's the normal search/patrol phase, not a reason to quit). Only turn this on for
    /// an Agent actually being driven by a real MLPolicy BT node in a gameplay scene.
    ///
    /// The grace period (not an instant release on the very first no-vision tick) exists because
    /// hasVision can flicker true/false on momentary occlusion (a wall, another tank passing
    /// between) - releasing on a single tick would ping-pong control back and forth between BT and
    /// ML far faster than either can act coherently.
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
            // EpisodeInterrupted (not EndEpisode) - this is a truncation, not a terminal state;
            // losing sight of a target the BT will now go search for again is not "the episode
            // failed," and marking it as such would teach the value function the wrong lesson
            // (Section 2.4's original design note on why OnDisable's implicit DoneReason.Disabled
            // path is wrong for this). Disabling afterward is a no-op for the reward/episode
            // bookkeeping since NotifyAgentDone no-ops once already done - it only performs the
            // actual BT/ML mutex handoff via OnDisable -> TankMan.SetNavAIEnabled(true).
            EpisodeInterrupted();
            enabled = false;
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuous = actionsOut.ContinuousActions;
        var keyboard = Keyboard.current;
        if (keyboard == null)
        {
            continuous[0] = 0f;
            continuous[1] = 0f;
            return;
        }

        float move = 0f;
        if (keyboard.wKey.isPressed) move += 1f;
        if (keyboard.sKey.isPressed) move -= 1f;

        float turn = 0f;
        if (keyboard.dKey.isPressed) turn += 1f;
        if (keyboard.aKey.isPressed) turn -= 1f;

        continuous[0] = move;
        continuous[1] = turn;
    }

    private static float NormalizeAngle(float angle)
    {
        if (angle > 180f) angle -= 360f;
        return angle;
    }
}
