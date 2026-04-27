using Fusion;
using UnityEngine;
using System.Collections;

/// <summary>
/// Handles team-based abilities:
///   Team A (Heroes, teamId = 0) → Shield: immune to damage for 5 seconds (player glows blue).
///   Team B (Aliens, teamId = 1) → Invisibility: hidden from other players for 5 seconds.
///
/// Charge system: ability starts ready. Using it consumes the charge.
/// The charge is restored only when the player earns a kill (or on respawn).
/// </summary>
public class PlayerAbilityController : NetworkBehaviour
{
    // ---------------------------------------------------------------
    // Networked State
    // ---------------------------------------------------------------

    [Networked] public NetworkBool IsShielded  { get; set; }
    [Networked] public NetworkBool IsInvisible { get; set; }

    /// <summary>
    /// True when the player has a charge available to use.
    /// Starts true, set false on use, set true again on kill/respawn.
    /// </summary>
    [Networked] public NetworkBool AbilityReady { get; set; }
    
    // Client-side prediction for instant ability feedback
    private bool hasPredictedAbility;
    private bool predictedShield;
    private float predictedAbilityTime;

    // ---------------------------------------------------------------
    // Inspector Settings
    // ---------------------------------------------------------------

    [Header("Ability Settings")]
    [SerializeField] private float abilityDuration = 5f;

    [Header("Shared Renderers (Hero + Alien)")]
    [Tooltip("Drag ALL Renderer components of this player character here (body, arms, head, etc.).")]
    [SerializeField] private Renderer[] playerRenderers;

    [Header("Shield Glow — Team A / Hero")]
    [Tooltip("Assign your custom Hologram Material here. It overlays a glowing shell *on top* of your original mesh when the shield is active!")]
    [SerializeField] private Material shieldMaterial;

    [Header("Invisibility — Team B / Alien")]
    [Tooltip("Assign a ghostly transparent material here to be used when invisible.")]
    [SerializeField] private Material invisibilityMaterial;

    [Header("Ability Sound Effects")]
    [Tooltip("Sound played when shield ability is activated")]
    [SerializeField] private AudioClip shieldStartSound;
    
    [Tooltip("Sound played when invisibility ability is activated")]
    [SerializeField] private AudioClip invisibilityStartSound;
    
    [Tooltip("Sound played once when shield ability is used")]
    [SerializeField] private AudioClip shieldUseSound;
    
    [Tooltip("Sound played once when invisibility ability is used")]
    [SerializeField] private AudioClip invisibilityUseSound;
    
    [Tooltip("Sound played when ability ends")]
    [SerializeField] private AudioClip abilityEndSound;

    // ---------------------------------------------------------------
    // Private
    // ---------------------------------------------------------------

    private PlayerNetworkData _playerData;
    private Coroutine _abilityCoroutine;
    private System.Collections.Generic.Dictionary<Renderer, Material[]> _originalMaterials = new System.Collections.Generic.Dictionary<Renderer, Material[]>();
    
    // Audio components
    private AudioSource _abilityAudioSource;
    private bool _isLocalPlayer;

    // ---------------------------------------------------------------
    // Lifecycle
    // ---------------------------------------------------------------

    public override void Spawned()
    {
        base.Spawned();
        _playerData = GetComponent<PlayerNetworkData>();

        // Auto-fetch renderers if the array is empty or contains missing references (e.g. after mesh replacement)
        bool needsRenderers = (playerRenderers == null || playerRenderers.Length == 0);
        if (!needsRenderers)
        {
            foreach (var r in playerRenderers)
            {
                if (r == null) { needsRenderers = true; break; }
            }
        }
        
        if (needsRenderers)
        {
            playerRenderers = GetComponentsInChildren<Renderer>(true);
        }

        // Cache original materials
        _originalMaterials.Clear();
        foreach (var r in playerRenderers)
        {
            if (r != null)
            {
                _originalMaterials[r] = r.sharedMaterials; // Use sharedMaterials to avoid instancing duplicates
            }
        }

        SetShieldGlow(false); // ensure no glow at spawn

        // Give charge on spawn
        if (Object.HasStateAuthority)
            AbilityReady = true;
            
        // Setup audio components for local player only
        SetupAudio();
    }

    public override void Render()
    {
        SetShieldGlow(IsShielded);
        ApplyInvisibilityVisuals();
    }

    // ---------------------------------------------------------------
    // Public API
    // ---------------------------------------------------------------

