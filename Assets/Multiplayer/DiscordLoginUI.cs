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
    
    private void Start()
    {
        // Setup button listeners
        if (loginButton != null)
            loginButton.onClick.AddListener(OnLoginButtonClicked);
            
        if (logoutButton != null)
            logoutButton.onClick.AddListener(OnLogoutButtonClicked);
        
        // Subscribe to Discord Manager events
        if (DiscordManager.Instance != null)
        {
            DiscordManager.Instance.OnLoginSuccess += HandleLoginSuccess;
            DiscordManager.Instance.OnLoginFailed += HandleLoginFailed;
            DiscordManager.Instance.OnLogout += HandleLogout;
        }
        
        UpdateUI();
    }
    
    private void OnLoginButtonClicked()
    {
        if (DiscordManager.Instance != null)
        {
            Debug.Log("[DiscordLoginUI] Login button clicked");
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
        Debug.Log("[DiscordLoginUI] Login successful");
        UpdateUI();
    }
    
    private void HandleLoginFailed(string error)
    {
        Debug.LogError($"[DiscordLoginUI] Login failed: {error}");
        UpdateUI();
    }
    
    private void HandleLogout()
    {
        Debug.Log("[DiscordLoginUI] Logged out");
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
