using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using System.IO;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

public enum GameMode
{
    Singleplayer,
    Multiplayer
}

public class ArenaManager : MonoBehaviour
{    
    [Header("Spawn Points")]
    public Transform[] spawnPoints = new Transform[20]; // Increased to support more spawn points
    public GameObject tankPrefab; // Assign modular tank prefab in Inspector
    // Player tanks now loaded from JSON via TankSlotJsonManager
    
    [Header("Dynamic Arena Configuration")]
    [Tooltip("Automatically load enemy tanks based on selected arena")]
    public bool useDynamicArenaLoading = true;
    [Tooltip("Current league (set by workshop UI)")]
    public string currentLeague = "League1";
    [Tooltip("Current round (set by workshop UI)")]
    public string currentRound = "Round1";
    
    [Header("Enemy Tank Configuration")]
    [Tooltip("Enemy tanks loaded from JSON files in Resources/Workshop/TankSlotData/Enemies/")]
    private List<TankSlotDataJson> enemyTankSlots = new List<TankSlotDataJson>(); // Enemy-only tank configurations
    [Tooltip("Enemy spawn points (if different from player spawn points)")]
    public Transform[] enemySpawnPoints = new Transform[10];
    [Tooltip("Manually set enemy folder path (overrides dynamic loading)")]
    public string manualEnemyFolderPath = "";
    
    [Header("Game Mode Configuration")]
    [SerializeField] private GameMode gameMode = GameMode.Singleplayer;
    [SerializeField] private int playerCount = 1; // For multiplayer modes
    
    [Header("Victory/Loss System")]
    [SerializeField] private Canvas uiCanvas; // Main UI Canvas
    [SerializeField] private GameObject victoryPanel; // Victory panel to show when player wins
    [SerializeField] private GameObject lossPanel; // Loss panel to show when player loses
    [SerializeField] private GameObject tipPanel; // Tip panel to show after victory/loss
    [SerializeField] private float gameEndCheckInterval = 1f; // How often to check game state
    
    [Header("Legacy Team Layer Configuration (Deprecated)")]
    [Tooltip("Unity layer for Team A tanks - DEPRECATED: Use SimpleTeamManager instead")]
    public int teamALayer = 10; // Layer 10 for allies
    [Tooltip("Unity layer for Team B tanks - DEPRECATED: Use SimpleTeamManager instead")]
    public int teamBLayer = 11; // Layer 11 for enemies
    
    private bool gameEnded = false;
    private SimpleTeamManager teamManager;
    
    void Start()
    {
        // Ensure TankSlotJsonManager is properly initialized before we use it
        if (TankSlotJsonManager.Instance == null)
        {
            Debug.LogError("[ArenaManager] TankSlotJsonManager.Instance is null! Creating new instance...");
            GameObject managerGO = new GameObject("TankSlotJsonManager");
            managerGO.AddComponent<TankSlotJsonManager>();
        }
        
        // Force initialization of tank slots
        TankSlotJsonManager.Instance.InitializeTankSlots();
        
        // Load game mode configuration from PlayerPrefs (set by TeamConfigUI)
        LoadGameModeSettings();
        
        // Load arena-specific configuration
        LoadArenaConfiguration();
        
        // Load enemy tanks for this arena
        if (useDynamicArenaLoading)
        {
            LoadEnemyTanksForCurrentArena();
        }
        
        // Ensure time scale is reset to normal when arena starts (fixes pause bug)
        Time.timeScale = 1f;
        
        SpawnActiveTanks();
        
        // Assign teams after all tanks are spawned
        AssignTeams();
        
        // Refresh camera anchors after all tanks have spawned
        var camController = Object.FindFirstObjectByType<CameraController>();
        if (camController != null)
            camController.RefreshAnchors();
        
        // Start monitoring game state for victory/loss conditions
        InvokeRepeating(nameof(CheckGameState), gameEndCheckInterval, gameEndCheckInterval);
    }
    
