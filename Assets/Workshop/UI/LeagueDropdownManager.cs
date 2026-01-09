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
        public List<ArenaButtonConfig> arenaButtons; // Assign arena buttons and their configs in Inspector
    }

    [System.Serializable]
    public class ArenaButtonConfig
    {
        public Button button; // The arena button
        public int arenaNumber; // Arena scene number (1 = Arena1, 2 = Arena2, etc.)
        public int leagueNumber; // League folder number (1 = League1, 2 = League2, etc.)
        public int roundNumber; // Round folder number (1 = Round1, 2 = Round2, etc.)
        public float weightLimit = 70f; // Maximum total weight of all active tanks
        public int entryFee = 100; // Entry fee cost in cash
        public GameObject progressUnlock; // Grey box image that blocks this arena until unlocked by completing a previous arena
        
        [Header("Unlock Rewards")]
        [Tooltip("Components that will be unlocked and added to the shop when this arena is completed")]
        public List<ComponentData> componentRewards = new List<ComponentData>(); // Components to unlock when arena is won
        
        // Computed properties (read-only in inspector)
        public string SceneName => $"Arena{arenaNumber}";
        public string LeagueName => $"League{leagueNumber}";
        public string RoundName => $"Round{roundNumber}";
        public string ArenaKey => $"{LeagueName}_{RoundName}_Arena{arenaNumber}"; // Unique identifier for save data
    }

    public List<LeagueDropdown> leagues; // Assign in Inspector
    
    [Header("Error Display")]
    public TMP_Text errorText; // Optional: Assign a TMP_Text to display error messages
    public float errorDisplayDuration = 3f; // How long to show error messages

    void Start()
    {
        // Check for completed arenas and unlock progress blockers
        CheckAndUnlockProgressBlockers();
        
        for (int i = 0; i < leagues.Count; i++)
        {
            int index = i; // Capture index for closure
            leagues[i].leagueButton.onClick.AddListener(() => OnLeagueButtonClicked(index));
            // Start with all collapsed
            if (leagues[i].arenaListPanel != null)
                leagues[i].arenaListPanel.SetActive(false);
            
            // Set up arena button listeners
            for (int j = 0; j < leagues[i].arenaButtons.Count; j++)
            {
                int leagueIndex = i;
                int arenaIndex = j;
                var config = leagues[i].arenaButtons[j];
                if (config.button != null)
                {
                    // Validate configuration
                    if (config.arenaNumber <= 0 || config.leagueNumber <= 0 || config.roundNumber <= 0)
                    {
                        Debug.LogWarning($"[LeagueDropdownManager] Arena button {j} in league {i} has invalid numbers");
                        continue;
                    }
                    
                    // Set up the button listener programmatically with captured config values
                    var capturedConfig = config; // Capture for closure
                    config.button.onClick.RemoveAllListeners(); // Clear any existing listeners
                    config.button.onClick.AddListener(() => {
                        OnArenaButtonClicked(
                            capturedConfig.SceneName, 
                            capturedConfig.LeagueName, 
                            capturedConfig.RoundName, 
                            capturedConfig.weightLimit, 
                            capturedConfig.entryFee,
                            capturedConfig.ArenaKey,
                            capturedConfig.componentRewards
                        );
                    });
                }
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

    // Called programmatically by button listeners set up in Start()
    public void OnArenaButtonClicked(string sceneName, string leagueName, string roundName, float weightLimit, int entryFee, string arenaKey, List<ComponentData> componentRewards)
    {
        // Validate that at least one tank is active before starting the match
        if (!ValidateActiveTanks())
        {
            ShowError("No Active Tanks!");
            return;
        }
        
        // Validate weight limit
        float totalWeight = CalculateTotalActiveTankWeight();
        if (weightLimit > 0 && totalWeight > weightLimit)
        {
            ShowError($"Weight Limit Exceeded! Total: {totalWeight:F1}kg / Limit: {weightLimit:F1}kg");
            return;
        }
        
        // Validate entry fee (check player cash)
        if (entryFee > 0)
        {
            var playerDataManager = PlayerDataManager.Instance;
            int currentCash = playerDataManager != null ? playerDataManager.GetPlayerCash() : 0;
            if (playerDataManager == null || currentCash < entryFee)
            {
                ShowError($"Insufficient Funds! Need: ${entryFee} / Have: ${currentCash}");
                return;
            }
            
            // Deduct entry fee
            playerDataManager.SpendPlayerCash(entryFee);
        }
        
        // Save component rewards to PlayerPrefs so ArenaManager can unlock them on completion
        if (componentRewards != null && componentRewards.Count > 0)
        {
            string rewardsJson = string.Join(",", componentRewards.ConvertAll(c => c.id));
            PlayerPrefs.SetString($"ArenaRewards_{arenaKey}", rewardsJson);
        }
        
        // Set PlayerPrefs so ArenaManager knows which enemies to load and rewards to give
        PlayerPrefs.SetString("SelectedLeague", leagueName);
        PlayerPrefs.SetString("SelectedRound", roundName);
        PlayerPrefs.SetString("SelectedArenaKey", arenaKey);
        PlayerPrefs.SetInt("ArenaEntryFee", entryFee);
        PlayerPrefs.Save();
        
        Debug.Log($"[LeagueDropdownManager] Loading {leagueName}/{roundName} - {sceneName}");
        
        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
    }
    
    /// <summary>
    /// Checks if at least one tank slot is active
    /// </summary>
    private bool ValidateActiveTanks()
    {
        if (TankSlotJsonManager.Instance == null) return false;
        
        var activeTanks = TankSlotJsonManager.Instance.GetActiveTankSlots();
        return activeTanks != null && activeTanks.Count > 0;
    }
    
    /// <summary>
    /// Calculates total weight of all active tanks
    /// </summary>
    private float CalculateTotalActiveTankWeight()
    {
        if (TankSlotJsonManager.Instance == null) return 0f;
        
        var activeTanks = TankSlotJsonManager.Instance.GetActiveTankSlots();
        float totalWeight = 0f;
        
        foreach (var tank in activeTanks)
        {
            totalWeight += tank.totalWeight;
        }
        
        return totalWeight;
    }
    
    /// <summary>
    /// Shows an error message to the user using WorkshopUIManager's debug message system
    /// </summary>
    private void ShowError(string message)
    {
        // Play error sound
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayErrorSound();
        }
        
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
    
    /// <summary>
    /// Checks PlayerPrefs for completed arenas and unlocks their associated progress blockers
    /// </summary>
    private void CheckAndUnlockProgressBlockers()
    {
        foreach (var league in leagues)
        {
            foreach (var arenaConfig in league.arenaButtons)
            {
                if (arenaConfig.progressUnlock != null)
                {
                    // Check if this arena has been completed
                    bool isCompleted = PlayerPrefs.GetInt($"ArenaCompleted_{arenaConfig.ArenaKey}", 0) == 1;
                    
                    if (isCompleted)
                    {
                        // Deactivate the progress blocker
                        arenaConfig.progressUnlock.SetActive(false);
                    }
                }
            }
        }
    }
}