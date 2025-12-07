using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class LeagueDropdownManager : MonoBehaviour
{
    [System.Serializable]
    public class LeagueDropdown
    {
        public Button leagueButton; // The top-level league button
        public GameObject arenaListPanel; // The panel containing arena buttons for this league
        public List<Button> arenaButtons; // Assign arena buttons in Inspector (Round1, Round2, etc.)
    }

    public List<LeagueDropdown> leagues; // Assign in Inspector
    
    [Header("Error Display")]
    public TMP_Text errorText; // Optional: Assign a TMP_Text to display error messages
    public float errorDisplayDuration = 3f; // How long to show error messages

    void Start()
    {
        for (int i = 0; i < leagues.Count; i++)
        {
            int index = i; // Capture index for closure
            leagues[i].leagueButton.onClick.AddListener(() => OnLeagueButtonClicked(index));
            // Start with all collapsed
            if (leagues[i].arenaListPanel != null)
                leagues[i].arenaListPanel.SetActive(false);
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

    // Call this from the OnClick of any arena button in the Inspector
    public void OnArenaButtonClicked(string sceneName)
    {
        // Validate that at least one tank is active before starting the match
        if (!ValidateActiveTanks())
        {
            ShowError("No Active Tanks!");
            return;
        }
        
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
