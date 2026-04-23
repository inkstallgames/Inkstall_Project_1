using UnityEngine;

/// <summary>
/// Industry-standard 3D spatial audio manager for multiplayer FPS games.
/// Follows best practices from games like Valorant, CS:GO, and Overwatch.
/// 
/// Features:
/// - Directional sound based on player position
/// - Realistic distance attenuation
/// - Audio source pooling for performance
/// - Local vs Remote audio handling
/// - Occlusion and reverb zones support
/// </summary>
public class NetworkAudioManager : MonoBehaviour
{
    public static NetworkAudioManager Instance { get; private set; }

    [Header("Audio Settings")]
    [Tooltip("Maximum distance where sound can be heard (meters)")]
    [SerializeField] private float maxDistance = 50f;
    
    [Tooltip("Distance where sound starts to fade (meters)")]
    [SerializeField] private float minDistance = 1f;
    
    [Tooltip("How quickly sound fades with distance")]
    [SerializeField] private AnimationCurve volumeRolloff = AnimationCurve.EaseInOut(1f, 1f, 50f, 0f);
    
    [Tooltip("Number of audio sources to pool for performance")]
    [SerializeField] private int audioPoolSize = 30;
    
    [Header("Advanced Settings")]
    [Tooltip("Enable doppler effect for moving sounds")]
    [SerializeField] private bool enableDoppler = false;
    
    [Tooltip("Enable audio occlusion (requires colliders)")]
    [SerializeField] private bool enableOcclusion = false;
    
    [Tooltip("Layers that block sound for occlusion")]
    [SerializeField] private LayerMask occlusionLayers = -1;

    // Audio source pooling
    private AudioSource[] audioSourcePool;
    private int currentPoolIndex = 0;
    private bool[] poolInUse;

