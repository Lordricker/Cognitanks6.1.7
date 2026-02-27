using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using System.Collections;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("Volumes")]
    [Tooltip("Master volume multiplier (0-1).")]
    [Range(0f, 1f)]
    public float masterVolume = 1f;

    [Header("Button Sounds")]
    [Tooltip("Enable/disable all button sounds.")]
    public bool enableSounds = true;

    [Tooltip("Volume for UI sounds (buttons).")]
    [Range(0f, 1f)]
    public float uiSoundsVolume = 1f;

    [Tooltip("Audio clips for different button sounds.")]
    public AudioClip[] buttonSounds; // Assign clips in Inspector, indexed by enum

    [Tooltip("Individual volumes for button sounds (0-1).")]
    public float[] buttonSoundVolumes;

    [Header("SFX Sounds")]
    [Tooltip("Volume for SFX sounds (tanks, bullets, etc).")]
    [Range(0f, 1f)]
    public float sfxVolume = 1f;

    [Tooltip("Gain multiplier applied to all positional SFX via AudioMixer (above 1 boosts beyond the normal 0-1 cap, up to +20dB at 10x).")]
    [Range(1f, 10f)]
    public float sfxGainMultiplier = 1f;

    [Tooltip("AudioMixer used for SFX gain boost. Assign the SFX group's mixer in the Inspector.")]
    public AudioMixer sfxMixer;

    [Tooltip("AudioMixerGroup to route positional SFX through (the SFX group inside NewAudioMixer).")]
    public AudioMixerGroup sfxMixerGroup;

    // The exposed parameter name on the mixer group (set in Unity's AudioMixer window)
    private const string SFXGainParam = "SFXGain";

    [Tooltip("Tank driving sound clip.")]
    public AudioClip tankDrivingSound;
    [Range(0f, 1f)]
    public float tankDrivingVolume = 1f;

    [Tooltip("Gunshot sound clip.")]
    public AudioClip gunshotSound;
    [Range(0f, 1f)]
    public float gunshotVolume = 1f;

    [Tooltip("Bullet hit sound clip.")]
    public AudioClip bulletHitSound;
    [Range(0f, 1f)]
    public float bulletHitVolume = 1f;

    [Tooltip("Round loss sound clip.")]
    public AudioClip roundLossSound;
    [Range(0f, 1f)]
    public float roundLossVolume = 1f;

    [Tooltip("Round win sound clip.")]
    public AudioClip roundWinSound;
    [Range(0f, 1f)]
    public float roundWinVolume = 1f;

    [Tooltip("Explosion sound clip.")]
    public AudioClip explosionSound;
    [Range(0f, 1f)]
    public float explosionVolume = 1f;

    [Tooltip("Error sound clip.")]
    public AudioClip errorSound;
    [Range(0f, 1f)]
    public float errorSoundVolume = 1f;

    [Header("Music")]
    [Tooltip("Global volume for music.")]
    [Range(0f, 1f)]
    public float musicVolume = 1f;

    [Tooltip("Duration for music fade in/out (seconds).")]
    public float fadeDuration = 1f;

    [Header("Track Volumes (Dev Only)")]
    // Now using individual volumes above

    [Tooltip("Music clips for Arena scenes (random selection).")]
    public AudioClip[] arenaMusic;

    [Tooltip("Individual volumes for Arena music tracks (0-1).")]
    public float[] arenaMusicVolumes;

    [Tooltip("Music clip for AiEditor and Shop scenes.")]
    public AudioClip editorShopMusic;

    [Tooltip("Volume for Editor/Shop music track (0-1).")]
    public float editorShopMusicVolume = 1f;

    private AudioSource buttonAudioSource;
    private AudioSource sfxAudioSource;
    private AudioSource musicAudioSource;
    private AudioClip currentMusicClip;
    private int currentArenaIndex = -1;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Button sounds source
        buttonAudioSource = GetComponent<AudioSource>();
        if (buttonAudioSource == null)
        {
            buttonAudioSource = gameObject.AddComponent<AudioSource>();
            buttonAudioSource.playOnAwake = false;
        }

        // SFX sounds source
        sfxAudioSource = gameObject.AddComponent<AudioSource>();
        sfxAudioSource.playOnAwake = false;
        if (sfxMixerGroup != null)
            sfxAudioSource.outputAudioMixerGroup = sfxMixerGroup;
        ApplySFXGain();

        // Music source
        musicAudioSource = gameObject.AddComponent<AudioSource>();
        musicAudioSource.playOnAwake = false;
        musicAudioSource.loop = true;

        // Auto-load SFX clips from Resources
        if (tankDrivingSound == null)
            tankDrivingSound = Resources.Load<AudioClip>("Sounds/UISounds/TankDriving");
        if (explosionSound == null)
            explosionSound = Resources.Load<AudioClip>("Sounds/UISounds/Explosion");
        if (roundWinSound == null)
            roundWinSound = Resources.Load<AudioClip>("Sounds/UISounds/RoundWin");

        // Load settings
        int savedEnable = PlayerPrefs.GetInt("EnableSounds", -1);
        if (savedEnable != -1) enableSounds = savedEnable == 1;
        else PlayerPrefs.SetInt("EnableSounds", enableSounds ? 1 : 0);

        float savedMaster = PlayerPrefs.GetFloat("MasterVolume", -1f);
        if (savedMaster != -1f) masterVolume = savedMaster;
        else PlayerPrefs.SetFloat("MasterVolume", masterVolume);

        float savedUI = PlayerPrefs.GetFloat("UISoundsVolume", -1f);
        if (savedUI != -1f) uiSoundsVolume = savedUI;
        else PlayerPrefs.SetFloat("UISoundsVolume", uiSoundsVolume);

        float savedMusic = PlayerPrefs.GetFloat("MusicVolume", -1f);
        if (savedMusic != -1f) musicVolume = savedMusic;
        else PlayerPrefs.SetFloat("MusicVolume", musicVolume);

        float savedSFX = PlayerPrefs.GetFloat("SFXVolume", -1f);
        if (savedSFX != -1f) sfxVolume = savedSFX;
        else PlayerPrefs.SetFloat("SFXVolume", sfxVolume);

        float savedGain = PlayerPrefs.GetFloat("SFXGainMultiplier", -1f);
        if (savedGain != -1f) sfxGainMultiplier = savedGain;
        else PlayerPrefs.SetFloat("SFXGainMultiplier", sfxGainMultiplier);

        // Listen to scene changes
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;

        // Play music for initial scene
        OnSceneLoaded(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        string sceneName = scene.name;
        AudioClip newClip = null;
        float volumeMultiplier = 1f;

        if (sceneName.StartsWith("Arena"))
        {
            if (arenaMusic.Length > 0)
            {
                int index = Random.Range(0, arenaMusic.Length);
                newClip = arenaMusic[index];
                currentArenaIndex = index;
                volumeMultiplier = arenaMusicVolumes.Length > index ? arenaMusicVolumes[index] : 1f;
            }
        }
        else if (sceneName.StartsWith("AiEditor"))
        {
            newClip = editorShopMusic;
            volumeMultiplier = editorShopMusicVolume;
        }
        else if (sceneName.StartsWith("Shop"))
        {
            newClip = editorShopMusic;
            volumeMultiplier = editorShopMusicVolume;
        }

        if (newClip != null && newClip != currentMusicClip)
        {
            StartCoroutine(FadeToNewClip(newClip, volumeMultiplier));
        }
        else if (newClip == null)
        {
            StartCoroutine(FadeOut());
        }
        else
        {
            // If same clip, just update volume
            musicAudioSource.volume = musicVolume * volumeMultiplier;
        }
    }

    public void PlayButtonSound(ButtonSoundType soundType)
    {
        int index = (int)soundType;
        if (!enableSounds || buttonSounds.Length <= index || buttonSounds[index] == null) return;
        float volume = (buttonSoundVolumes.Length > index ? buttonSoundVolumes[index] : 1f) * masterVolume * uiSoundsVolume;
        buttonAudioSource.PlayOneShot(buttonSounds[index], volume);
    }

    // Call this to save settings
    public void SaveSettings()
    {
        PlayerPrefs.SetInt("EnableSounds", enableSounds ? 1 : 0);
        PlayerPrefs.SetFloat("MasterVolume", masterVolume);
        PlayerPrefs.SetFloat("UISoundsVolume", uiSoundsVolume);
        PlayerPrefs.SetFloat("MusicVolume", musicVolume);
        PlayerPrefs.SetFloat("SFXVolume", sfxVolume);
        PlayerPrefs.SetFloat("SFXGainMultiplier", sfxGainMultiplier);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Applies sfxGainMultiplier to the AudioMixer as a dB value so SFX can
    /// exceed the 0-1 volume cap. Requires the mixer to have an exposed float
    /// parameter named "SFXGain". Range: 1x = 0dB, 2x ≈ +6dB, 4x ≈ +12dB, 10x ≈ +20dB.
    /// </summary>
    public void ApplySFXGain()
    {
        if (sfxMixer == null) return;
        // Convert linear multiplier to decibels: dB = 20 * log10(gain)
        // Floor at -80dB to avoid log(0)
        float gain = Mathf.Max(sfxGainMultiplier, 0.0001f);
        float dB = Mathf.Log10(gain) * 20f;
        sfxMixer.SetFloat(SFXGainParam, dB);
    }

    // SFX Play Methods
    public void PlayGunshot()
    {
        if (gunshotSound != null)
            sfxAudioSource.PlayOneShot(gunshotSound, masterVolume * sfxVolume * gunshotVolume);
    }

    public void PlayBulletHit()
    {
        if (bulletHitSound != null)
            sfxAudioSource.PlayOneShot(bulletHitSound, masterVolume * sfxVolume * bulletHitVolume);
    }

    public void PlayRoundLoss()
    {
        if (roundLossSound != null)
            sfxAudioSource.PlayOneShot(roundLossSound, masterVolume * sfxVolume * roundLossVolume);
    }

    public void PlayRoundWin()
    {
        if (roundWinSound != null)
            sfxAudioSource.PlayOneShot(roundWinSound, masterVolume * sfxVolume * roundWinVolume);
    }

    public void PlayExplosion()
    {
        if (explosionSound != null)
            sfxAudioSource.PlayOneShot(explosionSound, masterVolume * sfxVolume * explosionVolume);
    }

    public void PlayErrorSound()
    {
        if (errorSound != null)
            sfxAudioSource.PlayOneShot(errorSound, masterVolume * sfxVolume * errorSoundVolume);
    }

    // Positional SFX methods (3D audio)
    public void PlayGunshotAtPosition(Vector3 position)
    {
        PlayPositionalSound(gunshotSound, position, gunshotVolume);
    }

    public void PlayBulletHitAtPosition(Vector3 position)
    {
        PlayPositionalSound(bulletHitSound, position, bulletHitVolume);
    }

    public void PlayExplosionAtPosition(Vector3 position)
    {
        PlayPositionalSound(explosionSound, position, explosionVolume);
    }

    public void PlayRoundLossAtPosition(Vector3 position)
    {
        PlayPositionalSound(roundLossSound, position, roundLossVolume);
    }

    public void PlayRoundWinAtPosition(Vector3 position)
    {
        PlayPositionalSound(roundWinSound, position, roundWinVolume);
    }

    public void PlayErrorAtPosition(Vector3 position)
    {
        PlayPositionalSound(errorSound, position, errorSoundVolume);
    }

    private void PlayPositionalSound(AudioClip clip, Vector3 position, float devVolume)
    {
        if (clip == null || !enableSounds) return;
        
        GameObject tempAudio = new GameObject("TempAudio");
        tempAudio.transform.position = position;
        
        AudioSource source = tempAudio.AddComponent<AudioSource>();
        source.clip = clip;
        source.spatialBlend = 1f; // Full 3D audio
        source.volume = Mathf.Clamp01(masterVolume * sfxVolume * devVolume);
        if (sfxMixerGroup != null)
            source.outputAudioMixerGroup = sfxMixerGroup; // Mixer handles gain above 1.0
        source.Play();
        
        Destroy(tempAudio, clip.length + 0.1f); // Destroy after sound finishes
    }

    // Tank driving sound with fade (returns AudioSource for control)
    public AudioSource StartTankDrivingSound(float fadeDuration = 0.5f)
    {
        if (tankDrivingSound == null) return null;
        
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.clip = tankDrivingSound;
        source.loop = true;
        source.volume = 0f;
        if (sfxMixerGroup != null)
            source.outputAudioMixerGroup = sfxMixerGroup;
        source.Play();
        StartCoroutine(FadeAudioSource(source, Mathf.Clamp01(masterVolume * sfxVolume * tankDrivingVolume), fadeDuration));
        return source;
    }

    public void StopTankDrivingSound(AudioSource source, float fadeDuration = 0.5f)
    {
        if (source != null)
            StartCoroutine(FadeOutAndDestroy(source, fadeDuration));
    }

    private IEnumerator FadeAudioSource(AudioSource source, float targetVolume, float duration)
    {
        float startVolume = source.volume;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            source.volume = Mathf.Lerp(startVolume, targetVolume, elapsed / duration);
            yield return null;
        }
        source.volume = targetVolume;
    }

    private IEnumerator FadeOutAndDestroy(AudioSource source, float duration)
    {
        float startVolume = source.volume;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            source.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
            yield return null;
        }
        source.Stop();
        Destroy(source);
    }

    public void UpdateCurrentMusicVolume()
    {
        if (musicAudioSource.isPlaying)
        {
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            float multiplier = 1f;
            if (sceneName.StartsWith("Arena") && currentArenaIndex >= 0)
            {
                multiplier = arenaMusicVolumes.Length > currentArenaIndex ? arenaMusicVolumes[currentArenaIndex] : 1f;
            }
            else if (sceneName.StartsWith("AiEditor") || sceneName.StartsWith("Shop"))
            {
                multiplier = editorShopMusicVolume;
            }
            musicAudioSource.volume = masterVolume * musicVolume * multiplier;
        }
    }

    private IEnumerator FadeToNewClip(AudioClip newClip, float volumeMultiplier)
    {
        // Fade out current
        float startVolume = musicAudioSource.volume;
        float targetVolume = 0f;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            musicAudioSource.volume = Mathf.Lerp(startVolume, targetVolume, elapsed / fadeDuration);
            yield return null;
        }

        musicAudioSource.volume = targetVolume;

        // Switch clip
        currentMusicClip = newClip;
        musicAudioSource.clip = newClip;
        musicAudioSource.Play();

        // Fade in
        startVolume = 0f;
        targetVolume = masterVolume * musicVolume * volumeMultiplier;
        elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            musicAudioSource.volume = Mathf.Lerp(startVolume, targetVolume, elapsed / fadeDuration);
            yield return null;
        }

        musicAudioSource.volume = targetVolume;
    }

    private IEnumerator FadeOut()
    {
        float startVolume = musicAudioSource.volume;
        float targetVolume = 0f;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            musicAudioSource.volume = Mathf.Lerp(startVolume, targetVolume, elapsed / fadeDuration);
            yield return null;
        }

        musicAudioSource.volume = targetVolume;
        musicAudioSource.Stop();
        currentMusicClip = null;
    }

    private void OnDestroy()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}

public enum ButtonSoundType
{
    DefaultClick,
    SpecialClick,
    // Add more as needed
}