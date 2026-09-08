using System.Collections;
using UnityEngine;

/// <summary>
/// Ties episode boundaries together across NavPolicyAgent, TurretPolicyAgent, and one BT-driven
/// opponent tank (BottomUpAgentPlan.md Section 8). Solo/1v1 training arena only - team/opponent-
/// pool support (Section 8.1) is explicitly deferred.
///
/// Terminal on: this tank's death, the opponent's death, or a wall-clock timeout.
///
/// Whichever tank actually died is hidden (TankMan.SetDetectable(false)) for `respawnDelay`
/// game-seconds before being revived - without this, a tank that reappears instantly in the same
/// spot never gives its opponent a real "lost the target" moment, so positioning/flanking never
/// actually matters and the BT just keeps firing at a known location forever.
///
/// Known gap, not handled here: fall-out. TankMan.Update() still calls RespawnAtSpawnPoint() on
/// fallRespawnHeight, which mid-episode teleports with no reward attached (Section 8 flags this
/// as a silent MDP corruption). Fine for the Phase 1 spike on a flat TestArena where fall-out
/// shouldn't happen; needs fixing (disable respawn in training mode, end episode with a penalty
/// instead) before ramps are added.
/// </summary>
public class TrainingEpisodeManager : MonoBehaviour
{
    [Header("Agents")]
    [SerializeField] private NavPolicyAgent navAgent;
    [SerializeField] private TurretPolicyAgent turretAgent;

    [Header("Tanks")]
    [SerializeField] private TankMan tankMan;
    [SerializeField] private TankMan opponent;

    [Header("Spawn points (falls back to current transform if unset)")]
    [SerializeField] private Transform tankSpawnPoint;
    [SerializeField] private Transform opponentSpawnPoint;

    [Header("Episode")]
    [Tooltip("Wall-clock timeout in seconds (Section 8: '~60s wall-clock equivalent, tune in the spike').")]
    [SerializeField] private float maxEpisodeSeconds = 60f;

    [Tooltip("Game-time seconds a dead tank stays hidden/undetectable before respawning, giving its opponent a genuine window to lose the target and fall back to search/wander instead of instantly re-acquiring it.")]
    [SerializeField] private float respawnDelay = 3f;

    [Header("Spawn Randomization")]
    [Tooltip("Randomizes each tank's starting yaw by +/- this many degrees around tankSpawnPoint/opponentSpawnPoint's own rotation, re-rolled every episode. 0 = always spawn facing exactly the spawn point's fixed rotation (legacy behavior). Matters a lot for a stationary-nav curriculum stage (Section 5, 2026-09-01): with movement disabled, a fixed spawn rotation gives the turret the exact same relative angle to the opponent every single episode forever - it can learn to memorize one absolute world direction and get perfect aimAccuracyWeight reward without ever actually tracking anything, which would not transfer once movement is reintroduced. The turret always resets to local rotation zero relative to the hull each episode (see ResetForNewEpisode), so randomizing hull yaw here randomizes the turret's starting facing too.")]
    [SerializeField] private float spawnYawJitterDegrees = 15f;

    [Tooltip("Randomizes each tank's starting position within this radius (world XZ plane) around tankSpawnPoint/opponentSpawnPoint, re-rolled every episode. 0 = always spawn at the exact fixed point (legacy behavior). Same overfitting risk as spawnYawJitterDegrees applies to position, not just facing.")]
    [SerializeField] private float spawnPositionJitterRadius = 0f;

    private float episodeStartTime;
    private bool episodeEnding;

    private void OnEnable()
    {
        if (tankMan != null) tankMan.OnDied += HandleTankDied;
        if (opponent != null) opponent.OnDied += HandleOpponentDied;
        StartNewEpisode();
    }

    private void OnDisable()
    {
        if (tankMan != null) tankMan.OnDied -= HandleTankDied;
        if (opponent != null) opponent.OnDied -= HandleOpponentDied;
    }

    private void Update()
    {
        if (episodeEnding)
            return;
        if (Time.time - episodeStartTime >= maxEpisodeSeconds)
            StartCoroutine(EndEpisodeRoutine("timeout", null));
    }

    private void HandleTankDied() => StartCoroutine(EndEpisodeRoutine("tank died", tankMan));
    private void HandleOpponentDied() => StartCoroutine(EndEpisodeRoutine("opponent died", opponent));

