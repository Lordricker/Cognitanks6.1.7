using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using MultiplayerData;

/// <summary>
/// Sends match event notifications to a Discord channel via webhook.
/// Attach this component to the same GameObject as FirebaseMatchService,
/// then paste your Discord webhook URL into the Inspector field.
/// </summary>
public class DiscordWebhookLogger : MonoBehaviour
{
    public static DiscordWebhookLogger Instance { get; private set; }

    [Header("Discord Webhook")]
    [Tooltip("Paste your Discord channel webhook URL here.")]
    [SerializeField] private string webhookUrl = "";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Call when a player posts a new match.
    /// Sends: MATCH POSTED! | PlayerName | 4v4
    /// </summary>
    public void LogMatchPosted(string playerName, MatchType matchType)
    {
        string typeLabel = matchType == MatchType.FourVsFour ? "4v4" : "10v10";
        string message = $"🔴 **MATCH POSTED!** | **{playerName}** | **{typeLabel}**";
        StartCoroutine(SendWebhook(message));
    }

    /// <summary>
    /// Call when a player joins (challenges) a posted match.
    /// Sends: VIEW MATCH! | PosterName vs ChallengerName | 4v4
    /// </summary>
    public void LogMatchJoined(string posterName, string challengerName, MatchType matchType)
    {
        string typeLabel = matchType == MatchType.FourVsFour ? "4v4" : "10v10";
        string message = $"⚔️ **VIEW MATCH!** | **{posterName}** vs **{challengerName}** | **{typeLabel}**";
        StartCoroutine(SendWebhook(message));
    }

    private IEnumerator SendWebhook(string content)
    {
        if (string.IsNullOrEmpty(webhookUrl))
        {
            Debug.LogWarning("[DiscordWebhookLogger] Webhook URL is not set — skipping Discord log.");
            yield break;
        }

        string json = $"{{\"content\": \"{EscapeJson(content)}\"}}";
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        using (UnityWebRequest request = new UnityWebRequest(webhookUrl, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
                Debug.LogWarning($"[DiscordWebhookLogger] Failed to send webhook: {request.error}");
            else
                Debug.Log("[DiscordWebhookLogger] Webhook sent successfully.");
        }
    }

    private static string EscapeJson(string s)
    {
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
