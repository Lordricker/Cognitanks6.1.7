using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class NodeDeleteUI : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler
{
    public Button deleteButton;
    public Image nodeImage;

    private NodeDraggable nodeDraggable;
    private Canvas parentCanvas;

    private Vector2 pointerDownPos;
    private float pointerDownTime;
    private const float clickThreshold = 10f; // pixels
    private const float clickTime = 0.25f; // seconds

    void Awake()
    {
        nodeDraggable = GetComponent<NodeDraggable>();
        parentCanvas = GetComponentInParent<Canvas>();
        if (deleteButton != null)
            deleteButton.gameObject.SetActive(false);
        if (deleteButton != null)
            deleteButton.onClick.AddListener(OnDeleteClicked);
    }

    void Update()
    {
        // No longer hide the delete button on click outside; only hide if node image is clicked again
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pointerDownPos = eventData.position;
        pointerDownTime = Time.unscaledTime;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        float dist = Vector2.Distance(pointerDownPos, eventData.position);
        float t = Time.unscaledTime - pointerDownTime;
        if (dist < clickThreshold && t < clickTime && eventData.pointerPress == nodeImage.gameObject)
        {
            if (deleteButton != null && deleteButton.gameObject.activeSelf)
                HideDeleteButton();
            else
                ShowDeleteButton();
        }
        else
        {
            // Do nothing
        }
    }

    public void OnPointerClick(PointerEventData eventData) { /* No-op, handled by up/down */ }

    public void ShowDeleteButton()
    {
        if (deleteButton != null)
            deleteButton.gameObject.SetActive(true);
            
        // Also show number input for range comparison nodes
        ShowNumberInputIfApplicable();
    }

    public void HideDeleteButton()
    {
        Debug.Log($"hiding button");
        if (deleteButton != null)
            deleteButton.gameObject.SetActive(false);
            
        // Also hide number input
        HideNumberInput();
    }
    
    private void ShowNumberInputIfApplicable()
    {
        // Check if this node should have number input based on its label
        string nodeLabel = GetNodeLabel();
        if (ShouldHaveNumberInput(nodeLabel))
        {
            var inputField = transform.Find("#InputField (TMP)");
            if (inputField != null)
            {
                inputField.gameObject.SetActive(true);
                var tmpInputField = inputField.GetComponent<TMPro.TMP_InputField>();
                if (tmpInputField != null)
                {
                    // Extract current number from the label or use default
                    string currentNumber = ExtractNumberFromLabel(nodeLabel);
                    tmpInputField.text = currentNumber;
                    
                    // Add listeners for when editing is finished
                    tmpInputField.onEndEdit.RemoveAllListeners();
                    tmpInputField.onEndEdit.AddListener(OnNumberInputFinished);
                }
            }
        }
    }
    
    private void OnNumberInputFinished(string newValue)
    {
        Debug.Log($"OnNumberInputFinished called with value: '{newValue}'");
        
        // Update the node's text with the new number
        UpdateNodeTextWithNumber(newValue);
        
        // Delay hiding to allow the update to be processed
        StartCoroutine(DelayedHideAfterUpdate());
    }
    
    private System.Collections.IEnumerator DelayedHideAfterUpdate()
    {
        // Wait a frame to ensure the text update is processed
        yield return null;
        
        // Hide both delete button and input field
        Debug.Log("Hiding UI after number update");
        HideDeleteButton();
    }
    
    private void UpdateNodeTextWithNumber(string number)
    {
        Debug.Log($"UpdateNodeTextWithNumber called with number: '{number}'");
        string currentLabel = GetNodeLabel();
        Debug.Log($"Current label is: '{currentLabel}'");
        
        if (ShouldHaveNumberInput(currentLabel))
        {
            string newLabel = "";
            
            // Handle all the specific patterns
            if (currentLabel.Contains("If Self HP>#") || currentLabel.StartsWith("If Self HP>"))
            {
                newLabel = $"If Self HP>{number}";
                Debug.Log($"Created new label for 'If Self HP>' pattern: '{newLabel}'");
            }
            else if (currentLabel.Contains("If Self HP<#") || currentLabel.StartsWith("If Self HP<"))
            {
                newLabel = $"If Self HP<{number}";
                Debug.Log($"Created new label for 'If Self HP<' pattern: '{newLabel}'");
            }
            else if (currentLabel.Contains("If HP < #") || currentLabel.StartsWith("If HP < "))
            {
                newLabel = $"If HP < {number}";
                Debug.Log($"Created new label for 'If HP < ' pattern: '{newLabel}'");
            }
            else if (currentLabel.Contains("If HP > #") || currentLabel.StartsWith("If HP > "))
            {
                newLabel = $"If HP > {number}";
                Debug.Log($"Created new label for 'If HP > ' pattern: '{newLabel}'");
            }
            else if (currentLabel.Contains("If Tag = #") || currentLabel.StartsWith("If Tag = "))
            {
                newLabel = $"If Tag = {number}";
                Debug.Log($"Created new label for 'If Tag = ' pattern: '{newLabel}'");
            }
            else if (currentLabel.Contains("If Tag < #") || currentLabel.StartsWith("If Tag < "))
            {
                newLabel = $"If Tag < {number}";
                Debug.Log($"Created new label for 'If Tag < ' pattern: '{newLabel}'");
            }
            else if (currentLabel.Contains("If Tag > #") || currentLabel.StartsWith("If Tag > "))
            {
                newLabel = $"If Tag > {number}";
                Debug.Log($"Created new label for 'If Tag > ' pattern: '{newLabel}'");
            }
            else if (currentLabel.Contains("If Range<#") || currentLabel.StartsWith("If Range<"))
            {
                newLabel = $"If Range<{number}";
                Debug.Log($"Created new label for 'If Range<' pattern: '{newLabel}'");
            }
            else if (currentLabel.Contains("If Range>#") || currentLabel.StartsWith("If Range>"))
            {
                newLabel = $"If Range>{number}";
                Debug.Log($"Created new label for 'If Range>' pattern: '{newLabel}'");
            }
            
            if (!string.IsNullOrEmpty(newLabel))
            {
                Debug.Log($"Updating node label from '{currentLabel}' to '{newLabel}'");
                SetNodeLabel(newLabel);
            }
            else
            {
                Debug.LogWarning($"Could not determine new label for current label: '{currentLabel}'");
            }
        }
        else
        {
            Debug.LogWarning($"ShouldHaveNumberInput returned false for label: '{currentLabel}'");
        }
    }
    
    private void SetNodeLabel(string newLabel)
    {
        Debug.Log($"SetNodeLabel called with: '{newLabel}'");
        bool updated = false;
        
        // PRIORITY 1: Try to find and update "NodeText" specifically
        var nodeText = transform.Find("NodeText");
        if (nodeText != null)
        {
            var tmpText = nodeText.GetComponent<TMPro.TMP_Text>();
            if (tmpText != null)
            {
                Debug.Log($"Found NodeText child, updating from '{tmpText.text}' to '{newLabel}'");
                tmpText.text = newLabel;
                updated = true;
            }
            else
            {
                Debug.LogWarning("Found NodeText child but no TMP_Text component!");
            }
        }
        else
        {
            Debug.LogWarning("Could not find NodeText child object!");
        }
        
        // PRIORITY 2: Also try to set label in TitleName component (if exists)
        var titleName = GetComponentInChildren<TitleName>();
        if (titleName != null)
        {
            Debug.Log($"Found TitleName component, updating to '{newLabel}'");
            titleName.SetTitle(newLabel);
        }
        
        // Log result
        if (updated)
        {
            Debug.Log($"Successfully updated NodeText to '{newLabel}'");
        }
        else
        {
            Debug.LogError($"Failed to update NodeText! Could not find NodeText child object.");
        }
    }
    
    private void HideNumberInput()
    {
        var inputField = transform.Find("#InputField (TMP)");
        if (inputField != null)
        {
            inputField.gameObject.SetActive(false);
        }
    }
    
    private bool ShouldHaveNumberInput(string nodeLabel)
    {
        if (string.IsNullOrEmpty(nodeLabel))
            return false;
            
        // Check for all the specific patterns that need number input
        return nodeLabel.Contains("If Self HP>#") || 
               nodeLabel.Contains("If Self HP<#") ||
               nodeLabel.Contains("If HP < #") ||
               nodeLabel.Contains("If HP > #") ||
               nodeLabel.Contains("If Tag = #") ||
               nodeLabel.Contains("If Tag < #") ||
               nodeLabel.Contains("If Tag > #") ||
               nodeLabel.Contains("If Range<#") ||
               nodeLabel.Contains("If Range>#") ||
               // Also check for patterns that already have numbers (not just #)
               nodeLabel.StartsWith("If Self HP>") ||
               nodeLabel.StartsWith("If Self HP<") ||
               nodeLabel.StartsWith("If HP < ") ||
               nodeLabel.StartsWith("If HP > ") ||
               nodeLabel.StartsWith("If Tag = ") ||
               nodeLabel.StartsWith("If Tag < ") ||
               nodeLabel.StartsWith("If Tag > ") ||
               nodeLabel.StartsWith("If Range<") ||
               nodeLabel.StartsWith("If Range>");
    }
    
    private string GetNodeLabel()
    {
        // PRIORITY 1: Look specifically for "NodeText" child
        var nodeText = transform.Find("NodeText");
        if (nodeText != null)
        {
            var tmpText = nodeText.GetComponent<TMPro.TMP_Text>();
            if (tmpText != null && !string.IsNullOrEmpty(tmpText.text))
            {
                Debug.Log($"GetNodeLabel: Found NodeText with text: '{tmpText.text}'");
                return tmpText.text;
            }
        }
        
        // PRIORITY 2: Try to get label from TitleName component
        var titleName = GetComponentInChildren<TitleName>();
        if (titleName != null && !string.IsNullOrEmpty(titleName.titleText.text))
        {
            Debug.Log($"GetNodeLabel: Found TitleName with text: '{titleName.titleText.text}'");
            return titleName.titleText.text;
        }
        
        // PRIORITY 3: Fallback to any TMP_Text
        var tmpTextFallback = GetComponentInChildren<TMPro.TMP_Text>();
        if (tmpTextFallback != null)
        {
            Debug.Log($"GetNodeLabel: Found fallback TMP_Text with text: '{tmpTextFallback.text}'");
            return tmpTextFallback.text;
        }
        
        // Final fallback to GameObject name
        Debug.Log($"GetNodeLabel: Using GameObject name: '{gameObject.name}'");
        return gameObject.name;
    }

    private string ExtractNumberFromLabel(string nodeLabel)
    {
        if (string.IsNullOrEmpty(nodeLabel))
            return "0";
            
        // Extract number from various patterns
        if (nodeLabel.StartsWith("If Self HP>"))
        {
            string numberPart = nodeLabel.Substring(11); // Skip "If Self HP>"
            if (numberPart == "#" || string.IsNullOrEmpty(numberPart))
                return "0";
            return numberPart;
        }
        else if (nodeLabel.StartsWith("If Self HP<"))
        {
            string numberPart = nodeLabel.Substring(11); // Skip "If Self HP<"
            if (numberPart == "#" || string.IsNullOrEmpty(numberPart))
                return "0";
            return numberPart;
        }
        else if (nodeLabel.StartsWith("If HP < "))
        {
            string numberPart = nodeLabel.Substring(8); // Skip "If HP < "
            if (numberPart == "#" || string.IsNullOrEmpty(numberPart))
                return "0";
            return numberPart;
        }
        else if (nodeLabel.StartsWith("If HP > "))
        {
            string numberPart = nodeLabel.Substring(8); // Skip "If HP > "
            if (numberPart == "#" || string.IsNullOrEmpty(numberPart))
                return "0";
            return numberPart;
        }
        else if (nodeLabel.StartsWith("If Tag = "))
        {
            string numberPart = nodeLabel.Substring(9); // Skip "If Tag = "
            if (numberPart == "#" || string.IsNullOrEmpty(numberPart))
                return "0";
            return numberPart;
        }
        else if (nodeLabel.StartsWith("If Tag < "))
        {
            string numberPart = nodeLabel.Substring(9); // Skip "If Tag < "
            if (numberPart == "#" || string.IsNullOrEmpty(numberPart))
                return "0";
            return numberPart;
        }
        else if (nodeLabel.StartsWith("If Tag > "))
        {
            string numberPart = nodeLabel.Substring(9); // Skip "If Tag > "
            if (numberPart == "#" || string.IsNullOrEmpty(numberPart))
                return "0";
            return numberPart;
        }
        else if (nodeLabel.StartsWith("If Range<"))
        {
            string numberPart = nodeLabel.Substring(9); // Skip "If Range<"
            if (numberPart == "#" || string.IsNullOrEmpty(numberPart))
                return "0";
            return numberPart;
        }
        else if (nodeLabel.StartsWith("If Range>"))
        {
            string numberPart = nodeLabel.Substring(9); // Skip "If Range>"
            if (numberPart == "#" || string.IsNullOrEmpty(numberPart))
                return "0";
            return numberPart;
        }
        
        return "0";
    }

    public void OnDeleteClicked()
    {
        Debug.Log($"OnDeleteClicked called on {gameObject.name}");
        var nd = GetComponent<NodeDraggable>();
        if (nd != null)
        {
            Debug.Log($"Deleting all connected lines for {gameObject.name}");
            nd.DeleteAllConnectedLines();
        }
        else
        {
            Debug.Log($"No NodeDraggable found on {gameObject.name}");
        }
        Destroy(gameObject);
    }
}