    /// <summary>
    /// Simple team assignment using SimpleTeamManager
    /// </summary>
    void AssignTeams()
    {
        teamManager = FindFirstObjectByType<SimpleTeamManager>();
        if (teamManager == null)
        {
            GameObject teamManagerObj = new GameObject("SimpleTeamManager");
            teamManager = teamManagerObj.AddComponent<SimpleTeamManager>();
        }
        
        teamManager.AssignTeamsFromBattleMode();
        // Teams are assigned automatically via TankAssembly component
    }
    
    /// <summary>
    /// Load arena configuration from PlayerPrefs (set by workshop league/round selection)
    /// </summary>
    void LoadArenaConfiguration()
    {
        if (PlayerPrefs.HasKey("SelectedLeague"))
        {
            currentLeague = PlayerPrefs.GetString("SelectedLeague");
        }
        
        if (PlayerPrefs.HasKey("SelectedRound"))
        {
            currentRound = PlayerPrefs.GetString("SelectedRound");
        }
        
        Debug.Log($"[ArenaManager] Arena Configuration: {currentLeague}/{currentRound}");
    }
    
    /// <summary>
    /// Load enemy tanks specific to the current league and round from Assets folder
    /// </summary>
    void LoadEnemyTanksForCurrentArena()
    {
        Debug.Log($"[ArenaManager] Loading enemy tanks for {currentLeague}/{currentRound}");
        
        // Clear existing enemy tanks
        enemyTankSlots.Clear();
        
        // Load enemy tanks from Resources using the league/round structure
        string enemyResourcePath = $"Workshop/TankSlotData/Enemies/{currentLeague}/{currentRound}";
        
        // Try to load all JSON files from the Resources folder
        TextAsset[] enemyJsonFiles = Resources.LoadAll<TextAsset>(enemyResourcePath);
        
        if (enemyJsonFiles.Length == 0)
        {
            Debug.LogWarning($"[ArenaManager] No enemy tank JSON files found in Resources/{enemyResourcePath}. Make sure enemy tanks exist for {currentLeague}/{currentRound}");
            return;
        }
        
        List<TankSlotDataJson> loadedEnemies = new List<TankSlotDataJson>();
        
        foreach (TextAsset jsonFile in enemyJsonFiles)
        {
            try
            {
                TankSlotDataJson enemyTank = JsonUtility.FromJson<TankSlotDataJson>(jsonFile.text);
                
                if (enemyTank != null && !enemyTank.isPlayerControlled)
                {
                    // Ensure enemy tank is properly configured
                    enemyTank.isActive = true;
                    enemyTank.teamId = 1; // Enemy team
                    
                    // Extract spawn point name from file name (e.g., "SpawnPoint10" from "SpawnPoint10.json")
                    string fileName = jsonFile.name;
                    enemyTank.spawnPointName = fileName; // Store the spawn point name
                    
                    loadedEnemies.Add(enemyTank);
                    Debug.Log($"[ArenaManager] Loaded enemy tank: {enemyTank.displayName} from {fileName}.json with spawn point {enemyTank.spawnPointName}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ArenaManager] Failed to load enemy tank from {jsonFile.name}: {e.Message}");
            }
        }
        
        // Sort enemies by spawn point name for consistent ordering
        loadedEnemies.Sort((a, b) => string.Compare(a.spawnPointName, b.spawnPointName));
        
        // Assign loaded enemies to enemyTankSlots list
        enemyTankSlots.AddRange(loadedEnemies);
        
        Debug.Log($"[ArenaManager] Loaded {enemyTankSlots.Count} enemy tanks from Resources/{enemyResourcePath}");
    }
    
    void LoadGameModeSettings()
    {
        if (PlayerPrefs.HasKey("GameMode"))
        {
            gameMode = (GameMode)PlayerPrefs.GetInt("GameMode");
        }
        
        if (PlayerPrefs.HasKey("PlayerCount"))
        {
            playerCount = PlayerPrefs.GetInt("PlayerCount");
        }
        
        Debug.Log($"[ArenaManager] Loaded settings: Mode={gameMode}, Players={playerCount}");
    }
    
    void SpawnActiveTanks()
    {
        if (gameMode == GameMode.Singleplayer)
        {
            SpawnSingleplayerTanks();
        }
        else
        {
            SpawnMultiplayerTanks();
        }
    }
    
    void SpawnSingleplayerTanks()
    {
        // Load player tanks from JSON
        var playerTankSlots = TankSlotJsonManager.Instance.GetAllTankSlots();
        
        Debug.Log($"[ArenaManager] SpawnSingleplayerTanks: Found {playerTankSlots.Count} tank slots");
        for (int i = 0; i < playerTankSlots.Count; i++)
        {
            var slot = playerTankSlots[i];
            Debug.Log($"[ArenaManager] Tank slot {i}: isActive={slot.isActive}, hasEngine={!string.IsNullOrEmpty(slot.engineFrameInstanceId)}, engineId='{slot.engineFrameInstanceId}', displayName='{slot.displayName}'");
        }
        
        // Spawn player tanks (they will get teamId from their TankSlotDataJson)
        SpawnPlayerTanks(playerTankSlots);
        
        // Load and spawn enemy tanks
        if (useDynamicArenaLoading)
        {
            LoadEnemyTanksForCurrentArena();
        }
        SpawnEnemyTanksAtNamedSpawnPoints();
    }
    
    void SpawnMultiplayerTanks()
    {
        // Load player tanks from JSON
        var playerTankSlots = TankSlotJsonManager.Instance.GetAllTankSlots();
        
        // Just spawn all active tanks - they'll get teamId from TankSlotDataJson
        SpawnPlayerTanks(playerTankSlots);
    }
    
    void SpawnPlayerTanks(List<TankSlotDataJson> slots)
    {
        Debug.Log($"[ArenaManager] SpawnPlayerTanks called with {slots.Count} slots");
        
        for (int i = 0; i < slots.Count && i < spawnPoints.Length; i++)
        {
            var slot = slots[i];
            if (slot == null)
            {
                Debug.LogWarning($"[ArenaManager] Tank slot {i} is null, skipping");
                continue;
            }
            
            Debug.Log($"[ArenaManager] Processing slot {i}: isActive={slot.isActive}, hasEngine={!string.IsNullOrEmpty(slot.engineFrameInstanceId)}, engineId='{slot.engineFrameInstanceId}'");
            
            if (slot.isActive && !string.IsNullOrEmpty(slot.engineFrameInstanceId))
            {
                if (spawnPoints[i] == null) 
                {
                    Debug.LogWarning($"[ArenaManager] Spawn point {i} is null, skipping tank spawn");
                    continue;
                }
                
                Debug.Log($"[ArenaManager] Spawning tank at spawn point {i}: {spawnPoints[i].position}");
                
                GameObject tank = Instantiate(tankPrefab, spawnPoints[i].position, spawnPoints[i].rotation);
                
                // Set the tank's name to include team and type information
                string tankName = !string.IsNullOrEmpty(slot.displayName) ? slot.displayName : $"PlayerTank_{i}";
                tank.name = $"{tankName}_Team{slot.teamId}";
                
                TankAssembly assembly = tank.GetComponent<TankAssembly>();
                if (assembly != null)   
                {
                    Debug.Log($"[ArenaManager] Calling TankAssembly.Assemble for tank {tank.name}");
                    assembly.Assemble(slot);
                }
                else
                {
                    Debug.LogError($"[ArenaManager] No TankAssembly component found on tank prefab!");
                }
                
                Debug.Log($"Tank {tank.name} (Player, Team {slot.teamId}) spawned at {tank.transform.position}");
            }
            else
            {
                Debug.Log($"[ArenaManager] Skipping slot {i}: isActive={slot.isActive}, hasEngine={!string.IsNullOrEmpty(slot.engineFrameInstanceId)}");
            }
        }
    }
    
    void SpawnEnemyTanksAtNamedSpawnPoints()
    {
        Debug.Log($"[ArenaManager] SpawnEnemyTanksAtNamedSpawnPoints called. Enemy tank slots count: {enemyTankSlots.Count}");
        
        for (int i = 0; i < enemyTankSlots.Count; i++)
        {
            if (enemyTankSlots[i] != null)
            {
                Debug.Log($"[ArenaManager] Enemy slot {i}: {enemyTankSlots[i].displayName}, isActive: {enemyTankSlots[i].isActive}, hasEngine: {!string.IsNullOrEmpty(enemyTankSlots[i].engineFrameInstanceId)}, spawnPoint: {enemyTankSlots[i].spawnPointName}");
                
                if (enemyTankSlots[i].isActive && !string.IsNullOrEmpty(enemyTankSlots[i].engineFrameInstanceId))
                {
                    // Find the spawn point by name
                    Transform spawnPoint = FindSpawnPointByName(enemyTankSlots[i].spawnPointName);
                    
                    if (spawnPoint != null)
                    {
                        GameObject tank = Instantiate(tankPrefab, spawnPoint.position, spawnPoint.rotation);
                        
                        // Set the tank's name to include team and spawn point information
                        string tankName = !string.IsNullOrEmpty(enemyTankSlots[i].displayName) ? enemyTankSlots[i].displayName : $"EnemyTank_{i}";
                        tank.name = $"{tankName}_Team{enemyTankSlots[i].teamId}_{enemyTankSlots[i].spawnPointName}";
                        
                        TankAssembly assembly = tank.GetComponent<TankAssembly>();
                        if (assembly != null)   
                        {
                            assembly.Assemble(enemyTankSlots[i]);
                        }
                        
                        Debug.Log($"Enemy tank {tank.name} (Team {enemyTankSlots[i].teamId}) spawned at {enemyTankSlots[i].spawnPointName}: {tank.transform.position}");
                    }
                    else
                    {
                        Debug.LogWarning($"[ArenaManager] Could not find spawn point '{enemyTankSlots[i].spawnPointName}' for enemy tank {enemyTankSlots[i].displayName}");
                    }
                }
            }
            else
            {
                Debug.Log($"[ArenaManager] Enemy slot {i} is null");
            }
        }
    }
    
    /// <summary>
    /// Find a spawn point by name, checking both regular spawn points and enemy spawn points
    /// Handles multiple naming conventions: "SpawnPoint10", "SpawnPoint (10)", etc.
    /// </summary>
    Transform FindSpawnPointByName(string spawnPointName)
    {
        if (string.IsNullOrEmpty(spawnPointName))
            return null;
            
        // Try multiple naming variations
        string[] possibleNames = new string[]
        {
            spawnPointName,                                    // "SpawnPoint10"
            spawnPointName.Replace("SpawnPoint", "SpawnPoint ("), // "SpawnPoint (10"
            spawnPointName.Replace("SpawnPoint", "SpawnPoint (") + ")", // "SpawnPoint (10)"
            spawnPointName.Replace("SpawnPoint", "").Trim(),   // "10" (just the number)
            $"SpawnPoint ({spawnPointName.Replace("SpawnPoint", "").Trim()})" // " (10)"
        };
            
        // First check regular spawn points
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (spawnPoints[i] != null)
            {
                foreach (string nameVariation in possibleNames)
                {
                    if (spawnPoints[i].name.Equals(nameVariation, System.StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.Log($"[ArenaManager] Found spawn point '{spawnPointName}' -> '{nameVariation}' in spawnPoints array at index {i}");
                        return spawnPoints[i];
                    }
                }
            }
        }
        
        // Then check enemy spawn points
        for (int i = 0; i < enemySpawnPoints.Length; i++)
        {
            if (enemySpawnPoints[i] != null)
            {
                foreach (string nameVariation in possibleNames)
                {
                    if (enemySpawnPoints[i].name.Equals(nameVariation, System.StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.Log($"[ArenaManager] Found spawn point '{spawnPointName}' -> '{nameVariation}' in enemySpawnPoints array at index {i}");
                        return enemySpawnPoints[i];
                    }
                }
            }
        }
        
        // If no exact name match, try to extract spawn point number and use array indexing
        int spawnPointIndex = ExtractSpawnPointFromName(spawnPointName);
        if (spawnPointIndex >= 0)
        {
            // Use enemy spawn points array for extracted indices
            if (spawnPointIndex < enemySpawnPoints.Length && enemySpawnPoints[spawnPointIndex] != null)
            {
                Debug.Log($"[ArenaManager] Using extracted spawn point index {spawnPointIndex} for '{spawnPointName}'");
                return enemySpawnPoints[spawnPointIndex];
            }
        }
        
        Debug.LogWarning($"[ArenaManager] Could not find spawn point '{spawnPointName}' in either spawn point array");
        return null;
    }
    
    int ExtractSpawnPointFromName(string soName)
    {
        // Look for "SpawnPoint" followed by a number (e.g., "SpawnPoint10")
        if (soName.StartsWith("SpawnPoint"))
        {
            string numberPart = soName.Substring("SpawnPoint".Length);
            
            if (int.TryParse(numberPart, out int spawnIndex))
            {
                // Convert SpawnPoint10-19 to array indices 0-9
                // SpawnPoint10 -> index 0, SpawnPoint11 -> index 1, etc.
                return spawnIndex - 10;
            }
        }
        
        Debug.LogWarning($"[ArenaManager] Could not extract spawn point number from SO name: {soName}. Expected format: SpawnPoint##");
        return -1;
    }

    /// <summary>
    /// Get the Unity layer for a specific team - LEGACY METHOD
    /// Use SimpleTeamManager instead for new implementations
    /// </summary>
    private int GetLayerForTeam(int teamId)
    {
        switch (teamId)
        {
            case 0: return teamALayer; // Team A
            case 1: return teamBLayer; // Team B
            default: 
                Debug.LogWarning($"Unknown team ID: {teamId}. Using Team A layer.");
                return teamALayer;
        }
    }
    
    /// <summary>
    /// Recursively assign a layer to a GameObject and all its children - LEGACY METHOD
    /// Use SimpleTeamManager instead for new implementations
    /// </summary>
    private void AssignLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            AssignLayerRecursively(child.gameObject, layer);
        }
    }

