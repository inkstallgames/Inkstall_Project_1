using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    AudioSource audioSource;

    [Header("Audio Sources")]
    public AudioClip musicSource;
    public AudioClip sfxSource;

    [Header("Audio Settings")]
    [Range(0f, 1f)] public float musicVolume = 1f;
    [Range(0f, 1f)] public float sfxVolume = 1f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        audioSource = GetComponent<AudioSource>();
    }

    private void Start()
    {
        PlayMusic();
    }

    public void PlayMusic()
    {
        audioSource.PlayOneShot(musicSource, musicVolume);
    }

    public void PlaySFX(AudioClip clip)
    {
        audioSource.PlayOneShot(clip, sfxVolume);
    }

    public void SetMusicVolume(float volume)
    {
        musicVolume = volume;
        audioSource.volume = musicVolume;
    }

    public void SetSFXVolume(float volume)
    {
        sfxVolume = volume;
    }

    public void ToggleMusic(bool isOn)
    {
        audioSource.mute = !isOn;
    }

    private void ApplyVolumeSettings()
    {
        audioSource.volume = musicVolume;
    }
}
