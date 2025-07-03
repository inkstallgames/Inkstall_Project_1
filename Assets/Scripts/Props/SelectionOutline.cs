using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Creates an outline effect that highlights only the edges of objects
/// </summary>
public class SelectionOutline : MonoBehaviour
{
    [SerializeField] private Color outlineColor = Color.white;
    [SerializeField] private float outlineWidth = 3.0f;
    
    private GameObject outlineObject;
    private Material outlineMaterial;
    private bool isOutlineEnabled = false;
    
    private void Awake()
    {
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
            CreateOutlineObject();
        }
        else
        {
            DestroyOutlineObject();
        }
    }
    
    private void CreateOutlineObject()
    {
        // Destroy any existing outline object
        DestroyOutlineObject();
        
        // Create a new outline object
        outlineObject = new GameObject(gameObject.name + "_Outline");
        outlineObject.transform.SetParent(transform);
        outlineObject.transform.localPosition = Vector3.zero;
        outlineObject.transform.localRotation = Quaternion.identity;
        outlineObject.transform.localScale = Vector3.one;
        
        // Copy all mesh renderers and mesh filters from original object
        CopyMeshes(GetComponentsInChildren<MeshFilter>());
        CopySkinnedMeshes(GetComponentsInChildren<SkinnedMeshRenderer>());
    }
    
    private void CopyMeshes(MeshFilter[] meshFilters)
    {
        foreach (MeshFilter originalMeshFilter in meshFilters)
        {
            if (originalMeshFilter.sharedMesh == null) continue;
            
            // Create child object with same hierarchy
            GameObject outlineChild = new GameObject(originalMeshFilter.gameObject.name + "_Outline");
            outlineChild.transform.SetParent(outlineObject.transform);
            outlineChild.transform.position = originalMeshFilter.transform.position;
            outlineChild.transform.rotation = originalMeshFilter.transform.rotation;
            outlineChild.transform.localScale = originalMeshFilter.transform.localScale;
            
            // Add mesh filter and renderer
            MeshFilter meshFilter = outlineChild.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = originalMeshFilter.sharedMesh;
            
            MeshRenderer meshRenderer = outlineChild.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = outlineMaterial;
        }
    }
    
    private void CopySkinnedMeshes(SkinnedMeshRenderer[] skinnedMeshRenderers)
    {
        foreach (SkinnedMeshRenderer originalRenderer in skinnedMeshRenderers)
        {
            if (originalRenderer.sharedMesh == null) continue;
            
            // Create child object with same hierarchy
            GameObject outlineChild = new GameObject(originalRenderer.gameObject.name + "_Outline");
            outlineChild.transform.SetParent(outlineObject.transform);
            outlineChild.transform.position = originalRenderer.transform.position;
            outlineChild.transform.rotation = originalRenderer.transform.rotation;
            outlineChild.transform.localScale = originalRenderer.transform.localScale;
            
            // Add skinned mesh renderer
            SkinnedMeshRenderer renderer = outlineChild.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = originalRenderer.sharedMesh;
            renderer.sharedMaterial = outlineMaterial;
            renderer.bones = originalRenderer.bones;
            renderer.rootBone = originalRenderer.rootBone;
        }
    }
    
    private void DestroyOutlineObject()
    {
        if (outlineObject != null)
        {
            if (Application.isPlaying)
            {
                Destroy(outlineObject);
            }
            else
            {
                DestroyImmediate(outlineObject);
            }
            outlineObject = null;
        }
    }
    
    private void OnDestroy()
    {
        // Clean up outline object
        DestroyOutlineObject();
        
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
