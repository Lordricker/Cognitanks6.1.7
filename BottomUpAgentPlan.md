# Bottom-Up (RL) Agent Plan — Cognitanks

Status: Phase 0 complete, Phase 1 spike actively running (solo/1v1, `NavPolicyAgent`/`TurretPolicyAgent`, see Section 13)
Scope: extends the existing behavior-tree AI (`Assets/AiEditor`) with a learned, "bottom-up" agent type that plugs into the tree as a custom node

> **Revision note (2026-08-06):** this draft has been checked against the actual codebase. Claims that were wrong are corrected inline and called out in Section 13. File:line references throughout are verified.
>
> **Revision note (2026-08-31):** Phase 0 actuator/observation work and Phase 1's hand-wired agents, reward config, and training episode manager are built and running (`Assets/AiEditor/AIScripts/ML/`, `MLTraining/`). Risk 7 (ML-Agents/Unity compatibility) confirmed clean in practice, not just on paper. Added Section 10c (pre-trained starter models / shop distribution) and a miss-distance refinement note to Section 5, both from ideas surfaced during the spike session. Section 11's Risk 1 (training-time spike) is in progress, not yet answered — first genuinely clean multi-hour run underway.

---

## 1. Vision

The game currently ships one AI paradigm: behavior trees authored in the node editor (`Assets/AiEditor`), executed by `TankMan.cs`. That system is interpretable by design — a player (or Ben) can point at a node and say why the tank did what it did.

The bottom-up system adds a second paradigm alongside it, not instead of it: reinforcement-learned policies that control a tank directly through continuous, raw actuator inputs, trained by reward rather than authored by hand. The two are deliberately asymmetric:

- Behavior trees: debuggable, hand-authored, predictable. Good for teaching the game, good for the "I saw the debug overlay" crowd.
- RL policies: emergent, trained, not meaningfully explainable action-by-action. Good for the "I was too busy cheering for my bot to care what it was doing" crowd — a real signal from playtesting, not a hypothetical.

A behavior tree can hand off to an RL policy for an entire engagement (not micromanage it node-by-node), and can hard-override it for things outside the policy's authority (e.g., forced retreat to a repair zone). This is the standard hierarchical/options-RL pattern: BT handles macro-strategy, RL owns a bounded sub-problem it was specifically trained for.

Long-term product goal: a player plays the BT version of the game for a while, then gets prompted with something like "train an ML version of this tank for more emergent behavior." Training is a player-facing feature, not a developer-only tool — Ben does not want to be the one hand-training every agent.

---

## 2. Architecture Overview

### 2.1 What already exists

`AiTreeAsset` / `AiNodeData` / `AiConnectionData` define the BT graph in the node editor. `AiExecutableNode` is the compiled, executable form. `TankMan.cs` walks the tree via `ExecuteNode`, dispatching to `ExecuteCondition` / `ExecuteAction` by `methodName`.

**Critical structural fact the earlier draft missed: every tank runs TWO trees concurrently, not one.**

`AiTreeAsset` carries `AiBranchType { None, Turret, Nav }` (`AiTreeAsset.cs:6`). `TankMan` loads `runtimeNavAI` and `runtimeTurretAI` separately from `tankSlotData.navAIInstanceId` / `turretAIInstanceId` (`TankMan.cs:307-313`) and runs them as two independent coroutines with independent cursors — `currentNavNode` (`TankMan.cs:1473-1499`) and `currentTurretNode` (`TankMan.cs:1508-1534`). `ExecuteNode` disambiguates via `bool isNavAI = (tree == runtimeNavAI)` (`TankMan.cs:1548`) and keeps parallel per-branch action coroutines (`TankMan.cs:2595-2597`).

The two trees are also **separate purchasable shop components**: `AiTreeAsset : ComponentData` with `cost = 100`, `weight = 1` defaults (`AiTreeAsset.cs:56-57`), and `navAIWeight` + `turretAIWeight` both feed `totalWeight` in `CalculateStats()` (`TankMan.cs:756`).

### 2.2 The MLPolicy node — branch-scoped

**Decision: an MLPolicy node is scoped to the branch it lives in.** This is the resolution of the biggest open question in the earlier draft, which specified a four-actuator action space (throttle, turn, turret, fire) without noticing that those actuators are owned by two trees that run at the same time. A single node reaching across both branches would have had to suspend its sibling tree, which needs an arbitration/mutex design and makes the shop economy ambiguous (which slot did the player buy?).

Branch-scoped instead means:

| Branch | Action space | Owns |
|---|---|---|
| **Nav MLPolicy** | throttle (-1..1), turn (-1..1) | chassis movement |
| **Turret MLPolicy** | turret yaw rate (-1..1), turret pitch rate (-1..1), fire (discrete 0/1) | aiming and shooting |

Consequences, stated plainly so they aren't rediscovered later:

- **Composes for free** with the existing architecture, economy, and serialization. An MLPolicy node is just another node in one tree; the other tree keeps running whatever it was running. No mutex, no suspend/resume.
- **A player can mix paradigms** — learned movement under a hand-authored turret tree, or vice versa. That is a genuinely nice product surface and worth exposing deliberately.
- **You give up joint move+aim coordination.** Circle-strafing while tracking, or backing off while holding aim, are behaviors that live in the coupling between the two. A nav policy can only learn them insofar as the turret tree's behavior is *stable and observable* — which means the turret's current aim state must be in the nav policy's observation vector (Section 3), and the two branches are effectively co-adapting during training. This is the main thing to watch for in the Phase 1 spike.
- **Credit assignment gets harder for the nav branch.** "Damage dealt" is the natural reward signal but it is produced by the turret branch. A nav policy rewarded on damage dealt is learning through a noisy intermediary. Positional reward terms (distance band, angle-to-cone) are the better primary signal for nav policies; damage terms should be secondary. Section 5 tags terms by branch accordingly.

When tree execution reaches an `MLPolicy` node, control of that branch's actuators passes to the trained policy (ONNX inference at runtime) until the node's **release condition** fires, at which point the BT resumes from the node's exit port. See Section 2.4 — release conditions and training episode-end conditions are related but not the same thing, and the earlier draft conflated them.

### 2.3 Serialization — two enums must change together

`AiNodeType` (`AiTreeAsset.cs:103-109`) has a mirror, `AiNodeTypeJson` (`Assets/Scripts/AiTreeAssetJson.cs:208-214`), and conversion is a **raw numeric cast**: `jsonExecNode.nodeType = (AiNodeTypeJson)execNode.nodeType` (`AiTreeAssetJson.cs:116`). Adding `MLPolicy` to one and not the other, or adding it in a different ordinal position, silently corrupts every serialized tree rather than failing loudly. Append `MLPolicy` last in both enums, in the same call.

### 2.4 MLGoalNodeAsset

A new node-editor asset type, parallel to `AiTreeAsset`, authored in a companion editor screen. It defines:

- `branchType` (Nav or Turret) — determines the action space, and must match the tree it is dropped into
- which observations the policy sees
- which reward terms are active and how they're weighted
- **release condition** (play time): when the policy hands the branch back to the BT — e.g. target lost, HP below threshold, timeout, explicit BT override
- **episode-end condition** (training only): when the trainer resets — death, kill, timeout, fall-out
- compatible turret-type set (Section 6)
- recorded training-loadout range (Section 6)
- after training: reference to the trained artifact (Section 7)

