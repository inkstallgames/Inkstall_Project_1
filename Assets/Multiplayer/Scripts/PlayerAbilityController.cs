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
    [Tooltip("Emission colour added to the player's materials while shielded.")]
    [SerializeField] private Color shieldGlowColor = new Color(0f, 0.5f, 1f); // cyan-blue
    [Tooltip("HDR brightness multiplier for the glow. 0 = off, 0.3 = subtle, 1 = full bright.")]
    [Range(0f, 3f)]
    [SerializeField] private float shieldGlowIntensity = 0.4f;

    [Header("Invisibility — Team B / Alien")]
    [Tooltip("Opacity applied to the LOCAL player's own renderers while invisible.")]
    [Range(0f, 1f)]
    [SerializeField] private float selfInvisibleAlpha = 0.15f;

    // ---------------------------------------------------------------
    // Private
    // ---------------------------------------------------------------

    private PlayerNetworkData _playerData;
    private Coroutine _abilityCoroutine;

    // ---------------------------------------------------------------
    // Lifecycle
    // ---------------------------------------------------------------

    public override void Spawned()
    {
        base.Spawned();
        _playerData = GetComponent<PlayerNetworkData>();
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

        Color emissionColor = on
            ? shieldGlowColor * shieldGlowIntensity
            : Color.black;

        foreach (var r in playerRenderers)
        {
            if (r == null) continue;
            foreach (var mat in r.materials)
            {
                if (on)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    mat.SetColor("_EmissionColor", emissionColor);

                    // Tint base color heavily to ensure visibility on mobile (where bloom is off)
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", shieldGlowColor);
                    else if (mat.HasProperty("_Color")) mat.SetColor("_Color", shieldGlowColor);
                }
                else
                {
                    mat.DisableKeyword("_EMISSION");
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                    mat.SetColor("_EmissionColor", Color.black);

                    // Revert base color
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
                    else if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
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
                    SetRendererAlpha(r, selfInvisibleAlpha); // faint self-view
                else
                    r.enabled = false;                        // fully hidden from others
            }
            else
            {
                r.enabled = true;
                SetRendererAlpha(r, 1f);
            }
        }
    }

    private void SetRendererAlpha(Renderer r, float alpha)
    {
        foreach (var mat in r.materials)
        {
            if (!mat.HasProperty("_Color")) continue;

            Color c = mat.color;
            c.a = alpha;
            mat.color = c;

            if (alpha < 1f)
            {
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;
            }
            else
            {
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                mat.SetInt("_ZWrite", 1);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.DisableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = -1;
            }
        }
    }
}
