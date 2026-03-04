using UnityEngine;

/// <summary>
/// Simple laser impact visual effect.
/// Creates a spark/explosion effect when laser hits something.
/// </summary>
public class LaserImpactEffect : MonoBehaviour
{
    private ParticleSystem particleSystem;
    private Light impactLight;
    private float lifetime = 1f;
    private float currentAge = 0f;
    
    void Start()
    {
        // Create particle system
        particleSystem = GetComponent<ParticleSystem>();
        if (particleSystem == null)
        {
            particleSystem = gameObject.AddComponent<ParticleSystem>();
        }
        
        // Configure particle system for laser impact
        var main = particleSystem.main;
        main.startColor = Color.red;
        main.startSize = 0.1f;
        main.startSpeed = 5f;
        main.startLifetime = 0.5f;
        main.duration = 0.2f;
        main.loop = false;
        
        var emission = particleSystem.emission;
        emission.enabled = true;
        emission.rateOverTime = 50f; // Use rateOverTime instead of emissionRate
        emission.SetBursts(new ParticleSystem.Burst[] {
            new ParticleSystem.Burst(0f, 20)
        });
        
        var shape = particleSystem.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.1f;
        
        // Add light for impact flash
        impactLight = gameObject.AddComponent<Light>();
        impactLight.type = LightType.Point;
        impactLight.color = Color.red;
        impactLight.intensity = 2f;
        impactLight.range = 3f;
        impactLight.enabled = true;
    }
    
    void Update()
    {
        currentAge += Time.deltaTime;
        
        // Fade out light
        if (impactLight != null)
        {
            float intensity = Mathf.Lerp(2f, 0f, currentAge / lifetime);
            impactLight.intensity = intensity;
        }
        
        // Destroy after lifetime
        if (currentAge >= lifetime)
        {
            Destroy(gameObject);
        }
    }
}
