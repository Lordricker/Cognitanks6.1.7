using UnityEngine;
using TMPro;

/// <summary>
/// Displays the current AI branch (Nav and Turret) for the tank being followed by the camera.
/// Only shows when camera is following a specific tank, hidden when in global view.
/// </summary>
public class TankAIDebugUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text navDebugText;
    [SerializeField] private TMP_Text turretDebugText;
    
    [Header("Camera Reference")]
    [SerializeField] private CameraController cameraController;
    
    private TankMan currentTrackedTank;
    private Transform globalAnchor;
    
    void Start()
    {
        // Find UI elements if not assigned
        if (navDebugText == null)
        {
            GameObject navDebugObj = GameObject.Find("NavDebug");
            if (navDebugObj != null)
                navDebugText = navDebugObj.GetComponent<TMP_Text>();
        }
        
        if (turretDebugText == null)
        {
            GameObject turretDebugObj = GameObject.Find("TurretDebug");
            if (turretDebugObj != null)
                turretDebugText = turretDebugObj.GetComponent<TMP_Text>();
        }
        
        // Find camera controller if not assigned
        if (cameraController == null)
        {
            cameraController = FindFirstObjectByType<CameraController>();
        }
        
        // Store reference to global anchor
        if (cameraController != null)
        {
            globalAnchor = cameraController.globalAnchor;
        }
        
        // Start with text hidden (game starts in global view)
        if (navDebugText != null)
            navDebugText.gameObject.SetActive(false);
        if (turretDebugText != null)
            turretDebugText.gameObject.SetActive(false);
    }
    
    void Update()
    {
        if (cameraController == null || navDebugText == null || turretDebugText == null)
            return;
        
        // Check which anchor the camera is currently targeting
        Transform currentAnchor = GetCurrentCameraAnchor();
        
        // If we're at the global anchor, hide the debug text
        if (currentAnchor == globalAnchor || currentAnchor == null)
        {
            navDebugText.gameObject.SetActive(false);
            turretDebugText.gameObject.SetActive(false);
            currentTrackedTank = null;
            return;
        }
        
        // We're following a specific tank - find which one
        TankMan trackedTank = FindTankForAnchor(currentAnchor);
        
        if (trackedTank != null)
        {
            // Show debug text
            if (!navDebugText.gameObject.activeSelf)
                navDebugText.gameObject.SetActive(true);
            if (!turretDebugText.gameObject.activeSelf)
                turretDebugText.gameObject.SetActive(true);
            
            currentTrackedTank = trackedTank;
            
            // Update debug text with current AI chains
            UpdateDebugText();
        }
        else
        {
            // No tank found, hide text
            navDebugText.gameObject.SetActive(false);
            turretDebugText.gameObject.SetActive(false);
            currentTrackedTank = null;
        }
    }
    
    /// <summary>
    /// Gets the current anchor that the camera is targeting
    /// </summary>
    private Transform GetCurrentCameraAnchor()
    {
        // Use reflection to access the private targetAnchor field
        var field = typeof(CameraController).GetField("targetAnchor", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (field != null)
        {
            return (Transform)field.GetValue(cameraController);
        }
        
        return null;
    }
    
    /// <summary>
    /// Finds the TankMan component for a given camera anchor
    /// </summary>
    private TankMan FindTankForAnchor(Transform anchor)
    {
        if (anchor == null) return null;
        
        // Camera anchors are children of tank objects
        // Walk up the hierarchy to find the TankMan component
        Transform current = anchor;
        while (current != null)
        {
            TankMan tankMan = current.GetComponent<TankMan>();
            if (tankMan != null)
                return tankMan;
            
            current = current.parent;
        }
        
        return null;
    }
    
    /// <summary>
    /// Updates the debug text with the current AI chains from the tracked tank
    /// </summary>
    private void UpdateDebugText()
    {
        if (currentTrackedTank == null)
            return;
        
        // Access the lastLoggedNavChain and lastLoggedTurretChain from TankMan using reflection
        var navChainField = typeof(TankMan).GetField("lastLoggedNavChain", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var turretChainField = typeof(TankMan).GetField("lastLoggedTurretChain", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (navChainField != null && navDebugText != null)
        {
            string navChain = (string)navChainField.GetValue(currentTrackedTank);
            if (!string.IsNullOrEmpty(navChain))
            {
                navDebugText.text = $"[Nav] {navChain}";
            }
            else
            {
                navDebugText.text = "[Nav] (No active branch)";
            }
        }
        
        if (turretChainField != null && turretDebugText != null)
        {
            string turretChain = (string)turretChainField.GetValue(currentTrackedTank);
            if (!string.IsNullOrEmpty(turretChain))
            {
                turretDebugText.text = $"[Turret] {turretChain}";
            }
            else
            {
                turretDebugText.text = "[Turret] (No active branch)";
            }
        }
    }
}
