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
        public Sprite leagueArenaImage; // Image to display when this league is selected (pic of the arena)
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
        
        [Tooltip("AI JSON files (TextAssets) that will be copied to the shop folders when this arena is completed")]
        public List<TextAsset> aiFileRewards = new List<TextAsset>(); // AI files to unlock when arena is won
        
        // Computed properties (read-only in inspector)
        public string SceneName => $"Arena{arenaNumber}";
        public string LeagueName => $"League{leagueNumber}";
        public string RoundName => $"Round{roundNumber}";
        public string ArenaKey => $"{LeagueName}_{RoundName}_Arena{arenaNumber}"; // Unique identifier for save data
    }

    public List<LeagueDropdown> leagues; // Assign in Inspector
    
    [Header("Arena Image Display")]
    [Tooltip("Image component that will display the arena picture when a league is clicked")]
    public UnityEngine.UI.Image arenaImageDisplay;
    
    [Header("Scene Transition")]
    [Tooltip("Reference to the PortalTransition component for fancy scene transitions")]
    public PortalTransition portalTransition;

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
                            capturedConfig.componentRewards,
                            capturedConfig.aiFileRewards
                        );
                    });
                }
            }
        }
    }

    void OnLeagueButtonClicked(int clickedIndex)
    {
        bool toggledOn = false;
        
        for (int i = 0; i < leagues.Count; i++)
        {
            bool isActive = (i == clickedIndex && !leagues[i].arenaListPanel.activeSelf);
            if (leagues[i].arenaListPanel != null)
                leagues[i].arenaListPanel.SetActive(isActive);
            
            if (isActive)
                toggledOn = true;
        }
        
        // Show the arena image for the clicked league if it was opened
        if (toggledOn)
        {
            ShowLeagueArenaImage(clickedIndex);
        }
        else if (arenaImageDisplay != null)
        {
            // Hide image when all leagues are collapsed
            arenaImageDisplay.gameObject.SetActive(false);
        }
    }
    
    /// <summary>
    /// Shows the arena image for the specified league
    /// </summary>
    /// <param name="leagueIndex">Index of the league whose image should be displayed</param>
    void ShowLeagueArenaImage(int leagueIndex)
    {
        if (arenaImageDisplay == null)
        {
            Debug.LogWarning("[LeagueDropdownManager] Arena image display not assigned!");
            return;
        }
        
        if (leagueIndex < 0 || leagueIndex >= leagues.Count)
        {
            Debug.LogWarning($"[LeagueDropdownManager] Invalid league index: {leagueIndex}");
            return;
        }
        
        var league = leagues[leagueIndex];
        
        // If the league has an image, show it
        if (league.leagueArenaImage != null)
        {
            arenaImageDisplay.sprite = league.leagueArenaImage;
            arenaImageDisplay.gameObject.SetActive(true);
        }
        else
        {
            // No image assigned for this league - hide the image display
            arenaImageDisplay.gameObject.SetActive(false);
        }
    }

    // Called programmatically by button listeners set up in Start()
    public void OnArenaButtonClicked(string sceneName, string leagueName, string roundName, float weightLimit, int entryFee, string arenaKey, List<ComponentData> componentRewards, List<TextAsset> aiFileRewards)
    {
        // Validate that at least one tank is active before starting the match
        if (!ValidateActiveTanks())
        {
            ShowError("No Active Tanks!");
            return;
        }
        
        // Validate that all active tanks have all required components
        string missingComponentError = ValidateTankComponents();
        if (!string.IsNullOrEmpty(missingComponentError))
        {
            ShowError(missingComponentError);
            return;
        }
        
        // Validate individual tank weight capacities
        string weightCapacityError = ValidateTankWeightCapacities();
        if (!string.IsNullOrEmpty(weightCapacityError))
        {
            ShowError(weightCapacityError);
            return;
        }
        
        // Validate total weight limit for the arena
        float totalWeight = CalculateTotalActiveTankWeight();
        if (weightLimit > 0 && totalWeight > weightLimit)
        {
            ShowError($"Weight Limit Exceeded! Total: {totalWeight:F1}kg / Limit: {weightLimit:F1}kg");
            return;
        }
        
        // Validate entry fee (check player cash)
        // First round (League1, Round1) is free to prevent softlock from running out of money
        bool isFirstRound = leagueName == "League1" && roundName == "Round1";
        
        if (entryFee > 0 && !isFirstRound)
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
        
        // Store whether this is the first round for reward calculation
        PlayerPrefs.SetInt("IsFirstRound", isFirstRound ? 1 : 0);
        
        // Save component rewards to PlayerPrefs so ArenaManager can unlock them on completion
        if (componentRewards != null && componentRewards.Count > 0)
        {
            string rewardsJson = string.Join(",", componentRewards.ConvertAll(c => c.id));
            PlayerPrefs.SetString($"ArenaRewards_{arenaKey}", rewardsJson);
        }
        
        // Save AI file rewards to PlayerPrefs so ArenaManager can unlock them on completion
        if (aiFileRewards != null && aiFileRewards.Count > 0)
        {
            string aiRewardsJson = string.Join(",", aiFileRewards.ConvertAll(a => a.name));
            PlayerPrefs.SetString($"ArenaAIRewards_{arenaKey}", aiRewardsJson);
        }
        
        // Set PlayerPrefs so ArenaManager knows which enemies to load and rewards to give
        PlayerPrefs.SetString("SelectedLeague", leagueName);
        PlayerPrefs.SetString("SelectedRound", roundName);
        PlayerPrefs.SetString("SelectedArenaKey", arenaKey);
        PlayerPrefs.SetInt("ArenaEntryFee", entryFee);
        PlayerPrefs.Save();
        
        Debug.Log($"[LeagueDropdownManager] Loading {leagueName}/{roundName} - {sceneName}");
        
        // Use portal transition if available, otherwise load directly
        if (portalTransition != null)
        {
            portalTransition.StartTransition(sceneName);
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
        }
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
    /// Validates that all active tanks have all 5 required components
    /// Returns error message if missing, or empty string if valid
    /// </summary>
    private string ValidateTankComponents()
    {
        if (TankSlotJsonManager.Instance == null) return "Tank data not available!";
        
        var activeTanks = TankSlotJsonManager.Instance.GetActiveTankSlots();
        
        foreach (var tank in activeTanks)
        {
            int tankNumber = tank.slotIndex + 1; // Display as 1-based for user
            
            // Check Turret
            if (string.IsNullOrEmpty(tank.turretInstanceId))
            {
                return $"Tank {tankNumber} missing Turret";
            }
            
            // Check Armor
            if (string.IsNullOrEmpty(tank.armorInstanceId))
            {
                return $"Tank {tankNumber} missing Armor";
            }
            
            // Check Engine Frame
            if (string.IsNullOrEmpty(tank.engineFrameInstanceId))
            {
                return $"Tank {tankNumber} missing Engine Frame";
            }
            
            // Check Turret AI
            if (string.IsNullOrEmpty(tank.turretAIInstanceId))
            {
                return $"Tank {tankNumber} missing Turret AI";
            }
            
            // Check Nav AI
            if (string.IsNullOrEmpty(tank.navAIInstanceId))
            {
                return $"Tank {tankNumber} missing Nav AI";
            }
        }
        
        return string.Empty; // All tanks valid
    }
    
    /// <summary>
    /// Validates that each active tank's total weight does not exceed its engine frame capacity
    /// Returns error message if exceeded, or empty string if valid
    /// </summary>
    private string ValidateTankWeightCapacities()
    {
        if (TankSlotJsonManager.Instance == null) return "Tank data not available!";
        
        var activeTanks = TankSlotJsonManager.Instance.GetActiveTankSlots();
        
        foreach (var tank in activeTanks)
        {
            int tankNumber = tank.slotIndex + 1; // Display as 1-based for user
            
            // Skip if no engine frame (already caught by component validation)
            if (string.IsNullOrEmpty(tank.engineFrameInstanceId) || tank.engineWeightCapacity <= 0)
                continue;
            
            // Check if total slot weight exceeds engine frame capacity
            // totalWeight includes all components: armor + turret + engine + AI
            if (tank.totalWeight > tank.engineWeightCapacity)
            {
                return $"Tank {tankNumber} is overweight";
            }
        }
        
        return string.Empty; // All tanks within capacity
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
                // Check if this arena has been completed
                bool isCompleted = PlayerPrefs.GetInt($"ArenaCompleted_{arenaConfig.ArenaKey}", 0) == 1;
                
                if (isCompleted)
                {
                    // Deactivate the progress blocker
                    if (arenaConfig.progressUnlock != null)
                    {
                        arenaConfig.progressUnlock.SetActive(false);
                    }
                    
                    // Change button color to green to indicate completion
                    if (arenaConfig.button != null)
                    {
                        ColorBlock colors = arenaConfig.button.colors;
                        colors.normalColor = Color.green;
                        colors.highlightedColor = new Color(0f, 0.8f, 0f); // Darker green for hover
                        colors.pressedColor = new Color(0f, 0.6f, 0f); // Even darker green for press
                        colors.selectedColor = Color.green;
                        arenaConfig.button.colors = colors;
                    }
                }
            }
        }
    }
}