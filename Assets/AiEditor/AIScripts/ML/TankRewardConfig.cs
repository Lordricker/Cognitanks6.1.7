using UnityEngine;

/// <summary>
/// Reward-weight palette for a single trained tank (BottomUpAgentPlan.md Section 5), scoped to
/// solo/1v1 training - the Ally-HP-nearby and Heal-delivered terms are deliberately omitted since
/// team infrastructure (Section 3's ally-status broadcast) isn't built yet.
///
/// Inert at play time (Section 2.4) - these weights only shape the trainer. NavPolicyAgent and
/// TurretPolicyAgent each read the terms relevant to their branch and ignore the rest.
///
/// Dense per-decision terms (aim accuracy, survival, distance band, flanking) are deliberately
/// left as raw per-decision constants rather than auto-normalized by episode length (Section 5's
/// "reward scale hygiene" warns these can dwarf terminal kill/death rewards if too large) -
/// keep them small; the [Range] sliders below default to sane, small-magnitude ranges as a guardrail.
/// </summary>
[CreateAssetMenu(fileName = "TankRewardConfig", menuName = "AI/ML Reward Config")]
public class TankRewardConfig : ScriptableObject
{
    [Header("Terminal (both branches)")]
    public float killBonus = 1f;
    public float deathPenalty = -1f;

    [Header("Turret - primary signal")]
    [Tooltip("Reward applied once per point of damage dealt.")]
    public float damageDealtWeight = 0.02f;

    [Tooltip("Dense per-decision GUIDANCE nudge for pointing toward the opponent: 0 at 180 degrees off, this value when dead-on. Smaller than aimHitWeight by design - its job is giving PPO a gradient to climb before the turret is precisely on-target; aimHitWeight is the term that actually matters. Pointing at the target is critical, so both this and aimHitWeight are weighted well above the other dense terms.")]
    [Range(0f, 0.05f)]
    public float aimAccuracyWeight = 0.01f;

    [Tooltip("Dense per-decision bonus when the turret's aim raycast actually connects with the opponent's real hitbox right now (not just angle-to-center) - the real payoff for precise aim, weighted well above aimAccuracyWeight's guidance role.")]
    [Range(0f, 0.05f)]
    public float aimHitWeight = 0.01f;

    [Header("Nav - primary signal")]
    [Tooltip("Penalty applied once per point of damage taken.")]
    public float damageTakenWeight = -0.02f;

    [Tooltip("Dense per-decision reward just for being alive.")]
    [Range(0f, 0.02f)]
    public float survivalWeight = 0.002f;

    [Tooltip("Fraction of the tank's own current weapon Range (TankMan.Range - varies per turret loadout, e.g. Rifle=100, Sniper=700) below which distanceBandWeight is withheld. Section 5 revision 2026-09-01: this used to be a fixed 15-30 world-unit band, which was a bug - real turret ranges run 100-700+ units, so that band was rewarding near point-blank camping regardless of what was actually equipped. A small nonzero minimum keeps 'in range' from also paying out for ramming the opponent.")]
    [Range(0f, 1f)]
    public float distanceBandMinRatio = 0.2f;

    [Tooltip("Fraction of the tank's own current weapon Range (TankMan.Range) up to which distanceBandWeight is paid. 1.0 = anywhere inside actual engagement range - the point is rewarding 'somewhere a shot could land,' not an arbitrary fixed distance.")]
    [Range(0f, 1.5f)]
    public float distanceBandMaxRatio = 1f;

    [Tooltip("Dense per-decision reward while inside the distance band.")]
    [Range(0f, 0.05f)]
    public float distanceBandWeight = 0.01f;

    [Tooltip("Dense per-decision reward while outside the opponent's vision cone but within their vision range (flanking).")]
    [Range(0f, 0.05f)]
    public float flankingWeight = 0.01f;

    [Tooltip("Anti-idling nudge (not part of Section 5's original palette): dense per-decision reward proportional to actual forward chassis speed (0 stationary/reversing, full weight at MoveSpeed). Nothing else in this palette rewards movement itself - distanceBand/flanking only pay for already being in the right spot, so a tank that spawns positioned well has zero incentive to ever move. This exists purely to keep PPO sampling nonzero throttle long enough to discover whether movement helps (evasion, repositioning), not as a real skill signal on its own - keep it in the same ballpark as the other dense Nav terms so it doesn't become its own free-reward exploit (e.g. driving in circles for no tactical reason).")]
    [Range(0f, 0.05f)]
    public float movementWeight = 0.001f;
}