    /// <summary>
    /// Checks if the game should end based on remaining teams
    /// </summary>
    void CheckGameState()
    {
        if (gameEnded || teamManager == null)
            return;
            
        // Get all alive tanks organized by team
        var aliveTanksByTeam = GetAliveTanksByTeam();
        
        // Check if only one team remains or no teams remain
        if (aliveTanksByTeam.Count <= 1)
        {
            EndGame(aliveTanksByTeam);
        }
    }
    
    /// <summary>
    /// Gets all alive tanks organized by team ID
    /// </summary>
    System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<TankMan>> GetAliveTanksByTeam()
    {
        var aliveTanksByTeam = new System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<TankMan>>();
        
        // Find all TankMan components
        TankMan[] allTanks = FindObjectsByType<TankMan>(FindObjectsSortMode.None);
        
        foreach (TankMan tank in allTanks)
        {
            // Only count alive tanks
            if (tank.CurrentHealth > 0)
            {
                // Get team info
                TankTeamInfo teamInfo = tank.GetComponent<TankTeamInfo>();
                if (teamInfo != null)
                {
                    int teamId = teamInfo.teamId;
                    
                    if (!aliveTanksByTeam.ContainsKey(teamId))
                    {
                        aliveTanksByTeam[teamId] = new System.Collections.Generic.List<TankMan>();
                    }
                    
                    aliveTanksByTeam[teamId].Add(tank);
                }
            }
        }
        
        return aliveTanksByTeam;
    }
    
