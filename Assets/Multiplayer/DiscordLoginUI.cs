using UnityEngine;
using UnityEngine.UI;

public class DiscordLoginUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button loginButton;
    [SerializeField] private Button logoutButton;
    
    [Header("Button Colors")]
    [SerializeField] private Color loggedInColor = new Color(0.047f, 0.702f, 0f, 1f); // #0CB300
    [SerializeField] private Color loggedOutColor = new Color(0.455f, 0.318f, 0.337f, 1f); // #745156
    
    private bool subscribedToEvents = false;
    
    private void Start()
    {
        Debug.LogWarning("====== [DiscordLoginUI] START CALLED - SCRIPT IS ALIVE! ======");
        
        // Setup button listeners
        if (loginButton != null)
        {
            loginButton.onClick.AddListener(OnLoginButtonClicked);
            Debug.LogWarning("[DiscordLoginUI] Login button listener added");
        }
        else
        {
            Debug.LogError("[DiscordLoginUI] loginButton is NOT assigned in Inspector!");
        }
            
        if (logoutButton != null)
            logoutButton.onClick.AddListener(OnLogoutButtonClicked);
        
        TrySubscribeToDiscordManager();
        UpdateUI();
    }
    
    private void TrySubscribeToDiscordManager()
    {
        if (subscribedToEvents) return;
        
        if (DiscordManager.Instance != null)
        {
            DiscordManager.Instance.OnLoginSuccess += HandleLoginSuccess;
            DiscordManager.Instance.OnLoginFailed += HandleLoginFailed;
            DiscordManager.Instance.OnLogout += HandleLogout;
            subscribedToEvents = true;
            Debug.Log("[DiscordLoginUI] Subscribed to DiscordManager events");
        }
        else
        {
            Debug.LogWarning("[DiscordLoginUI] DiscordManager.Instance not ready yet, will retry...");
        }
    }
    
    private void Update()
    {
        // Retry subscribing if DiscordManager wasn't ready at Start
        if (!subscribedToEvents)
        {
            TrySubscribeToDiscordManager();
        }
        
        // Update UI when logged in
        if (DiscordManager.Instance != null && DiscordManager.Instance.IsLoggedIn())
        {
            UpdateUI();
        }
    }
    
    private void OnLoginButtonClicked()
    {
        Debug.LogError("====== [DiscordLoginUI] LOGIN BUTTON CLICKED! ======");
        
        if (DiscordManager.Instance != null)
        {
            Debug.LogWarning($"[DiscordLoginUI] DiscordManager found. IsLoggedIn: {DiscordManager.Instance.IsLoggedIn()}");
            DiscordManager.Instance.Login();
        }
        else
        {
            Debug.LogError("[DiscordLoginUI] Discord Manager not found!");
        }
    }
    
    private void OnLogoutButtonClicked()
    {
        if (DiscordManager.Instance != null)
        {
            DiscordManager.Instance.Logout();
        }
    }
    
    private void HandleLoginSuccess()
    {
        Debug.Log("[DiscordLoginUI] >>> LOGIN SUCCESS EVENT RECEIVED <<<");
        UpdateUI();
    }
    
    private void HandleLoginFailed(string error)
    {
        Debug.LogError($"[DiscordLoginUI] >>> LOGIN FAILED: {error} <<<");
        UpdateUI();
    }
    
    private void HandleLogout()
    {
        Debug.Log("[DiscordLoginUI] >>> LOGOUT EVENT RECEIVED <<<");
        UpdateUI();
    }
    
    private void UpdateUI()
    {
        bool isLoggedIn = DiscordManager.Instance != null && DiscordManager.Instance.IsLoggedIn();
        
        // Change login button color based on login status
        if (loginButton != null)
        {
            Image buttonImage = loginButton.GetComponent<Image>();
            if (buttonImage != null)
            {
                buttonImage.color = isLoggedIn ? loggedInColor : loggedOutColor;
            }
        }
        
        // Optional: Show/hide logout button
        if (logoutButton != null)
            logoutButton.gameObject.SetActive(isLoggedIn);
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from events
        if (DiscordManager.Instance != null)
        {
            DiscordManager.Instance.OnLoginSuccess -= HandleLoginSuccess;
            DiscordManager.Instance.OnLoginFailed -= HandleLoginFailed;
            DiscordManager.Instance.OnLogout -= HandleLogout;
        }
    }
}
