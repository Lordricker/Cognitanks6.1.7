using UnityEngine;
using Discord.Sdk;
using System;

public class DiscordManager : MonoBehaviour
{
    [Header("Discord Configuration")]
    [SerializeField] private ulong clientId; // Your Discord Application Client ID
    
    [Header("Status")]
    [SerializeField] private bool isInitialized = false;
    [SerializeField] private bool isLoggedIn = false;
    
    private Discord.Sdk.Client discordClient;
    private Discord.Sdk.UserHandle currentUserHandle;
    private string currentAccessToken; // Store the token for logout
    
    // Singleton instance (optional, but useful)
    public static DiscordManager Instance { get; private set; }
    
    // Events for UI/other systems to listen to
    public event Action OnLoginSuccess;
    public event Action<string> OnLoginFailed;
    public event Action OnLogout;
    
    private void Awake()
    {
        // Singleton setup
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
        InitializeDiscord();
    }
    
    private void InitializeDiscord()
    {
        if (clientId == 0)
        {
            Debug.LogError("Discord Client ID is not set! Please set it in the Inspector.");
            return;
        }
        
        try
        {
            // Create the Discord Client (no client ID needed here - it's in AuthorizationArgs)
            discordClient = new Discord.Sdk.Client();
            
            // Subscribe to status changes
            discordClient.SetStatusChangedCallback(HandleStatusChanged);
            
            isInitialized = true;
            Debug.Log("Discord Social SDK initialized successfully!");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to initialize Discord SDK: {e.Message}");
            isInitialized = false;
        }
    }
    
    private void HandleStatusChanged(Discord.Sdk.Client.Status status, Discord.Sdk.Client.Error error, int errorDetail)
    {
        Debug.Log($"Discord Status Changed: {status}, Error: {error}");
        
        if (status == Discord.Sdk.Client.Status.Ready)
        {
            // SDK is ready, check if we have a user
            try
            {
                currentUserHandle = discordClient.GetCurrentUserV2();
                if (currentUserHandle != null)
                {
                    isLoggedIn = true;
                    var userId = currentUserHandle.Id();
                    var username = currentUserHandle.Username();
                    Debug.Log($"Already logged in as: {username} (ID: {userId})");
                    OnLoginSuccess?.Invoke();
                }
            }
            catch (Exception e)
            {
                Debug.Log($"Not logged in yet: {e.Message}");
            }
        }
        else if (error != Discord.Sdk.Client.Error.None)
        {
            Debug.LogError($"Discord Error: {error}, Detail: {errorDetail}");
        }
    }
    
    /// <summary>
    /// Call this method to start the Discord login flow
    /// </summary>
    public void Login()
    {
        if (!isInitialized)
        {
            Debug.LogError("Discord SDK is not initialized!");
            OnLoginFailed?.Invoke("SDK not initialized");
            return;
        }
        
        if (isLoggedIn)
        {
            Debug.Log("Already logged in!");
            return;
        }
        
        Debug.Log("Starting Discord login flow...");
        
        try
        {
            // Create authorization arguments
            var authArgs = new Discord.Sdk.AuthorizationArgs();
            authArgs.SetClientId(clientId);
            authArgs.SetScopes(Discord.Sdk.Client.GetDefaultPresenceScopes()); // or GetDefaultCommunicationScopes() for full features
            
            // Start the authorization flow
            discordClient.Authorize(authArgs, (result, code, redirectUri) =>
            {
                if (result.Type() == Discord.Sdk.ErrorType.None)
                {
                    Debug.Log($"Authorization successful! Code: {code}");
                    
                    // Get the current user
                    try
                    {
                        currentUserHandle = discordClient.GetCurrentUserV2();
                        if (currentUserHandle != null)
                        {
                            isLoggedIn = true;
                            var userId = currentUserHandle.Id();
                            var username = currentUserHandle.Username();
                            Debug.Log($"Successfully logged in as: {username} (ID: {userId})");
                            OnLoginSuccess?.Invoke();
                        }
                        else
                        {
                            Debug.Log("Auth succeeded but user not available yet. Waiting for Ready status...");
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Failed to get user after auth: {e.Message}");
                        OnLoginFailed?.Invoke(e.Message);
                    }
                }
                else
                {
                    string errorMsg = $"Authorization failed: {result.Error()}";
                    Debug.LogError(errorMsg);
                    OnLoginFailed?.Invoke(errorMsg);
                }
            });
        }
        catch (Exception e)
        {
            Debug.LogError($"Discord login failed: {e.Message}");
            OnLoginFailed?.Invoke(e.Message);
            isLoggedIn = false;
        }
    }
    
    /// <summary>
    /// Log out the current Discord user
    /// </summary>
    public void Logout()
    {
        if (!isLoggedIn)
        {
            Debug.Log("Not logged in!");
            return;
        }
        
        try
        {
            // For logout, we just clear our local state
            // RevokeToken requires the applicationId and token which we'd need to store from auth
            isLoggedIn = false;
            
            if (currentUserHandle != null)
            {
                currentUserHandle.Dispose();
                currentUserHandle = null;
            }
            
            Debug.Log("Logged out successfully");
            OnLogout?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogError($"Logout failed: {e.Message}");
        }
    }
    
    /// <summary>
    /// Get the current logged-in user info
    /// </summary>
    public Discord.Sdk.UserHandle GetCurrentUser()
    {
        if (!isLoggedIn)
            return null;
            
        return currentUserHandle;
    }
    
    /// <summary>
    /// Check if user is currently logged in
    /// </summary>
    public bool IsLoggedIn()
    {
        return isLoggedIn;
    }
    
    // Note: RunCallbacks is handled automatically by the Discord SDK via Unity's PlayerLoop
    // No need to call it manually in Update()
    
    private void OnDestroy()
    {
        // Clean up
        if (currentUserHandle != null)
        {
            currentUserHandle.Dispose();
            currentUserHandle = null;
        }
        
        if (discordClient != null)
        {
            discordClient.Dispose();
            discordClient = null;
        }
    }
}
