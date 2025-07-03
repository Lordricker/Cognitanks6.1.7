using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;
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
    public TankSlotData[] tankSlots = new TankSlotData[10]; // Assign ScriptableObjects in Inspector
    
    [Header("Dynamic Arena Configuration")]
    [Tooltip("Automatically load enemy tanks based on selected arena")]
    public bool useDynamicArenaLoading = true;
    [Tooltip("Current league (set by workshop UI)")]
    public string currentLeague = "League1";
    [Tooltip("Current round (set by workshop UI)")]
    public string currentRound = "Round1";
    
    [Header("Enemy Tank Configuration")]
    [Tooltip("Pre-configured enemy tanks for singleplayer mode")]
    public TankSlotData[] enemyTankSlots = new TankSlotData[10]; // Enemy-only tank configurations
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
    /// Load enemy tanks specific to the current league and round
    /// </summary>
    void LoadEnemyTanksForCurrentArena()
    {
#if UNITY_EDITOR
        // Determine folder path
        string enemyFolderPath;
        if (!string.IsNullOrEmpty(manualEnemyFolderPath))
        {
            enemyFolderPath = manualEnemyFolderPath;
        }
        else
        {
            enemyFolderPath = $"Workshop/TankSlotData/Enemies/{currentLeague}/{currentRound}";
        }
        
        string fullPath = $"Assets/{enemyFolderPath}";
        
        // Clear existing enemy tanks
        System.Array.Clear(enemyTankSlots, 0, enemyTankSlots.Length);
        
        // Find all TankSlotData assets in the enemy folder
        string[] guids = AssetDatabase.FindAssets("t:TankSlotData", new[] { fullPath });
        
        if (guids.Length == 0)
        {
            Debug.LogWarning($"[ArenaManager] No enemy tanks found in {fullPath}. Make sure enemy tanks exist for {currentLeague}/{currentRound}");
            return;
        }
        
        System.Collections.Generic.List<TankSlotData> loadedEnemies = new System.Collections.Generic.List<TankSlotData>();
        
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            TankSlotData enemyTank = AssetDatabase.LoadAssetAtPath<TankSlotData>(assetPath);
            
            if (enemyTank != null && !enemyTank.isPlayerControlled)
            {
                // Ensure enemy tank is properly configured
                enemyTank.isActive = true;
                enemyTank.teamId = 1; // Enemy team
                
                loadedEnemies.Add(enemyTank);
                Debug.Log($"[ArenaManager] Loaded enemy tank: {enemyTank.displayName} ({enemyTank.name})");
            }
        }
        
        // Sort enemies by name for consistent ordering
        loadedEnemies.Sort((a, b) => string.Compare(a.name, b.name));
        
        // Assign loaded enemies to enemyTankSlots array
        for (int i = 0; i < enemyTankSlots.Length && i < loadedEnemies.Count; i++)
        {
            enemyTankSlots[i] = loadedEnemies[i];
        }
        
        Debug.Log($"[ArenaManager] Loaded {loadedEnemies.Count} enemy tanks for {currentLeague}/{currentRound}");
#else
        Debug.LogWarning("[ArenaManager] Dynamic enemy loading only works in the Unity Editor. Please manually assign enemy tanks for builds.");
#endif
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
        // Spawn player tanks (they will get teamId from their TankSlotData)
        SpawnTankArray(tankSlots, spawnPoints, "Player");
        
        // Spawn enemy tanks using their filename to determine spawn point
        SpawnEnemyTanksAtNamedSpawnPoints();
    }
    
    void SpawnMultiplayerTanks()
    {
        // Just spawn all active tanks - they'll get teamId from TankSlotData
        SpawnTankArray(tankSlots, spawnPoints, "Player");
    }
    
    void SpawnTankArray(TankSlotData[] slots, Transform[] spawns, string tankType, int spawnIndexOffset = 0)
    {
        for (int i = 0; i < slots.Length && i < spawns.Length; i++)
        {
            if (slots[i] != null && slots[i].isActive && slots[i].engineFramePrefab != null)
            {
                int spawnIndex = (i + spawnIndexOffset) % spawns.Length;
                if (spawns[spawnIndex] == null) continue;
                
                GameObject tank = Instantiate(tankPrefab, spawns[spawnIndex].position, spawns[spawnIndex].rotation);
                
                // Set the tank's name to include team and type information
                string tankName = !string.IsNullOrEmpty(slots[i].displayName) ? slots[i].displayName : $"{tankType}Tank_{i}";
                tank.name = $"{tankName}_Team{slots[i].teamId}";
                
                TankAssembly assembly = tank.GetComponent<TankAssembly>();
                if (assembly != null)   
                {
                    assembly.Assemble(slots[i]);
                }
                
                Debug.Log($"Tank {tank.name} ({tankType}, Team {slots[i].teamId}) spawned");
            }
        }
    }
    
    void SpawnEnemyTanksAtNamedSpawnPoints()
    {
        Debug.Log($"[ArenaManager] SpawnEnemyTanksAtNamedSpawnPoints called. Enemy tank slots count: {enemyTankSlots.Length}");
        
        for (int i = 0; i < enemyTankSlots.Length; i++)
        {
            if (enemyTankSlots[i] != null)
            {
                Debug.Log($"[ArenaManager] Enemy slot {i}: {enemyTankSlots[i].name}, isActive: {enemyTankSlots[i].isActive}, hasEngine: {enemyTankSlots[i].engineFramePrefab != null}");
                
                if (enemyTankSlots[i].isActive && enemyTankSlots[i].engineFramePrefab != null)
                {
                    // Extract spawn point number from the SO name and convert to array index
                    // SpawnPoint10 -> array index 0, SpawnPoint11 -> array index 1, etc.
                    string soName = enemyTankSlots[i].name;
                    int spawnPointIndex = ExtractSpawnPointFromName(soName);
                    
                    if (spawnPointIndex >= 0 && spawnPointIndex < enemySpawnPoints.Length)
                    {
                        if (enemySpawnPoints[spawnPointIndex] != null)
                        {
                            GameObject tank = Instantiate(tankPrefab, enemySpawnPoints[spawnPointIndex].position, enemySpawnPoints[spawnPointIndex].rotation);
                            
                            // Set the tank's name to include team and type information
                            string tankName = !string.IsNullOrEmpty(enemyTankSlots[i].displayName) ? enemyTankSlots[i].displayName : $"EnemyTank_{i}";
                            tank.name = $"{tankName}_Team{enemyTankSlots[i].teamId}_Spawn{spawnPointIndex}";
                            
                            TankAssembly assembly = tank.GetComponent<TankAssembly>();
                            if (assembly != null)   
                            {
                                assembly.Assemble(enemyTankSlots[i]);
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"[ArenaManager] Enemy spawn point {spawnPointIndex} is null in enemySpawnPoints array");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[ArenaManager] Invalid enemy spawn point index {spawnPointIndex} for enemy tank {soName}. Array length: {enemySpawnPoints.Length}");
                    }
                }
            }
            else
            {
                Debug.Log($"[ArenaManager] Enemy slot {i} is null");
            }
        }
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
            if (victoryPanel != null)
            {
                StartCoroutine(FadeInPanel(victoryPanel));
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
            }
            else
            {
                Debug.LogWarning("[ArenaManager] Loss panel not found! Please create a 'LossPanel' GameObject under the UI Canvas.");
            }
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
}

