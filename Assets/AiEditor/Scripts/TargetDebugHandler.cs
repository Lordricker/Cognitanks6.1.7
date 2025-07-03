using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;


public class TargetDebugHandler : MonoBehaviour
{    [Header("Target Debug Settings")]
    public bool hasTargetFlag = false;

    private TextMeshProUGUI warningText;
    private bool isConditionNode = false;
    private bool isTargetNode = false;
    private float lastFlagReceived = 0f;    void Start()
    {
        // Always try to find the warning text component
        FindWarningText();

        // Check if this is a condition or target node
        CheckNodeType();

        // Start the appropriate coroutine based on node type
        if (isTargetNode)
        {
            StartCoroutine(BroadcastTargetFlag());
        }

        if (isConditionNode)
        {
            StartCoroutine(CheckForTargetFlag());
            // Condition nodes also need to broadcast if they receive a flag
            StartCoroutine(BroadcastTargetFlag());
        }
    }    private void FindWarningText()
    {
        // Since we're always on the parent MiddleNode, find the TargetDebug child
        Transform targetDebugTransform = transform.Find("TargetDebug");
        
        if (targetDebugTransform != null)
        {
            warningText = targetDebugTransform.GetComponent<TextMeshProUGUI>();
        }
        else
        {
            Debug.LogWarning($"[TargetDebug] {gameObject.name} - TargetDebug child object not found!");
        }
        
        // Make sure warning text starts inactive
        if (warningText != null)
        {
            warningText.gameObject.SetActive(false);
        }
    }
    private void CheckNodeType()
    {
        string nodeText = "";

        // Since we're on the parent MiddleNode, look for NodeText as a direct child
        Transform nodeTextTransform = transform.Find("NodeText");        if (nodeTextTransform != null)
        {
            TextMeshProUGUI nodeTextComponent = nodeTextTransform.GetComponent<TextMeshProUGUI>();
            
            if (nodeTextComponent != null)
            {
                nodeText = nodeTextComponent.text.ToLower();
            }
        }        if (string.IsNullOrEmpty(nodeText))
        {
            return;
        }
        
        // Check if this is a target node first (things that provide targets)
        isTargetNode = nodeText.Contains("if self") ||
                      nodeText.Contains("if enemy") ||
                      nodeText.Contains("if ally") ||
                      nodeText.Contains("if any");        // Check for condition nodes (things that need targets)
        // Any node that starts with "if" but is NOT a target node is a condition node
        // Also include nodes with common condition keywords
        // EXCLUDE self conditions as they don't need targets
        bool isSelfCondition = nodeText.Contains("if self") || 
                              nodeText.Contains("ifself") ||
                              nodeText.Contains("self hp") ||
                              nodeText.Contains("self ammo") ||
                              nodeText.Contains("self damaged") ||
                              nodeText.Contains("self reloading");
        
        isConditionNode = ((nodeText.StartsWith("if") && !isTargetNode && !isSelfCondition) ||
                         nodeText.Contains("condition") ||
                         nodeText.Contains("check") ||
                         nodeText.Contains("when") ||
                         nodeText.Contains("has ")) &&
                         !isSelfCondition; // Exclude self conditions
        
        // For HP, armor, range, etc. - only consider them condition nodes if they're NOT self conditions
        if (!isSelfCondition)
        {
            isConditionNode = isConditionNode ||
                             nodeText.Contains("hp") ||
                             nodeText.Contains("armor") ||
                             nodeText.Contains("range") ||
                             nodeText.Contains("rifle") ||
                             nodeText.Contains("weapon") ||
                             nodeText.Contains("tag");
        }
          if (isTargetNode)
        {
            hasTargetFlag = true;
        }
        
        if (isConditionNode && warningText != null)        {
            // Don't show warning initially - let the CheckForTargetFlag coroutine handle it after the delay
        }
    }
    
    // Target nodes broadcast their flag every 0.4 seconds
    private IEnumerator BroadcastTargetFlag()
    {
        while (gameObject != null)
        {
            yield return new WaitForSeconds(0.4f);
            // Any node with a target flag should propagate it (not just original target nodes)
            if (hasTargetFlag)
            {
                PropagateTargetFlag();
            }        }
    }
    
    // Condition nodes check for target flag every 0.5 seconds
    private IEnumerator CheckForTargetFlag()
    {
        // Wait 0.6 seconds after spawning before starting flag checks
        // This gives target nodes time to broadcast their flags first
        yield return new WaitForSeconds(0.6f);

        while (gameObject != null)
        {
            yield return new WaitForSeconds(0.5f);

            if (isConditionNode && warningText != null)
            {
                // Check if we received a flag recently (within last 0.6 seconds)
                bool hasRecentFlag = (Time.time - lastFlagReceived) < 0.6f;
                bool shouldShowWarning = !hasRecentFlag;
                warningText.gameObject.SetActive(shouldShowWarning);
            }        }
    }
    
    // Called when receiving a target flag from connected nodes
    public void ReceiveTargetFlag()
    {
        lastFlagReceived = Time.time;
        hasTargetFlag = true; // This node now has a target flag and can propagate it
          // Immediately propagate the flag to any nodes connected to our output
        PropagateTargetFlag();
    }    // Called when connections change - triggers immediate validation
    public void OnConnectionChanged()
    {
        // If this node has a target flag, immediately broadcast it
        if (hasTargetFlag)
        {
            PropagateTargetFlag();
        }
        
        // If this is a condition node, immediately check for target flags
        if (isConditionNode)
        {
            CheckTargetFlagImmediate();
        }
    }
    
    // Propagates target flag to connected input nodes through actual connections
    private void PropagateTargetFlag()
    {
        // Find all UILineConnector components in the scene
        UILineConnector[] allLines = FindObjectsByType<UILineConnector>(FindObjectsSortMode.None);
        
        foreach (var line in allLines)
        {
            // Check if this node is the OUTPUT (source) of this line
            if (line.outputRect != null)
            {
                // Check if the output belongs to this node
                NodeDraggable outputNode = line.outputRect.GetComponentInParent<NodeDraggable>();
                if (outputNode != null && outputNode.gameObject == this.gameObject)
                {
                    // This line starts from our node, so propagate to the connected input node
                    if (line.inputRect != null)
                    {
                        NodeDraggable inputNode = line.inputRect.GetComponentInParent<NodeDraggable>();
                        if (inputNode != null)                        {
                            TargetDebugHandler inputHandler = inputNode.GetComponent<TargetDebugHandler>();
                            
                            if (inputHandler != null)
                            {
                                // Propagate to ANY connected node (both condition and target nodes can receive flags)
                                inputHandler.ReceiveTargetFlag();
                            }
                        }
                    }
                }
            }
        }
    }    // Immediate check for target flags (used when connections change)
    private void CheckTargetFlagImmediate()
    {
        if (warningText == null) return;

        bool hasRecentTargetFlag = lastFlagReceived > 0 && (Time.time - lastFlagReceived) <= 0.6f;
        
        if (hasRecentTargetFlag && warningText.gameObject.activeSelf)
        {
            warningText.gameObject.SetActive(false);
        }
        else if (!hasRecentTargetFlag && !warningText.gameObject.activeSelf)
        {
            warningText.gameObject.SetActive(true);
        }
    }
}