Note that reward weights are **inert at play time**. They exist only to shape the trainer. The node editor UI should make that obvious, or players will edit rewards on a trained node and expect behavior to change without retraining.

`MLGoalNodeAsset` also needs a `cost` and `weight` in the shop economy, since it lives inside an `AiTreeAsset` that already has both. Open question in Section 12.

**Runtime mechanism built 2026-09-03**, ahead of the full authoring UI, to prove it out: `MLPolicy` appended last to both `AiNodeType`/`AiNodeTypeJson` per Section 2.3 exactly as specced, `TankMan.ExecuteNode()` has a real case for it (enables the branch's `NavPolicyAgent`/`TurretPolicyAgent`, which claims control via the existing BT/ML mutex), and `NavPolicyAgent`/`TurretPolicyAgent` gained an opt-in `releaseControlOnLostVision` (off by default - only meaningful for a real BT-driven tank, not the training arena) that self-monitors vision each tick and, after a short grace period (debounces flicker on momentary occlusion), calls `EpisodeInterrupted()` (truncation, not a terminal state - matches this section's own release-vs-episode-end distinction) and disables itself, handing control back. Authored today via a plain node label ("MLPolicy") recognized by `AiMethodConverter.DetermineNodeType` - no dedicated palette prefab yet, that's still the companion-editor-screen work below.

**Authoring workflow, 2026-09-03 (SubAI-list unification):** rather than a separate insertion flow, `MLPolicy` nodes should share the *same* picker list SubAI nodes already use - one list of reusable, pre-built things to drop into a tree, BT sub-trees and ML nodes both, color-coded to tell them apart (BT blue, ML purple). A pinned **"New ML"** entry at the top of that list creates a brand-new, untrained `MLGoalNodeAsset` on the spot - genuinely fast, not a background job, since a freshly-initialized (random-weight) PPO network needs no training steps to exist, just instantiation and an immediate checkpoint+ONNX export. That's the literal "drunken sailor" starting point, created as casually as a blank BT tree.

Downstream of that: the exported **ONNX is what a real match always uses** (Section 9 - pure inference, no trainer). Inside the **training scene specifically**, expose a toggle between the ONNX (watch the current best result, no active training, no trainer-connection overhead/risk) and the **checkpoint** (the live, actively-training version, wired to the bundled trainer). This is the same `Behavior Type` distinction (`Inference Only` vs. trainer-connected `Default`) already used by hand throughout this session's spike, surfaced as a player-facing toggle instead of an Inspector field.

**Pausing an active training session** (same session, raised alongside the above): the direct mechanism is freezing simulation time (`Time.timeScale = 0` / Editor pause) while keeping the Python trainer process alive and connected, not stopping it. Not yet verified: ml-agents' Unity<->Python communicator has a `--timeout-wait` flag (visible in the frozen exe's own `--help` output, Section 11 Risk 2), implying a real timeout on how long Python will wait without hearing from Unity - a short pause is almost certainly fine, but whether an indefinite pause (player walks away for an hour) survives needs an actual empirical check before this is treated as solved, not assumed.

---

## 3. Observation Design

Two observation channels, matching the "raw sensory data, not BT abstractions" decision made earlier — the policy should not be fed `IfHP`/`IfEnemy`-style booleans, those are BT-level abstractions that would just constrain it back toward BT-like behavior.

**Self / proprioceptive (fixed-size vector):** HP%, ammo%, reload cooldown, local linear velocity, local angular velocity, turret local yaw and pitch, ground normal beneath the tank (relevant now that the training arena has slopes), and the tank's own component stats — vision range, vision cone angle, engine power, turn rate, damage, weapon range, bullet speed. Feeding component stats as inputs (not baking them into fixed network weights) is what lets one trained policy adapt its behavior to whatever loadout is equipped, discussed further in Section 6.

**Hidden actuator state — required for the MDP to be Markov.** `TankMan.ApplyMovement()` is not a stateless map from input to force. The same action produces different results depending on state the policy cannot currently see:

1. **Turn ramp-up.** `currentTurningPower` lerps toward `turningPower * powerPercent`, where `powerPercent` interpolates from `turnStartPowerPercent` to 1.0 over `turnRampUpTime` since turn input began (`TankMan.cs:405-424`). A turn command issued after two seconds of turning is several times stronger than the same command issued cold. **Expose ramp progress (0..1) and normalized `currentTurningPower` as observations.**
2. **Airborne cutoff.** When `!isGrounded`, all movement forces are skipped and inputs are zeroed (`TankMan.cs:388-396`). **Expose `isGrounded`.** Without it, the policy sees its actions randomly stop working mid-air.
3. **Coms speed penalty.** `MoveSpeed` and `TurnSpeed` are multiplied by `COMS_SPEED_PENALTY` while `isCurrentlyUsingComs` (`TankMan.cs:159`). **Expose `isCurrentlyUsingComs`**, or the top-speed clamp moves under the policy's feet.

Skipping these is the classic way to get a policy that trains to mediocrity and plateaus for reasons nobody can diagnose.

**Cross-branch observation.** Because MLPolicy is branch-scoped (Section 2.2), each branch must observe the other's state or it is acting in a partially-observable environment of its own making:

- Nav policy observes: turret local yaw/pitch, whether the turret branch currently has a target, time since last shot.
- Turret policy observes: chassis linear/angular velocity, heading (it already does via self-observations).

**Decision cadence.** The BT ticks on `aiUpdateInterval = 0.1f` (`TankMan.cs:20`), extended by `COMS_ALLY_DELAY_PER_TANK * aliveAllyCount` when Coms is in use (`TankMan.cs:2095`). A policy should **not** inherit that cadence — it's variable and it's a game-design knob, not a control-loop knob. Use ML-Agents' `DecisionRequester` on a fixed multiple of `FixedUpdate` (start at every 5 physics steps ≈ 0.1s at default 50Hz, tune in the spike) with action repeat in between. Record the decision period in `MLGoalNodeAsset` — a policy trained at one cadence will not transfer to another.

**Perception (variable-length):** raycast fan via ML-Agents' `RayPerceptionSensor3D`, tagged by hit type (enemy / wall / ramp / ally) — a policy-friendly analog of the `RaycastAll`/`OverlapSphere` vision `TankMan` already does. For the list of currently-visible allies and enemies (position, HP%, armor value, turret type one-hot, engine type one-hot, distance), a fixed-size padded vector is the wrong tool — number of visible tanks varies. Use ML-Agents' `BufferSensorComponent`, built specifically for variable-length entity lists (it runs them through attention internally), instead of hand-rolled max-N slot padding.

**Team intel dependency (Coms extension, prerequisite work):** `AllyTargetList.cs` already implements team-shared *enemy* sighting — `TargetEntry` (`AllyTargetList.cs:39-64`) carries `worldPosition`, `turretType`, an armor bucket string, `maxHP`/`currentHP`, reporter, `lastUpdateTime`, populated whenever `TankMan.ReportTarget` fires on an `IfEnemy` detection. Three gaps need closing before the observation design above is fully fed:

