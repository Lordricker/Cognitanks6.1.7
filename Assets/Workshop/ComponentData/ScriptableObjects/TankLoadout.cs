using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class TankLoadout
{
    public string tankName;

    public ComponentData equippedTurret;
    public ComponentData equippedArmor;
    // public ComponentData equippedAIModule; // Deprecated: use NavAIData and TurretAIData instead
    public ComponentData equippedEngineFrame;

    public bool HasComponent(ComponentData component)
    {
        return component == equippedTurret ||
               component == equippedArmor ||
               /*component == equippedAIModule ||*/
               component == equippedEngineFrame;
    }

    public void EquipComponent(ComponentData component)
    {
        if (component is TurretData) equippedTurret = component;
        else if (component is ArmorData) equippedArmor = component;
        // else if (component is AIModuleData) equippedAIModule = component; // Deprecated
        else if (component is EngineFrameData) equippedEngineFrame = component;
    }
}

