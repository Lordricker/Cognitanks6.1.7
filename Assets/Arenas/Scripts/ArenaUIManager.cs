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

        speedSlider.minValue = 0;
        speedSlider.maxValue = speedLevels.Length - 1;
        speedSlider.wholeNumbers = true;
        speedSlider.value = currentSpeedIndex;
        speedSlider.onValueChanged.AddListener(OnSpeedSliderChanged);

        UpdateSpeedText();
        pauseOverlay.SetActive(false);
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
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
        // Unpause time before loading workshop to prevent model viewport issues
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene("Shop");
    }

    void OpenSettings()
    {
        if (settingsPanel != null)
        {
            bool isActive = settingsPanel.activeSelf;
            settingsPanel.SetActive(!isActive);
        }
    }
}
