using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    AudioSource audioSource;

    [Header("Music Clips")]
    public AudioClip scene0Music;
    public AudioClip otherScenesMusic;

    // PlayerPrefs keys (same as in OptionsMenuManager)
    private const string VOLUME_KEY = "VolumeLevel";
    private const string MUSIC_KEY = "MusicVolume";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Keep the AudioManager across scenes
            SceneManager.sceneLoaded += OnSceneLoaded;
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
        float musicVolume = PlayerPrefs.GetFloat(MUSIC_KEY, 0.5f);
        SetMusicVolume(musicVolume);

        // Initial music play for the starting scene
        PlayMusicForScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void PlayMusic()
    {
        // This method is now a wrapper to play the correct music for the current scene
        PlayMusicForScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void StopMusic()
    {
        audioSource.Stop();
    }

    public float sfxVolume = 0.5f;

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

    public void UpdateSFXVolume(float volume)
    {
        sfxVolume = volume;
    }

    public void PlaySFXAtPoint(AudioClip clip, Vector3 position)
    {
        if (clip != null)
        {
            AudioSource.PlayClipAtPoint(clip, position, sfxVolume);
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

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        PlayMusicForScene(scene.buildIndex);
    }

    private void PlayMusicForScene(int sceneIndex)
    {
        AudioClip clipToPlay = (sceneIndex == 0) ? scene0Music : otherScenesMusic;

        // Only change and play if the clip is different and not null
        if (clipToPlay != null && audioSource.clip != clipToPlay)
        {
            audioSource.clip = clipToPlay;
            audioSource.Play();
        }
        else if (clipToPlay == null)
        {
            audioSource.Stop();
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe to prevent memory leaks
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }
}
 