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
    
    /// <summary>
    /// Number of particles to emit per burst when using Emit mode.
    /// </summary>
    [SerializeField] private int burstParticleCount = 15;
    
    public void Play()
    {
        // Force initialization if not done (handles cases where Awake() wasn't called)
        if (!isInitialized)
        {
            ForceInitialize();
        }
        
        if (particleSystem != null)
        {
            if (isContinuous)
            {
                // Continuous mode: use Play() so the system keeps emitting
                particleSystem.Play();
            }
            else
            {
                // Single burst mode: use Emit() to guarantee particles spawn this frame.
                // Play() can miss if the system hasn't reset or calls come in rapid succession.
                EmitBurst();
            }
        }
        else
        {
            Debug.LogError("[MuzzleFlashEffect] ParticleSystem is null!");
        }
        
        if (flashLight != null)
        {
            flashLight.enabled = true;
            // Reset intensity for the fade
            flashLight.intensity = isContinuous ? 1.5f : 3f;
        }
    }
    
    /// <summary>
    /// Immediately emits a burst of particles using ParticleSystem.Emit().
    /// This is guaranteed to produce particles on the current frame,
    /// unlike Play() which can miss if called in rapid succession.
    /// </summary>
    public void EmitBurst()
    {
        if (!isInitialized)
        {
            ForceInitialize();
        }
        
        if (particleSystem != null)
        {
            // Emit() is instant and cannot be "missed" - particles are created this frame
            particleSystem.Emit(burstParticleCount);
        }
        else
        {
            Debug.LogError("[MuzzleFlashEffect] ParticleSystem is null, cannot emit burst!");
        }
        
        if (flashLight != null)
        {
            flashLight.enabled = true;
            flashLight.intensity = 3f;
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
