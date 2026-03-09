using UnityEngine;

/// <summary>
/// Muzzle flash particle effect for weapons.
/// Uses a pre-configured particle system prefab - just instantiate and play!
/// </summary>
public class MuzzleFlashEffect : MonoBehaviour
{
    private ParticleSystem particleSystem;
    private Light flashLight;
    private bool isContinuous = false;
    private bool isInitialized = false;
    
    private void Awake()
    {
        // Try to find existing particle system component
        particleSystem = GetComponent<ParticleSystem>();
        if (particleSystem == null)
        {
            Debug.LogError("[MuzzleFlashEffect] No ParticleSystem found on prefab! Please add a ParticleSystem component to your muzzle flash prefab.");
            return;
        }
        
        // Try to find existing light component
        flashLight = GetComponent<Light>();
        if (flashLight == null)
        {
            // Add a simple flash light if none exists
            flashLight = gameObject.AddComponent<Light>();
            flashLight.type = LightType.Point;
            flashLight.color = Color.white;
            flashLight.intensity = 3f;
            flashLight.range = 2f;
            flashLight.enabled = false;
        }
        
        isInitialized = true;
        Debug.Log("[MuzzleFlashEffect] Muzzle flash effect initialized with existing ParticleSystem");
    }
    
    private void Start()
    {
        // Ensure light is off by default
        if (flashLight != null)
        {
            flashLight.enabled = false;
        }
    }
    
    public void SetContinuousMode(bool continuous)
    {
        isContinuous = continuous;
        if (continuous)
        {
            // Configure for continuous emission
            if (particleSystem != null)
            {
                var emission = particleSystem.emission;
                emission.rateOverTime = 50f; // Continuous rate
                emission.SetBursts(new ParticleSystem.Burst[0]); // Remove bursts
                
                var main = particleSystem.main;
                main.loop = true;
                main.duration = 0.2f;
            }
            
            // Keep light on continuously but dimmer
            if (flashLight != null)
            {
                flashLight.intensity = 1.5f;
            }
        }
        else
        {
            // Configure for single burst
            if (particleSystem != null)
            {
                var emission = particleSystem.emission;
                emission.rateOverTime = 0f; // No continuous rate
                emission.SetBursts(new ParticleSystem.Burst[] {
                    new ParticleSystem.Burst(0f, 15)
                });
                
                var main = particleSystem.main;
                main.loop = false;
                main.duration = 0.1f;
            }
            
            // Bright flash for single shot
            if (flashLight != null)
            {
                flashLight.intensity = 3f;
            }
        }
    }
    
    public void Play()
    {
        Debug.Log("[MuzzleFlashEffect] Play() called!");
        
        // Force initialization if not done (handles cases where Awake() wasn't called)
        if (!isInitialized)
        {
            Debug.Log("[MuzzleFlashEffect] Not initialized, forcing initialization now...");
            ForceInitialize();
        }
        
        if (particleSystem != null)
        {
            Debug.Log("[MuzzleFlashEffect] Playing particle system from prefab");
            
            // Debug particle system state before playing
            var main = particleSystem.main;
            Debug.Log($"[MuzzleFlashEffect] ParticleSystem config - Duration: {main.duration}, Loop: {main.loop}, PlayOnAwake: {main.playOnAwake}");
            
            var emission = particleSystem.emission;
            Debug.Log($"[MuzzleFlashEffect] Emission config - RateOverTime: {emission.rateOverTime.constant}, BurstCount: {emission.burstCount}");
            
            Debug.Log($"[MuzzleFlashEffect] Particle count before play: {particleSystem.particleCount}");
            
            particleSystem.Play();
            
            Debug.Log($"[MuzzleFlashEffect] ParticleSystem is now playing: {particleSystem.isPlaying}");
            Debug.Log($"[MuzzleFlashEffect] Particle count after play: {particleSystem.particleCount}");
            
            // Force play again if not playing
            if (!particleSystem.isPlaying)
            {
                Debug.LogWarning("[MuzzleFlashEffect] ParticleSystem not playing after Play() call, trying Simulate()...");
                particleSystem.Simulate(0.01f, true, true);
                Debug.Log($"[MuzzleFlashEffect] After Simulate - Playing: {particleSystem.isPlaying}, Count: {particleSystem.particleCount}");
            }
        }
        else
        {
            Debug.LogError("[MuzzleFlashEffect] ParticleSystem is null!");
        }
        
        if (flashLight != null)
        {
            flashLight.enabled = true;
            Debug.Log("[MuzzleFlashEffect] Flash light enabled");
        }
    }
    
    private void ForceInitialize()
    {
        Debug.Log("[MuzzleFlashEffect] ForceInitialize() called!");
        
        // Try to find existing particle system component
        particleSystem = GetComponent<ParticleSystem>();
        if (particleSystem == null)
        {
            Debug.LogError("[MuzzleFlashEffect] No ParticleSystem found on prefab! Please add a ParticleSystem component to your muzzle flash prefab.");
            return;
        }
        
        // Try to find existing light component
        flashLight = GetComponent<Light>();
        if (flashLight == null)
        {
            // Add a simple flash light if none exists
            flashLight = gameObject.AddComponent<Light>();
            flashLight.type = LightType.Point;
            flashLight.color = Color.white;
            flashLight.intensity = 3f;
            flashLight.range = 2f;
            flashLight.enabled = false;
        }
        
        isInitialized = true;
        Debug.Log("[MuzzleFlashEffect] Muzzle flash effect force-initialized with existing ParticleSystem");
    }
    
    public void Stop()
    {
        if (particleSystem != null && particleSystem.isPlaying)
        {
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        
        if (flashLight != null)
        {
            flashLight.enabled = false;
        }
    }
    
    void Update()
    {
        // Auto-fade light for single shots
        if (!isContinuous && flashLight != null && flashLight.enabled)
        {
            float fadeSpeed = 10f;
            flashLight.intensity = Mathf.Max(0f, flashLight.intensity - fadeSpeed * Time.deltaTime);
            
            if (flashLight.intensity <= 0f)
            {
                flashLight.enabled = false;
            }
        }
    }
}
