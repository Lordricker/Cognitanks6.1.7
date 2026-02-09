using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using MultiplayerData;

/// <summary>
/// Firebase Realtime Database REST API wrapper for multiplayer match system
/// </summary>
public class FirebaseMatchService : MonoBehaviour
{
    public static FirebaseMatchService Instance { get; private set; }

    [Header("Firebase Configuration")]
    [SerializeField] private string firebaseDatabaseUrl = "https://cognitanks-default-rtdb.firebaseio.com";
    
    // Callbacks for async operations
    public event Action<List<MatchEntry>> OnMatchesLoaded;
    public event Action<List<ReplayData>> OnReplaysLoaded;
    public event Action<PlayerProfile> OnPlayerProfileLoaded;
    public event Action<string> OnError;
    public event Action<string> OnSuccess;

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

    #region Match Operations

    /// <summary>
    /// Post a new match to Firebase
    /// </summary>
    public void PostMatch(MatchPostData matchData, Action<string> onSuccess = null, Action<string> onError = null)
    {
        StartCoroutine(PostMatchCoroutine(matchData, onSuccess, onError));
    }

    private IEnumerator PostMatchCoroutine(MatchPostData matchData, Action<string> onSuccess, Action<string> onError)
    {
        // Create the match entry
        MatchEntry entry = new MatchEntry
        {
            odiscordUserId = matchData.odiscordUserId,
            discordUsername = matchData.discordUsername,
            teamName = matchData.teamName,
            playerElo = matchData.playerElo,
            matchType = matchData.matchType,
            state = MatchState.Posted,
            postedTimestamp = MatchDataHelper.GetCurrentTimestamp(),
            tankConfigs = matchData.tankConfigs
        };

        string json = MatchDataHelper.ToJson(entry);
        string url = $"{firebaseDatabaseUrl}/matches.json";

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                // Firebase returns {"name": "matchId"}
                string response = request.downloadHandler.text;
                FirebasePostResponse postResponse = JsonUtility.FromJson<FirebasePostResponse>(response);
                
                Debug.Log($"[FirebaseMatchService] Match posted successfully: {postResponse.name}");
                onSuccess?.Invoke(postResponse.name);
                OnSuccess?.Invoke("Match posted successfully!");
            }
            else
            {
                Debug.LogError($"[FirebaseMatchService] Failed to post match: {request.error}");
                onError?.Invoke(request.error);
                OnError?.Invoke($"Failed to post match: {request.error}");
            }
        }
    }

    /// <summary>
    /// Get all available matches (Posted state only)
    /// </summary>
    public void GetAvailableMatches(Action<List<MatchEntry>> onSuccess = null, Action<string> onError = null)
    {
        StartCoroutine(GetAvailableMatchesCoroutine(onSuccess, onError));
    }

    private IEnumerator GetAvailableMatchesCoroutine(Action<List<MatchEntry>> onSuccess, Action<string> onError)
    {
        // Get all matches - we'll filter client-side to avoid needing Firebase indexes
        string url = $"{firebaseDatabaseUrl}/matches.json";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string response = request.downloadHandler.text;
                List<MatchEntry> matches = ParseMatchesResponse(response);
                
                // Filter for Posted state only (client-side)
                matches = matches.FindAll(m => m.state == MatchState.Posted);
                
                Debug.Log($"[FirebaseMatchService] Loaded {matches.Count} available matches");
                onSuccess?.Invoke(matches);
                OnMatchesLoaded?.Invoke(matches);
            }
            else
            {
                Debug.LogError($"[FirebaseMatchService] Failed to get matches: {request.error}");
                onError?.Invoke(request.error);
                OnError?.Invoke($"Failed to load matches: {request.error}");
            }
        }
    }

    /// <summary>
    /// Delete a match (only owner should call this)
    /// </summary>
    public void DeleteMatch(string matchId, Action onSuccess = null, Action<string> onError = null)
    {
        StartCoroutine(DeleteMatchCoroutine(matchId, onSuccess, onError));
    }

    private IEnumerator DeleteMatchCoroutine(string matchId, Action onSuccess, Action<string> onError)
    {
        string url = $"{firebaseDatabaseUrl}/matches/{matchId}.json";

        using (UnityWebRequest request = UnityWebRequest.Delete(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[FirebaseMatchService] Match deleted: {matchId}");
                onSuccess?.Invoke();
                OnSuccess?.Invoke("Match removed successfully!");
            }
            else
            {
                Debug.LogError($"[FirebaseMatchService] Failed to delete match: {request.error}");
                onError?.Invoke(request.error);
                OnError?.Invoke($"Failed to remove match: {request.error}");
            }
        }
    }

    /// <summary>
    /// Check if player already has an active match of the given type
    /// </summary>
    public void CheckExistingMatch(string odiscordUserId, MatchType matchType, Action<bool> onResult, Action<string> onError = null)
    {
        StartCoroutine(CheckExistingMatchCoroutine(odiscordUserId, matchType, onResult, onError));
    }

    private IEnumerator CheckExistingMatchCoroutine(string odiscordUserId, MatchType matchType, Action<bool> onResult, Action<string> onError)
    {
        // Get all matches - filter client-side
        string url = $"{firebaseDatabaseUrl}/matches.json";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string response = request.downloadHandler.text;
                List<MatchEntry> matches = ParseMatchesResponse(response);
                
                // Check if any match by this user of this type is still Posted
                bool hasExisting = matches.Exists(m => m.odiscordUserId == odiscordUserId && m.matchType == matchType && m.state == MatchState.Posted);
                onResult?.Invoke(hasExisting);
            }
            else
            {
                onError?.Invoke(request.error);
                onResult?.Invoke(false); // Assume no existing on error
            }
        }
    }

    #endregion

    #region Replay Operations

    /// <summary>
    /// Submit a completed match result and create replay
    /// </summary>
    public void SubmitMatchResult(ReplayData replayData, Action<string> onSuccess = null, Action<string> onError = null)
    {
        StartCoroutine(SubmitMatchResultCoroutine(replayData, onSuccess, onError));
    }

    private IEnumerator SubmitMatchResultCoroutine(ReplayData replayData, Action<string> onSuccess, Action<string> onError)
    {
        replayData.completedTimestamp = MatchDataHelper.GetCurrentTimestamp();
        string json = MatchDataHelper.ToJson(replayData);
        string url = $"{firebaseDatabaseUrl}/replays.json";

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                FirebasePostResponse postResponse = JsonUtility.FromJson<FirebasePostResponse>(request.downloadHandler.text);
                
                // Also delete the original match since it's completed
                yield return DeleteMatchCoroutine(replayData.matchId, null, null);
                
                // Update both players' ELO
                yield return UpdatePlayerEloCoroutine(replayData.posterDiscordId, 
                    replayData.posterEloBeforeMatch + replayData.posterEloChange, null, null);
                yield return UpdatePlayerEloCoroutine(replayData.challengerDiscordId, 
                    replayData.challengerEloBeforeMatch + replayData.challengerEloChange, null, null);
                
                Debug.Log($"[FirebaseMatchService] Match result submitted: {postResponse.name}");
                onSuccess?.Invoke(postResponse.name);
                OnSuccess?.Invoke("Match completed!");
            }
            else
            {
                Debug.LogError($"[FirebaseMatchService] Failed to submit result: {request.error}");
                onError?.Invoke(request.error);
            }
        }
    }

    /// <summary>
    /// Get replays for a specific player (matches they posted that were challenged)
    /// </summary>
    public void GetPlayerReplays(string odiscordUserId, Action<List<ReplayData>> onSuccess = null, Action<string> onError = null)
    {
        StartCoroutine(GetPlayerReplaysCoroutine(odiscordUserId, onSuccess, onError));
    }

    private IEnumerator GetPlayerReplaysCoroutine(string odiscordUserId, Action<List<ReplayData>> onSuccess, Action<string> onError)
    {
        // Get all replays - filter client-side to avoid needing Firebase indexes
        string url = $"{firebaseDatabaseUrl}/replays.json";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string response = request.downloadHandler.text;
                List<ReplayData> replays = ParseReplaysResponse(response);
                
                // Filter for this player (as poster or challenger)
                replays = replays.FindAll(r => r.posterDiscordId == odiscordUserId || r.challengerDiscordId == odiscordUserId);
                
                Debug.Log($"[FirebaseMatchService] Loaded {replays.Count} replays for player");
                onSuccess?.Invoke(replays);
                OnReplaysLoaded?.Invoke(replays);
            }
            else
            {
                Debug.LogError($"[FirebaseMatchService] Failed to get replays: {request.error}");
                onError?.Invoke(request.error);
            }
        }
    }

    #endregion

    #region Player Profile Operations

    /// <summary>
    /// Get or create player profile
    /// </summary>
    public void GetOrCreatePlayerProfile(string odiscordUserId, string username, Action<PlayerProfile> onSuccess = null, Action<string> onError = null)
    {
        StartCoroutine(GetOrCreatePlayerProfileCoroutine(odiscordUserId, username, onSuccess, onError));
    }

    private IEnumerator GetOrCreatePlayerProfileCoroutine(string odiscordUserId, string username, Action<PlayerProfile> onSuccess, Action<string> onError)
    {
        string url = $"{firebaseDatabaseUrl}/players/{odiscordUserId}.json";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string response = request.downloadHandler.text;
                
                if (response == "null" || string.IsNullOrEmpty(response))
                {
                    // Create new profile
                    PlayerProfile newProfile = new PlayerProfile
                    {
                        odiscordUserId = odiscordUserId,
                        discordUsername = username,
                        elo = 1000,
                        matchesPlayed = 0,
                        matchesWon = 0,
                        lastActiveTimestamp = MatchDataHelper.GetCurrentTimestamp()
                    };
                    
                    yield return CreatePlayerProfileCoroutine(newProfile, onSuccess, onError);
                }
                else
                {
                    PlayerProfile profile = MatchDataHelper.FromJson<PlayerProfile>(response);
                    
                    // Update username if changed
                    if (profile.discordUsername != username)
                    {
                        profile.discordUsername = username;
                        yield return UpdatePlayerProfileCoroutine(profile, null, null);
                    }
                    
                    Debug.Log($"[FirebaseMatchService] Player profile loaded: {profile.discordUsername} (ELO: {profile.elo})");
                    onSuccess?.Invoke(profile);
                    OnPlayerProfileLoaded?.Invoke(profile);
                }
            }
            else
            {
                Debug.LogError($"[FirebaseMatchService] Failed to get profile: {request.error}");
                onError?.Invoke(request.error);
            }
        }
    }

    private IEnumerator CreatePlayerProfileCoroutine(PlayerProfile profile, Action<PlayerProfile> onSuccess, Action<string> onError)
    {
        string json = MatchDataHelper.ToJson(profile);
        string url = $"{firebaseDatabaseUrl}/players/{profile.odiscordUserId}.json";

        using (UnityWebRequest request = UnityWebRequest.Put(url, json))
        {
            request.SetRequestHeader("Content-Type", "application/json");
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[FirebaseMatchService] New player profile created: {profile.discordUsername}");
                onSuccess?.Invoke(profile);
                OnPlayerProfileLoaded?.Invoke(profile);
            }
            else
            {
                Debug.LogError($"[FirebaseMatchService] Failed to create profile: {request.error}");
                onError?.Invoke(request.error);
            }
        }
    }

    private IEnumerator UpdatePlayerProfileCoroutine(PlayerProfile profile, Action onSuccess, Action<string> onError)
    {
        string json = MatchDataHelper.ToJson(profile);
        string url = $"{firebaseDatabaseUrl}/players/{profile.odiscordUserId}.json";

        using (UnityWebRequest request = UnityWebRequest.Put(url, json))
        {
            request.SetRequestHeader("Content-Type", "application/json");
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                onSuccess?.Invoke();
            }
            else
            {
                onError?.Invoke(request.error);
            }
        }
    }

    private IEnumerator UpdatePlayerEloCoroutine(string odiscordUserId, int newElo, Action onSuccess, Action<string> onError)
    {
        string url = $"{firebaseDatabaseUrl}/players/{odiscordUserId}/elo.json";

        using (UnityWebRequest request = UnityWebRequest.Put(url, newElo.ToString()))
        {
            request.SetRequestHeader("Content-Type", "application/json");
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[FirebaseMatchService] Updated ELO for {odiscordUserId}: {newElo}");
                onSuccess?.Invoke();
            }
            else
            {
                onError?.Invoke(request.error);
            }
        }
    }

    #endregion

    #region Helper Methods

    private List<MatchEntry> ParseMatchesResponse(string json)
    {
        List<MatchEntry> matches = new List<MatchEntry>();
        
        if (string.IsNullOrEmpty(json) || json == "null")
            return matches;

        // Firebase returns { "key1": {...}, "key2": {...} }
        // We need to parse this manually since Unity's JsonUtility doesn't handle dictionaries
        try
        {
            // Simple parsing - find each match entry
            var wrapper = JsonUtility.FromJson<FirebaseDictWrapper>(WrapForUnity(json, "matches"));
            // This is a simplified approach - for production, use a proper JSON library like Newtonsoft
            
            // For now, use a regex-like approach or manual parsing
            matches = ParseFirebaseDict<MatchEntry>(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[FirebaseMatchService] Error parsing matches: {e.Message}");
        }

        return matches;
    }

    private List<ReplayData> ParseReplaysResponse(string json)
    {
        List<ReplayData> replays = new List<ReplayData>();
        
        if (string.IsNullOrEmpty(json) || json == "null")
            return replays;

        try
        {
            replays = ParseFirebaseDict<ReplayData>(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[FirebaseMatchService] Error parsing replays: {e.Message}");
        }

        return replays;
    }

    /// <summary>
    /// Parse Firebase dictionary response into list of objects
    /// Firebase returns: { "key1": {object}, "key2": {object} }
    /// </summary>
    private List<T> ParseFirebaseDict<T>(string json) where T : class
    {
        List<T> items = new List<T>();
        
        if (string.IsNullOrEmpty(json) || json == "null" || json == "{}")
            return items;

        // Remove outer braces
        json = json.Trim();
        if (json.StartsWith("{")) json = json.Substring(1);
        if (json.EndsWith("}")) json = json.Substring(0, json.Length - 1);
        
        // Find each key-value pair
        int depth = 0;
        int startIndex = -1;
        string currentKey = "";
        
        for (int i = 0; i < json.Length; i++)
        {
            char c = json[i];
            
            if (c == '"' && depth == 0 && startIndex == -1)
            {
                // Start of key
                int endQuote = json.IndexOf('"', i + 1);
                currentKey = json.Substring(i + 1, endQuote - i - 1);
                i = endQuote;
            }
            else if (c == '{')
            {
                if (depth == 0) startIndex = i;
                depth++;
            }
            else if (c == '}')
            {
                depth--;
                if (depth == 0 && startIndex != -1)
                {
                    string objectJson = json.Substring(startIndex, i - startIndex + 1);
                    T item = JsonUtility.FromJson<T>(objectJson);
                    
                    // Set the ID field if it's a MatchEntry or ReplayData
                    if (item is MatchEntry match)
                        match.matchId = currentKey;
                    else if (item is ReplayData replay)
                        replay.replayId = currentKey;
                    
                    items.Add(item);
                    startIndex = -1;
                    currentKey = "";
                }
            }
        }

        return items;
    }

    private string WrapForUnity(string json, string key)
    {
        return $"{{\"{key}\": {json}}}";
    }

    [Serializable]
    private class FirebasePostResponse
    {
        public string name; // The generated key
    }

    [Serializable]
    private class FirebaseDictWrapper
    {
        // Placeholder for wrapped dictionary
    }

    #endregion
}
