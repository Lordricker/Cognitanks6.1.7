using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// Simple scene management script for switching between game scenes.
/// Wire the public methods to UI buttons.
/// </summary>
public class SceneManager : MonoBehaviour
{
    [Header("Scene Loading")]
    [Tooltip("Show debug messages when switching scenes")]
    public bool showDebugMessages = true;

    /// <summary>
    /// Load the AI Editor scene for designing tank AI
    /// </summary>
    public void LoadAIEditor()
    {
        if (showDebugMessages)
            Debug.Log("Loading AI Editor scene...");
        
        UnityEngine.SceneManagement.SceneManager.LoadScene("AiEditor");
    }

    /// <summary>
    /// Load the Shop scene for buying/customizing tanks
    /// </summary>
    public void LoadShop()
    {
        if (showDebugMessages)
            Debug.Log("Loading Shop scene...");
        
        UnityEngine.SceneManagement.SceneManager.LoadScene("Shop");
    }

    /// <summary>
    /// Load the Multiplayer scene for online matches.
    /// Validates that all active tank slots have all 5 required components first.
    /// </summary>
    public void LoadMultiplayer()
    {
        string error = ValidateActiveTankComponents();
        if (!string.IsNullOrEmpty(error))
        {
            ShowValidationError(error);
            return;
        }
        
        if (showDebugMessages)
            Debug.Log("Loading Multiplayer scene...");
        
        UnityEngine.SceneManagement.SceneManager.LoadScene("Multiplayer");
    }

    /// <summary>
    /// Load Arena1 scene for tank battles.
    /// Validates that all active tank slots have all 5 required components first.
    /// </summary>
    public void LoadArena1()
    {
        string error = ValidateActiveTankComponents();
        if (!string.IsNullOrEmpty(error))
        {
            ShowValidationError(error);
            return;
        }
        
        // Advance tutorial step when entering the arena
        AdvanceTutorialStep();
        
        if (showDebugMessages)
            Debug.Log("Loading Arena1 scene...");
        
        UnityEngine.SceneManagement.SceneManager.LoadScene("Arena1");
    }
    
    /// <summary>
    /// Advances the tutorial step when the player enters the arena.
    /// Step 0 → 1 (after first fight), Step 1 → 2 (after second fight).
    /// </summary>
    private void AdvanceTutorialStep()
    {
        if (PlayerDataManager.Instance == null) return;
        
        int step = PlayerDataManager.Instance.playerData.tutorialStep;
        if (step < 2)
        {
            PlayerDataManager.Instance.playerData.tutorialStep = step + 1;
            PlayerDataManager.Instance.SavePlayerData();
            Debug.Log($"[SceneManager] Tutorial step advanced: {step} → {step + 1}");
        }
    }

    /// <summary>
    /// Validates all active tank slots have all 5 required components.
    /// Returns an error string if invalid, or empty string if all good.
    /// </summary>
    public static string ValidateActiveTankComponents()
    {
        if (TankSlotJsonManager.Instance == null)
            return "Tank data not available!";
        
        var activeTanks = TankSlotJsonManager.Instance.GetActiveTankSlots();
        
        if (activeTanks == null || activeTanks.Count == 0)
            return "No Active Tanks!";
        
        foreach (var tank in activeTanks)
        {
            int tankNumber = tank.slotIndex + 1; // Display as 1-based for user
            
            if (string.IsNullOrEmpty(tank.engineFrameInstanceId))
                return $"Tank {tankNumber} is missing Engine Frame";
            
            if (string.IsNullOrEmpty(tank.armorInstanceId))
                return $"Tank {tankNumber} is missing Armor";
            
            if (string.IsNullOrEmpty(tank.turretInstanceId))
                return $"Tank {tankNumber} is missing Turret";
            
            if (string.IsNullOrEmpty(tank.turretAIInstanceId))
                return $"Tank {tankNumber} is missing Turret AI";
            
            if (string.IsNullOrEmpty(tank.navAIInstanceId))
                return $"Tank {tankNumber} is missing Nav AI";
        }
        
        return ""; // All valid
    }
    
    /// <summary>
    /// Show a validation error using WorkshopUIManager if available, or Debug.LogWarning
    /// </summary>
    private void ShowValidationError(string message)
    {
        Debug.LogWarning($"[SceneManager] Validation failed: {message}");
        
        // Try WorkshopUIManager (shop scene)
        var workshopUI = Object.FindFirstObjectByType<WorkshopUIManager>();
        if (workshopUI != null)
        {
            workshopUI.ShowDebugMessage(message, 3f);
            return;
        }
        
        // Try LeagueDropdownManager error display
        // Fall through to just the debug log if no UI is available
    }

    /// <summary>
    /// Reload the current scene
    /// </summary>
    public void ReloadCurrentScene()
    {
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        
        if (showDebugMessages)
            Debug.Log($"Reloading current scene: {currentScene}");
        
        UnityEngine.SceneManagement.SceneManager.LoadScene(currentScene);
    }

    /// <summary>
    /// Quit the application (works in builds, not in editor)
    /// </summary>
    public void QuitGame()
    {
        if (showDebugMessages)
            Debug.Log("Quitting game...");
        
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }

    /// <summary>
    /// Load a scene by name (generic method)
    /// </summary>
    /// <param name="sceneName">Name of the scene to load</param>
    public void LoadScene(string sceneName)
    {
        if (showDebugMessages)
            Debug.Log($"Loading scene: {sceneName}");
        
        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
    }

    /// <summary>
    /// Check if a scene exists in the build settings
    /// </summary>
    /// <param name="sceneName">Name of the scene to check</param>
    /// <returns>True if scene exists in build settings</returns>
    public bool SceneExists(string sceneName)
    {
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings; i++)
        {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
            string sceneNameFromPath = System.IO.Path.GetFileNameWithoutExtension(scenePath);
            
            if (sceneNameFromPath == sceneName)
                return true;
        }
        return false;
    }
}
