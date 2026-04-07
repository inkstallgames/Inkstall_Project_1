using UnityEngine;
using Fusion;

/// <summary>
/// Ensures FPS hands are always visible when the player spawns.
/// This script runs independently of the wobble system to guarantee hands visibility.
/// Attach this to your FPS hands GameObject.
/// </summary>
public class FPSHandsVisibility : NetworkBehaviour
{
    [Header("Visibility Settings")]
    [SerializeField] private bool forceVisible = true;
    [SerializeField] private bool showDebugLogs = false;
    
    private void Awake()
    {
        // Ensure hands are visible immediately on awake
        if (forceVisible)
        {
            EnsureHandsVisible();
        }
    }
    
    private void Start()
    {
        // Backup visibility check
        if (forceVisible)
        {
            EnsureHandsVisible();
        }
    }
    
    public override void Spawned()
    {
        // Network spawn visibility check
        if (forceVisible)
        {
            EnsureHandsVisible();
            
            if (showDebugLogs)
            {
                Debug.Log("[FPSHandsVisibility] Hands forced visible on spawn!");
            }
        }
    }
    
    public void EnsureHandsVisible()
    {
        // Make sure the GameObject is active
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
            
            if (showDebugLogs)
                Debug.Log("[FPSHandsVisibility] Activated disabled hands GameObject");
        }
        
        // Check if renderer exists and enable it
        var renderer = GetComponent<Renderer>();
        if (renderer != null && !renderer.enabled)
        {
            renderer.enabled = true;
            
            if (showDebugLogs)
                Debug.Log("[FPSHandsVisibility] Enabled main renderer");
        }
        
        // Check all child renderers
        var childRenderers = GetComponentsInChildren<Renderer>();
        foreach (var childRenderer in childRenderers)
        {
            if (!childRenderer.enabled)
            {
                childRenderer.enabled = true;
                
                if (showDebugLogs)
                    Debug.Log($"[FPSHandsVisibility] Enabled child renderer: {childRenderer.name}");
            }
        }
        
        // Check for SkinnedMeshRenderers (common for character hands)
        var skinnedRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();
        foreach (var skinnedRenderer in skinnedRenderers)
        {
            if (!skinnedRenderer.enabled)
            {
                skinnedRenderer.enabled = true;
                
                if (showDebugLogs)
                    Debug.Log($"[FPSHandsVisibility] Enabled SkinnedMeshRenderer: {skinnedRenderer.name}");
            }
        }
        
        // Force update of any animators
        var animators = GetComponentsInChildren<Animator>();
        foreach (var animator in animators)
        {
            if (!animator.enabled)
            {
                animator.enabled = true;
                
                if (showDebugLogs)
                    Debug.Log($"[FPSHandsVisibility] Enabled Animator: {animator.name}");
            }
        }
    }
    
    /// <summary>
    /// Call this method if you need to manually force hands to be visible
    /// </summary>
    public void ForceVisible()
    {
        EnsureHandsVisible();
    }
    
    private void OnValidate()
    {
        // Ensure this component is always enabled in the editor
        if (!enabled)
        {
            enabled = true;
            
            if (showDebugLogs)
                Debug.Log("[FPSHandsVisibility] Component auto-enabled in editor");
        }
    }
}
