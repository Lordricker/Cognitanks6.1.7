using System;
using System.Collections;
using System.Net;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Discord OAuth2 login manager using manual HTTP flow (no SDK dependency).
/// Uses implicit grant flow with a local HTTP listener to capture the access token.
/// </summary>
public class DiscordManager : MonoBehaviour
{
    [Header("Discord Configuration")]
    [SerializeField] private string clientId = ""; // Your Discord Application Client ID
    [SerializeField] private int callbackPort = 9521; // Local port for OAuth2 callback

    [Header("Status")]
    [SerializeField] private bool isLoggedIn = false;

    // User info
    private string discordUserId;
    private string discordUsername;
    private string discordAvatar;
    private string accessToken;

    // Local HTTP listener
    private HttpListener httpListener;
    private Thread listenerThread;
    private volatile string capturedToken;
    private volatile bool tokenCaptured = false;
    private volatile bool listenerError = false;
    private volatile string listenerErrorMessage;
    private bool loginInProgress = false;

    // Singleton
    public static DiscordManager Instance { get; private set; }

    // Events (same interface as before)
    public event Action OnLoginSuccess;
    public event Action<string> OnLoginFailed;
    public event Action OnLogout;

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

    private void Start()
    {
        if (string.IsNullOrEmpty(clientId))
        {
            Debug.LogError("[DiscordManager] Client ID is not set! Please set it in the Inspector.");
        }
        else
        {
            Debug.LogWarning($"[DiscordManager] Initialized with Client ID: {clientId}, Callback port: {callbackPort}");
        }
    }

    #region Public API

    /// <summary>
    /// Start the Discord OAuth2 login flow
    /// </summary>
    public void Login()
    {
        if (isLoggedIn)
        {
            Debug.LogWarning("[DiscordManager] Already logged in!");
            return;
        }

        if (loginInProgress)
        {
            Debug.LogWarning("[DiscordManager] Login already in progress!");
            return;
        }

        if (string.IsNullOrEmpty(clientId))
        {
            Debug.LogError("[DiscordManager] Client ID not set!");
            OnLoginFailed?.Invoke("Client ID not configured");
            return;
        }

        StartCoroutine(LoginCoroutine());
    }

    /// <summary>
    /// Log out the current user
    /// </summary>
    public void Logout()
    {
        if (!isLoggedIn)
        {
            Debug.Log("[DiscordManager] Not logged in!");
            return;
        }

        isLoggedIn = false;
        discordUserId = null;
        discordUsername = null;
        discordAvatar = null;
        accessToken = null;

        Debug.LogWarning("[DiscordManager] Logged out successfully");
        OnLogout?.Invoke();
    }

    /// <summary>
    /// Check if user is currently logged in
    /// </summary>
    public bool IsLoggedIn()
    {
        return isLoggedIn;
    }

    /// <summary>
    /// Get the Discord user ID as string (e.g. "123456789012345678")
    /// </summary>
    public string GetUserId()
    {
        return discordUserId;
    }

    /// <summary>
    /// Get the Discord username
    /// </summary>
    public string GetUsername()
    {
        return discordUsername;
    }

    /// <summary>
    /// Get the Discord avatar hash
    /// </summary>
    public string GetAvatarHash()
    {
        return discordAvatar;
    }

    #endregion

    #region OAuth2 Flow