    /// <summary>Called via UI button or Q key. Sends to server to activate ability.</summary>
    public void RequestAbility()
    {
        if (_playerData == null) return;
        if (!AbilityReady) return;       // no charge available
        
        // Client-side prediction for instant ability feedback
        if (Object.HasInputAuthority && !hasPredictedAbility)
        {
            PredictAbility();
        }
        
        // Send to server
        RPC_UseAbility();
    }
    
    /// <summary>Called via UI button or Q key. Sends to server to activate ability.</summary>
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_UseAbility()
    {
        if (_playerData == null) return;
        if (!AbilityReady) return;       // no charge available

        AbilityReady = false;            // consume the charge
        
        // Clear client-side prediction when server processes
        if (Object.HasInputAuthority)
        {
            ClearAbilityPrediction();
        }

        if (_playerData.TeamId == 0)      ActivateShield();
        else if (_playerData.TeamId == 1) ActivateInvisibility();
    }
    
    /// <summary>
    /// Predict ability activation locally for instant feedback (Among Us style)
    /// </summary>
    private void PredictAbility()
    {
        if (!AbilityReady) return;
        
        // Store prediction data
        hasPredictedAbility = true;
        predictedAbilityTime = Time.time;
        predictedShield = (_playerData.TeamId == 0);
        
        // Play instant ability effects
        PlayPredictedAbilityEffects();
    }
    
    /// <summary>
    /// Play predicted ability effects instantly
    /// </summary>
    private void PlayPredictedAbilityEffects()
    {
        if (predictedShield)
        {
            // Predicted shield effects
            PlayAbilityStartSound(true);
            StartAbilityLoopSound(true);
            SetShieldGlow(true);
        }
        else
        {
            // Predicted invisibility effects
            PlayAbilityStartSound(false);
            StartAbilityLoopSound(false);
            ApplyInvisibilityVisuals();
        }
        
        // Predict ability state
        AbilityReady = false;
        if (predictedShield)
        {
            IsShielded = true;
            IsInvisible = false;
        }
        else
        {
            IsInvisible = true;
            IsShielded = false;
        }
    }

    /// <summary>
    /// Restores one ability charge. Called server-side on kill or respawn.
    /// </summary>
    public void GrantAbilityCharge()
    {
        if (!Object.HasStateAuthority) return;
        AbilityReady = true;
    }

    /// <summary>True when the ability cannot be used (no charge).</summary>
    public bool IsOnCooldown() => !AbilityReady;

    /// <summary>Returns 0 when ready, 1 when no charge (used for UI fill amount).</summary>
    public float GetCooldownRemaining() => AbilityReady ? 0f : 1f;

    /// <summary>Returns the max duration of the ability.</summary>
    public float GetAbilityDuration() => abilityDuration;

    /// <summary>True when the ability is currently active.</summary>
    public bool IsAbilityActive => IsShielded || IsInvisible;

    // ---------------------------------------------------------------
    // Server-side activation
    // ---------------------------------------------------------------

    private void ActivateShield()
    {
        IsShielded  = true;
        IsInvisible = false;
        RPC_SyncAbilityState(true, false);
        if (_abilityCoroutine != null) StopCoroutine(_abilityCoroutine);
        _abilityCoroutine = StartCoroutine(DeactivateAfterDelay(abilityDuration));
        
        // Play shield sounds
        PlayAbilityStartSound(true);
        PlayAbilityUseSound(true);
    }

    private void ActivateInvisibility()
    {
        IsInvisible = true;
        IsShielded  = false;
        RPC_SyncAbilityState(false, true);
        if (_abilityCoroutine != null) StopCoroutine(_abilityCoroutine);
        _abilityCoroutine = StartCoroutine(DeactivateAfterDelay(abilityDuration));
        
        // Play invisibility sounds
        PlayAbilityStartSound(false);
        PlayAbilityUseSound(false);
    }

    private IEnumerator DeactivateAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        IsShielded  = false;
        IsInvisible = false;
        RPC_SyncAbilityState(false, false);
        