    private IEnumerator EndEpisodeRoutine(string reason, TankMan diedTank)
    {
        if (episodeEnding)
            yield break;
        episodeEnding = true;

        // Everything below is wrapped in try/finally (not try/catch - C# doesn't allow yield
        // inside a try block that has a catch clause, only try/finally) so that ANY exception
        // anywhere in this sequence - including inside navAgent/turretAgent.EndEpisode(), which
        // forces a fresh CollectObservations()+sensor update internally and would surface a bug
        // in a newly-added sensor - still guarantees StartNewEpisode() runs and episodeEnding gets
        // reset. Without this, one exception here permanently bricks all future episode-ending:
        // the game keeps running fine (nothing else depends on this script), but no episode is
        // ever reported complete again, which is exactly the "stuck at 'no episode completed'
        // forever, even well past the timeout" failure mode this fixes.
        try
        {
            Debug.Log($"[TrainingEpisodeManager] Episode ended after {Time.time - episodeStartTime:F2}s ({reason})"); // TEMP DEBUG

            // Tell ml-agents the episode is over BEFORE resetting the arena, so the terminal
            // observation/reward it sends reflects the actual death/timeout state, not the fresh
            // reset. Per-branch death/kill rewards (Section 5) are already applied by
            // NavPolicyAgent/TurretPolicyAgent's own OnDied subscriptions, which fire
            // synchronously off the same TankMan.OnDied event before this handler runs.
            //
            // Gated on isActiveAndEnabled, not just != null (Section 2.4 curriculum training,
            // 2026-09-01): a PolicyAgent left permanently disabled so its branch stays BT-driven
            // is still a non-null reference, but its Agent.OnEnable() (which allocates m_Info via
            // LazyInitialize) never ran - Agent.EndEpisode() has no null-guard on m_Info and throws
            // a NullReferenceException immediately, which - because it's mid-try-block - would
            // abort everything below it this episode (both tanks' SetDetectable/ResetForNewEpisode)
            // without ever reaching the finally's StartNewEpisode() rescue for THIS iteration's
            // arena reset (episode-ending itself still recovers next time, but every reset this
            // pass would be silently skipped).
            if (navAgent != null && navAgent.isActiveAndEnabled) navAgent.EndEpisode();
            if (turretAgent != null && turretAgent.isActiveAndEnabled) turretAgent.EndEpisode();

            if (diedTank != null)
            {
                diedTank.SetDetectable(false);
                yield return new WaitForSeconds(respawnDelay);
            }

            if (tankMan != null)
            {
                Vector3 pos = tankSpawnPoint != null ? JitteredSpawnPosition(tankSpawnPoint.position) : tankMan.transform.position;
                Quaternion rot = tankSpawnPoint != null ? JitteredSpawnRotation(tankSpawnPoint.rotation) : tankMan.transform.rotation;
                // SetDetectable(true) (un-freezing the Rigidbody) must happen BEFORE
                // ResetForNewEpisode sets the position - flipping isKinematic false right after a
                // position write left while still kinematic can resume dynamic simulation from a
                // stale physics-internal position instead of the fresh transform.position.
                tankMan.SetDetectable(true);
                tankMan.ResetForNewEpisode(pos, rot);
            }
            if (opponent != null)
            {
                Vector3 pos = opponentSpawnPoint != null ? JitteredSpawnPosition(opponentSpawnPoint.position) : opponent.transform.position;
                Quaternion rot = opponentSpawnPoint != null ? JitteredSpawnRotation(opponentSpawnPoint.rotation) : opponent.transform.rotation;
                opponent.SetDetectable(true);
                opponent.ResetForNewEpisode(pos, rot);
            }
        }
        finally
        {
            StartNewEpisode();
        }
    }

    private void StartNewEpisode()
    {
        episodeStartTime = Time.time;
        episodeEnding = false;
    }

    // Same yaw-jitter behavior as ArenaManager.JitteredSpawnRotation (kept as a separate copy
    // rather than a shared call - this class resets an existing tank in place every episode via
    // ResetForNewEpisode, it never goes through ArenaManager.Instantiate at all).
    private Quaternion JitteredSpawnRotation(Quaternion baseRotation)
    {
        if (spawnYawJitterDegrees <= 0f)
            return baseRotation;
        float yawOffset = Random.Range(-spawnYawJitterDegrees, spawnYawJitterDegrees);
        return baseRotation * Quaternion.Euler(0f, yawOffset, 0f);
    }

    private Vector3 JitteredSpawnPosition(Vector3 basePosition)
    {
        if (spawnPositionJitterRadius <= 0f)
            return basePosition;
        Vector2 offset = Random.insideUnitCircle * spawnPositionJitterRadius;
        return basePosition + new Vector3(offset.x, 0f, offset.y);
    }
}
