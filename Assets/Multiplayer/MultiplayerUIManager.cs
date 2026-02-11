using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MultiplayerData;

/// <summary>
/// Main UI manager for the Multiplayer scene
/// Handles login checks, match posting, and scroll view population
/// </summary>
public class MultiplayerUIManager : MonoBehaviour
{
    public static MultiplayerUIManager Instance { get; private set; }

    [Header("Navigation Buttons")]
    [SerializeField] private Button loginButton;
    [SerializeField] private Button logoutButton;

    [Header("Match Type Selection")]
    [SerializeField] private Button fourVsFourButton;
    [SerializeField] private Button tenVsTenButton;
    [SerializeField] private Color selectedColor = new Color(0.047f, 0.702f, 0f, 1f); // #0CB300
    [SerializeField] private Color unselectedColor = new Color(0.3f, 0.3f, 0.3f, 1f);
    
    [Header("Team Input")]
    [SerializeField] private TMP_InputField teamNameInput;
    [SerializeField] private Button postTankTeamButton;

    [Header("Player Info Display")]
    [SerializeField] private TMP_Text playerEloText;
    [SerializeField] private TMP_Text playerNameText;

    [Header("Scroll Views")]
    [SerializeField] private Transform postedMatchesContent;
    [SerializeField] private Transform replaysContent;
    [SerializeField] private GameObject multiplayerEntryPrefab;

    [Header("Debug UI")]
    [SerializeField] private TMP_Text debugText;

    // State
    private MultiplayerData.MatchType selectedMatchType = MultiplayerData.MatchType.FourVsFour;
    private PlayerProfile currentPlayerProfile;
    private Coroutine debugTextCoroutine;
    private List<GameObject> postedMatchEntries = new List<GameObject>();
    private List<GameObject> replayEntries = new List<GameObject>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        SetupButtonListeners();
        UpdateMatchTypeButtons();
        
        // Subscribe to Discord events
        if (DiscordManager.Instance != null)
        {
            DiscordManager.Instance.OnLoginSuccess += OnDiscordLoginSuccess;
            DiscordManager.Instance.OnLogout += OnDiscordLogout;
            
            // Check if already logged in
            if (DiscordManager.Instance.IsLoggedIn())
            {
                OnDiscordLoginSuccess();
            }
            else
            {
                UpdateUIForLoggedOutState();
            }
        }
        else
        {
            UpdateUIForLoggedOutState();
        }

