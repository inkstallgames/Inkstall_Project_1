using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Creates an outline effect similar to the Unity Scene view selection highlight
/// </summary>
public class SelectionOutline : MonoBehaviour
{
    [SerializeField] private Color outlineColor = Color.white;
    [SerializeField] private float outlineWidth = 2.0f;
    
    private List<Renderer> renderers = new List<Renderer>();
    private Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();
    private Material outlineMaterial;
    private bool isOutlineEnabled = false;
    
    private void Awake()
    {
        // Find all renderers in this object and its children
        renderers.AddRange(GetComponentsInChildren<Renderer>());
        
        // Store original materials
        foreach (Renderer renderer in renderers)
        {
            originalMaterials[renderer] = renderer.sharedMaterials;
        }
        
        // Create outline material
        CreateOutlineMaterial();
    }
    
    private void CreateOutlineMaterial()
    {
        // Find the outline shader
        Shader outlineShader = Shader.Find("Custom/SelectionOutline");
        if (outlineShader == null)
        {
            Debug.LogError("SelectionOutline shader not found! Make sure it's properly imported.");
            return;
        }
        
        // Create outline material
        outlineMaterial = new Material(outlineShader);
        outlineMaterial.SetColor("_OutlineColor", outlineColor);
        outlineMaterial.SetFloat("_OutlineWidth", outlineWidth);
    }
    
    public Color OutlineColor
    {
        get { return outlineColor; }
        set 
        { 
            outlineColor = value;
            if (outlineMaterial != null)
            {
                outlineMaterial.SetColor("_OutlineColor", outlineColor);
            }
        }
    }
    
    public float OutlineWidth
    {
        get { return outlineWidth; }
        set 
        { 
            outlineWidth = value;
            if (outlineMaterial != null)
            {
                outlineMaterial.SetFloat("_OutlineWidth", outlineWidth);
            }
        }
    }
    
    /// <summary>
    /// Enable or disable the outline effect
    /// </summary>
    public void EnableOutline(bool enable)
    {
        if (isOutlineEnabled == enable || outlineMaterial == null) return;
        isOutlineEnabled = enable;
        
        if (enable)
        {
            ApplyOutlineMaterial();
        }
        else
        {
            RestoreOriginalMaterials();
        }
    }
    
    private void ApplyOutlineMaterial()
    {
        foreach (Renderer renderer in renderers)
        {
            // Create a new array with one extra slot for the outline material
            Material[] originalMats = originalMaterials[renderer];
            Material[] newMaterials = new Material[originalMats.Length + 1];
            
            // Copy the original materials
            for (int i = 0; i < originalMats.Length; i++)
            {
                newMaterials[i] = originalMats[i];
            }
            
            // Add the outline material at the end
            newMaterials[originalMats.Length] = outlineMaterial;
            
            // Apply the new materials array
            renderer.materials = newMaterials;
        }
    }
    
    private void RestoreOriginalMaterials()
    {
        foreach (Renderer renderer in renderers)
        {
            if (originalMaterials.ContainsKey(renderer))
            {
                renderer.materials = originalMaterials[renderer];
            }
        }
    }
    
    private void OnDestroy()
    {
        // Restore original materials before destroying
        RestoreOriginalMaterials();
        
        // Clean up created material
        if (outlineMaterial != null)
        {
            if (Application.isPlaying)
            {
                Destroy(outlineMaterial);
            }
            else
            {
                DestroyImmediate(outlineMaterial);
            }
        }
    }
}
