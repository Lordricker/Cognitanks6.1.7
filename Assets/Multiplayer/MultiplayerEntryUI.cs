using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MultiplayerData;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// UI component for a multiplayer entry in the scroll views
/// Handles both Posted Matches and Replays display
/// </summary>
public class MultiplayerEntryUI : MonoBehaviour
{
    [Header("Display Fields")]
    [SerializeField] private TMP_Text teamNameText;
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text playerEloText;

    [Header("Buttons")]
    [SerializeField] private Button joinMatchButton;
    [SerializeField] private Button removeButton;
    [SerializeField] private Button viewReplayButton;

    // Data references
    private MatchEntry currentMatch;
    private ReplayData currentReplay;
    private bool isOwner;

    /// <summary>
    /// Setup this entry as a Posted Match entry
    /// </summary>
    public void SetupAsMatch(MatchEntry match, bool isOwner)
    {
        this.currentMatch = match;
        this.isOwner = isOwner;
        this.currentReplay = null;

        // Populate text fields
        if (teamNameText != null)
            teamNameText.text = match.teamName;
        
        if (playerNameText != null)
            playerNameText.text = match.discordUsername;
        
        if (playerEloText != null)
            playerEloText.text = $"ELO: {match.playerElo}";

        // Setup button visibility based on ownership
        // For testing: show both Join and Remove buttons for owners
        if (isOwner)
        {
            // Owner sees both Join (for testing) and Remove buttons
            SetButtonVisibility(joinMatch: true, remove: true, viewReplay: false);
        }
        else
        {
            // Other players see Join button only
            SetButtonVisibility(joinMatch: true, remove: false, viewReplay: false);
        }

        // Setup button listeners
        if (joinMatchButton != null)
            joinMatchButton.onClick.AddListener(OnJoinMatchClicked);
        
        if (removeButton != null)
            removeButton.onClick.AddListener(OnRemoveClicked);
    }

    /// <summary>
    /// Setup this entry as a Replay entry
    /// Shows the challenger's info (opponent who challenged your posted match)
    /// </summary>
    public void SetupAsReplay(ReplayData replay)
    {
        this.currentReplay = replay;
        this.currentMatch = null;
        this.isOwner = true; // Replays are always for the viewing player

        // Show challenger info (the opponent)
        if (teamNameText != null)
            teamNameText.text = replay.challengerTeamName;
        
        if (playerNameText != null)
            playerNameText.text = replay.challengerUsername;
        
        if (playerEloText != null)
            playerEloText.text = $"ELO: {replay.challengerEloBeforeMatch}";

        // Replays only show View Replay button
        SetButtonVisibility(joinMatch: false, remove: false, viewReplay: true);

        // Setup button listener
        if (viewReplayButton != null)
            viewReplayButton.onClick.AddListener(OnViewReplayClicked);
    }

    private void SetButtonVisibility(bool joinMatch, bool remove, bool viewReplay)
    {
        if (joinMatchButton != null)
            joinMatchButton.gameObject.SetActive(joinMatch);
        
        if (removeButton != null)
            removeButton.gameObject.SetActive(remove);
        
        if (viewReplayButton != null)
            viewReplayButton.gameObject.SetActive(viewReplay);
    }

    #region Button Handlers

    private void OnJoinMatchClicked()
    {
        if (currentMatch == null)
            return;
        
        StartCoroutine(JoinMatchCoroutine());
    }
    
