using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    AudioSource audioSource;

    [Header("Audio Sources")]
    public AudioClip musicSource;

    // PlayerPrefs keys (same as in OptionsMenuManager)
    private const string VOLUME_KEY = "VolumeLevel";
    private const string MUSIC_KEY = "MusicEnabled";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Keep the AudioManager across scenes
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        audioSource = GetComponent<AudioSource>();
        audioSource.loop = true; // Ensure music loops
    }

    private void Start()
    {
        // Load saved settings
        float savedVolume = PlayerPrefs.GetFloat(VOLUME_KEY, 0.5f);
        bool musicEnabled = PlayerPrefs.GetInt(MUSIC_KEY, 1) == 1;
        
        // Apply volume setting
        audioSource.volume = savedVolume;
        
        // Play music only if it was enabled in saved settings
        if (musicEnabled)
        {
            PlayMusic();
        }
    }

    public void PlayMusic()
    {
        if (audioSource.clip != musicSource)
        {
            audioSource.clip = musicSource;
        }
        audioSource.Play();
    }

    public void StopMusic()
    {
        audioSource.Stop();
    }

    public bool IsMusicPlaying()
    {
        return audioSource.isPlaying;
    }
}