    /// <summary>
    /// Ends the game and shows victory or loss panel
    /// </summary>
    void EndGame(System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<TankMan>> aliveTanksByTeam)
    {
        gameEnded = true;
        CancelInvoke(nameof(CheckGameState)); // Stop checking game state
        
        Debug.Log($"[ArenaManager] Game ended! Remaining teams: {aliveTanksByTeam.Count}");
        
        // Find UI Canvas if not assigned
        if (uiCanvas == null)
        {
            uiCanvas = FindFirstObjectByType<Canvas>();
        }
        
        if (uiCanvas == null)
        {
            Debug.LogError("[ArenaManager] No UI Canvas found! Cannot show victory/loss panels.");
            return;
        }
        
        // Look for victory and loss panels under the UI Canvas
        if (victoryPanel == null)
        {
            Transform victoryTransform = uiCanvas.transform.Find("VictoryPanel");
            if (victoryTransform != null)
                victoryPanel = victoryTransform.gameObject;
        }
        
        if (lossPanel == null)
        {
            Transform lossTransform = uiCanvas.transform.Find("LossPanel");
            if (lossTransform != null)
                lossPanel = lossTransform.gameObject;
        }
        
        if (tipPanel == null)
        {
            Transform tipTransform = uiCanvas.transform.Find("TipPanel");
            if (tipTransform != null)
                tipPanel = tipTransform.gameObject;
        }
        
        // Determine if player team won
        bool playerWon = false;
        
        if (aliveTanksByTeam.Count == 1)
        {
            // One team remains - check if it's the player team (team 0 in singleplayer)
            int winningTeamId = aliveTanksByTeam.Keys.First();
            
            if (gameMode == GameMode.Singleplayer)
            {
                // In singleplayer, player is team 0, enemies are team 1
                playerWon = (winningTeamId == 0);
            }
            else
            {
                // In multiplayer, check if winning team matches player's team
                int myTeamId = PlayerPrefs.GetInt("MyTeamId", 0);
                playerWon = (winningTeamId == myTeamId);
            }
            
            Debug.Log($"[ArenaManager] Winning team: {winningTeamId}, Player won: {playerWon}");
        }
        else
        {
            // No teams remain (draw) - treat as loss
            playerWon = false;
            Debug.Log("[ArenaManager] No teams remain - treating as loss");
        }
        
        // Show appropriate panel with fade-in animation
        if (playerWon)
        {
            // Award victory rewards: refund entry fee + fee per alive tank
            AwardVictoryRewards(aliveTanksByTeam);
            
            // Mark arena as completed for progress unlocking
            MarkArenaCompleted();
            
            if (victoryPanel != null)
            {
                StartCoroutine(FadeInPanel(victoryPanel));
                
                // Play round win sound
                if (SoundManager.Instance != null)
                    SoundManager.Instance.PlayRoundWin();
                
                Debug.Log("[ArenaManager] Victory panel shown!");
            }
            else
            {
                Debug.LogWarning("[ArenaManager] Victory panel not found! Please create a 'VictoryPanel' GameObject under the UI Canvas.");
            }
        }
        else
        {
            if (lossPanel != null)
            {
                StartCoroutine(FadeInPanel(lossPanel));
                Debug.Log("[ArenaManager] Loss panel shown!");
                
                // Play round loss sound
                if (SoundManager.Instance != null)
                    SoundManager.Instance.PlayRoundLoss();
            }
            else
            {
                Debug.LogWarning("[ArenaManager] Loss panel not found! Please create a 'LossPanel' GameObject under the UI Canvas.");
            }
        }
        
        // Show tip panel after a short delay
        if (tipPanel != null)
        {
            StartCoroutine(ShowTipPanelDelayed());
        }
        else
        {
            Debug.LogWarning("[ArenaManager] Tip panel not found! Please create a 'TipPanel' GameObject under the UI Canvas.");
        }
    }
    
