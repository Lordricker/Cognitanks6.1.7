using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class NodeDeleteUI : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler
{
    public Button deleteButton;
    public Image nodeImage;
    public GameObject nodeDetailsPanel; // Assign in inspector - the NodeDetails GameObject

    private NodeDraggable nodeDraggable;
    private Canvas parentCanvas;
    private static NodeDeleteUI currentActiveNode; // Track which node is currently showing details

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
        
        // Find NodeDetails panel if not assigned
        if (nodeDetailsPanel == null)
        {
            GameObject uiCanvas = GameObject.Find("UICanvas");
            if (uiCanvas != null)
            {
                Transform nodeDetails = uiCanvas.transform.Find("NodeDetails");
                if (nodeDetails != null)
                {
                    nodeDetailsPanel = nodeDetails.gameObject;
                }
            }
        }
        
        // Ensure NodeDetails starts deactivated
        if (nodeDetailsPanel != null && currentActiveNode != this)
        {
            nodeDetailsPanel.SetActive(false);
        }
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
            {
                HideDeleteButton();
                HideNodeDetails();
            }
            else
            {
                ShowDeleteButton();
                ShowNodeDetails();
            }
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
        
        // Also hide node details
        HideNodeDetails();
    }
    
    public static void HideAllActiveUI()
    {
        if (currentActiveNode != null)
        {
            currentActiveNode.HideDeleteButton();
        }
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
        
        // Auto-save after parameter change
        var aiEditorFileUI = FindFirstObjectByType<AiEditorFileUI>();
        if (aiEditorFileUI != null)
        {
            aiEditorFileUI.AutoSave();
        }
        
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
            else if (currentLabel.Contains("LeadTarget#") || currentLabel.StartsWith("LeadTarget"))
            {
                newLabel = $"LeadTarget{number}";
                Debug.Log($"Created new label for 'LeadTarget' pattern: '{newLabel}'");
            }
            else if (currentLabel.Contains("Lead Target #") || currentLabel.StartsWith("Lead Target "))
            {
                newLabel = $"Lead Target {number}";
                Debug.Log($"Created new label for 'Lead Target ' pattern: '{newLabel}'");
            }
            else if (currentLabel.Contains("RotateUp") || currentLabel.Contains("Rotate Up"))
            {
                newLabel = $"Rotate Up {number}°";
                Debug.Log($"Created new label for 'Rotate Up' pattern: '{newLabel}'");
            }
            else if (currentLabel.Contains("RotateDown") || currentLabel.Contains("Rotate Down"))
            {
                newLabel = $"Rotate Down {number}°";
                Debug.Log($"Created new label for 'Rotate Down' pattern: '{newLabel}'");
            }
            else if (currentLabel.Contains("RotateLeft") || currentLabel.Contains("Rotate Left"))
            {
                newLabel = $"Rotate Left {number}°";
                Debug.Log($"Created new label for 'Rotate Left' pattern: '{newLabel}'");
            }
            else if (currentLabel.Contains("RotateRight") || currentLabel.Contains("Rotate Right"))
            {
                newLabel = $"Rotate Right {number}°";
                Debug.Log($"Created new label for 'Rotate Right' pattern: '{newLabel}'");
            }
            else if (currentLabel.Contains("Cycle"))
            {
                newLabel = $"Cycle {number}";
                Debug.Log($"Created new label for 'Cycle' pattern: '{newLabel}'");
            }
            // MyTag and TeamTag condition nodes
            else if (currentLabel.Contains("If MyTag = #") || currentLabel.StartsWith("If MyTag = "))
            {
                newLabel = $"If MyTag = {number}";
                Debug.Log($"Created new label for 'If MyTag = ' pattern: '{newLabel}'");
            }
            else if (currentLabel.Contains("If MyTag < #") || currentLabel.StartsWith("If MyTag < "))
            {
                newLabel = $"If MyTag < {number}";
                Debug.Log($"Created new label for 'If MyTag < ' pattern: '{newLabel}'");
            }
            else if (currentLabel.Contains("If MyTag > #") || currentLabel.StartsWith("If MyTag > "))
            {
                newLabel = $"If MyTag > {number}";
                Debug.Log($"Created new label for 'If MyTag > ' pattern: '{newLabel}'");
            }
            else if (currentLabel.Contains("If MyTag != #") || currentLabel.StartsWith("If MyTag != "))
            {
                newLabel = $"If MyTag != {number}";
                Debug.Log($"Created new label for 'If MyTag != ' pattern: '{newLabel}'");
            }
            else if (currentLabel.Contains("If TeamTag = #") || currentLabel.StartsWith("If TeamTag = "))
            {
                newLabel = $"If TeamTag = {number}";
                Debug.Log($"Created new label for 'If TeamTag = ' pattern: '{newLabel}'");
            }
            else if (currentLabel.Contains("If TeamTag < #") || currentLabel.StartsWith("If TeamTag < "))
            {
                newLabel = $"If TeamTag < {number}";
                Debug.Log($"Created new label for 'If TeamTag < ' pattern: '{newLabel}'");
            }
            else if (currentLabel.Contains("If TeamTag > #") || currentLabel.StartsWith("If TeamTag > "))
            {
                newLabel = $"If TeamTag > {number}";
                Debug.Log($"Created new label for 'If TeamTag > ' pattern: '{newLabel}'");
            }
            else if (currentLabel.Contains("If TeamTag != #") || currentLabel.StartsWith("If TeamTag != "))
            {
                newLabel = $"If TeamTag != {number}";
                Debug.Log($"Created new label for 'If TeamTag != ' pattern: '{newLabel}'");
            }
            // MyTag and TeamTag action nodes
            else if (currentLabel.Contains("MyTag"))
            {
                newLabel = $"MyTag{number}";
                Debug.Log($"Created new label for 'MyTag' pattern: '{newLabel}'");
            }
            else if (currentLabel.Contains("TeamTag"))
            {
                newLabel = $"TeamTag{number}";
                Debug.Log($"Created new label for 'TeamTag' pattern: '{newLabel}'");
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
               nodeLabel.Contains("LeadTarget#") ||
               nodeLabel.Contains("Lead Target #") ||
               nodeLabel.Contains("RotateUp") ||
               nodeLabel.Contains("Rotate Up") ||
               nodeLabel.Contains("RotateDown") ||
               nodeLabel.Contains("Rotate Down") ||
               nodeLabel.Contains("RotateLeft") ||
               nodeLabel.Contains("Rotate Left") ||
               nodeLabel.Contains("RotateRight") ||
               nodeLabel.Contains("Rotate Right") ||
               // MyTag and TeamTag condition nodes
               nodeLabel.Contains("If MyTag = #") ||
               nodeLabel.Contains("If MyTag < #") ||
               nodeLabel.Contains("If MyTag > #") ||
               nodeLabel.Contains("If MyTag != #") ||
               nodeLabel.Contains("If TeamTag = #") ||
               nodeLabel.Contains("If TeamTag < #") ||
               nodeLabel.Contains("If TeamTag > #") ||
               nodeLabel.Contains("If TeamTag != #") ||
               // MyTag and TeamTag action nodes
               nodeLabel.Contains("MyTag") ||
               nodeLabel.Contains("TeamTag") ||
               // Also check for patterns that already have numbers (not just #)
               nodeLabel.StartsWith("If Self HP>") ||
               nodeLabel.StartsWith("If Self HP<") ||
               nodeLabel.StartsWith("If HP < ") ||
               nodeLabel.StartsWith("If HP > ") ||
               nodeLabel.StartsWith("If Tag = ") ||
               nodeLabel.StartsWith("If Tag < ") ||
               nodeLabel.StartsWith("If Tag > ") ||
               nodeLabel.StartsWith("If MyTag = ") ||
               nodeLabel.StartsWith("If MyTag < ") ||
               nodeLabel.StartsWith("If MyTag > ") ||
               nodeLabel.StartsWith("If MyTag != ") ||
               nodeLabel.StartsWith("If TeamTag = ") ||
               nodeLabel.StartsWith("If TeamTag < ") ||
               nodeLabel.StartsWith("If TeamTag > ") ||
               nodeLabel.StartsWith("If TeamTag != ") ||
               nodeLabel.StartsWith("If Range<") ||
               nodeLabel.StartsWith("If Range>") ||
               nodeLabel.StartsWith("LeadTarget") ||
               nodeLabel.StartsWith("Lead Target ") ||
               nodeLabel.Contains("Cycle"); // Added for cycle nodes
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
        else if (nodeLabel.StartsWith("LeadTarget"))
        {
            string numberPart = nodeLabel.Substring(10); // Skip "LeadTarget"
            if (numberPart == "#" || string.IsNullOrEmpty(numberPart))
                return "0";
            return numberPart;
        }
        else if (nodeLabel.StartsWith("Lead Target "))
        {
            string numberPart = nodeLabel.Substring(12); // Skip "Lead Target "
            if (numberPart == "#" || string.IsNullOrEmpty(numberPart))
                return "0";
            return numberPart;
        }
        else if (nodeLabel.Contains("Rotate Left"))
        {
            int index = nodeLabel.IndexOf("Rotate Left");
            if (index >= 0)
            {
                string numberPart = nodeLabel.Substring(index + 11); // Skip "Rotate Left"
                // Remove the ° symbol if present
                numberPart = numberPart.Replace("°", "");
                if (numberPart == "#" || string.IsNullOrEmpty(numberPart))
                    return "0";
                return numberPart;
            }
        }
        else if (nodeLabel.Contains("Rotate Right"))
        {
            int index = nodeLabel.IndexOf("Rotate Right");
            if (index >= 0)
            {
                string numberPart = nodeLabel.Substring(index + 12); // Skip "Rotate Right"
                // Remove the ° symbol if present
                numberPart = numberPart.Replace("°", "");
                if (numberPart == "#" || string.IsNullOrEmpty(numberPart))
                    return "0";
                return numberPart;
            }
        }
        else if (nodeLabel.Contains("Cycle"))
        {
            int index = nodeLabel.IndexOf("Cycle");
            if (index >= 0)
            {
                string numberPart = nodeLabel.Substring(index + 6); // Skip "Cycle "
                if (numberPart == "#" || string.IsNullOrEmpty(numberPart))
                    return "0";
                return numberPart;
            }
        }
        // MyTag and TeamTag condition nodes
        else if (nodeLabel.StartsWith("If MyTag = "))
        {
            string numberPart = nodeLabel.Substring(11); // Skip "If MyTag = "
            if (numberPart == "#" || string.IsNullOrEmpty(numberPart))
                return "0";
            return numberPart;
        }
        else if (nodeLabel.StartsWith("If MyTag < "))
        {
            string numberPart = nodeLabel.Substring(11); // Skip "If MyTag < "
            if (numberPart == "#" || string.IsNullOrEmpty(numberPart))
                return "0";
            return numberPart;
        }
        else if (nodeLabel.StartsWith("If MyTag > "))
        {
            string numberPart = nodeLabel.Substring(11); // Skip "If MyTag > "
            if (numberPart == "#" || string.IsNullOrEmpty(numberPart))
                return "0";
            return numberPart;
        }
        else if (nodeLabel.StartsWith("If MyTag != "))
        {
            string numberPart = nodeLabel.Substring(12); // Skip "If MyTag != "
            if (numberPart == "#" || string.IsNullOrEmpty(numberPart))
                return "0";
            return numberPart;
        }
        else if (nodeLabel.StartsWith("If TeamTag = "))
        {
            string numberPart = nodeLabel.Substring(13); // Skip "If TeamTag = "
            if (numberPart == "#" || string.IsNullOrEmpty(numberPart))
                return "0";
            return numberPart;
        }
        else if (nodeLabel.StartsWith("If TeamTag < "))
        {
            string numberPart = nodeLabel.Substring(13); // Skip "If TeamTag < "
            if (numberPart == "#" || string.IsNullOrEmpty(numberPart))
                return "0";
            return numberPart;
        }
        else if (nodeLabel.StartsWith("If TeamTag > "))
        {
            string numberPart = nodeLabel.Substring(13); // Skip "If TeamTag > "
            if (numberPart == "#" || string.IsNullOrEmpty(numberPart))
                return "0";
            return numberPart;
        }
        else if (nodeLabel.StartsWith("If TeamTag != "))
        {
            string numberPart = nodeLabel.Substring(14); // Skip "If TeamTag != "
            if (numberPart == "#" || string.IsNullOrEmpty(numberPart))
                return "0";
            return numberPart;
        }
        // MyTag and TeamTag action nodes
        else if (nodeLabel.Contains("MyTag"))
        {
            string numberPart = nodeLabel.Substring(5); // Skip "MyTag"
            if (numberPart == "#" || string.IsNullOrEmpty(numberPart))
                return "0";
            return numberPart;
        }
        else if (nodeLabel.Contains("TeamTag"))
        {
            string numberPart = nodeLabel.Substring(7); // Skip "TeamTag"
            if (numberPart == "#" || string.IsNullOrEmpty(numberPart))
                return "0";
            return numberPart;
        }
        
        return "0";
    }

    public void OnDeleteClicked()
    {
        Debug.Log($"OnDeleteClicked called on {gameObject.name}");
        
        // Hide node details when deleting
        HideNodeDetails();
        
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

        // Auto-save after node deletion
        var aiEditorFileUI = FindFirstObjectByType<AiEditorFileUI>();
        if (aiEditorFileUI != null)
        {
            aiEditorFileUI.AutoSave();
        }

        Destroy(gameObject);
    }
    
    private void ShowNodeDetails()
    {
        if (nodeDetailsPanel == null)
            return;
        
        // Hide details from any previously active node
        if (currentActiveNode != null && currentActiveNode != this)
        {
            currentActiveNode.HideNodeDetails();
        }
        
        // Set this as the active node
        currentActiveNode = this;
        
        // Show the panel with fade
        StartCoroutine(FadeNodeDetailsPanel(true));
        
        // Get the node label and description
        string nodeLabel = GetNodeLabel();
        string description = GetNodeDescription(nodeLabel);
        
        // Find and update the TextMeshPro child
        TMPro.TMP_Text descriptionText = nodeDetailsPanel.GetComponentInChildren<TMPro.TMP_Text>();
        if (descriptionText != null)
        {
            descriptionText.text = description;
        }
    }
    
    private void HideNodeDetails()
    {
        if (nodeDetailsPanel != null && currentActiveNode == this)
        {
            StartCoroutine(FadeNodeDetailsPanel(false));
        }
    }
    
    private System.Collections.IEnumerator FadeNodeDetailsPanel(bool show)
    {
        CanvasGroup canvasGroup = nodeDetailsPanel.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = nodeDetailsPanel.AddComponent<CanvasGroup>();
        }

        // Always disable raycast blocking for the node details panel
        canvasGroup.blocksRaycasts = false;

        if (show)
        {
            // Fade in
            nodeDetailsPanel.SetActive(true);
            canvasGroup.alpha = 0f;
            float time = 0f;
            const float duration = 0.2f;

            while (time < duration)
            {
                time += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(0f, 1f, time / duration);
                yield return null;
            }

            canvasGroup.alpha = 1f;
        }
        else
        {
            // Fade out
            float startAlpha = canvasGroup.alpha;
            float time = 0f;
            const float duration = 0.2f;

            while (time < duration)
            {
                time += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, time / duration);
                yield return null;
            }

            canvasGroup.alpha = 0f;
            nodeDetailsPanel.SetActive(false);
            currentActiveNode = null;
        }
    }
    
    private string GetNodeDescription(string nodeLabel)
    {
        if (string.IsNullOrEmpty(nodeLabel))
            return "Unknown node type.";
        
        string lowerLabel = nodeLabel.ToLower();
        
        // Conditions
        if (lowerLabel.Contains("if self"))
            return "Sets only the evaluation target as itself e.g. Ifself - Ifhp<90";
        if (lowerLabel.Contains("if enemy"))
            return "Checks for enemies in vision range. Passes to the output if an enemy is detected and sets it as both evaluation and current target.";
        if (lowerLabel.Contains("if ally"))
            return "Checks for allies in vision range. Passes to the output if an ally is detected and sets it as both evaluation and current target.";
        if (lowerLabel.Contains("if any"))
            return "Checks for any tank (enemy or ally) in vision range. Selects the closest tank as the evaluation and current target.";
        if (lowerLabel.Contains("if rifle"))
            return "Checks if the evaluation target has a rifle turret type.";
        if (lowerLabel.Contains("if hp"))
            return "Checks the HP of the evaluation target. Passes if target's HP meets the condition.";
        if (lowerLabel.Contains("if armor"))
            return "Checks if the evaluation target has a specific armor type.";
        if (lowerLabel.Contains("if range"))
            return "Checks the distance to the evaluation target. Passes if distance meets the condition.";
        if (lowerLabel.Contains("if my tag") || lowerLabel.Contains("if my tag"))
            return "Checks your personal tag on the evaluation target. Passes if the tag value meets the condition. Personal tags are only visible to you.";
        if (lowerLabel.Contains("if team tag") || lowerLabel.Contains("if team tag"))
            return "Checks the team tag on the evaluation target. Passes if the tag value meets the condition. Team tags are shared across all teammates.";
        
        // Actions - Movement
        
        if (lowerLabel.Contains("wander"))
            return "Selects a random point within 100 units of the tanks current location and attempts to move there";
        if (lowerLabel.Contains("forward"))
            return "Tank Drives forward, use with Cycle nodes to patrol an area";
        if (lowerLabel.Contains("rotate right"))
            return "Tank Pivots to the right by the specified degrees.";
        if (lowerLabel.Contains("rotate left"))
            return "Tank Pivots to the left by the specified degrees.";
        if (lowerLabel.Contains("wait"))
            return "Stops movement. Tank remains stationary.";
        if (lowerLabel.Contains("chase"))
            return "Continuously pursues the current target, closing distance. Useful for aggressive behavior.";
        if (lowerLabel.Contains("flee"))
            return "Moves away from the current target, can be used for maintaining or increasing distance.";
        if (lowerLabel.Contains("map center"))
            return "Navigates to the center of the map. Useful for controlling key positions.";
        if (lowerLabel.Contains("home"))
            return "Tank Returns to its spawn position.";
        
        // Actions - Turret
        if (lowerLabel.Contains("fire"))
            return "Fires the tanks weapon. Use leadtarget to have the turret aim before firing";
        if (lowerLabel.Contains("leadtarget") || lowerLabel.Contains("lead target"))
            return "Using this under a Fire node will force it to verify aim before shooting. 0 will point right at target, any other numbers will predict enemy position e.g. leadtarget 15";
        if (lowerLabel.Contains("align front"))
            return "Rotates turret to face forward relative to the tank body.";
        if (lowerLabel.Contains("align right"))
            return "Rotates turret to face right relative to the tank body.";
        if (lowerLabel.Contains("align left"))
            return "Rotates turret to face left relative to the tank body.";
        if (lowerLabel.Contains("align back"))
            return "Rotates turret to face backward relative to the tank body.";
        if (lowerLabel.Contains("rotate up"))
            return "Tilts turret upward by the specified degrees.";
        if (lowerLabel.Contains("rotate down"))
            return "Tilts turret downward by the specified degrees.";

        //Special Nodes
        if (lowerLabel.Contains("cycle"))
            return "Cycles through connected nodes in top to bottom sequence, entered value is number of seconds spent on each action. Returns to the first after completing all.";
        if (lowerLabel.Contains("if coms") || lowerLabel.Contains("if comms"))
            return "(Boolean Node)Target nodes used after this will have access to an ally target list in addition to their own vision (all tanks update their teams ally target list every 0.1 sec)";
        
        return "Custom node. Check the node label for behavior details.";
    }
}