    private IEnumerator LoginCoroutine()
    {
        loginInProgress = true;
        Debug.LogWarning("[DiscordManager] Starting OAuth2 login flow...");

        // Clean up any leftover listener from a previous attempt
        StopListener();

        tokenCaptured = false;
        capturedToken = null;
        listenerError = false;
        listenerErrorMessage = null;

        string redirectUri = $"http://127.0.0.1:{callbackPort}/callback/";

        // Start local HTTP listener (listen on root to handle both /callback/ and /token)
        try
        {
            httpListener = new HttpListener();
            httpListener.Prefixes.Add($"http://127.0.0.1:{callbackPort}/");
            httpListener.Start();
            Debug.LogWarning($"[DiscordManager] Local HTTP listener started on port {callbackPort}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[DiscordManager] Failed to start HTTP listener: {e.Message}");
            OnLoginFailed?.Invoke($"Failed to start local server: {e.Message}");
            loginInProgress = false;
            yield break;
        }

        // Start listening on background thread
        listenerThread = new Thread(ListenForCallback);
        listenerThread.IsBackground = true;
        listenerThread.Start();

        // Open browser for Discord OAuth2 with implicit grant (response_type=token)
        // This returns the access token directly — no code exchange needed!
        string authUrl = $"https://discord.com/oauth2/authorize" +
                         $"?client_id={clientId}" +
                         $"&response_type=token" +
                         $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                         $"&scope=identify";

        Debug.LogWarning($"[DiscordManager] Opening browser for authorization...");
        Debug.Log($"[DiscordManager] Auth URL: {authUrl}");
        Application.OpenURL(authUrl);

        // Wait for token capture (with timeout)
        float timeout = 120f; // 2 minutes
        float elapsed = 0f;

        while (!tokenCaptured && !listenerError && elapsed < timeout)
        {
            yield return new WaitForSeconds(0.3f);
            elapsed += 0.3f;
        }

        // Cleanup listener
        StopListener();

        if (listenerError)
        {
            Debug.LogError($"[DiscordManager] Listener error: {listenerErrorMessage}");
            OnLoginFailed?.Invoke(listenerErrorMessage);
            loginInProgress = false;
            yield break;
        }

        if (!tokenCaptured || string.IsNullOrEmpty(capturedToken))
        {
            Debug.LogError("[DiscordManager] Login timed out or was cancelled");
            OnLoginFailed?.Invoke("Login timed out. Please try again.");
            loginInProgress = false;
            yield break;
        }

        accessToken = capturedToken;
        Debug.LogWarning("[DiscordManager] Token captured! Fetching user info...");

        // Fetch user info from Discord API
        yield return FetchUserInfo();
    }

    /// <summary>
    /// Background thread: Listen for the OAuth2 callback.
    /// Discord redirects to our local server with the token in the URL fragment.
    /// Since HTTP servers can't see URL fragments, we serve an HTML page that
    /// extracts the token via JavaScript and sends it back.
    /// </summary>
    private void ListenForCallback()
    {
        try
        {
            UnityEngine.Debug.Log("[DiscordManager] [Thread] Waiting for callback...");
            
            // Request 1: Discord redirects here. Token is in URL fragment (#access_token=...)
            // We serve an HTML page that extracts it via JavaScript.
            var context = httpListener.GetContext();
            
            UnityEngine.Debug.Log($"[DiscordManager] [Thread] Received request: {context.Request.Url}");

            string html = @"<!DOCTYPE html>
<html>
<head><title>Cognitanks Login</title></head>
<body style='font-family: sans-serif; text-align: center; padding-top: 50px; background: #1a1a2e; color: white;'>
<h2>Processing login...</h2>
<div id='debug' style='margin-top: 20px; font-size: 12px; color: #888;'></div>
<script>
var debugDiv = document.getElementById('debug');
debugDiv.innerHTML = 'Full URL: ' + window.location.href + '<br>Hash: ' + window.location.hash;

var hash = window.location.hash.substring(1);
debugDiv.innerHTML += '<br>Hash params: ' + hash;

var params = new URLSearchParams(hash);
var token = params.get('access_token');
var error = params.get('error');
var errorDesc = params.get('error_description');

debugDiv.innerHTML += '<br>Token: ' + (token ? 'FOUND' : 'NOT FOUND');
if (error) {
    debugDiv.innerHTML += '<br>Error: ' + error + '<br>Description: ' + errorDesc;
}

if (token) {
    debugDiv.innerHTML += '<br>Sending token to Unity...';
    fetch('/token?access_token=' + encodeURIComponent(token))
        .then(function(response) {
            debugDiv.innerHTML += '<br>Response status: ' + response.status;
            document.body.innerHTML = '<h2 style=""color: #0CB300;"">&#10004; Login successful!</h2><p>You can close this tab and return to Cognitanks.</p>';
        })
        .catch(function(err) {
            debugDiv.innerHTML += '<br>Fetch error: ' + err;
            document.body.innerHTML = '<h2 style=""color: #ff4444;"">&#10008; Something went wrong. Please try again.</h2><div>' + err + '</div>';
        });
} else if (error) {
    document.body.innerHTML = '<h2 style=""color: #ff4444;"">&#10008; Discord Authorization Error</h2><p>' + error + ': ' + errorDesc + '</p>';
} else {
    document.body.innerHTML = '<h2 style=""color: #ff4444;"">&#10008; No token in URL</h2><p>URL hash: ' + hash + '</p>';
}
</script>
</body>
</html>";

            byte[] buffer = Encoding.UTF8.GetBytes(html);
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.ContentLength64 = buffer.Length;
            context.Response.StatusCode = 200;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.Close();

            UnityEngine.Debug.Log("[DiscordManager] [Thread] HTML page served, waiting for token callback...");

            // Request 2: JavaScript sends us the token via fetch
            var context2 = httpListener.GetContext();
            string token = context2.Request.QueryString["access_token"];

            UnityEngine.Debug.Log($"[DiscordManager] [Thread] Token callback received! Token: {(string.IsNullOrEmpty(token) ? "EMPTY" : "CAPTURED")}");

            string responseText = "OK";
            byte[] responseBuffer = Encoding.UTF8.GetBytes(responseText);
            context2.Response.ContentLength64 = responseBuffer.Length;
            context2.Response.StatusCode = 200;
            context2.Response.OutputStream.Write(responseBuffer, 0, responseBuffer.Length);
            context2.Response.Close();

            if (!string.IsNullOrEmpty(token))
            {
                capturedToken = token;
                tokenCaptured = true;
                UnityEngine.Debug.Log("[DiscordManager] [Thread] Token captured successfully!");
            }
            else
            {
                listenerError = true;
                listenerErrorMessage = "No access token received from Discord";
                UnityEngine.Debug.LogError("[DiscordManager] [Thread] No token in callback!");
            }
        }
        catch (HttpListenerException e)
        {
            UnityEngine.Debug.Log($"[DiscordManager] [Thread] HttpListenerException (expected on cleanup): {e.Message}");
        }
        catch (ObjectDisposedException e)
        {
            UnityEngine.Debug.Log($"[DiscordManager] [Thread] ObjectDisposedException (expected on cleanup): {e.Message}");
        }
        catch (Exception e)
        {
            listenerError = true;
            listenerErrorMessage = $"Callback listener error: {e.Message}";
            UnityEngine.Debug.LogError($"[DiscordManager] [Thread] Unexpected error: {e.Message}\n{e.StackTrace}");
        }
    }

