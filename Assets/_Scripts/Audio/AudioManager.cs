using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

[System.Serializable]
public class SceneMusicMapping
{
    public string sceneName;
    public AudioClip musicClip;
}

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    AudioSource audioSource;

    [Header("Scene Music Mappings")]
    public List<SceneMusicMapping> sceneMusicMappings;
    private Dictionary<string, AudioClip> musicLookup;

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
            
            // Initialize music lookup dictionary
            musicLookup = new Dictionary<string, AudioClip>();
            foreach (var mapping in sceneMusicMappings)
            {
                if (!string.IsNullOrEmpty(mapping.sceneName) && mapping.musicClip != null)
                {
                    musicLookup[mapping.sceneName] = mapping.musicClip;
                }
            }
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
        PlayMusicForScene(SceneManager.GetActiveScene().name);
    }

    public void PlayMusic()
    {
        // This method is now a wrapper to play the correct music for the current scene
        PlayMusicForScene(SceneManager.GetActiveScene().name);
    }

    public void StopMusic()
    {
        audioSource.Stop();
    }

    public float sfxVolume = 0.5f;

    public void SetMusicVolume(float volume)
    {
        audioSource.volume = volume;

        if (volume > 0)
        {
            // If we're increasing from zero, make sure we have a clip to play
            if (audioSource.clip == null)
            {
                // Get the appropriate clip for the current scene
                string currentSceneName = SceneManager.GetActiveScene().name;
                AudioClip clipToPlay = musicLookup.ContainsKey(currentSceneName) ? musicLookup[currentSceneName] : null;
                if (clipToPlay != null)
                {
                    audioSource.clip = clipToPlay;
                }
            }
            
            // If not playing, start playback
            if (!audioSource.isPlaying)
            {
                audioSource.Play();
            }
            // If paused, unpause
            else if (!audioSource.isPlaying && audioSource.time > 0)
            {
                audioSource.UnPause();
            }
        }
        else if (volume == 0 && audioSource.isPlaying)
        {
            audioSource.Pause();
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
        PlayMusicForScene(scene.name);
    }

    private void PlayMusicForScene(string sceneName)
    {
        AudioClip clipToPlay = musicLookup.ContainsKey(sceneName) ? musicLookup[sceneName] : null;

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
 