    private IEnumerator JoinMatchCoroutine()
    {
        Debug.Log($"[MultiplayerEntryUI] Joining match: {currentMatch.matchId} posted by {currentMatch.discordUsername}");

        // 1. Validate TankSlotJsonManager
        if (TankSlotJsonManager.Instance == null)
        {
            ShowMessage("Error: TankSlotJsonManager not ready");
            yield break;
        }

        // 2. Count active tank slots (isActive == true only, same as posting)
        var activeTankSlots = TankSlotJsonManager.Instance.GetActiveTankSlots();
        int activeCount = activeTankSlots?.Count ?? 0;
        int requiredTanks = (int)currentMatch.matchType; // 4 or 10
        
        Debug.Log($"[MultiplayerEntryUI] Active tanks: {activeCount}, Required: {requiredTanks}");
        
        if (activeCount < requiredTanks)
        {
            ShowMessage($"Need {requiredTanks} active tanks! You have {activeCount}");
            yield break;
        }
        
        // 3. Validate all active tanks have all 5 components
        string componentError = SceneManager.ValidateActiveTankComponents();
        if (!string.IsNullOrEmpty(componentError))
        {
            ShowMessage(componentError);
            yield break;
        }

        // 4. Build challenger tank configs (with AI file contents)
        List<TankConfigReference> challengerConfigs;
        if (MultiplayerUIManager.Instance != null)
        {
            challengerConfigs = MultiplayerUIManager.Instance.BuildTankConfigs(activeTankSlots, requiredTanks);
        }
        else
        {
            ShowMessage("Error: MultiplayerUIManager not found");
            yield break;
        }
        
        // 5. Verify AI was loaded for all challenger tanks
        for (int i = 0; i < challengerConfigs.Count; i++)
        {
            var cfg = challengerConfigs[i];
            if (string.IsNullOrEmpty(cfg.turretAIScript) || string.IsNullOrEmpty(cfg.navAIScript))
            {
                Debug.LogWarning($"[MultiplayerEntryUI] Challenger tank {i} ({cfg.displayName}) missing AI: turret={cfg.turretAIScript?.Length ?? 0}, nav={cfg.navAIScript?.Length ?? 0}");
            }
        }
        
        // 6. Verify poster match has AI data
        int posterAIMissing = 0;
        foreach (var cfg in currentMatch.tankConfigs)
        {
            if (string.IsNullOrEmpty(cfg.turretAIScript) || string.IsNullOrEmpty(cfg.navAIScript))
                posterAIMissing++;
        }
        if (posterAIMissing > 0)
        {
            Debug.LogWarning($"[MultiplayerEntryUI] {posterAIMissing} poster tanks are missing AI data from server!");
        }

        // 7. Temp AI files are written during arena spawning by ConfigToSlotData()
        //    No need to pre-write them here — they survive because SetTankSlotData loads
        //    AI during Assemble() before Clear() cleans up temp files.

        // 8. Generate a deterministic match seed
        int matchSeed = currentMatch.matchId.GetHashCode();

        // 9. Populate MultiplayerMatchRunner
        MultiplayerMatchRunner.IsMultiplayerMatch = true;
        MultiplayerMatchRunner.PosterMatch = currentMatch;
        MultiplayerMatchRunner.ChallengerTankConfigs = challengerConfigs;
        MultiplayerMatchRunner.ChallengerDiscordId = DiscordManager.Instance?.GetUserId() ?? "unknown";
        MultiplayerMatchRunner.ChallengerUsername = DiscordManager.Instance?.GetUsername() ?? "Unknown";
        MultiplayerMatchRunner.ChallengerTeamName = "Challenger";
        MultiplayerMatchRunner.ChallengerElo = PlayerDataManager.Instance != null 
            ? PlayerDataManager.Instance.GetPlayerElo() 
            : 1000;
        MultiplayerMatchRunner.MatchSeed = matchSeed;

        Debug.Log($"[MultiplayerEntryUI] Match runner populated. Seed={matchSeed}, " +
                  $"PosterTanks={currentMatch.tankConfigs.Count}, ChallengerTanks={challengerConfigs.Count}");

        // 10. Set GameMode to Multiplayer and load Arena1
        PlayerPrefs.SetInt("GameMode", (int)GameMode.Multiplayer);
        PlayerPrefs.Save();

        UnityEngine.SceneManagement.SceneManager.LoadScene("Arena1");
    }

    private void ShowMessage(string message)
    {
        Debug.Log($"[MultiplayerEntryUI] {message}");
        if (MultiplayerUIManager.Instance != null)
            MultiplayerUIManager.Instance.ShowDebugMessage(message, true);
    }

    private void OnRemoveClicked()
    {
        if (currentMatch == null)
            return;
        
        Debug.Log($"[MultiplayerEntryUI] Removing match: {currentMatch.matchId}");
        
        // Delete from Firebase
        if (FirebaseMatchService.Instance != null)
        {
            FirebaseMatchService.Instance.DeleteMatch(currentMatch.matchId,
                () => {
                    // Success - destroy this entry
                    Destroy(gameObject);
                },
                (error) => {
                    if (MultiplayerUIManager.Instance != null)
                        MultiplayerUIManager.Instance.ShowDebugMessage($"Failed to remove: {error}");
                });
        }
    }

    private void OnViewReplayClicked()
    {
        if (currentReplay == null)
            return;
        
        Debug.Log($"[MultiplayerEntryUI] Viewing replay: {currentReplay.replayId}");
        
        // TODO: Implement replay viewing
        // 1. Load both team configurations
        // 2. Set the random seed
        // 3. Start match scene in replay mode
        
        if (MultiplayerUIManager.Instance != null)
        {
            MultiplayerUIManager.Instance.ShowDebugMessage("View replay - Not yet implemented", true);
        }
        
        // For now, just log the action
        // In full implementation:
        // MultiplayerMatchRunner.Instance.StartReplay(currentReplay);
    }

    #endregion

    private void OnDestroy()
    {
        // Clean up listeners
        if (joinMatchButton != null)
            joinMatchButton.onClick.RemoveAllListeners();
        
        if (removeButton != null)
            removeButton.onClick.RemoveAllListeners();
        
        if (viewReplayButton != null)
            viewReplayButton.onClick.RemoveAllListeners();
    }
}
