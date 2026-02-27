using UnityEngine;
using UnityEngine.UI;

public class SettingsUI : MonoBehaviour
{
    public Toggle buttonSoundToggle;
    public Slider masterVolumeSlider;
    public Slider uiSoundsVolumeSlider;
    public Slider musicVolumeSlider;
    public Slider sfxVolumeSlider;
    public Slider sfxGainSlider; // Optional: wire up in Inspector for a gain boost slider (1-10)

    void Start()
    {
        if (SoundManager.Instance != null)
        {
            buttonSoundToggle.isOn = SoundManager.Instance.enableSounds;
            masterVolumeSlider.value = SoundManager.Instance.masterVolume;
            uiSoundsVolumeSlider.value = SoundManager.Instance.uiSoundsVolume;
            musicVolumeSlider.value = SoundManager.Instance.musicVolume;
            sfxVolumeSlider.value = SoundManager.Instance.sfxVolume;
            if (sfxGainSlider != null)
                sfxGainSlider.value = SoundManager.Instance.sfxGainMultiplier;
        }

        buttonSoundToggle.onValueChanged.AddListener(OnButtonSoundToggleChanged);
        masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        uiSoundsVolumeSlider.onValueChanged.AddListener(OnUISoundsVolumeChanged);
        musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
        if (sfxGainSlider != null)
            sfxGainSlider.onValueChanged.AddListener(OnSFXGainChanged);
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

    private void OnSFXGainChanged(float value)
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.sfxGainMultiplier = value;
            SoundManager.Instance.ApplySFXGain();
            SoundManager.Instance.SaveSettings();
        }
    }
}