        // Play end sound
        PlayAbilityEndSound();
    }

    // ---------------------------------------------------------------
    // RPC — sync to all clients
    // ---------------------------------------------------------------

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SyncAbilityState(NetworkBool shielded, NetworkBool invisible)
    {
        IsShielded  = shielded;
        IsInvisible = invisible;
    }

    // ---------------------------------------------------------------
    // Visual — Shield Glow (emission on player's own renderers)
    // ---------------------------------------------------------------

    private void SetShieldGlow(bool on)
    {
        if (playerRenderers == null) return;
        if (shieldMaterial == null) return;

        foreach (var r in playerRenderers)
        {
            if (r == null) continue;
            
            if (on)
            {
                // ADD the shield material as an extra layer on top of the original materials!
                Material[] original = _originalMaterials.ContainsKey(r) ? _originalMaterials[r] : r.sharedMaterials;
                Material[] newMats = new Material[original.Length + 1];
                for (int i = 0; i < original.Length; i++) newMats[i] = original[i];
                newMats[newMats.Length - 1] = shieldMaterial;
                r.sharedMaterials = newMats;
            }
            else
            {
                if (_originalMaterials.TryGetValue(r, out var original))
                {
                    r.sharedMaterials = original;
                }
            }
        }
    }

    // ---------------------------------------------------------------
    // Visual — Invisibility (alpha fade)
    // ---------------------------------------------------------------

    private void ApplyInvisibilityVisuals()
    {
        if (playerRenderers == null || playerRenderers.Length == 0) return;

        bool isLocalPlayer = Object != null && Object.HasInputAuthority;

        foreach (var r in playerRenderers)
        {
            if (r == null) continue;

            if (IsInvisible)
            {
                if (isLocalPlayer)
                {
                    if (invisibilityMaterial != null)
                    {
                        Material[] newMats = new Material[r.sharedMaterials.Length];
                        for (int i = 0; i < newMats.Length; i++) newMats[i] = invisibilityMaterial;
                        r.sharedMaterials = newMats;
                    }
                }
                else
                {
                    r.enabled = false;
                }
            }
            else
            {
                r.enabled = true;
                if (invisibilityMaterial != null)
                {
                    if (_originalMaterials.TryGetValue(r, out var original))
                    {
                        r.sharedMaterials = original;
                    }
                }
            }
        }
    }
    
    // ---------------------------------------------------------------
    // Audio System
    // ---------------------------------------------------------------
    
    /// <summary>
    /// Setup audio components for local player only
    /// </summary>
    private void SetupAudio()
    {
        // Only setup audio for local player
        _isLocalPlayer = Object.HasInputAuthority;
        
        if (!_isLocalPlayer) return;
        
        // Create AudioSource for one-shot sounds (ability start/end)
        _abilityAudioSource = gameObject.AddComponent<AudioSource>();
        _abilityAudioSource.playOnAwake = false;
        _abilityAudioSource.spatialBlend = 0f; // 2D sound for local player
        

    }
    
    /// <summary>
    /// Play ability start sound
    /// </summary>
    private void PlayAbilityStartSound(bool isShield)
    {
        if (!_isLocalPlayer || _abilityAudioSource == null) return;
        
        AudioClip clipToPlay = isShield ? shieldStartSound : invisibilityStartSound;
        
        if (clipToPlay != null)
        {
            _abilityAudioSource.PlayOneShot(clipToPlay);
        }
        else
        {
            // Sound not assigned
        }
    }
    
    /// <summary>
    /// Play ability use sound (one-shot)
    /// </summary>
    private void PlayAbilityUseSound(bool isShield)
    {
        if (!_isLocalPlayer || _abilityAudioSource == null) return;
        
        AudioClip clipToPlay = isShield ? shieldUseSound : invisibilityUseSound;
        
        if (clipToPlay != null)
        {
            _abilityAudioSource.PlayOneShot(clipToPlay);
            Debug.Log($"[PlayerAbilityController] Playing {(isShield ? "shield" : "invisibility")} use sound");
        }
        else
        {
            Debug.LogWarning($"[PlayerAbilityController] {(isShield ? "shield" : "invisibility")} use sound is not assigned!");
        }
    }
    
    /// <summary>
    /// Play ability end sound
    /// </summary>
    private void PlayAbilityEndSound()
    {
        if (!_isLocalPlayer || _abilityAudioSource == null) return;
        
        if (abilityEndSound != null)
        {
            _abilityAudioSource.PlayOneShot(abilityEndSound);
        }
        else
        {
            // Sound not assigned
        }
    }
    
    /// <summary>
    /// Clear client-side ability prediction
    /// </summary>
    private void ClearAbilityPrediction()
    {
        if (hasPredictedAbility)
        {
            hasPredictedAbility = false;
            predictedShield = false;
            predictedAbilityTime = 0f;
        }
    }
}