    private void Awake()
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
    }

    private void Start()
    {
        InitializeAudioPool();
        Debug.Log($"[NetworkAudioManager] Initialized with {audioPoolSize} audio sources | Range: {minDistance}-{maxDistance}m");
    }

    /// <summary>
    /// Initialize the audio source pool for optimal performance
    /// </summary>
    private void InitializeAudioPool()
    {
        audioSourcePool = new AudioSource[audioPoolSize];
        poolInUse = new bool[audioPoolSize];

        for (int i = 0; i < audioPoolSize; i++)
        {
            GameObject audioObj = new GameObject($"AudioSource_{i}", typeof(AudioSource));
            audioObj.transform.SetParent(transform);
            
            AudioSource source = audioObj.GetComponent<AudioSource>();
            
            // Configure industry-standard 3D audio settings
            source.playOnAwake = false;
            source.spatialBlend = 1f; // Full 3D spatial audio
            source.rolloffMode = AudioRolloffMode.Custom;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, volumeRolloff);
            source.dopplerLevel = enableDoppler ? 1f : 0f;
            source.reverbZoneMix = 1f;
            
            audioSourcePool[i] = source;
            audioObj.SetActive(false);
        }
    }

    /// <summary>
    /// Play 3D spatial sound at specific position
    /// Industry standard: Local sounds are 2D, Remote sounds are 3D
    /// </summary>
    public void PlaySound(AudioClip clip, Vector3 position, float volume = 1f, bool isLocalPlayer = false)
    {
        if (clip == null) return;

        AudioSource source = GetPooledAudioSource();
        if (source == null) return;

        // Position the audio source
        source.transform.position = position;

        // Configure based on player type
        if (isLocalPlayer)
        {
            // Local player: 2D centered sound (industry standard for own actions)
            source.spatialBlend = 0f;
            source.volume = volume;
        }
        else
        {
            // Remote player: Full 3D spatial audio
            source.spatialBlend = 1f;
            
            // Apply distance-based volume
            float distance = Vector3.Distance(position, GetListenerPosition());
            float distanceVolume = CalculateDistanceVolume(distance);
            float finalVolume = volume * distanceVolume;
            
            // Apply occlusion if enabled
            if (enableOcclusion)
            {
                finalVolume *= CalculateOcclusion(position);
            }
            
            source.volume = Mathf.Clamp01(finalVolume);
        }

        // Play the sound
        source.clip = clip;
        source.gameObject.SetActive(true);
        source.Play();

        // Return to pool after sound finishes
        StartCoroutine(ReturnToPool(source, clip.length));
    }

    /// <summary>
    /// Play one-shot sound effect (instant, no pooling needed for very short sounds)
    /// </summary>
    public void PlayOneShot(AudioClip clip, Vector3 position, float volume = 1f)
    {
        if (clip == null) return;

        AudioSource.PlayClipAtPoint(clip, position, volume);
    }

    /// <summary>
    /// Calculate volume based on distance using industry-standard rolloff
    /// </summary>
    private float CalculateDistanceVolume(float distance)
    {
        if (distance <= minDistance)
            return 1f;
        
        if (distance >= maxDistance)
            return 0f;
        
        // Use the custom rolloff curve for realistic attenuation
        float normalizedDistance = (distance - minDistance) / (maxDistance - minDistance);
        return volumeRolloff.Evaluate(normalizedDistance);
    }

    /// <summary>
    /// Calculate occlusion factor (0 = fully occluded, 1 = no occlusion)
    /// </summary>
    private float CalculateOcclusion(Vector3 soundPosition)
    {
        if (!enableOcclusion) return 1f;

        Vector3 listenerPos = GetListenerPosition();
        Vector3 direction = (soundPosition - listenerPos).normalized;
        float distance = Vector3.Distance(listenerPos, soundPosition);

        // Cast rays to check for occlusion
        int rayCount = 3;
        int blockedRays = 0;
        
        for (int i = 0; i < rayCount; i++)
        {
            // Cast rays in a small cone
            Vector3 offset = Vector3.Cross(direction, Vector3.up) * (i - 1) * 0.5f;
            Vector3 rayOrigin = listenerPos + offset;
            
            if (Physics.Raycast(rayOrigin, direction, distance, occlusionLayers))
            {
                blockedRays++;
            }
        }

        // Calculate occlusion based on blocked rays
        float occlusionFactor = 1f - (float)blockedRays / rayCount;
        return Mathf.Lerp(0.3f, 1f, occlusionFactor); // Minimum 30% volume when occluded
    }

    /// <summary>
    /// Get the listener position (usually main camera)
    /// </summary>
    private Vector3 GetListenerPosition()
    {
        var mainCamera = Camera.main;
        if (mainCamera != null)
            return mainCamera.transform.position;

        return Vector3.zero;
    }

    /// <summary>
    /// Get an available audio source from the pool
    /// </summary>
    private AudioSource GetPooledAudioSource()
    {
        for (int i = 0; i < audioSourcePool.Length; i++)
        {
            int index = (currentPoolIndex + i) % audioSourcePool.Length;
            
            if (!poolInUse[index] && !audioSourcePool[index].isPlaying)
            {
                poolInUse[index] = true;
                currentPoolIndex = index;
                return audioSourcePool[index];
            }
        }

        Debug.LogWarning("[NetworkAudioManager] No available audio sources in pool");
        return null;
    }

    /// <summary>
    /// Return audio source to pool after use
    /// </summary>
    private System.Collections.IEnumerator ReturnToPool(AudioSource source, float delay)
    {
        yield return new WaitForSeconds(delay + 0.1f);
        
        int index = System.Array.IndexOf(audioSourcePool, source);
        if (index >= 0)
        {
            poolInUse[index] = false;
            source.gameObject.SetActive(false);
            source.clip = null;
        }
    }

    /// <summary>
    /// Draw audio range gizmos in Scene view
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (audioSourcePool == null) return;

        // Draw range for NetworkAudioManager
        Gizmos.color = new Color(0f, 0.5f, 1f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, maxDistance);
        
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, minDistance);

        // Draw active audio sources
        for (int i = 0; i < audioSourcePool.Length; i++)
        {
            AudioSource source = audioSourcePool[i];
            if (source != null && source.isPlaying && source.gameObject.activeInHierarchy)
            {
                // Draw sound position
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(source.transform.position, 0.3f);
                
                // Draw range
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);
                Gizmos.DrawWireSphere(source.transform.position, maxDistance);
                
                // Draw label
                #if UNITY_EDITOR
                UnityEditor.Handles.Label(source.transform.position + Vector3.up, 
                    $"{source.clip?.name ?? "Audio"}\nVol: {source.volume:F2}\n3D: {source.spatialBlend:F1}");
                #endif
            }
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
