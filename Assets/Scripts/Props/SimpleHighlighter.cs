using UnityEngine;

/// <summary>
/// A simpler highlighting component that uses emission instead of outline shader
/// This is a performance-friendly alternative that works with standard materials
/// </summary>
[RequireComponent(typeof(Renderer))]
public class SimpleHighlighter : MonoBehaviour
{
    [Header("Highlight Settings")]
    [SerializeField] private Color highlightColor = Color.white;
    [SerializeField] private float emissionIntensity = 0.5f;
    
    private Renderer[] renderers;
    private MaterialPropertyBlock propertyBlock;
    private bool isHighlighted = false;
    
    // Store original emission colors
    private Color[][] originalEmissionColors;
    
    private void Awake()
    {
        // Get all renderers (including children)
        renderers = GetComponentsInChildren<Renderer>();
        propertyBlock = new MaterialPropertyBlock();
        
        // Initialize storage for original emission colors
        originalEmissionColors = new Color[renderers.Length][];
        
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            int materialCount = renderer.materials.Length;
            originalEmissionColors[i] = new Color[materialCount];
            
            // Store original emission colors
            for (int j = 0; j < materialCount; j++)
            {
                if (renderer.materials[j].HasProperty("_EmissionColor"))
                {
                    originalEmissionColors[i][j] = renderer.materials[j].GetColor("_EmissionColor");
                }
                else
                {
                    originalEmissionColors[i][j] = Color.black;
                }
            }
        }
    }
    
    /// <summary>
    /// Apply highlight effect to the object using emission
    /// </summary>
    public void Highlight()
    {
        if (isHighlighted) return;
        isHighlighted = true;
        
        // Apply highlight to all renderers
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            
            for (int j = 0; j < renderer.materials.Length; j++)
            {
                Material material = renderer.materials[j];
                
                if (material.HasProperty("_EmissionColor"))
                {
                    // Enable emission if it's not already enabled
                    if (!material.IsKeywordEnabled("_EMISSION"))
                    {
                        material.EnableKeyword("_EMISSION");
                    }
                    
                    // Set emission color using property block for better performance
                    renderer.GetPropertyBlock(propertyBlock, j);
                    propertyBlock.SetColor("_EmissionColor", highlightColor * emissionIntensity);
                    renderer.SetPropertyBlock(propertyBlock, j);
                }
            }
        }
    }
    
    /// <summary>
    /// Remove highlight effect from the object
    /// </summary>
    public void Unhighlight()
    {
        if (!isHighlighted) return;
        isHighlighted = false;
        
        // Restore original emission for all renderers
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            
            for (int j = 0; j < renderer.materials.Length; j++)
            {
                Material material = renderer.materials[j];
                
                if (material.HasProperty("_EmissionColor"))
                {
                    // Restore original emission color using property block
                    renderer.GetPropertyBlock(propertyBlock, j);
                    propertyBlock.SetColor("_EmissionColor", originalEmissionColors[i][j]);
                    renderer.SetPropertyBlock(propertyBlock, j);
                    
                    // Disable emission if original color was black (indicating no emission)
                    if (originalEmissionColors[i][j] == Color.black)
                    {
                        material.DisableKeyword("_EMISSION");
                    }
                }
            }
        }
    }
}
