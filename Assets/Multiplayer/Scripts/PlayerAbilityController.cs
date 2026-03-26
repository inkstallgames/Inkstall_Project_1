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

    // ---------------------------------------------------------------
    // Private
    // ---------------------------------------------------------------

    private PlayerNetworkData _playerData;
    private Coroutine _abilityCoroutine;
    private System.Collections.Generic.Dictionary<Renderer, Material[]> _originalMaterials = new System.Collections.Generic.Dictionary<Renderer, Material[]>();

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
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_UseAbility()
    {
        if (_playerData == null) return;
        if (!AbilityReady) return;       // no charge available

        AbilityReady = false;            // consume the charge

        if (_playerData.TeamId == 0)      ActivateShield();
        else if (_playerData.TeamId == 1) ActivateInvisibility();
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
    }

    private void ActivateInvisibility()
    {
        IsInvisible = true;
        IsShielded  = false;
        RPC_SyncAbilityState(false, true);
        if (_abilityCoroutine != null) StopCoroutine(_abilityCoroutine);
        _abilityCoroutine = StartCoroutine(DeactivateAfterDelay(abilityDuration));
    }

    private IEnumerator DeactivateAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        IsShielded  = false;
        IsInvisible = false;
        RPC_SyncAbilityState(false, false);
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
}
