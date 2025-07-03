using UnityEngine;
using UnityEngine.SceneManagement;

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
    /// Load Arena1 scene for tank battles
    /// </summary>
    public void LoadArena1()
    {
        if (showDebugMessages)
            Debug.Log("Loading Arena1 scene...");
        
        UnityEngine.SceneManagement.SceneManager.LoadScene("Arena1");
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
