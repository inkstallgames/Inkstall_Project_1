using UnityEngine;

/// <summary>
/// Simple laser beam visual effect.
/// Attach to a GameObject with LineRenderer for the laser beam.
/// </summary>
public class LaserBeamEffect : MonoBehaviour
{
    private LineRenderer lineRenderer;
    private float lifetime = 0.15f;
    private float currentAge = 0f;
    
    void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
        }
        
        // Configure line renderer for laser beam
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startWidth = 0.02f;  // Thinner laser beam
        lineRenderer.endWidth = 0.02f;    // Thinner laser beam
        lineRenderer.useWorldSpace = true;
        lineRenderer.positionCount = 2;
        
        // Set color through material
        lineRenderer.material.color = Color.red;
        
        // Add glow effect
        lineRenderer.material.enableInstancing = true;
    }
    
    void Update()
    {
        currentAge += Time.deltaTime;
        
        // Fade out effect
        if (lineRenderer != null)
        {
            float alpha = Mathf.Lerp(1f, 0f, currentAge / lifetime);
            Color color = lineRenderer.material.color;
            color.a = alpha;
            lineRenderer.material.color = color;
        }
        
        // Destroy after lifetime
        if (currentAge >= lifetime)
        {
            Destroy(gameObject);
        }
    }
    
    public void SetBeamPositions(Vector3 start, Vector3 end)
    {
        if (lineRenderer != null)
        {
            lineRenderer.SetPosition(0, start);
            lineRenderer.SetPosition(1, end);
        }
    }
}
