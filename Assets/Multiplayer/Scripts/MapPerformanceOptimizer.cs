using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Fixes sudden FPS drops in dense map areas (Rust Decals / shadow casters).
/// Runs once per loaded scene: turns off shadows on decal meshes and caps URP shadow cost.
/// </summary>
public class MapPerformanceOptimizer : MonoBehaviour
{
    [Header("Decals (alpha-cutout overdraw)")]
    [SerializeField] private bool disableDecalShadows = true;
    [SerializeField] private int renderersPerFrame = 250;

    [Header("URP shadow budget (multiplayer)")]
    [SerializeField] private bool tuneUrpShadows = true;
    [SerializeField] private float maxShadowDistance = 50f;
    [SerializeField] private int maxShadowCascades = 2;
    [SerializeField] private bool forceHardShadows = true;

    static bool _urpTuned;
    static MapPerformanceOptimizer _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoBootstrap()
    {
        // Only care about gameplay / Rust-style maps — skip if already present
        if (_instance != null) return;
        var go = new GameObject("[MapPerformanceOptimizer]");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<MapPerformanceOptimizer>();
    }

    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        ApplyForActiveScene();
    }

    void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        ApplyForActiveScene();
    }

    void ApplyForActiveScene()
    {
        if (tuneUrpShadows)
            TuneUrpShadows();

        if (disableDecalShadows)
        {
            StopAllCoroutines();
            StartCoroutine(OptimizeDecalShadowsRoutine());
        }
    }

    void TuneUrpShadows()
    {
        if (_urpTuned) return;

        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp == null) return;

        if (urp.shadowDistance > maxShadowDistance)
            urp.shadowDistance = maxShadowDistance;

        // Soft shadows are configured on the URP asset (read-only at runtime).
        // Cap distance here; QualitySettings hard shadows further reduces cost.
        QualitySettings.shadowDistance = Mathf.Min(QualitySettings.shadowDistance, maxShadowDistance);
        if (forceHardShadows)
            QualitySettings.shadows = UnityEngine.ShadowQuality.HardOnly;

        _urpTuned = true;
    }

    IEnumerator OptimizeDecalShadowsRoutine()
    {
        // Spread work across frames so joining a match doesn't hitch
        MeshRenderer[] renderers = FindObjectsOfType<MeshRenderer>(true);
        int processed = 0;
        int changed = 0;

        for (int i = 0; i < renderers.Length; i++)
        {
            MeshRenderer r = renderers[i];
            if (r == null) continue;

            if (IsDecalRenderer(r))
            {
                if (r.shadowCastingMode != ShadowCastingMode.Off)
                {
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    changed++;
                }
                if (r.receiveShadows)
                {
                    r.receiveShadows = false;
                    changed++;
                }
            }

            processed++;
            if (processed >= renderersPerFrame)
            {
                processed = 0;
                yield return null;
            }
        }

        // Unused on purpose in shipping builds; keep for future profiling if needed
        _ = changed;
        _ = maxShadowCascades;
    }

    static bool IsDecalRenderer(MeshRenderer r)
    {
        // Rust map names objects "Decals", "Decals.001", etc. — these are the
        // alpha-cutout overdraw + shadow casters that spike FPS in dense zones.
        string n = r.gameObject.name;
        return n.StartsWith("Decals", System.StringComparison.OrdinalIgnoreCase);
    }
}
