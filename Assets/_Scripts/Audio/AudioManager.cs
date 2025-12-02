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
    private const string MUSIC_KEY = "MusicVolume";

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
        float musicVolume = PlayerPrefs.GetFloat(MUSIC_KEY, 0.5f);

        // Apply volume setting
        SetMusicVolume(musicVolume);

        // Play music if volume is greater than 0
        if (musicVolume > 0)
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

    public void SetMusicVolume(float volume)
    {
        audioSource.volume = volume;

        if (volume > 0 && !audioSource.isPlaying)
        {
            PlayMusic();
        }
        else if (volume == 0 && audioSource.isPlaying)
        {
            StopMusic();
        }
    }

    public bool IsMusicPlaying()
    {
        return audioSource.isPlaying;
    }

    public AudioSource GetAudioSource()
    {
        return audioSource;
    }
}
 