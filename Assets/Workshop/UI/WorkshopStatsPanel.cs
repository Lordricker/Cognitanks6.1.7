using UnityEngine;
using TMPro;

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

        // Example: show stats based on type
        string stats = $"Cost: {data.cost}\nWeight: {data.weight}";
        if (data is TurretData turret)
            stats += $"\nDamage: {turret.damage}\nRange: {turret.range}\nShots/sec: {turret.shotspersec}\nBullet Speed: {turret.bulletSpeed}\nVision Cone: {turret.visionCone}\nVision Range: {turret.visionRange}\nKnockback: {turret.knockback}";
        else if (data is ArmorData armor)
            stats += $"\nHP: {armor.HP}";
        else if (data is EngineFrameData engine)
            stats += $"\nWeight Cap: {engine.weightCapacity}\nEngine Power: {engine.enginePower}\nTurning Power: {engine.turningPower}";

        statsText.text = stats;
    }
}
