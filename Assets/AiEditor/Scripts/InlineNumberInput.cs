using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Text.RegularExpressions;

public class InlineNumberInput : MonoBehaviour
{
    [Header("Settings")]
    public string originalTemplate = ""; // Store the original template like "HP > #%"
    
    private string currentNumber = "0";
    
    /// <summary>
    /// Sets the template for this number input
    /// </summary>
    public void SetTemplate(string template)
    {
        originalTemplate = template;
        
        // Extract any existing number from the template
        if (!template.Contains("#"))
        {
            Match numMatch = Regex.Match(template, @"(\d+(?:\.\d+)?)");
            if (numMatch.Success)
            {
                currentNumber = numMatch.Groups[1].Value;
            }
        }
    }
    
    /// <summary>
    /// Gets the current number value
    /// </summary>
    public string GetCurrentNumber()
    {
        return currentNumber;
    }
    
    /// <summary>
    /// Sets the current number value
    /// </summary>
    public void SetCurrentNumber(string number)
    {
        float result;
        if (float.TryParse(number, out result))
        {
            currentNumber = number;
            Debug.Log($"[InlineNumberInput] Set number to {currentNumber}");
        }
        else
        {
            Debug.LogWarning($"[InlineNumberInput] Invalid number: {number}, keeping current value: {currentNumber}");
        }
    }
}