    /// <summary>
    /// Awards victory rewards: refund entry fee + fee per alive player tank
    /// </summary>
    void AwardVictoryRewards(System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<TankMan>> aliveTanksByTeam)
    {
        int entryFee = PlayerPrefs.GetInt("ArenaEntryFee", 0);
        
        if (entryFee <= 0)
        {
            Debug.Log("[ArenaManager] No entry fee to refund (free arena)");
            return;
        }
        
        // Count alive player tanks (team 0 in singleplayer)
        int aliveTankCount = 0;
        if (aliveTanksByTeam.ContainsKey(0))
        {
            aliveTankCount = aliveTanksByTeam[0].Count;
        }
        
        // Calculate reward: refund entry fee + fee per alive tank
        // Example: $10 fee, 2 alive tanks = $10 (refund) + $10 (tank 1) + $10 (tank 2) = $30 total
        int totalReward = entryFee + (entryFee * aliveTankCount);
        
        // Award cash to player
        if (PlayerDataManager.Instance != null)
        {
            PlayerDataManager.Instance.AddPlayerCash(totalReward);
            Debug.Log($"[ArenaManager] Victory rewards: Entry fee ${entryFee} refunded + ${entryFee} x {aliveTankCount} alive tanks = ${totalReward} total");
        }
        else
        {
            Debug.LogError("[ArenaManager] PlayerDataManager.Instance is null! Cannot award rewards.");
        }
    }
    
