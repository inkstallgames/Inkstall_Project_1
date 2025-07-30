using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    AudioSource audioSource;

    [Header("Audio Sources")]
    public AudioClip musicSource;


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
        PlayMusic();
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