    /// <summary>
    /// Use the access token to fetch the user's Discord profile
    /// </summary>
    private IEnumerator FetchUserInfo()
    {
        using (var request = UnityWebRequest.Get("https://discord.com/api/users/@me"))
        {
            request.SetRequestHeader("Authorization", $"Bearer {accessToken}");
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;
                Debug.LogWarning($"[DiscordManager] User info received!");

                var userInfo = JsonUtility.FromJson<DiscordUserResponse>(json);
                discordUserId = userInfo.id;
                discordUsername = userInfo.username;
                discordAvatar = userInfo.avatar;
                isLoggedIn = true;

                Debug.LogWarning($"[DiscordManager] Logged in as: {discordUsername} (ID: {discordUserId})");
                loginInProgress = false;
                OnLoginSuccess?.Invoke();
            }
            else
            {
                string error = $"Failed to fetch user info: {request.error} (HTTP {request.responseCode})";
                Debug.LogError($"[DiscordManager] {error}");
                loginInProgress = false;
                OnLoginFailed?.Invoke(error);
            }
        }
    }

    #endregion

    #region Cleanup

    private void StopListener()
    {
        try
        {
            if (httpListener != null)
            {
                if (httpListener.IsListening)
                    httpListener.Stop();
                httpListener.Close();
            }
        }
        catch (Exception) { }
        httpListener = null;

        // Abort the background listener thread if still alive
        try
        {
            if (listenerThread != null && listenerThread.IsAlive)
            {
                listenerThread.Join(500); // Wait up to 500ms for graceful exit
                if (listenerThread.IsAlive)
                    listenerThread.Abort();
            }
        }
        catch (Exception) { }
        listenerThread = null;
    }

    private void OnDestroy()
    {
        StopListener();
    }

    private void OnApplicationQuit()
    {
        StopListener();
    }

    #endregion

    [Serializable]
    private class DiscordUserResponse
    {
        public string id;
        public string username;
        public string discriminator;
        public string avatar;
        public string global_name;
    }
}