    /// <summary>
    /// Marks the current arena as completed in PlayerPrefs for progress unlocking
    /// </summary>
    void MarkArenaCompleted()
    {
        string arenaKey = PlayerPrefs.GetString("SelectedArenaKey", "");
        
        if (string.IsNullOrEmpty(arenaKey))
        {
            Debug.LogWarning("[ArenaManager] No arena key found in PlayerPrefs, cannot mark as completed");
            return;
        }
        
        PlayerPrefs.SetInt($"ArenaCompleted_{arenaKey}", 1);
        PlayerPrefs.Save();
        
        Debug.Log($"[ArenaManager] Marked arena as completed: {arenaKey}");
        
        // Unlock component rewards (saved to PlayerPrefs, will be loaded in workshop)
        UnlockArenaRewards(arenaKey);
    }
    
    /// <summary>
    /// Saves component unlock rewards to PlayerPrefs so they can be loaded in workshop
    /// </summary>
    void UnlockArenaRewards(string arenaKey)
    {
        // Get the reward data from PlayerPrefs (set by LeagueDropdownManager)
        string rewardsJson = PlayerPrefs.GetString($"ArenaRewards_{arenaKey}", "");
        
        if (!string.IsNullOrEmpty(rewardsJson))
        {
            // Parse component IDs and mark them as unlocked
            string[] componentIds = rewardsJson.Split(',');
            foreach (string componentId in componentIds)
            {
                if (!string.IsNullOrEmpty(componentId))
                {
                    PlayerPrefs.SetInt($"ComponentUnlocked_{componentId}", 1);
                    Debug.Log($"[ArenaManager] Unlocked component: {componentId}");
                }
            }
            PlayerPrefs.Save();
        }
    }
    