1. `TargetEntry` has no engine-frame type and no explicit distance field (distance is derivable from `worldPosition` but isn't stored).
2. Ally status is not currently broadcast at all — `ReportTarget` is only ever called for enemy sightings. Sharing a teammate's own HP/loadout/position when spotted (by an ally, not just relevant when spotted by an enemy) is new work: same team-keyed-dictionary pattern as the existing enemy list, applied to allies. This is required for any "Guardian" or ally-aware reward term (Section 5).
3. `lastUpdateTime` matters more to a policy than to a BT — a stale entry and a fresh one look identical in the vector. **Feed staleness (`Time.time - lastUpdateTime`) as an explicit per-entity feature**, not just as a filter threshold.

Also worth noting for the observation feed specifically: armor is currently stored as a derived string bucket (`"Light"/"Medium"/"Heavy"/"Ultra-Heavy"`) via `GetArmorTypeDescription` (`AllyTargetList.cs:76-86`). That's fine for a BT condition or a debug readout; a network input should get the raw armor float, not the bucket. `TargetEntry` should carry both.

---

## 4. Action Space & Actuator Gaps

Continuous, matching the "emergent behavior, not BT-mimicking" decision. Per Section 2.2, split by branch:

**Nav branch:** throttle (-1..1), turn (-1..1)
**Turret branch:** yaw rate (-1..1), pitch rate (-1..1) continuous; fire (0/1) discrete branch

ML-Agents supports mixed continuous + discrete branches in a single action spec, so fire doesn't need to be forced into a continuous value.

### 4.1 Actuator readiness — corrected

The earlier draft named `SimpleTankController.ApplyForward` / `ApplyTurn` as the "clean continuous 2-DOF drive interface that already exists and needs no rework." **That is wrong on three counts:**

1. **`SimpleTankController` is dead code.** Its GUID (`9394e07cf03896d499e815c0cea5c78c`) appears in no scene or prefab in the project. It is a standalone waypoint-patrol demo that drives itself from `FixedUpdate` → `NavigateToWaypoint()`.
2. **Both methods are private** (no access modifier, `SimpleTankController.cs:127,140`). Nothing outside the class can call them.
3. **`ApplyForward` does `Mathf.Clamp01(intensity)`** (`SimpleTankController.cs:129`) — it cannot reverse. A -1..1 throttle action would be silently truncated to 0..1, and the policy would spend training discovering that half its action range does nothing.

**The real drive interface is `TankMan.SetMovementInput(float moveInput, float turnInput)`** (`TankMan.cs:483-487`), which is public, clamps to -1..1 on both axes, and feeds `ApplyMovement()` in `FixedUpdate`. The nav action space maps to it directly. This part genuinely needs no new actuator — but it's a different method on a different class than the draft claimed, and the hidden state in `ApplyMovement` (Section 3) is a real constraint.

### 4.2 Blocking actuator gaps

**`RotateTurret(float yawIntensity, float pitchIntensity)` does not exist.** Turret rotation today only happens inside the target-tracking state machine (`TrackTargetAction` / `LeadTargetAction`, `TankMan.cs:2692-2709`, with `turretRotationStartTime` ramp logic). There is no raw rate-control method. Note the turret has **two** aiming DOF, not one: `LateUpdate` explicitly preserves AI-controlled local Y (yaw) and X (pitch) and clamps pitch to `minPitchAngle`/`maxPitchAngle` (`TankMan.cs:688-702`). The earlier draft's single "turret rotate" action would have left pitch permanently un-actuated.

**`Fire()` and `CanFire()` are private** (`TankMan.cs:2836`, `TankMan.cs:2983`). Only `TestFire()` is public (`TankMan.cs:3209`) and it is a debug entry point, not a policy actuator. A public, agent-safe fire path (respecting reload/`CanFire` gating rather than bypassing it) is a second blocking prerequisite the earlier draft missed entirely.

**Artillery aiming is a solved-angle problem, not a free-aim problem.** `Fire()` branches on turret type: `DirectFire` sets `bulletRb.useGravity = false` and fires straight (`TankMan.cs:3046-3047`); `Artillery` sets `useGravity = true` and computes a launch angle from a ballistic solve (`TankMan.cs:3039-3041`, `3258-3271`). Decide explicitly:

- **Option A (recommended for Phase 1):** the policy controls yaw only for Artillery, and the existing ballistic solver supplies pitch. Smaller action space, and the hard part (projectile-motion math the network would otherwise have to learn from sparse hit reward) is already solved in code.
- **Option B:** the policy controls pitch too and learns ballistics from scratch. More emergent, dramatically slower to train, and directly worsens the #1 risk in Section 11.

Either way this must be recorded in `MLGoalNodeAsset`, because it changes the action-space shape and therefore the ONNX signature.

---

## 5. Reward Template Library

Reward terms are a palette with per-term weights, not five hardcoded presets — the "goal node" is really just which terms get nonzero weight. Terms are tagged by which branch they're a *sensible primary signal* for (per Section 2.2 on credit assignment); a term can still be enabled on the other branch as a weak secondary.

| Term | Primary branch | Notes |
|---|---|---|
| Damage dealt | Turret | Core combat signal, present in `TankMan`'s HP/damage handling. |
| Damage taken (penalty) | Nav | Positioning is what avoids damage. |
| Kill bonus / death penalty | Both | Sparse but important terminal signals. |
| Survival time | Nav | Biases toward caution. |
| Distance-to-target band | Nav | Reward for holding a range band relative to the tank's own weapon `range` stat; penalty below a threshold. Sniper-style specialization. |
| Angle relative to enemy vision cone | Nav | Reward for being outside the opponent's `visionCone` before engaging (same `visionRange`/`visionCone` stats already on `TurretDataJson`), penalty for lingering inside it unengaged. Flanking. |
| Aim error (angle to target) | Turret | Dense shaping signal — much faster to learn from than hit/miss alone. |
| Shot efficiency (hit / shots fired) | Turret | Discourages spraying. |
| Ally HP nearby | Both | Depends on the ally-status broadcast extension in Section 3. Support/cover behavior. |
| Heal delivered | Turret | Healer turrets only — see Section 6.2. |

Named presets are just weight combinations over this palette, e.g.:

| Preset | Branch | Dominant terms |
|---|---|---|
| Duelist | Turret | damage dealt, aim error, kill/death |
| Kiter | Nav | distance-band, damage taken, survival time |
| Survivor | Nav | HP preserved, survival time, heavy damage-taken penalty |
| Flanker | Nav | angle-to-enemy-cone, engagement bonus once flanked |
| Guardian | Nav | ally HP nearby, positioning near ally |
| Medic | Turret | heal delivered, ally HP nearby |

**Reward scale hygiene.** Terminal terms (kill/death) and dense per-step terms (aim error, distance band) are on wildly different scales. If the palette lets a player set both to "weight 1.0," the dense terms will dominate the terminal ones by two or three orders of magnitude over an episode. Normalize per-step terms by expected episode length internally, and expose the UI weight as a relative slider, not a raw coefficient. Confirmed the hard way during the Phase 1 spike (2026-08-31 session): the naive per-decision-cadence math is wrong by exactly the `DecisionRequester.TakeActionsBetweenDecisions` factor — `OnActionReceived` fires every physics step by default, not just every `DecisionPeriod` steps, so per-tick terms accumulate 5x faster than a "per decision" mental model suggests. Size dense weights against the actual physics tick rate, not the decision rate.

**Miss-distance refinement to Aim error (deferred, needs Section 3 Perception).** The current `aimAccuracyWeight` term (Turret) is privileged and purely geometric — it rewards the turret being pointed near the opponent's current position, computed directly from a C# reference the network never sees. A sharper version: report back how far each fired bullet's impact point (or closest approach, on a miss) landed from the target, via a new event on `BulletScript`'s existing `firingTank` hookup. This captures things pure angle can't — lead/timing quality against a moving target, actual landing accuracy for Artillery's ballistic arc — that angle-to-target at the moment of firing doesn't. Deliberately not built yet: without the network actually perceiving the target (Section 3's deferred `RayPerceptionSensorComponent3D`/`BufferSensorComponent` work), a miss-distance reward is still usable but not learnable in a generalizable way — the policy can optimize against a number it's given without ever connecting "target was there, I aimed this way, missed by X" into transferable aiming skill. Build this once real perception lands, not before.

---

## 5b. `MLGoalNodeAsset` — Full Configuration Surface (Phase 2)

Emerged from the Phase 1 spike session (2026-09-02), once the manual `TankRewardConfig` editing this spike has been doing by hand (edit the ScriptableObject, restart training, repeat) had a concrete enough shape to generalize. This is what `MLGoalNodeAsset` actually needs to contain — the "hand-fixed... not yet configurable through `MLGoalNodeAsset`" placeholder referenced throughout Sections 2-5 becomes this.

**The panel is one config surface, two audiences.** The full raw lever set below is not dev-only and not player-locked — it's the same panel for both, gated by a confirmation, not an access tier. Opening the weight-tuning section requires clicking through an explicit "only tune weights if you know what you are doing" acknowledgement, and a **Reset to Defaults** button is always present as the undo. This is Section 10c's "sandbox, not safety rail" philosophy applied specifically to this panel: a player who understands what they're doing should be able to make their tank flop, get worse, or go full drunken-sailor on purpose, same as Ben can right now by hand-editing `TankRewardConfig.asset`. The gate is a speed bump for people who wandered in without reading anything, not a wall keeping capability away from players who want it.

**Dev Presets.** Named, saved configurations built with this same panel — the actual recipe used to train a shipped shop model (Section 10c), not a separate, simpler tool. Gives players a documented "here's what produced this" starting point rather than a black box, and gives Ben a reusable library instead of hand-editing a `.asset` file per experiment (i.e., formalizing exactly what this whole spike session has been doing manually).

**Lever list**, broken out by what's already implemented (Phase 1 spike, `TankRewardConfig`/`NavPolicyAgent`/`TurretPolicyAgent`) vs. genuinely new work this surface requires:

*Episode/session:*
- Episode time limit — exists (`TrainingEpisodeManager.maxEpisodeSeconds`).

*Distance-band weights (Nav) — partially new:*
- In-band reward — exists (`distanceBandWeight`, now expressed as a ratio of the tank's own live `Range`, not a fixed world-unit band — Section 5's original table entry undersold this as a single band+penalty; the shipped version is reward-only with no penalty terms at all yet).
- Too-close penalty — **new.** Nothing currently discourages closing distance past the band's inner edge specifically; today the only related pressure is `damageTakenWeight`, which penalizes actually getting hit, not proximity itself.
- Too-far penalty — **new**, symmetric case on the outer edge.

*Sparse event weights — mostly exists, one important distinction:*
- Killed enemy — exists (`killBonus`).
- Got hit — exists (`damageTakenWeight`, scaled by damage amount).
- Enemy spotted / ally spotted as their own reward — **new**, and deliberately **not recommended** without a specific reason to override the default of zero: Section 5 (Nav vision-gating discussion, 2026-09-01) already flagged that a flat "you can currently see the enemy" reward is a camping exploit waiting to happen, since `VisionRange` and weapon `Range` aren't consistently ordered across turret types (confirmed: Rifle Range 100 < VisionRange 200, Sniper Range 700 > VisionRange 500) — a tank could learn to sit at max vision range holding a lock without ever closing to engagement range. Expose the lever since this is a sandbox, but the panel should default it to 0 and probably carry its own inline warning.
- **Bullet actually hit enemy — new, and explicitly a separate sparse term from the two dense aim terms below.** Ties to `BulletScript`'s existing `RecordDamageDealt`/`firingTank` hookup (`TankMan.NotifyDamageDealt`) — a discrete "a fired shot connected" event, distinct from `aimHitWeight`'s dense per-tick "is the raycast touching the opponent right now" signal. The two aren't redundant: raycast-touching can be true for many ticks without a shot ever being fired during them (reload cooldown), and a fired shot can land without the raycast having been continuously on-target the instant before (bullet travel time, `turretBulletSpeed`, deflection). Confirmed 2026-09-02: this stays a distinct term, not folded into `aimHitWeight`.
- Fired bullet — **exists, but currently hardcoded to "no term" rather than a real lever.** `firePenalty` was deliberately removed entirely from `TankRewardConfig` (Section 5 revision, 2026-09-01) rather than left as a weight defaulted to 0, because at the time this was reward-authoring by hand-editing C# files per change. Under this panel, it should never have been a deletion — it should be a lever defaulting to 0, so "should firing cost anything" is a dial a preset can turn, not a code change.

*Dense event weights — mostly exists, one gap:*
- Aim accuracy (graded angle-to-target) — exists (`aimAccuracyWeight`).
- Aim raycast touching enemy — exists (`aimHitWeight`).
- Forward movement — exists (`movementWeight`).
- No-movement penalty — **new**, and distinct from `movementWeight` despite looking like its inverse: `movementWeight` rewards actual forward speed and simply pays nothing at zero speed; a true no-movement penalty would apply a cost specifically for near-zero speed, a materially stronger anti-idling pressure. Exposing both as separate levers (rather than trying to derive one from the other) lets a preset tune "encourage motion" and "punish stillness" independently.

*Observations toggles — mostly new, all subject to the same non-negotiable constraint:*
- Current HP, own vision range, own weapon range — exist (already in one or both branches' `CollectObservations`).
- Bullet speed — **new**, not currently observed by either branch.
- Enemy distance — exists, but **only** via `TankVisionCheck`'s vision-gated `normalizedDistance` (zeroed when not currently visible) — there is no toggle path to an unconditional version in the shipped product surface.
- Enemy HP, enemy turret type — **new.**

**The constraint that applies to every item in that last group, confirmed 2026-09-02: gated to the turret's real, current vision detection (`TankVisionCheck`/`UpdateSensorData`'s cone+range+line-of-sight check), full stop.** Toggling "enemy turret type" on means "reveal it when currently visible, zero otherwise" — never "always know it regardless of sight." This is the same non-negotiable line Section 3 drew at the very start of this spike (rejecting a raw-position observation as illegitimate specifically because it bypasses the tank's actual vision constraints) and it applies to every new observation toggle this panel adds, not just the ones that existed when that decision was made.

**One narrow, explicitly-labeled exception:** an always-omniscient mode is legitimately useful as a **dev-only diagnostic**, never a shippable Dev Preset — training an upper-bound baseline with full ground truth to measure how much headroom legitimate vision is leaving on the table. If built, this needs to be structurally distinct from the normal toggle set (a separate, clearly-labeled mode) so it can't end up in a preset that reaches a player by accident.

**Live-adjustment apply flow, 2026-09-02: every lever applies the same way — save checkpoint, restart trainer, `--initialize-from` whatever still fits.** Earlier drafting of this idea split levers into two tiers (reward/scenario changes hot-applied at the next episode boundary; observation-shape changes requiring a heavier "starts training over" flow, since adding/removing an observation field changes the size of the vector the network takes as input and the in-progress checkpoint can't accept a differently-shaped input). That split is unnecessary — a uniform restart-based flow handles both cases correctly with no per-lever-type branching, and it's the same mechanism already proven working in this session's own curriculum transition (`ML-01 → ML-10`, Section 13): ml-agents' per-behavior `init_path` naturally degrades per behavior, not globally — if an observation toggle changes `TurretPolicy`'s shape but not `NavPolicy`'s, the restart just omits `init_path` for `TurretPolicy` (starts that one fresh) while `NavPolicy` still loads its prior weights unaffected. No code needs to know or care which category of lever triggered the restart.

Flow: player adjusts levers → clicks Apply → **pending-change message shown** ("applying at the end of this episode," not instant) → current episode finishes naturally (avoids a mid-trajectory reward-function change, and gives a clean point to save from) → checkpoint saved → trainer process restarts with the new config, `--initialize-from` per behavior wherever the shape still matches → **change-applied confirmation shown** once the new trainer reconnects and the next episode begins. The two message beats matter independently of each other - a player watching a short/fast-forwarded episode needs to see the pending state so "why didn't my slider do anything" doesn't read as broken, and needs to see the applied confirmation since a restart is a visible enough event (however brief) that silently resuming would look like nothing happened.

One open technical question, not yet verified either way: whether the Unity side can reconnect to a freshly-restarted Python trainer within the same running Play/game session, or whether ml-agents' Academy/Communicator handshake only ever pairs once at startup and needs at least a scene reload to re-pair with a new trainer process. Determines whether this flow is genuinely seamless (background process swap, player barely notices) or has a visible loading beat - build the applied-confirmation UI to cover either case rather than assuming the seamless one.

**Scope note:** this is Phase 2 design, captured now while the shape is fresh rather than because it blocks Phase 1. The current spike keeps using hand-edited `TankRewardConfig` for its own curriculum-training iteration (Section 13) — none of the above is a prerequisite for finishing that validation.

---

## 6. Loadout Specialization vs. Generalization

### 6.1 Continuous stats — the dial

A player may deliberately train a node while a specific loadout is equipped (e.g., a sniper turret) to specialize its behavior — that's a legitimate and expected use, not a bug to design around. Because the tank's own stats (vision cone, range, turn rate, etc.) are part of the observation vector (Section 3), a node trained on a sniper loadout still *runs* on a rifle loadout — it just performs in-distribution on stats it saw during training and extrapolates (unreliably, as neural nets do) outside that range.

This is a dial, not a defect: train narrow (fixed loadout, or tight randomization around it) for a sharp specialist; train wide (randomize across the full component range every episode) for a generalist that's decent everywhere but not exceptional anywhere. Each `MLGoalNodeAsset` should record the loadout range it was actually trained across, and the node editor should warn when the currently-equipped tank's stats fall outside that range — same spirit as the BT debug overlay, but "this policy is out of its comfort zone" instead of "this action just fired."

### 6.2 Turret type is categorical — the argument above does not cover it

`TurretType` is `{ DirectFire, Artillery, Hammer, Healer }` (`TurretData.cs:3-9`). These are not points on a continuous stat axis, and the "stats are in the observation, so it generalizes" argument does not apply across them:

- **Hammer** is melee AOE (`SwingHammer`, `TankMan.cs:3089+`). A distance-band reward tuned for weapon range is actively wrong; the optimal policy is "close to contact," the opposite of a sniper policy.
- **Healer** fires at *allies* to heal them. "Damage dealt" is not merely useless here — a policy trained on damage-dealt and run on a Healer will aim at enemies and accomplish nothing. The reward semantics invert.
- **Artillery** vs **DirectFire** differ in whether pitch is policy-controlled or solver-controlled (Section 4.2), which changes the action-space shape and therefore the ONNX signature.

**Therefore:** `MLGoalNodeAsset` declares a **compatible turret-type set**, and the node editor hard-blocks (not warns) assigning a policy to an incompatible turret type. Reward terms are filtered by turret type in the authoring UI. Across-type training is not a supported dial; within-type stat randomization is.

### 6.3 Catastrophic forgetting

Continuing training after a loadout switch (Section 7) is not purely additive — sequential retraining on a new loadout can degrade performance on the old one rather than smoothly generalizing. If the goal is one node that's good across multiple loadouts, loadouts need to be interleaved during training, not trained fully on one then switched to another. This directly constrains the UI copy for the "train it more" button (Risk 4, Section 11).

---

## 7. Node Asset Lifecycle & Persistence Model

Three distinct artifacts per `MLGoalNodeAsset`, not one "forever memory" blob:

1. **Config** — the editable part: branch type, observation toggles, reward-term weights, release/episode-end conditions, decision period, compatible turret types, recorded training-loadout range. Lives in the node asset, editable in the node editor like any other node.
2. **Training checkpoint** — ML-Agents' internal, resumable training state. This is what "train some more" actually resumes from.
3. **Exported ONNX** — a frozen, inference-only snapshot exported from a checkpoint. This is what actually runs in-game at play time. It is not itself resumable for further training.

**Config changes invalidate artifacts.** Editing an observation toggle, the action-space shape, or the decision period changes the network's input/output signature — the existing checkpoint and ONNX become unusable, not merely stale. The editor must detect signature-affecting edits and either block them on a trained node or explicitly offer "reset training." Reward-weight edits are the only config changes that are safely resumable. This was implicit in the earlier draft and is the most likely source of confusing runtime failures if left implicit.

Flow: build node → drop into the matching branch's BT canvas, wire entry/exit like any node → assign tree to a tank build → run training episodes in the training arena → checkpoint saves progress periodically → export ONNX (overwrites the previous one) → node plays using the new ONNX. Switching the loadout and training more resumes from the checkpoint (with the new loadout mixed into the episode distribution if generalization is the goal — see 6.3 on interleaving), then re-exports ONNX.

---

## 8. Training Arena & Episode Definition

Flat primitive geometry with basic ramps/slopes, not heightmap terrain — matches the existing scenes' approach but stripped down for training speed: cheap to reset between episodes, and ramp angle/placement/spawn points can be randomized per episode for free generalization instead of hand-authored variety. This is a separate, minimal scene from the game's real arenas (`Assets/Arenas/MiddleAges`, `StoneAge`, `Western` — all three confirmed present), built specifically for fast episode turnover.

**Episode definition** (absent from the earlier draft, and the trainer cannot run without it):

- **Terminal on:** agent death (`Die()`, `TankMan.cs:3396`), all enemies dead, or timeout (start at ~60s wall-clock equivalent, tune in the spike).
- **Fall-out is terminal, not a respawn.** `TankMan.Update()` currently calls `RespawnAtSpawnPoint()` when `transform.position.y < fallRespawnHeight` (`TankMan.cs:709-711`). Leaving that behavior on during training teleports the agent mid-episode with no reward signal attached, which is a silent, hard-to-diagnose corruption of the MDP. Disable respawn in training mode and end the episode with a penalty instead.
- **Reset must be total:** positions, velocities, HP, reload timers, turret angles, `AllyTargetList` contents (it's a `DontDestroyOnLoad` singleton, `AllyTargetList.cs:19-28` — it will carry stale entries across episodes if not explicitly cleared), and the turn-ramp state in `ApplyMovement`. Any of these leaking across episodes shows up as unexplained variance in training curves.

### 8.1 Opponent setup — reuse the multiplayer team builder

The earlier draft specified arena, rewards, and observations but never said what the agent trains *against*, which is the single largest determinant of whether the resulting policy is good or merely overfit to one scripted dummy.

**Approach:** duplicate the multiplayer matchmaking/team-setup flow for solo use. The player builds multiple teams, checkboxes which ones enter the training pool, and each episode draws an opponent team from the checked set at random.

Why this is the right shape here:

- It reuses UI and data structures that already exist rather than inventing a training-specific opponent config. `MultiplayerMatchData.TankConfigReference` already carries a complete tank spec — engine/armor/turret IDs, all derived stats, and both AI trees as JSON (`MultiplayerMatchData.cs:28-68`). `SimpleTeamManager.AssignTeamsFromBattleMode()` (`SimpleTeamManager.cs:21`) and `ArenaManager` (`ArenaManager.cs:147`) already handle team assignment and spawning from that data.
- Randomized selection across a checked pool gives opponent diversity for free, which is exactly what prevents overfitting to a single opponent.
- It puts opponent choice in the player's hands, which fits the "training is a player-facing feature" goal — a player who wants a counter to a specific meta build can train against it directly.

Implementation notes: add a `BattleMode` value for training; the training scene reads the checked team pool from the same PlayerPrefs/config path the multiplayer flow uses. Self-play (agent vs. snapshots of itself) is a possible Phase 5 addition but is deliberately out of scope for the spike — it multiplies training time, which is already Risk 1.

---

## 9. Headless Training Pipeline

Two separate processes, same Unity codebase:

**Unity side:** the existing project, built in `-batchmode -nographics` (no rendering, no window) pointed at the training arena scene instead of a game scene. Same `TankMan`, same physics. An `Agent` component wraps the tank and exposes the `MLGoalNodeAsset`'s observations/actions/reward to the outside world.

**Python side:** ML-Agents' trainer (`mlagents-learn`), a separate process running PPO — it has no knowledge of tanks, just receives observation/reward numbers and returns action numbers, over a local socket. Communication is step-by-step: Unity sends "here's what the agent sees + reward earned," Python sends back "here's what to do."

For fast dev iteration, the trainer can connect to a live Unity Editor Play-mode session instead of a headless build (slower, but no build step). Real throughput comes from running multiple environment instances in parallel against one trainer process, not from optimizing a single instance.

**Package prerequisite — not currently installed.** `Packages/manifest.json` contains no `com.unity.ml-agents` entry. The project is on Unity `6000.1.17f1` (Unity 6.1). ML-Agents' Unity-side inference historically ran on Barracuda, then Sentis (`com.unity.sentis`); Unity 6.1 ships the successor as Inference Engine (`com.unity.ai.inference`). Whether the current ML-Agents release resolves cleanly on 6000.1 — and which inference backend it pulls — is a **verification item, not an assumption** (Risk 7, Section 11). Confirm before Phase 1, because a version conflict here blocks everything downstream.

**Batchmode caveats to check in the spike:** `TankMan` and `SimpleTankController` both call `OnGUI`/`OnDrawGizmos` and emit `Debug.Log` on hot paths; audio, particles, tread-material scrolling (`TankMan.cs` `ApplyMovement`), and VFX all still tick in `-nographics` unless explicitly gated. Add a training-mode flag that short-circuits cosmetic systems, or throughput measured in Phase 1 will be misleadingly low.

The shipped, player-facing game build never runs any of this — it only ever loads the exported ONNX and does inference. No Python, no training loop, at play time.

---

## 10. Player-Facing Training Delivery

This is the hardest and least de-risked part of the plan, because the vision requires *players* to run the "run it a million times" step on their own machines, inside a downloaded game — not Ben training nodes on a dev machine and shipping static ONNX files.

**Default path — bundled local trainer.** Freeze `mlagents-learn` + PyTorch into a standalone executable (PyInstaller-style, no Python install required from the player) and ship it alongside the game, launched as a background subprocess when a player starts training. This reuses proven RL code instead of requiring a from-scratch trainer implementation. Costs: a few hundred MB to ~1-2GB added to the download, separate frozen builds needed per OS. Training speed on the player's own hardware is the open risk — see Section 11.

**GPU note, 2026-09-02, revising the assumption below:** the original framing here assumed a dedicated GPU meaningfully changes training time and treated CPU-only as the pessimistic case to validate against. Section 11 Risk 1's first real data point ran entirely on **CPU** (`torch==2.1.1+cpu` - not GPU-disabled by config, the CPU-only PyTorch wheel, incapable of CUDA regardless of hardware present) and still reached basic competence in ~3.3 hours. That's not a coincidence specific to this run - it's architectural: this project's networks are small vector-observation MLPs (2 layers, 128 hidden units, no camera/pixel input, `vis_encode_type: simple` doing nothing since there's no visual encoder in play), and Unity's own environment-stepping (physics, game logic per tick) is almost certainly the actual throughput bottleneck, not the PyTorch forward/backward pass. GPU acceleration earns its keep on large networks or large batches (CNN visual encoders, big transformer-style policies) - for a network this small, the CPU\<->GPU data-transfer overhead per tiny batch can make GPU training a wash or even *slower*, not faster. Concretely: **do not build a "GPU toggle" setting or a separate CUDA-enabled frozen trainer build without first A/B-measuring GPU vs. CPU on this exact network shape** - it's plausible extra packaging complexity (a second per-OS frozen build, driver-version compatibility surface) for a speedup that doesn't materialize. Revisit if a later phase adds genuinely GPU-hungry observations (camera-based vision, a large `BufferSensor` attention block over many entities) - that's the point where GPU would likely start to matter for real.

**Fallback path — Firebase-fronted cloud training.** The game already uses Firebase for asynchronous multiplayer matchmaking (`Assets/Multiplayer/FirebaseMatchService.cs`). Firebase itself (Firestore/Auth/Functions) is not a GPU compute host, but it's the natural orchestration layer in front of one — since Firebase sits on GCP, a training job can be queued/authenticated/tracked through Firebase while the actual training runs on a GCP Compute instance (or rented GPU service) behind it. This reuses infrastructure Ben already knows rather than introducing a new backend pattern. Important distinction from the existing multiplayer usage: GPU-hours cost real, ongoing money that scales with player usage, unlike the near-free Firestore reads/writes matchmaking uses — this needs to be budgeted as an operating cost, not folded into the existing cost model unexamined.

**Not recommended as a starting point:** a fully custom in-process C# trainer (no Python dependency at all) is the cleanest long-term shape for distribution — one executable, no subprocess — but means implementing an RL algorithm from scratch on top of everything else in this plan. Worth revisiting only if the bundled-Python approach proves untenable in practice.

---

## 10b. Multiplayer Distribution of Trained Policies

Not covered at all by the earlier draft, and it is arguably a larger unsolved problem than Section 10.

The game's async multiplayer sends a full tank spec to Firebase and runs the opponent's tank locally on the other player's machine. `TankConfigReference` carries `turretAIScript` and `navAIScript` as **full AI tree JSON strings** (`MultiplayerMatchData.cs:39-40`). A tank whose tree contains an `MLPolicy` node is not playable by an opponent unless the trained ONNX travels with it.

Three problems follow:

1. **Transport.** An ONNX is binary and megabytes-scale (larger with a `BufferSensor` attention block); the current path is inline JSON text in a Firestore document, which has a 1 MiB per-document limit and gets ~33% worse under base64. This needs Firebase **Storage** with a blob reference in the match document, not another JSON string field. Also a per-player storage quota, since every trained node is a separate artifact.
2. **Trust.** A downloaded ONNX from another player is untrusted content fed to an inference runtime. At minimum: validate the model's input/output signature against the `MLGoalNodeAsset` config before loading, cap tensor sizes, and reject anything that doesn't match. Do not load an arbitrary opponent-supplied graph unchecked.
3. **Determinism / replays.** The multiplayer flow stores replays and computes ELO (`MultiplayerEntryUI.cs:83-103`). If replays are re-simulated rather than recorded frame-by-frame, neural-net inference must be bit-reproducible across machines and GPU backends — which it generally is not. Confirm which model replay uses before shipping MLPolicy to multiplayer; if it re-simulates, MLPolicy nodes may need to be restricted to single-player until replay is changed to record outcomes.

**Recommendation:** ship MLPolicy single-player-only in Phase 4, and treat multiplayer support as its own phase with the three items above as its scope. Do not let it ride along implicitly.

---

## 10c. Sample Models & Shop Distribution

Emerged from the Phase 1 spike session (2026-08-31). Core design philosophy, stated plainly because it drives every technical decision below: **this is a sandbox, not a safety rail.** Ben trains sample models to *showcase what training can produce* — proof of the ceiling, not a deliberately-hobbled starting point — then hands full control to the player. The player can keep training a purchased model further, and it is explicitly an accepted, intended outcome that doing so might make it *worse* (Section 6.3's catastrophic forgetting, mismatched loadout, weak opponent pool, whatever). That is not a risk to be engineered around; it's the player owning the consequences of their own experimentation, same as tuning any other build choice in the game. Ben's job is to make the sample worth showing off, not to make it idiot-proof.

**Distribution flow:** a sample model is unlocked (e.g., after beating a level), purchased, and copied to the player's inventory as a custom node, droppable into a BT tree exactly like any other trained `MLPolicy` node.

**Technical consequence: every purchased model ships with both artifacts, not a choice between them.**

- The **ONNX** (Section 7's frozen, inference-only export) — so the model works immediately at play time with zero setup, same "Default + Model assigned, no trainer" path as any other shipped MLPolicy node (Section 9). This is the part that needs only runtime model loading (Inference Engine supports loading from raw bytes, not just the Editor's drag-in-`.onnx` import) and *not* the harder trust/validation problem Section 10b flags for multiplayer — this is first-party content Ben authored, not a peer-uploaded graph. Plausibly shippable before Section 10b's multiplayer trust problem is solved.
- The **training checkpoint** (Section 7's middle, resumable artifact — the one `--resume`/`--initialize-from` can load) — so "keep training" is always on the table, not gated behind a separate, safer product tier. Actually exercising this requires Section 10's bundled local trainer to be present on the player's machine; buying and immediately playing a sample doesn't.

One real technical constraint either way, inherited from Section 7 regardless of the philosophy above: a checkpoint is tied to the exact network architecture and observation/action shape it was trained with (the same "config changes invalidate artifacts" rule that applies to a player's own nodes). Shipping a sample means shipping the `MLGoalNodeAsset` config it depends on alongside it, or continued training simply can't load it.

Not gated on Section 11 Risk 1's number — the ONNX-only path works and is valuable regardless of how long from-scratch training turns out to take, and could plausibly ship before the full bundled-trainer story (Section 10) is de-risked.

**Confirmed 2026-09-01: this is load-bearing, not a stretch goal.** A blank ML node handed to a player with no RL background is not a rough-but-usable starting point - during the Phase 1 spike, an *untrained* policy reliably looked broken (pointing at the ground, circling instead of engaging, never firing) with no lever a non-technical player would know how to pull. Most players would read that as "this feature is broken" and bounce, not experiment with it. So the free blank-node tier is realistically for the enthusiast minority who want the from-scratch experience; the default path for most players has to be "buy something that already basically works, then optionally keep training it" - meaning the purchased baseline models aren't an upsell layered on top of the from-scratch feature, they're the thing that makes the feature approachable at all for its actual target audience.

That puts a concrete bar on what Ben's sample models need to clear before they're shippable - not "trains without crashing," but genuinely, visibly *basically competent*:
- **Turret** reliably points at and hits a target it can currently see - not sporadically.
- **Nav** holds a reasonable engagement range instead of idling in place or fleeing outright.
- Neither branch has an obvious degenerate tell that reads as "broken" rather than "unpolished but working" - circling forever, staring at the ground, never firing, camping a spawn point.

This is the same bar the Phase 1 curriculum work (per-branch BT/ML mutex for isolated training, stationary-duel aim isolation, vision-gated rewards that can't be satisfied by a memorized/omniscient shortcut) is aimed at clearing one branch at a time - producing a Nav+Turret pair validated to be worth putting in front of a player, not just whatever a training run happened to converge to.

---

## 11. Open Risks & Validation Spikes

These should be tested before investing in node-editor UI polish, since several could reshape the design:

1. ~~**Training-time spike.**~~ **First real data point 2026-09-02.** Curriculum-trained Nav+Turret (per-branch BT/ML mutex, Turret-only stationary-duel baseline then Nav added, per Section 10c/13) reached consistently-beats-level-1-BT competence - both tanks actively moving, ML circle-strafing to exploit the BT opponent's turret-tracking lag, a genuine emergent tactic rather than a reward-hacking artifact - at **step 1,000,000 / 11,793s elapsed (~3h 16m)** of wall-clock training (`ML-10` run log). Ran entirely on **CPU** (`torch==2.1.1+cpu`, no GPU involved at all - see Section 10's GPU note below, added the same day). Not a fully generalized answer yet (one turret/nav loadout, one opponent tier, one training machine), but a strong existence proof: "train an ML version" reaching basic competence in a few hours on CPU alone is a much more encouraging floor than the original unresolved-risk framing assumed. Re-validate against tougher BT tiers and other loadouts before treating this as the general number.
2. **Bundled-trainer packaging.** Confirm a frozen `mlagents-learn` + PyTorch build is actually feasible per target OS at an acceptable size, before committing the player-facing flow to it.
3. **BufferSensor observation validation.** Confirm the ally/enemy entity-list design (Section 3) works end-to-end once the `AllyTargetList` extensions exist.
4. **Catastrophic forgetting behavior.** Empirically test what happens to a node's performance when training is resumed on a different loadout than it was originally trained on, before designing UI copy that promises "train it more."
5. **`RotateTurret` + public fire actuator.** Blocking prerequisites — nothing in Section 4 works without both. Note the turret has two DOF (yaw + pitch), not one.
6. **`AllyTargetList`/`TargetEntry` extensions.** Blocking prerequisite for the full observation design in Section 3 (engine-frame type, explicit distance, raw armor float, staleness, and the new ally-status broadcast channel).
7. ~~**ML-Agents ↔ Unity 6.1 compatibility.**~~ **Verified 2026-08-07.** `release_23` (package v4.0.0) requires Unity ≥6000.0 and depends on `com.unity.ai.inference` 2.2.1 (Inference Engine, the Sentis successor) — compatible with `6000.1.17f1`. Install via git URL: `https://github.com/Unity-Technologies/ml-agents.git?path=/com.unity.ml-agents#release_23`. Python trainer (`pip install mlagents`, Python 3.10.x) is a separate install, not needed until Phase 1 training runs.
8. **Branch co-adaptation.** With branch-scoped policies (Section 2.2), verify in the spike that a nav policy trained under one turret tree doesn't collapse when the player swaps the turret tree. If it does, the cross-branch observations in Section 3 are insufficient and the design needs revisiting.
9. **Batchmode throughput.** Measure with cosmetic systems gated vs. not (Section 9). Determines whether Risk 1's number is real.
10. **Multiplayer replay determinism.** Section 10b, item 3. Determines whether MLPolicy can enter multiplayer at all in its current form.

---

## 12. Open Questions for Ben

1. **Shop economy for MLPolicy.** `AiTreeAsset` has `cost` and `weight` (`AiTreeAsset.cs:56-57`) that feed `totalWeight` and therefore tank speed. Does a tree containing a trained MLPolicy node cost more, weigh more, both, or neither? A trained node is strictly more capable than an untrained one, so "same cost" is a balance decision, not a default.
2. **Artillery pitch** — Option A (solver supplies pitch) or Option B (policy learns ballistics)? Section 4.2. Recommend A for Phase 1.
3. **Hammer and Healer** — are they in scope for MLPolicy at all, or is v1 DirectFire + Artillery only? Section 6.2. Scoping them out for v1 removes a lot of reward-palette complexity.
4. **Training-arena opponent pool size** — how many teams can a player check into the pool, and does the pool randomize per-episode or per-training-run?

---

## 13. Phased Roadmap

**Phase 0 — Engineering prerequisites.**
- Add `com.unity.ml-agents` to `Packages/manifest.json`; resolve the Unity 6.1 / inference-backend question (Risk 7) *first*, since it gates everything.
- Add `RotateTurret(float yaw, float pitch)` as a raw actuator independent of the tracking state machine.
- Add a public, `CanFire`-respecting fire actuator.
- Extend `AllyTargetList`/`TargetEntry`: engine-frame type, explicit distance, raw armor float alongside the bucket, ally-status broadcast channel.
- Expose the hidden actuator state from Section 3 (turn-ramp progress, `isGrounded`, `isCurrentlyUsingComs`) as public read-only properties.
- Build the minimal flat-plus-ramps training arena as its own scene, with total-reset and respawn-disabled training mode (Section 8).
- Gate cosmetic systems (audio, particles, tread scroll, `OnGUI`, `Debug.Log` hot paths) behind a training-mode flag.
- Delete or clearly mark `SimpleTankController.cs` as dead demo code so it stops misleading design work.

**Phase 1 — Spike (validation only, no UI).** Hand-wire one turret-branch policy on a DirectFire build into ML-Agents with a fixed observation/reward set, train it against a small fixed opponent pool, measure time-to-competence on representative hardware. Then repeat for one nav-branch policy to test branch co-adaptation (Risk 8). **Gate the rest of the plan on these results.**

**Phase 2 — Node editor extension.** Add `MLPolicy` to `AiNodeType` **and** `AiNodeTypeJson` together (Section 2.3). Build the `MLGoalNodeAsset` authoring UI — branch type, observation toggles, reward-term weights (filtered by turret type), release/episode-end conditions, decision period, compatible turret set. Implement signature-change invalidation (Section 7). Save/load like existing BT node assets.

**Phase 3 — Headless pipeline integration.** Batchmode build + local Python trainer wiring. The solo team-builder / opponent-pool UI (Section 8.1). Checkpoint and ONNX export tied to node assets. In-editor "Train" flow driving the pipeline end-to-end for a developer-run test.

**Phase 4 — Player-facing delivery, single-player only.** Bundle the frozen trainer executable. Build the in-game "train an ML version" prompt/UX. Wire the Firebase-fronted cloud fallback path. MLPolicy tanks are explicitly not postable to multiplayer yet.

**Phase 5 — Multiplayer support.** ONNX transport via Firebase Storage, signature validation of opponent-supplied models, replay determinism resolution (Section 10b).

**Phase 6 — Polish.** Loadout-range warnings in the editor, interleaved/curriculum training support for generalist nodes, self-play opponents, reward-palette expansion based on what Phases 1-4 actually surface as useful.

---

## Appendix — Corrections made to the previous draft

| # | Previous claim | Reality |
|---|---|---|
| 1 | `SimpleTankController.ApplyForward`/`ApplyTurn` is the drive interface, no rework needed | Dead code (GUID in no scene/prefab), both methods private, `ApplyForward` clamps 0..1 so it cannot reverse. Real interface is `TankMan.SetMovementInput` (public, -1..1). |
| 2 | One BT per tank | Two concurrent trees, `runtimeNavAI` + `runtimeTurretAI`, separate coroutines and separate shop components. Action space spanned both. Resolved as branch-scoped MLPolicy. |
| 3 | Turret rotate is one action | Turret has yaw *and* pitch, both AI-controlled, pitch clamped in `LateUpdate`. |
| 4 | `RotateTurret` is the only blocking actuator gap | `Fire()`/`CanFire()` are also private; only `TestFire()` is public. Second blocking gap. |
| 5 | Adding `MLPolicy` to `AiNodeType` | Must also add to `AiNodeTypeJson`; conversion is a raw ordinal cast that corrupts silently if they diverge. |
| 6 | Stats-in-observation ⇒ generalizes across loadouts | Holds for continuous stats only. `TurretType` is categorical; Hammer and Healer invert reward semantics, Artillery changes action-space shape. |
| 7 | (absent) | ML-Agents is not installed; Unity 6.1 compatibility unverified. |
| 8 | (absent) | No opponent specification. Resolved via solo team-builder + randomized pool. |
| 9 | (absent) | No episode definition; existing fall-out `RespawnAtSpawnPoint` silently corrupts episodes; `AllyTargetList` is a `DontDestroyOnLoad` singleton that leaks across resets. |
| 10 | (absent) | `ApplyMovement` has hidden non-Markov state (turn ramp, airborne cutoff, Coms speed penalty) that must be observed. |
| 11 | (absent) | Multiplayer ONNX distribution, model trust, and replay determinism — entire new section. |
| 12 | (absent) | Config edits invalidate checkpoints/ONNX by changing the network signature. |
| 13 | (absent) | No shop cost/weight defined for MLGoalNodeAsset despite `AiTreeAsset` having both. |
