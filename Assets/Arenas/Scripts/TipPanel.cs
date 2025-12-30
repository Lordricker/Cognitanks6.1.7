using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class TipPanel : MonoBehaviour
{
    [Header("Tip Configuration")]
    [SerializeField] private TMP_Text tipText;
    [SerializeField] private List<string> tips = new List<string>
    {
        "If your tank is hiccuping then it is too heavy for its engine",
        "Unless it is artillery, bullets travel in a straight line.",
        "The penalty for using coms increases with the number of your alive teammates.",
        "Mobility can be your best defense.",
        "Study enemy patterns to predict their moves.",
        "Use terrain to your advantage."
    };

    private void OnEnable()
    {
        SetRandomTip();
    }

    public void SetRandomTip()
    {
        if (tipText == null)
        {
            // Try to find TMP_Text in children if not assigned
            tipText = GetComponentInChildren<TMP_Text>();
        }

        if (tipText != null && tips.Count > 0)
        {
            int randomIndex = Random.Range(0, tips.Count);
            tipText.text = tips[randomIndex];
        }
    }

    public void AddTip(string newTip)
    {
        if (!string.IsNullOrEmpty(newTip))
        {
            tips.Add(newTip);
        }
    }

    public void RemoveTip(string tipToRemove)
    {
        tips.Remove(tipToRemove);
    }
}