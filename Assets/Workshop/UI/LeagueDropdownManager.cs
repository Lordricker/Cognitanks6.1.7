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
        
        // Computed properties (read-only in inspector)
        public string SceneName => $"Arena{arenaNumber}";
        public string LeagueName => $"League{leagueNumber}";
        public string RoundName => $"Round{roundNumber}";
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
                        Debug.LogWarning($"[LeagueDropdownManager] Arena button {j} in league {i} has invalid numbers: Arena={config.arenaNumber}, League={config.leagueNumber}, Round={config.roundNumber}");
                        continue;
                    }
                    
                    Debug.Log($"[LeagueDropdownManager] Arena button {j} configured: {config.SceneName} (League {config.leagueNumber}, Round {config.roundNumber}, Weight Limit: {config.weightLimit}, Entry Fee: ${config.entryFee})");
                    
                    // Note: Arena buttons should have their OnClick set in the Inspector to call OnArenaButtonClicked(config.SceneName)
                    // The method now includes full validation based on the config data
                }
                else
                {
                    Debug.LogWarning($"[LeagueDropdownManager] Arena button {j} in league {i} is null!");
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

    // Call this from the OnClick of any arena button in the Inspector
    public void OnArenaButtonClicked(string sceneName)
    {
        Debug.Log($"[LeagueDropdownManager] OnArenaButtonClicked called with sceneName={sceneName}");
        
        // Validate that at least one tank is active before starting the match
        if (!ValidateActiveTanks())
        {
            ShowError("No Active Tanks!");
            return;
        }
        
        // Find the arena config for this scene to get validation parameters
        ArenaButtonConfig config = null;
        string leagueName = "";
        string roundName = "";
        
        foreach (var league in leagues)
        {
            foreach (var arenaConfig in league.arenaButtons)
            {
                if (arenaConfig.SceneName == sceneName)
                {
                    config = arenaConfig;
                    leagueName = arenaConfig.LeagueName;
                    roundName = arenaConfig.RoundName;
                    break;
                }
            }
            if (config != null) break;
        }
        
        if (config == null)
        {
            Debug.LogError($"[LeagueDropdownManager] Could not find config for scene {sceneName}");
            ShowError("Arena configuration error!");
            return;
        }
        
        // Validate weight limit
        float totalWeight = CalculateTotalActiveTankWeight();
        Debug.Log($"[LeagueDropdownManager] Weight validation: totalWeight={totalWeight:F1}, weightLimit={config.weightLimit:F1}, condition={(config.weightLimit > 0 && totalWeight > config.weightLimit)}");
        if (config.weightLimit > 0 && totalWeight > config.weightLimit)
        {
            ShowError($"Weight Limit Exceeded! Total: {totalWeight:F1}kg / Limit: {config.weightLimit:F1}kg");
            Debug.Log($"[LeagueDropdownManager] Weight limit exceeded - blocking arena entry");
            return;
        }
        
        // Validate entry fee (check player cash)
        if (config.entryFee > 0)
        {
            var playerDataManager = PlayerDataManager.Instance;
            int currentCash = playerDataManager != null ? playerDataManager.GetPlayerCash() : 0;
            Debug.Log($"[LeagueDropdownManager] Cash validation: currentCash=${currentCash}, entryFee=${config.entryFee}, condition={(playerDataManager == null || currentCash < config.entryFee)}");
            if (playerDataManager == null || currentCash < config.entryFee)
            {
                ShowError($"Insufficient Funds! Need: ${config.entryFee} / Have: ${currentCash}");
                Debug.Log($"[LeagueDropdownManager] Insufficient funds - blocking arena entry");
                return;
            }
            
            // Deduct entry fee
            playerDataManager.SpendPlayerCash(config.entryFee);
            Debug.Log($"[LeagueDropdownManager] Deducted entry fee: ${config.entryFee}");
        }
        
        // Set PlayerPrefs so ArenaManager knows which enemies to load
        PlayerPrefs.SetString("SelectedLeague", leagueName);
        PlayerPrefs.SetString("SelectedRound", roundName);
        PlayerPrefs.Save();
        
        Debug.Log($"[LeagueDropdownManager] Set PlayerPrefs - League: {leagueName}, Round: {roundName}");
        Debug.Log($"[LeagueDropdownManager] Validation passed - Weight: {totalWeight:F1}kg / {config.weightLimit:F1}kg, Entry Fee: ${config.entryFee}");
        Debug.Log($"[LeagueDropdownManager] Loading arena scene: {sceneName}");
        
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
    /// Calculates total weight of all active tanks
    /// </summary>
    private float CalculateTotalActiveTankWeight()
    {
        if (TankSlotJsonManager.Instance == null)
        {
            Debug.LogError("[LeagueDropdownManager] TankSlotJsonManager.Instance is null!");
            return 0f;
        }
        
        var activeTanks = TankSlotJsonManager.Instance.GetActiveTankSlots();
        float totalWeight = 0f;
        
        foreach (var tank in activeTanks)
        {
            totalWeight += tank.totalWeight;
        }
        
        Debug.Log($"[LeagueDropdownManager] Calculated total active tank weight: {totalWeight:F1}kg ({activeTanks.Count} active tanks)");
        return totalWeight;
    }
    
    /// <summary>
    /// Shows an error message to the user using WorkshopUIManager's debug message system
    /// </summary>
    private void ShowError(string message)
    {
        Debug.LogWarning($"[LeagueDropdownManager] {message}");
        
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
}