using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// On-screen virtual joystick for multiplayer movement on Android.
/// Attach this to a UI Image (the joystick background) inside a Canvas.
/// Assign a child Image as the handleRect (knob).
/// NetworkPlayerSpawner.OnInput reads from NetworkJoystickControl.Instance.MovementInput.
/// </summary>
public class NetworkJoystickControl : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public static NetworkJoystickControl Instance { get; private set; }

    [Header("Rect References")]
    [Tooltip("The background circle of the joystick (this GameObject's RectTransform).")]
    [SerializeField] private RectTransform containerRect;

    [Tooltip("The movable knob inside the joystick.")]
    [SerializeField] private RectTransform handleRect;

    [Header("Settings")]
    [Tooltip("Maximum pixel distance the handle can move from center.")]
    [SerializeField] private float joystickRange = 50f;

    [Tooltip("Input below this magnitude is treated as zero (prevents drift).")]
    [SerializeField] private float deadZone = 0.1f;

    [Tooltip("Multiplier applied to the raw output value.")]
    [SerializeField] private float magnitudeMultiplier = 1f;

    [Header("Sprint Settings")]
    [Tooltip("Joystick magnitude threshold to trigger sprint (0-1). Recommended: 0.7-0.85")]
    [SerializeField] private float sprintThreshold = 0.75f;

    /// <summary>
    /// The current joystick direction as a normalized Vector2.
    /// X = horizontal (left/right), Y = vertical (forward/back).
    /// Magnitude is 0–1 (clamped).
    /// </summary>
    public Vector2 MovementInput { get; private set; }

    /// <summary>
    /// True if the joystick magnitude exceeds the sprint threshold.
    /// </summary>
    public bool IsSprinting { get; private set; }

    // ───────────────────────────────────────────────
    // Lifecycle
    // ───────────────────────────────────────────────

    private void Awake()
    {
        // Singleton — only one joystick should exist at a time
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            // Debug.LogWarning("[NetworkJoystickControl] Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }

        // Auto-assign containerRect if not set
        if (containerRect == null)
        {
            containerRect = GetComponent<RectTransform>();
        }
    }

    private void Start()
    {
        // Reset handle position
        if (handleRect != null)
        {
            handleRect.anchoredPosition = Vector2.zero;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // ───────────────────────────────────────────────
    // Pointer / Touch Handlers
    // ───────────────────────────────────────────────

    public void OnPointerDown(PointerEventData eventData)
    {
        // Treat initial touch the same as a drag
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (containerRect == null) return;

        // Convert screen position to local position inside the container
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            containerRect,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPoint
        );

        // Normalize to -1..1 range based on container size
        Vector2 normalized = new Vector2(
            localPoint.x / (containerRect.sizeDelta.x * 0.5f),
            localPoint.y / (containerRect.sizeDelta.y * 0.5f)
        );

        // Clamp magnitude to 1 (circular)
        Vector2 clamped = Vector2.ClampMagnitude(normalized, 1f);

        // Apply dead zone
        if (clamped.magnitude < deadZone)
        {
            MovementInput = Vector2.zero;
            IsSprinting = false;
        }
        else
        {
            MovementInput = clamped * magnitudeMultiplier;
            // Check if magnitude exceeds sprint threshold
            IsSprinting = clamped.magnitude >= sprintThreshold;
        }

        // Move the visual handle knob
        if (handleRect != null)
        {
            handleRect.anchoredPosition = clamped * joystickRange;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        // Reset everything when the finger lifts
        MovementInput = Vector2.zero;
        IsSprinting = false;

        if (handleRect != null)
        {
            handleRect.anchoredPosition = Vector2.zero;
        }
    }
}
