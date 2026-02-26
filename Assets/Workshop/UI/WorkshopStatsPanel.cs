using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class WorkshopStatsPanel : MonoBehaviour
{
    public TMP_Text descriptionText;
    public TMP_Text statsText;

    public void ShowStats(ComponentData data)
    {
        if (data == null)
        {
            descriptionText.text = "";
            statsText.text = "";
            return;
        }

        descriptionText.text = data.description;

        string stats = $"Cost: {data.cost}\nWeight: {data.weight}";
        if (data is TurretData turret)
            stats += $"\nDamage: {turret.damage}\nRange: {turret.range}\nShots/sec: {turret.shotspersec}\nBullet Speed: {turret.bulletSpeed}\nVision Cone: {turret.visionCone}\nVision Range: {turret.visionRange}\nKnockback: {turret.knockback}";
        else if (data is ArmorData armor)
            stats += $"\nHP: {armor.HP}";
        else if (data is EngineFrameData engine)
            stats += $"\nWeight Cap: {engine.weightCapacity}\nEngine Power: {engine.enginePower}\nTurning Power: {engine.turningPower}";

        statsText.text = stats;
    }

    /// <summary>
    /// Displays the full stats of all equipped components on a tank slot.
    /// </summary>
    public void ShowTankStats(Dictionary<ComponentCategory, ComponentData> equipped, float totalWeight, string tankName = "")
    {
        descriptionText.text = string.IsNullOrEmpty(tankName) ? "Tank Loadout" : tankName;

        var sb = new System.Text.StringBuilder();

        // AI components — check all AI-related categories
        ComponentData turretAi = null;
        ComponentData navAi = null;

        foreach (var kvp in equipped)
        {
            if (kvp.Value is AiEditor.AiTreeAsset aiAsset)
            {
                if (aiAsset.branchType == AiEditor.AiBranchType.Turret)
                    turretAi = kvp.Value;
                else if (aiAsset.branchType == AiEditor.AiBranchType.Nav)
                    navAi = kvp.Value;
            }
        }

        sb.AppendLine("── Turret AI ──");
        if (turretAi != null)
        {
            sb.AppendLine(turretAi.title);
            sb.AppendLine($"Weight: {turretAi.weight}kg");
        }
        else
        {
            sb.AppendLine("(none)");
        }
        sb.AppendLine();

        sb.AppendLine("── Nav AI ──");
        if (navAi != null)
        {
            sb.AppendLine(navAi.title);
            sb.AppendLine($"Weight: {navAi.weight}kg");
        }
        else
        {
            sb.AppendLine("(none)");
        }
        sb.AppendLine();

        // Engine Frame
        if (equipped.TryGetValue(ComponentCategory.EngineFrame, out var engineComp) && engineComp is EngineFrameData engine)
        {
            sb.AppendLine("── Engine Frame ──");
            sb.AppendLine(engine.title);
            sb.AppendLine($"Power: {engine.enginePower}");
            sb.AppendLine($"Turning: {engine.turningPower}");
            sb.AppendLine($"Weight Cap: {engine.weightCapacity}kg  Weight: {engine.weight}kg");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("── Engine Frame ──");
            sb.AppendLine("(none)");
            sb.AppendLine();
        }

        // Armor
        if (equipped.TryGetValue(ComponentCategory.Armor, out var armorComp) && armorComp is ArmorData armor)
        {
            sb.AppendLine("── Armor ──");
            sb.AppendLine(armor.title);
            sb.AppendLine($"HP: {armor.HP}  Weight: {armor.weight}kg");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("── Armor ──");
            sb.AppendLine("(none)");
            sb.AppendLine();
        }

        // Turret
        if (equipped.TryGetValue(ComponentCategory.Turret, out var turretComp) && turretComp is TurretData turret)
        {
            sb.AppendLine("── Turret ──");
            sb.AppendLine(turret.title);
            sb.AppendLine($"Range: {turret.range}");
            sb.AppendLine($"Damage: {turret.damage}");
            sb.AppendLine($"Bullet Speed: {turret.bulletSpeed}");
            sb.AppendLine($"Shots/sec: {turret.shotspersec}");
            sb.AppendLine($"Vision: {turret.visionRange}m / {turret.visionCone}°");
            sb.AppendLine($"Knockback: {turret.knockback}");
            sb.AppendLine($"Weight: {turret.weight}kg");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("── Turret ──");
            sb.AppendLine("(none)");
            sb.AppendLine();
        }

        sb.AppendLine($"── Total Weight: {totalWeight:F1}kg ──");

        statsText.text = sb.ToString();
    }
}
