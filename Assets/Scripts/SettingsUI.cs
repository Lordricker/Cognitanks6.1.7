using UnityEngine;
using UnityEngine.UI;

public class SettingsUI : MonoBehaviour
{
    public Toggle buttonSoundToggle;
    public Slider masterVolumeSlider;
    public Slider uiSoundsVolumeSlider;
    public Slider musicVolumeSlider;
    public Slider sfxVolumeSlider;

    void Start()
    {
        if (SoundManager.Instance != null)
        {
            buttonSoundToggle.isOn = SoundManager.Instance.enableSounds;
            masterVolumeSlider.value = SoundManager.Instance.masterVolume;
            uiSoundsVolumeSlider.value = SoundManager.Instance.uiSoundsVolume;
            musicVolumeSlider.value = SoundManager.Instance.musicVolume;
            sfxVolumeSlider.value = SoundManager.Instance.sfxVolume;
        }

        buttonSoundToggle.onValueChanged.AddListener(OnButtonSoundToggleChanged);
        masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        uiSoundsVolumeSlider.onValueChanged.AddListener(OnUISoundsVolumeChanged);
        musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
    }

    private void OnButtonSoundToggleChanged(bool isOn)
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.enableSounds = isOn;
            SoundManager.Instance.SaveSettings();
        }
    }

    private void OnMasterVolumeChanged(float value)
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.masterVolume = value;
            SoundManager.Instance.SaveSettings();
            SoundManager.Instance.UpdateCurrentMusicVolume();
        }
    }

    private void OnUISoundsVolumeChanged(float value)
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.uiSoundsVolume = value;
            SoundManager.Instance.SaveSettings();
        }
    }

    private void OnMusicVolumeChanged(float value)
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.musicVolume = value;
            SoundManager.Instance.SaveSettings();
            SoundManager.Instance.UpdateCurrentMusicVolume();
        }
    }

    private void OnSFXVolumeChanged(float value)
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.sfxVolume = value;
            SoundManager.Instance.SaveSettings();
        }
    }
}
