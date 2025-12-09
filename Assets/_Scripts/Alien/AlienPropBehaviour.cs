using UnityEngine;
using System.Collections;

public class AlienPropBehaviour : MonoBehaviour
{
    private float timer;
    private float randomInterval;

    public enum BehaviourType { Random, Wiggle, FlickerLight, RotateTowardsPlayer, Breathing, ChangeSize }
    [SerializeField] private BehaviourType behaviour = BehaviourType.Random;  // visible in inspector

    // --- Light (for lamps) ---
    private Light propLight; // for flicker if it's a lamp

    // --- Emission Glow ---
    [Header("Emission Glow Settings")]
    [SerializeField] private bool useEmissionGlow = true;
    [SerializeField] private Color emissionBaseColor = Color.black;   // default "off" color
    [SerializeField] private Color emissionGlowColor = Color.cyan;    // glow color
    [SerializeField] private float emissionMaxIntensity = 1f;       // how strong the glow is
    [SerializeField] private float emissionPulseDuration = 0.3f;      // seconds for a pulse

    private Renderer propRenderer;
    private MaterialPropertyBlock mpb;
    private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");

    private Quaternion originalRotation;

    void Start()
    {
        // Save original rotation for limiting later
        originalRotation = transform.rotation;

        // Check if this object has a Light (useful for lamp props)
        propLight = GetComponentInChildren<Light>();

        // Grab renderer for emission
        propRenderer = GetComponentInChildren<Renderer>();
        if (propRenderer != null)
        {
            mpb = new MaterialPropertyBlock();
            propRenderer.GetPropertyBlock(mpb);
            mpb.SetColor(EmissionColorID, emissionBaseColor);
            propRenderer.SetPropertyBlock(mpb);

            // Make sure emission is enabled on the material
            if (propRenderer.sharedMaterial != null)
            {
                propRenderer.sharedMaterial.EnableKeyword("_EMISSION");
            }
        }

        // If set to Random, pick a random behaviour at runtime
        if (behaviour == BehaviourType.Random)
        {
            behaviour = (BehaviourType)Random.Range(1, System.Enum.GetValues(typeof(BehaviourType)).Length);
        }

        // If Flicker chosen but no Light exists → reassign another behaviour
        if (behaviour == BehaviourType.FlickerLight && propLight == null)
        {
            do
            {
                behaviour = (BehaviourType)Random.Range(1, System.Enum.GetValues(typeof(BehaviourType)).Length);
            }
            while (behaviour == BehaviourType.FlickerLight);
        }

        Debug.Log($"{gameObject.name} assigned behaviour: {behaviour}");

        // Random timer for activity
        randomInterval = Random.Range(2f, 5f);
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (timer > randomInterval)
        {
            timer = 0f;
            randomInterval = Random.Range(2f, 5f); // reset interval

            TriggerBehaviour();

            // Every time a behaviour triggers, also pulse emission (if enabled)
            if (useEmissionGlow)
            {
                StartCoroutine(EmissionPulse());
            }
        }
    }

    void TriggerBehaviour()
    {
        switch (behaviour)
        {
            case BehaviourType.Wiggle:
                StartCoroutine(Wiggle());
                break;

            case BehaviourType.FlickerLight:
                if (propLight != null) StartCoroutine(FlickerLight());
                break;

            case BehaviourType.RotateTowardsPlayer:
                RotateTowardsPlayer();
                break;

            case BehaviourType.Breathing:
                StartCoroutine(Breathing());
                break;

            case BehaviourType.ChangeSize:
                StartCoroutine(ChangeSize());
                break;
        }
    }

    // === Behaviours ===

    IEnumerator Wiggle()
    {
        Vector3 startPos = transform.localPosition;
        Vector3 startRot = transform.localEulerAngles;

        for (float t = 0; t < 0.5f; t += Time.deltaTime)
        {
            float wiggleRot = Mathf.Sin(t * 20f) * 5f;      // rotation angle
            float wiggleMove = Mathf.Sin(t * 40f) * 0.02f;  // small position jiggle

            transform.localEulerAngles = startRot + new Vector3(0, wiggleRot, 0);
            transform.localPosition = startPos + new Vector3(wiggleMove, 0, 0);

            yield return null;
        }

        transform.localEulerAngles = startRot;
        transform.localPosition = startPos;
    }

    IEnumerator FlickerLight()
    {
        for (int i = 0; i < 5; i++)
        {
            propLight.enabled = !propLight.enabled;
            yield return new WaitForSeconds(Random.Range(0.05f, 0.2f));
        }
        propLight.enabled = true;
    }

    void RotateTowardsPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            Vector3 dir = (player.transform.position - transform.position).normalized;
            dir.y = 0; // only horizontal rotation

            Quaternion lookRot = Quaternion.LookRotation(new Vector3(dir.x, 0, dir.z));

            // Limit how far from the original rotation (e.g. ±30 degrees)
            Quaternion limitedRot = Quaternion.RotateTowards(
                originalRotation,
                lookRot,
                30f
            );

            // Smoothly rotate toward the limited rotation
            transform.rotation = Quaternion.Slerp(transform.rotation, limitedRot, Time.deltaTime * 1.5f);
        }
    }

    IEnumerator Breathing()
    {
        Vector3 originalScale = transform.localScale;
        Vector3 targetScale = originalScale * 1.05f;

        // Breathing in & out (about 1 second cycle)
        for (float t = 0; t < 1f; t += Time.deltaTime * 2f)
        {
            transform.localScale = Vector3.Lerp(
                originalScale,
                targetScale,
                Mathf.Sin(t * Mathf.PI)
            );
            yield return null;
        }

        transform.localScale = originalScale;

        // Wait a random gap (5–10 seconds) before next breath
        yield return new WaitForSeconds(Random.Range(5f, 10f));
    }

    IEnumerator ChangeSize()
    {
        Vector3 originalScale = transform.localScale;
        Vector3 targetScale = originalScale * 1.05f;

        for (float t = 0; t < 5f; t += Time.deltaTime)
        {
            transform.localScale = Vector3.Lerp(originalScale, targetScale, t / 5f);
            yield return null;
        }

        for (float t = 0; t < 5f; t += Time.deltaTime)
        {
            transform.localScale = Vector3.Lerp(targetScale, originalScale, t / 5f);
            yield return null;
        }

        transform.localScale = originalScale;
    }

    // === Emission Glow ===

    IEnumerator EmissionPulse()
    {
        if (propRenderer == null || mpb == null) yield break;

        float t = 0f;

        // Simple up-and-down pulse using a sine curve
        while (t < emissionPulseDuration)
        {
            t += Time.deltaTime;
            float normalized = Mathf.Clamp01(t / emissionPulseDuration);

            // 0 → 1 → 0 curve
            float curve = Mathf.Sin(normalized * Mathf.PI);

            Color target = emissionBaseColor + (emissionGlowColor * (curve * emissionMaxIntensity));
            ApplyEmissionColor(target);

            yield return null;
        }

        // Reset to base color
        ApplyEmissionColor(emissionBaseColor);
    }

    void ApplyEmissionColor(Color color)
    {
        if (propRenderer == null || mpb == null) return;

        propRenderer.GetPropertyBlock(mpb);
        mpb.SetColor(EmissionColorID, color);
        propRenderer.SetPropertyBlock(mpb);
    }
}