        // Subscribe to Firebase events
        if (FirebaseMatchService.Instance != null)
        {
            FirebaseMatchService.Instance.OnMatchesLoaded += OnMatchesLoaded;
            FirebaseMatchService.Instance.OnReplaysLoaded += OnReplaysLoaded;
            FirebaseMatchService.Instance.OnPlayerProfileLoaded += OnPlayerProfileLoaded;
            FirebaseMatchService.Instance.OnError += OnFirebaseError;
            FirebaseMatchService.Instance.OnSuccess += OnFirebaseSuccess;
        }
    }

    private void SetupButtonListeners()
    {
        // Login/Logout handled by DiscordLoginUI, but we can add checks here
        if (loginButton != null)
            loginButton.onClick.AddListener(OnLoginButtonClicked);
        
        if (logoutButton != null)
            logoutButton.onClick.AddListener(OnLogoutButtonClicked);

        // Match type buttons
        if (fourVsFourButton != null)
            fourVsFourButton.onClick.AddListener(() => SelectMatchType(MultiplayerData.MatchType.FourVsFour));
        
        if (tenVsTenButton != null)
            tenVsTenButton.onClick.AddListener(() => SelectMatchType(MultiplayerData.MatchType.TenVsTen));

        // Post button
        if (postTankTeamButton != null)
            postTankTeamButton.onClick.AddListener(OnPostTankTeamClicked);
    }

    #region Login State Handling

    private void OnDiscordLoginSuccess()
    {
        Debug.Log("[MultiplayerUIManager] Discord login success");
        
        string odiscordUserId = DiscordManager.Instance?.GetUserId();
        string username = DiscordManager.Instance?.GetUsername();
        
        if (!string.IsNullOrEmpty(odiscordUserId))
        {
            if (playerNameText != null)
                playerNameText.text = username;
            
            // Load player profile from Firebase
            if (FirebaseMatchService.Instance != null)
            {
                FirebaseMatchService.Instance.GetOrCreatePlayerProfile(odiscordUserId, username);
            }
            
            UpdateLoginButtonVisibility(true);
            RefreshMatchLists();
        }
    }

    private void OnDiscordLogout()
    {
        Debug.Log("[MultiplayerUIManager] Discord logout");
        currentPlayerProfile = null;
        UpdateUIForLoggedOutState();
    }

    private void UpdateUIForLoggedOutState()
    {
        if (playerNameText != null)
            playerNameText.text = "Not Logged In";
        
        if (playerEloText != null)
            playerEloText.text = "ELO: ---";
        
        UpdateLoginButtonVisibility(false);
        ClearScrollViews();
    }

    private void UpdateLoginButtonVisibility(bool isLoggedIn)
    {
        if (loginButton != null)
        {
            loginButton.gameObject.SetActive(!isLoggedIn);
        }
        
        if (logoutButton != null)
        {
            logoutButton.gameObject.SetActive(isLoggedIn);
        }
    }

    private void OnPlayerProfileLoaded(PlayerProfile profile)
    {
        currentPlayerProfile = profile;
        
        if (playerEloText != null)
            playerEloText.text = $"ELO: {profile.elo}";
        
        if (playerNameText != null)
            playerNameText.text = profile.discordUsername;
        
        Debug.Log($"[MultiplayerUIManager] Player profile loaded: {profile.discordUsername} (ELO: {profile.elo})");
    }

    #endregion

    #region Match Type Selection

    private void SelectMatchType(MultiplayerData.MatchType matchType)
    {
        if (!CheckLoggedIn())
        {
            ShowDebugMessage("Please login first!");
            return;
        }
        
        selectedMatchType = matchType;
        UpdateMatchTypeButtons();
        Debug.Log($"[MultiplayerUIManager] Selected match type: {matchType}");
        
        // Refresh match list to apply the new filter
        RefreshMatchLists();
    }

    private void UpdateMatchTypeButtons()
    {
        if (fourVsFourButton != null)
        {
            Image img = fourVsFourButton.GetComponent<Image>();
            if (img != null)
                img.color = selectedMatchType == MultiplayerData.MatchType.FourVsFour ? selectedColor : unselectedColor;
        }
        
        if (tenVsTenButton != null)
        {
            Image img = tenVsTenButton.GetComponent<Image>();
            if (img != null)
                img.color = selectedMatchType == MultiplayerData.MatchType.TenVsTen ? selectedColor : unselectedColor;
        }
    }

    #endregion

    #region Login/Logout

    private void OnLoginButtonClicked()
    {
        if (DiscordManager.Instance != null && !DiscordManager.Instance.IsLoggedIn())
        {
            DiscordManager.Instance.Login();
        }
    }

    private void OnLogoutButtonClicked()
    {
        if (DiscordManager.Instance != null && DiscordManager.Instance.IsLoggedIn())
        {
            DiscordManager.Instance.Logout();
        }
    }

    #endregion

    #region Match Posting

    private void OnPostTankTeamClicked()
    {
        StartCoroutine(PostTankTeamCoroutine());
    }

    private IEnumerator PostTankTeamCoroutine()
    {
        // 1. Check if logged in
        if (!CheckLoggedIn())
        {
            ShowDebugMessage("Please login first!");
            yield break;
        }

        // 1b. Check Firebase service
        if (FirebaseMatchService.Instance == null)
        {
            ShowDebugMessage("Firebase not connected!");
            Debug.LogError("[MultiplayerUIManager] FirebaseMatchService.Instance is null! Make sure the FirebaseMatchService component exists in the scene.");
            yield break;
        }

        // 2. Check match type is selected
        int requiredTanks = (int)selectedMatchType;
        
        // 3. Count active tank slots (isActive == true only)
        var allSlots = TankSlotJsonManager.Instance?.GetAllTankSlots();
        var activeTankSlots = TankSlotJsonManager.Instance?.GetActiveTankSlots();
        int activeCount = activeTankSlots?.Count ?? 0;
        
        Debug.Log($"[MultiplayerUIManager] PostTankTeam: selectedMatchType={selectedMatchType}, requiredTanks={requiredTanks}, activeCount={activeCount}");
        
        if (activeCount < requiredTanks)
        {
            ShowDebugMessage($"Need {requiredTanks} active tanks! You have {activeCount}");
            yield break;
        }
        
        // 4. Validate all active tanks have all 5 components
        string componentError = SceneManager.ValidateActiveTankComponents();
        if (!string.IsNullOrEmpty(componentError))
        {
            ShowDebugMessage(componentError);
            yield break;
        }

        // 5. Check if player already has an active post of this type
        string odiscordUserId = DiscordManager.Instance.GetUserId();
        bool hasExistingPost = false;
        bool checkComplete = false;
        
        FirebaseMatchService.Instance.CheckExistingMatch(odiscordUserId, selectedMatchType, 
            (exists) => { hasExistingPost = exists; checkComplete = true; },
            (error) => { checkComplete = true; });
        
        // Wait for check to complete
        while (!checkComplete)
            yield return null;
        
        if (hasExistingPost)
        {
            ShowDebugMessage($"You already have an active {requiredTanks}vs{requiredTanks} post!");
            yield break;
        }

        // 6. Build match post data (takes first N active tanks)
        string teamName = teamNameInput?.text ?? "Team";
        if (string.IsNullOrWhiteSpace(teamName))
            teamName = $"{DiscordManager.Instance.GetUsername()}'s Team";

        MatchPostData postData = new MatchPostData
        {
            odiscordUserId = odiscordUserId,
            discordUsername = DiscordManager.Instance.GetUsername(),
            teamName = teamName,
            playerElo = currentPlayerProfile?.elo ?? 1000,
            matchType = selectedMatchType,
            postedTimestamp = MatchDataHelper.GetCurrentTimestamp(),
            tankConfigs = BuildTankConfigs(activeTankSlots, requiredTanks)
        };

        // 7. Post to Firebase
        FirebaseMatchService.Instance.PostMatch(postData,
            (matchId) => {
                ShowDebugMessage("Match posted successfully!", false);
                RefreshMatchLists();
            },
            (error) => {
                ShowDebugMessage($"Failed to post: {error}");
            });
    }

    public List<TankConfigReference> BuildTankConfigs(List<TankSlotDataJson> activeSlots, int count)
    {
        List<TankConfigReference> configs = new List<TankConfigReference>();
        
        for (int i = 0; i < Mathf.Min(count, activeSlots.Count); i++)
        {
            var slot = activeSlots[i];
            
            // Read AI scripts from files
            string turretAI = ReadAIScriptFile(slot.turretAIInstanceId);
            string navAI = ReadAIScriptFile(slot.navAIInstanceId);
            
            Debug.Log($"[MultiplayerUIManager] BuildTankConfigs Tank {i}: turretAIId={slot.turretAIInstanceId}, navAIId={slot.navAIInstanceId}, turretAI_Length={turretAI?.Length ?? 0}, navAI_Length={navAI?.Length ?? 0}");
            
            configs.Add(new TankConfigReference
            {
                slotIndex = slot.slotIndex,
                displayName = slot.displayName,
                
                // Component IDs
                engineFrameId = slot.engineFrameInstanceId,
                armorId = slot.armorInstanceId,
                turretId = slot.turretInstanceId,
                
                // AI scripts
                turretAIScript = turretAI,
                navAIScript = navAI,
                turretAIWeight = slot.turretAIWeight,
                navAIWeight = slot.navAIWeight,
                
                // Engine stats
                engineForce = slot.engineForce,
                engineTopSpeed = slot.engineTopSpeed,
                engineTorque = slot.engineTorque,
                engineMaxTurnRate = slot.engineMaxTurnRate,
                engineTurnRampTime = slot.engineTurnRampTime,
                engineTurnStartPercent = slot.engineTurnStartPercent,
                engineFrameHP = slot.engineFrameHP,
                engineWeight = slot.engineWeight,
                
                // Armor stats
                armorHP = slot.armorHP,
                armorWeight = slot.armorWeight,
                
                // Turret stats
                turretDamage = slot.turretDamage,
                turretRange = slot.turretRange,
                turretShotsPerSec = slot.turretShotsPerSec,
                turretBulletSpeed = slot.turretBulletSpeed,
                turretVisionRange = slot.turretVisionRange,
                turretVisionCone = slot.turretVisionCone,
                turretWeight = slot.turretWeight
            });
            
            Debug.Log($"[MultiplayerUIManager] Tank {i}: {slot.displayName} | Engine: {slot.engineFrameInstanceId} | Armor: {slot.armorInstanceId} | Turret: {slot.turretInstanceId}");
        }
        
        return configs;
    }

    public string ReadAIScriptFile(string aiInstanceId)
    {
        if (string.IsNullOrEmpty(aiInstanceId))
        {
            Debug.LogWarning("[MultiplayerUIManager] ReadAIScriptFile: aiInstanceId is empty");
            return "";
        }
        
        string aiTreesFolder = Path.Combine(Application.persistentDataPath, "AiTrees");
        Debug.Log($"[MultiplayerUIManager] ReadAIScriptFile: Searching for instanceId={aiInstanceId} in {aiTreesFolder}");
        
        if (!Directory.Exists(aiTreesFolder))
        {
            Debug.LogWarning($"[MultiplayerUIManager] ReadAIScriptFile: AiTrees folder not found: {aiTreesFolder}");
            return "";
        }
        
        // Search all JSON files in AiTrees and all subdirectories
        string[] jsonFiles = Directory.GetFiles(aiTreesFolder, "*.json", SearchOption.AllDirectories);
        Debug.Log($"[MultiplayerUIManager] ReadAIScriptFile: Searching {jsonFiles.Length} total files in AiTrees");
        
        foreach (string filePath in jsonFiles)
        {
            try
            {
                // Quick check: if the filename contains the instanceId, it's very likely a match
                string fileName = Path.GetFileNameWithoutExtension(filePath);
                string jsonContent = File.ReadAllText(filePath);
                
                // Use the robust extraction method to get the actual instanceId from the JSON
                string foundInstanceId = ExtractInstanceIdFromJson(jsonContent);
                
                if (foundInstanceId == aiInstanceId)
                {
                    Debug.Log($"[MultiplayerUIManager] ReadAIScriptFile: ✓ Found {aiInstanceId} in {Path.GetFileName(filePath)}, length={jsonContent.Length}");
                    return jsonContent;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[MultiplayerUIManager] ReadAIScriptFile: Error reading {filePath}: {e.Message}");
            }
        }
        
        Debug.LogWarning($"[MultiplayerUIManager] ReadAIScriptFile: instanceId '{aiInstanceId}' not found in any AI file under {aiTreesFolder}");
        return "";
    }

    /// <summary>
    /// Extract instanceId value from AI JSON (handles any whitespace around the colon)
    /// </summary>
    private string ExtractInstanceIdFromJson(string json)
    {
        try
        {
            int idx = json.IndexOf("\"instanceId\"");
            if (idx < 0) return "";
            
            // Skip past "instanceId", then skip whitespace and colon
            int colonIdx = json.IndexOf(':', idx + 12);
            if (colonIdx < 0) return "";
            
            // Skip whitespace after colon, then find opening quote
            int startQuote = json.IndexOf('"', colonIdx + 1);
            if (startQuote < 0) return "";
            
            int endQuote = json.IndexOf('"', startQuote + 1);
            if (endQuote < 0) return "";
            
            return json.Substring(startQuote + 1, endQuote - startQuote - 1);
        }
        catch
        {
            return "";
        }
    }

    #endregion

    #region Match Lists

    private void RefreshMatchLists()
    {
        if (FirebaseMatchService.Instance == null)
            return;
        
        if (!CheckLoggedIn())
            return;
        
        string odiscordUserId = DiscordManager.Instance.GetUserId();
        
        // Load available matches (for Posted Matches scroll view)
        FirebaseMatchService.Instance.GetAvailableMatches();
        
        // Load player's replays
        FirebaseMatchService.Instance.GetPlayerReplays(odiscordUserId);
    }

    private void OnMatchesLoaded(List<MatchEntry> matches)
    {
        ClearPostedMatches();
        
        string currentUserId = "";
        if (DiscordManager.Instance != null && DiscordManager.Instance.IsLoggedIn())
        {
            currentUserId = DiscordManager.Instance.GetUserId();
        }
        
        // Filter to only show matches matching the currently selected match type
        int displayedCount = 0;
        foreach (var match in matches)
        {
            if (match.matchType == selectedMatchType)
            {
                CreateMatchEntry(match, currentUserId);
                displayedCount++;
            }
        }
        
        Debug.Log($"[MultiplayerUIManager] Displayed {displayedCount}/{matches.Count} matches (filter: {selectedMatchType})");
    }

    private void OnReplaysLoaded(List<ReplayData> replays)
    {
        ClearReplays();
        
        foreach (var replay in replays)
        {
            CreateReplayEntry(replay);
        }
        
        Debug.Log($"[MultiplayerUIManager] Displayed {replays.Count} replays");
    }

    private void CreateMatchEntry(MatchEntry match, string currentUserId)
    {
        if (multiplayerEntryPrefab == null || postedMatchesContent == null)
            return;
        
        GameObject entry = Instantiate(multiplayerEntryPrefab, postedMatchesContent);
        postedMatchEntries.Add(entry);
        
        MultiplayerEntryUI entryUI = entry.GetComponent<MultiplayerEntryUI>();
        if (entryUI != null)
        {
            bool isOwner = match.odiscordUserId == currentUserId;
            entryUI.SetupAsMatch(match, isOwner);
        }
    }

    private void CreateReplayEntry(ReplayData replay)
    {
        if (multiplayerEntryPrefab == null || replaysContent == null)
            return;
        
        GameObject entry = Instantiate(multiplayerEntryPrefab, replaysContent);
        replayEntries.Add(entry);
        
        MultiplayerEntryUI entryUI = entry.GetComponent<MultiplayerEntryUI>();
        if (entryUI != null)
        {
            entryUI.SetupAsReplay(replay);
        }
    }

    private void ClearScrollViews()
    {
        ClearPostedMatches();
        ClearReplays();
    }

    private void ClearPostedMatches()
    {
        foreach (var entry in postedMatchEntries)
        {
            if (entry != null) Destroy(entry);
        }
        postedMatchEntries.Clear();
    }

    private void ClearReplays()
    {
        foreach (var entry in replayEntries)
        {
            if (entry != null) Destroy(entry);
        }
        replayEntries.Clear();
    }

    #endregion

    #region Firebase Events

    private void OnFirebaseError(string error)
    {
        ShowDebugMessage(error);
    }

    private void OnFirebaseSuccess(string message)
    {
        // Could show success message if needed
        Debug.Log($"[MultiplayerUIManager] Firebase: {message}");
    }

    #endregion

    #region Utility Methods

    private bool CheckLoggedIn()
    {
        return DiscordManager.Instance != null && DiscordManager.Instance.IsLoggedIn();
    }

    /// <summary>
    /// Show flashing debug message (same pattern as WorkshopUIManager)
    /// </summary>
    public void ShowDebugMessage(string message, bool isError = true, float duration = 1.5f)
    {
        if (debugTextCoroutine != null)
            StopCoroutine(debugTextCoroutine);
        debugTextCoroutine = StartCoroutine(ShowDebugMessageRoutine(message, isError, duration));
        
        // Play error sound
        if (isError && SoundManager.Instance != null)
            SoundManager.Instance.PlayErrorSound();
    }

    private IEnumerator ShowDebugMessageRoutine(string message, bool isError, float duration)
    {
        if (debugText == null)
            yield break;
        
        debugText.text = message;
        debugText.color = isError ? Color.red : Color.green;
        debugText.gameObject.SetActive(true);

        // Flash effect
        float elapsed = 0f;
        while (elapsed < duration)
        {
            debugText.alpha = Mathf.PingPong(Time.time * 2f, 1f);
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        debugText.gameObject.SetActive(false);
        debugText.text = "";
    }

    #endregion

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (DiscordManager.Instance != null)
        {
            DiscordManager.Instance.OnLoginSuccess -= OnDiscordLoginSuccess;
            DiscordManager.Instance.OnLogout -= OnDiscordLogout;
        }
        
        if (FirebaseMatchService.Instance != null)
        {
            FirebaseMatchService.Instance.OnMatchesLoaded -= OnMatchesLoaded;
            FirebaseMatchService.Instance.OnReplaysLoaded -= OnReplaysLoaded;
            FirebaseMatchService.Instance.OnPlayerProfileLoaded -= OnPlayerProfileLoaded;
            FirebaseMatchService.Instance.OnError -= OnFirebaseError;
            FirebaseMatchService.Instance.OnSuccess -= OnFirebaseSuccess;
        }
    }
}
