using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class ArenaSetupManager : MonoBehaviour
{
    public List<TankSlotActiveButton> tankSlotButtons; // Assign in Inspector

    // Returns an array of which tank slots are active
    public bool[] GetActiveSlots()
    {
        bool[] activeSlots = new bool[tankSlotButtons.Count];
        for (int i = 0; i < tankSlotButtons.Count; i++)
        {
            activeSlots[i] = tankSlotButtons[i].isActive;
        }
        return activeSlots;
    }


}