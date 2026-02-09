using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MultiplayerData;

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
        if (isOwner)
        {
            // Owner sees Remove button only
            SetButtonVisibility(joinMatch: false, remove: true, viewReplay: false);
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
        
        Debug.Log($"[MultiplayerEntryUI] Joining match: {currentMatch.matchId}");
        
        // TODO: Implement join match flow
        // 1. Download match config
        // 2. Build challenger's tank configs
        // 3. Start the match scene with both teams
        // 4. After match, submit result to Firebase
        
        if (MultiplayerUIManager.Instance != null)
        {
            MultiplayerUIManager.Instance.ShowDebugMessage("Join match - Not yet implemented", true);
        }
        
        // For now, just log the action
        // In full implementation:
        // MultiplayerMatchRunner.Instance.StartMatch(currentMatch);
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
