using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles highlighting props with a white outline when player looks at them
/// </summary>
[RequireComponent(typeof(Renderer))]
public class PropHighlighter : MonoBehaviour
{
    [Header("Highlight Settings")]
    [SerializeField] private Color highlightColor = Color.white;
    [SerializeField] private float outlineWidth = 0.05f;
    [SerializeField] private Material outlineMaterial;
    
    private Renderer[] renderers;
    private List<Material[]> originalMaterials = new List<Material[]>();
    private List<Material[]> highlightMaterials = new List<Material[]>();
    private bool isHighlighted = false;
    
    private void Awake()
    {
        // Get all renderers (including children)
        renderers = GetComponentsInChildren<Renderer>();
        
        // Create outline material if not assigned
        if (outlineMaterial == null)
        {
            // Try to find the outline shader
            Shader outlineShader = Shader.Find("Custom/OutlineShader");
            
            // If outline shader not found, use a fallback approach
            if (outlineShader == null)
            {
                outlineMaterial = new Material(Shader.Find("Standard"));
                outlineMaterial.SetFloat("_Mode", 3); // Transparent mode
                outlineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                outlineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                outlineMaterial.SetInt("_ZWrite", 0);
                outlineMaterial.DisableKeyword("_ALPHATEST_ON");
                outlineMaterial.EnableKeyword("_ALPHABLEND_ON");
                outlineMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                outlineMaterial.renderQueue = 3000;
                outlineMaterial.color = new Color(highlightColor.r, highlightColor.g, highlightColor.b, 0.5f);
            }
            else
            {
                outlineMaterial = new Material(outlineShader);
                outlineMaterial.SetColor("_OutlineColor", highlightColor);
                outlineMaterial.SetFloat("_OutlineWidth", outlineWidth);
            }
        }
        
        // Cache original materials
        foreach (Renderer renderer in renderers)
        {
            originalMaterials.Add(renderer.materials);
            
            // Create highlight materials (original + outline)
            Material[] origMats = renderer.materials;
            Material[] highlightMats = new Material[origMats.Length + 1];
            for (int i = 0; i < origMats.Length; i++)
            {
                highlightMats[i] = origMats[i];
            }
            highlightMats[origMats.Length] = outlineMaterial;
            highlightMaterials.Add(highlightMats);
        }
    }
    
    /// <summary>
    /// Apply highlight effect to the object
    /// </summary>
    public void Highlight()
    {
        if (isHighlighted) return;
        isHighlighted = true;
        
        // Apply highlight materials
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].materials = highlightMaterials[i];
        }
    }
    
    /// <summary>
    /// Remove highlight effect from the object
    /// </summary>
    public void Unhighlight()
    {
        if (!isHighlighted) return;
        isHighlighted = false;
        
        // Restore original materials
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].materials = originalMaterials[i];
        }
    }
    
    private void OnDestroy()
    {
        // Clean up any created materials
        if (outlineMaterial != null && !outlineMaterial.name.Contains("Assets"))
        {
            Destroy(outlineMaterial);
        }
    }
}
