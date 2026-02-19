using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Opens Discord invite link when button is clicked
/// </summary>
public class DiscordButton : MonoBehaviour
{
    [Header("Discord Settings")]
    [Tooltip("Your Discord server invite link or permanent invite URL")]
    public string discordUrl = "https://discord.gg/yourInviteCode";
    
    private Button button;
    
    private void Start()
    {
        // Get the button component
        button = GetComponent<Button>();
        
        if (button == null)
        {
            Debug.LogError("[DiscordButton] No Button component found on this GameObject!");
            return;
        }
        
        // Add listener to open Discord when button is clicked
        button.onClick.AddListener(OpenDiscord);
    }
    
    /// <summary>
    /// Opens the Discord URL in the user's default browser
    /// </summary>
    public void OpenDiscord()
    {
        if (string.IsNullOrEmpty(discordUrl))
        {
            Debug.LogWarning("[DiscordButton] Discord URL is not set!");
            return;
        }
        
        Debug.Log($"[DiscordButton] Opening Discord URL: {discordUrl}");
        Application.OpenURL(discordUrl);
    }
}
