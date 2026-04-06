using UnityEngine;

/// <summary>
/// Simple bullet trail effect using LineRenderer.
/// This script is for the visual effect prefab that gets instantiated on shot.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class BulletTrail : MonoBehaviour
{
    [Header("Trail Settings")]
    [SerializeField] private float fadeSpeed = 2f;
    [SerializeField] private AnimationCurve widthCurve = AnimationCurve.Linear(0, 1, 1, 0);
    
    private LineRenderer lineRenderer;
    private float timeAlive = 0f;
    private float maxLifetime = 0.5f;

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer != null)
        {
            lineRenderer.startWidth = 0.05f;
            lineRenderer.endWidth = 0.05f;
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.startColor = Color.yellow;
            lineRenderer.endColor = Color.yellow;
        }
    }

    private void Update()
    {
        timeAlive += Time.deltaTime;
        
        if (lineRenderer != null)
        {
            float alpha = 1f - (timeAlive / maxLifetime);
            Color startColor = lineRenderer.startColor;
            Color endColor = lineRenderer.endColor;
            
            startColor.a = alpha;
            endColor.a = alpha;
            
            lineRenderer.startColor = startColor;
            lineRenderer.endColor = endColor;
        }

        if (timeAlive >= maxLifetime)
        {
            Destroy(gameObject);
        }
    }

    public void SetPositions(Vector3 start, Vector3 end)
    {
        if (lineRenderer != null)
        {
            lineRenderer.SetPosition(0, start);
            lineRenderer.SetPosition(1, end);
        }
    }
}