    /// <summary>
    /// Smoothly fades in a panel over 0.25 seconds
    /// </summary>
    System.Collections.IEnumerator FadeInPanel(GameObject panel)
    {
        // Ensure panel is active
        panel.SetActive(true);
        
        // Get or add CanvasGroup component for alpha control
        CanvasGroup canvasGroup = panel.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = panel.AddComponent<CanvasGroup>();
        }
        
        // Start with alpha at 0 (fully transparent)
        canvasGroup.alpha = 0f;
        
        // Fade in over 0.25 seconds
        float fadeDuration = 0.25f;
        float elapsedTime = 0f;
        
        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.unscaledDeltaTime; // Use unscaled time since game is paused
            canvasGroup.alpha = Mathf.Clamp01(elapsedTime / fadeDuration);
            yield return null;
        }
        
        // Ensure alpha is exactly 1 at the end
        canvasGroup.alpha = 1f;
    }
    
    /// <summary>
    /// Shows the tip panel after a short delay
    /// </summary>
    System.Collections.IEnumerator ShowTipPanelDelayed()
    {
        // Wait 2 seconds after victory/loss panel appears
        yield return new WaitForSecondsRealtime(2f);
        
        if (tipPanel != null)
        {
            StartCoroutine(FadeInPanel(tipPanel));
            Debug.Log("[ArenaManager] Tip panel shown!");
        }
    }
}

