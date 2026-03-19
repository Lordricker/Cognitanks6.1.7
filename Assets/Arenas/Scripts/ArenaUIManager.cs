using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ArenaUIManager : MonoBehaviour
{
    [Header("Camera Controls")]
    public Button globalCameraButton;
    public Button cycleTankCameraButton;
    public Button turretCameraButton; // New dedicated turret camera button

    [Header("Game Speed Controls")]
    public Slider speedSlider;
    public TMP_Text speedText;

    [Header("Pause Menu")]    
    public GameObject pauseOverlay;
    public Button pauseButton;
    public Button resumeButton;
    public Button settingsButton;
    public GameObject settingsPanel;

    [Header("Stats Panel")]
    public Button statsPanelButton;
    public GameObject statsPanel;

    private float[] speedLevels = { 0.2f, 0.5f, 1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f, 9f, 10f };
    private int currentSpeedIndex = 2;
    private CameraController cameraController; // Found automatically

    void Start()
    {
        // Find camera controller automatically
        cameraController = FindFirstObjectByType<CameraController>();

        globalCameraButton.onClick.AddListener(OnGlobalCamera);
        cycleTankCameraButton.onClick.AddListener(OnCycleTankCamera);
        if (turretCameraButton != null)
            turretCameraButton.onClick.AddListener(OnTurretCamera);
        pauseButton.onClick.AddListener(PauseGame);
        resumeButton.onClick.AddListener(ResumeGame);
        settingsButton.onClick.AddListener(OpenSettings);
        if (statsPanelButton != null)
            statsPanelButton.onClick.AddListener(ToggleStatsPanel);

        speedSlider.minValue = 0;
        speedSlider.maxValue = speedLevels.Length - 1;
        speedSlider.wholeNumbers = true;
        speedSlider.value = currentSpeedIndex;
        speedSlider.onValueChanged.AddListener(OnSpeedSliderChanged);

        UpdateSpeedText();
        pauseOverlay.SetActive(false);
        if (settingsPanel != null)
        {
            CanvasGroup settingsCanvasGroup = settingsPanel.GetComponent<CanvasGroup>();
            if (settingsCanvasGroup != null)
                settingsCanvasGroup.alpha = 0f;
            settingsPanel.SetActive(false);
        }
        
        // Initialize stats panel as hidden
        if (statsPanel != null)
            statsPanel.SetActive(false);
    }

    void OnGlobalCamera()
    {
        if (cameraController != null)
            cameraController.MoveToGlobalAnchor();
    }

    void OnCycleTankCamera()
    {
        if (cameraController != null)
            cameraController.CycleTankAnchor();
    }

    void OnTurretCamera()
    {
        if (cameraController != null)
            cameraController.SwitchToTurretCamera();
    }

    void OnSpeedSliderChanged(float value)
    {
        currentSpeedIndex = Mathf.RoundToInt(value);
        Time.timeScale = speedLevels[currentSpeedIndex];
        UpdateSpeedText();
    }

    void UpdateSpeedText()
    {
        speedText.text = $"x{speedLevels[currentSpeedIndex]} Speed";
    }

    void PauseGame()
    {
        Time.timeScale = 0f;
        pauseOverlay.SetActive(true);
    }

    void ResumeGame()
    {
        Time.timeScale = speedLevels[currentSpeedIndex];
        pauseOverlay.SetActive(false);
    }

    public void ReturnToWorkshop()
    {
        // If the match result was never recorded (player quit mid-game), count as a loss.
        if (PlayerPrefs.GetInt("ArenaMatchWon", 1) == 0 && PlayerDataManager.Instance != null)
        {
            PlayerDataManager.Instance.playerData.missionFailCount++;
            PlayerDataManager.Instance.SavePlayerData();
            Debug.Log($"[ArenaUIManager] Mid-game quit counted as fail. Count: {PlayerDataManager.Instance.playerData.missionFailCount}");
        }
        // Unpause time before loading workshop to prevent model viewport issues
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene("Shop");
    }

    void OpenSettings()
    {
        if (settingsPanel != null)
        {
            CanvasGroup canvasGroup = settingsPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                Debug.LogWarning("Settings panel needs a CanvasGroup component for fading!");
                settingsPanel.SetActive(!settingsPanel.activeSelf);
                return;
            }

            if (settingsPanel.activeSelf)
            {
                // Fade out
                StartCoroutine(FadePanel(settingsPanel, canvasGroup, 1f, 0f, 0.2f, () => settingsPanel.SetActive(false)));
            }
            else
            {
                // Set alpha to 0 BEFORE activating to prevent flash
                canvasGroup.alpha = 0f;
                settingsPanel.SetActive(true);
                // Fade in
                StartCoroutine(FadePanel(settingsPanel, canvasGroup, 0f, 1f, 0.2f, null));
            }
        }
    }

    void ToggleStatsPanel()
    {
        if (statsPanel != null)
        {
            CanvasGroup canvasGroup = statsPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                Debug.LogWarning("Stats panel needs a CanvasGroup component for fading!");
                statsPanel.SetActive(!statsPanel.activeSelf);
                return;
            }

            if (statsPanel.activeSelf)
            {
                // Fade out
                StartCoroutine(FadePanel(statsPanel, canvasGroup, 1f, 0f, 0.2f, () => statsPanel.SetActive(false)));
            }
            else
            {
                // Set alpha to 0 BEFORE activating to prevent flash
                canvasGroup.alpha = 0f;
                statsPanel.SetActive(true);
                // Fade in
                StartCoroutine(FadePanel(statsPanel, canvasGroup, 0f, 1f, 0.2f, null));
            }
        }
    }

    private System.Collections.IEnumerator FadePanel(GameObject panel, CanvasGroup canvasGroup, float startAlpha, float endAlpha, float duration, System.Action onComplete)
    {
        float time = 0f;
        canvasGroup.alpha = startAlpha;

        while (time < duration)
        {
            time += Time.unscaledDeltaTime; // Use unscaledDeltaTime so game speed doesn't affect UI fade
            canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, time / duration);
            yield return null;
        }

        canvasGroup.alpha = endAlpha;
        onComplete?.Invoke();
    }
}
