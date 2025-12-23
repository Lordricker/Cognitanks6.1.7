using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class LeagueDropdownManager : MonoBehaviour
{
    [System.Serializable]
    public class LeagueDropdown
    {
        public string leagueName = "League1"; // e.g., "League1", "League2", etc.
        public Button leagueButton; // The top-level league button
        public GameObject arenaListPanel; // The panel containing arena buttons for this league
        public List<ArenaButtonInfo> arenaButtons; // Assign arena buttons in Inspector
    }
    
    [System.Serializable]
    public class ArenaButtonInfo
    {
        public string roundName = "Round1"; // e.g., "Round1", "Round2", etc.
        public Button button; // The arena button
        public string sceneName = "Arena1"; // The scene to load (e.g., "Arena1")
    }

    public List<LeagueDropdown> leagues; // Assign in Inspector
    
    [Header("Error Display")]
    public TMP_Text errorText; // Optional: Assign a TMP_Text to display error messages
    public float errorDisplayDuration = 3f; // How long to show error messages

    void Start()
    {
        // Setup league buttons
        for (int i = 0; i < leagues.Count; i++)
        {
            int index = i; // Capture index for closure
            leagues[i].leagueButton.onClick.AddListener(() => OnLeagueButtonClicked(index));
            
            // Start with all collapsed
            if (leagues[i].arenaListPanel != null)
                leagues[i].arenaListPanel.SetActive(false);
                
            // Setup arena buttons for this league
            for (int j = 0; j < leagues[i].arenaButtons.Count; j++)
            {
                int leagueIdx = i;
                int arenaIdx = j;
                leagues[i].arenaButtons[j].button.onClick.AddListener(() => 
                    OnArenaButtonClicked(leagueIdx, arenaIdx));
            }
        }
    }

    void OnLeagueButtonClicked(int clickedIndex)
    {
        for (int i = 0; i < leagues.Count; i++)
        {
            if (leagues[i].arenaListPanel != null)
                leagues[i].arenaListPanel.SetActive(i == clickedIndex && !leagues[i].arenaListPanel.activeSelf);
        }
    }

    // Called when an arena button is clicked
    private void OnArenaButtonClicked(int leagueIndex, int arenaIndex)
    {
        // Validate that at least one tank is active before starting the match
        if (!ValidateActiveTanks())
        {
            ShowError("No Active Tanks!");
            return;
        }
        
        // Get league and round info
        string leagueName = leagues[leagueIndex].leagueName;
        string roundName = leagues[leagueIndex].arenaButtons[arenaIndex].roundName;
        string sceneName = leagues[leagueIndex].arenaButtons[arenaIndex].sceneName;
        
        // Save selected league and round to PlayerPrefs so ArenaManager can load correct enemies
        PlayerPrefs.SetString("SelectedLeague", leagueName);
        PlayerPrefs.SetString("SelectedRound", roundName);
        PlayerPrefs.Save();
        
        Debug.Log($"[LeagueDropdownManager] Loading {leagueName}/{roundName} (Scene: {sceneName})");
        
        // Load the arena scene
        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
    }
    
    /// <summary>
    /// Checks if at least one tank slot is active
    /// </summary>
    private bool ValidateActiveTanks()
    {
        if (TankSlotJsonManager.Instance == null)
        {
            Debug.LogError("[LeagueDropdownManager] TankSlotJsonManager.Instance is null!");
            return false;
        }
        
        var activeTanks = TankSlotJsonManager.Instance.GetActiveTankSlots();
        
        if (activeTanks == null || activeTanks.Count == 0)
        {
            Debug.LogWarning("[LeagueDropdownManager] No active tanks found!");
            return false;
        }
        
        Debug.Log($"[LeagueDropdownManager] Found {activeTanks.Count} active tank(s)");
        return true;
    }
    
    /// <summary>
    /// Shows an error message to the user using WorkshopUIManager's debug message system
    /// </summary>
    private void ShowError(string message)
    {
        Debug.LogWarning($"[LeagueDropdownManager] {message}");
        
        // Use WorkshopUIManager's existing ShowDebugMessage method for consistent styling
        var workshopUI = FindFirstObjectByType<WorkshopUIManager>();
        if (workshopUI != null)
        {
            workshopUI.ShowDebugMessage(message, errorDisplayDuration);
        }
        else if (errorText != null)
        {
            // Fallback: use our own error text if WorkshopUIManager is not available
            errorText.text = message;
            errorText.color = Color.red;
            errorText.gameObject.SetActive(true);
        }
    }
}
