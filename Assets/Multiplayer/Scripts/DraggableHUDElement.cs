using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach this to any HUD panel/button group you want the player to be able to
/// reposition and scale inside the HUD Customization editor.
///
/// Uses direct Input polling (not the UI EventSystem) so it works even on
/// elements that have their own pointer handlers (joystick, buttons, etc.)
///
/// In normal gameplay (IsEditMode == FALSE) this component does nothing and
/// has zero CPU cost.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class DraggableHUDElement : MonoBehaviour
{
    // ---------------------------------------------------------------
    // Inspector
    // ---------------------------------------------------------------

    [Tooltip("Human-readable name shown in the editor sidebar. Defaults to the GameObject name.")]
    public string displayName;

    [Tooltip("Minimum allowed scale multiplier.")]
    [Range(0.2f, 1f)] public float minScale = 0.4f;

    [Tooltip("Maximum allowed scale multiplier.")]
    [Range(1f, 3f)] public float maxScale = 2.5f;

    [Tooltip("If true, the element can be toggled visible/invisible in the editor.")]
    public bool allowToggleVisibility = true;

    // ---------------------------------------------------------------
    // Internal state
    // ---------------------------------------------------------------

    private RectTransform _rt;

    /// <summary>Lazily resolved to survive DestroyImmediate of the HUD Canvas.</summary>
    private Canvas _rootCanvas;

    // Mouse drag polling
    private static DraggableHUDElement _currentlyDragging; // only one at a time
    private Vector2 _lastMousePos;

    // Pinch tracking (mobile). Ownership is static so a two-finger pinch only
    // resizes the element it is centred on, never every element at once.
    private static DraggableHUDElement _currentlyPinching;
    private static float _pinchOwnerDistance;
    private float   _initialPinchDistance;
    private Vector3 _initialPinchScale;

    /// <summary>How far (screen px) from an element the pinch centre may be and still grab it.</summary>
    private const float PinchClaimRadius = 200f;

    // ---------------------------------------------------------------
    // Public API
    // ---------------------------------------------------------------

    public string        ElementId => gameObject.name;
    public RectTransform RT        => _rt != null ? _rt : (_rt = GetComponent<RectTransform>());

    private void Awake()
    {
        _rt = GetComponent<RectTransform>();
        if (string.IsNullOrEmpty(displayName))
            displayName = gameObject.name;
    }

    // ---------------------------------------------------------------
    // Canvas resolution helper
    // ---------------------------------------------------------------

    private Canvas GetRootCanvas()
    {
        if (_rootCanvas != null) return _rootCanvas;
        _rootCanvas = GetComponentInParent<Canvas>();
        return _rootCanvas;
    }

    // ---------------------------------------------------------------
    // Update — polling, no EventSystem required
    // ---------------------------------------------------------------

    private void Update()
    {
        if (!HUDCustomizationManager.IsEditMode)
        {
            // Release ownership if edit mode was exited mid-drag
            if (_currentlyDragging == this) _currentlyDragging = null;
            if (_currentlyPinching == this) _currentlyPinching = null;
            return;
        }

        // A two-finger pinch must not also drag the element around.
        if (Input.touchCount >= 2)
        {
            if (_currentlyDragging == this) _currentlyDragging = null;
        }
        else
        {
            HandleMouseDrag();
        }

        HandlePinchScale();
        HandleScrollWheelScale();
    }

    // ---------------------------------------------------------------
    // Mouse drag (PC)
    // ---------------------------------------------------------------

    private void HandleMouseDrag()
    {
        // Claim drag on mouse-down if pointer is inside this element
        if (Input.GetMouseButtonDown(0))
        {
            if (_currentlyDragging == null && IsPointerOverThis())
            {
                _currentlyDragging = this;
                _lastMousePos      = Input.mousePosition;

                // In the lobby preview, bringing the element to front helps.
                // On the live HUD it would reorder the real UI and draw the
                // element over the editor toolbar, so leave the order alone.
                if (!HUDCustomizationManager.IsLiveEditModeActive)
                    _rt.SetAsLastSibling();
                Debug.Log($"[HUDCustomize] Started dragging '{ElementId}' at {_rt.anchoredPosition}.");
            }
        }

        // Release drag
        if (Input.GetMouseButtonUp(0) && _currentlyDragging == this)
        {
            _currentlyDragging = null;
            Debug.Log($"[HUDCustomize] Dropped '{ElementId}' at {_rt.anchoredPosition}, scale {_rt.localScale.x:0.00}.");
        }

        // Move while dragging
        if (_currentlyDragging == this && Input.GetMouseButton(0))
        {
            Vector2 currentPos = Input.mousePosition;
            Vector2 delta      = currentPos - _lastMousePos;
            _lastMousePos      = currentPos;

            Canvas canvas      = GetRootCanvas();
            float  scaleFactor = (canvas != null && canvas.scaleFactor > 0f)
                                 ? canvas.scaleFactor : 1f;

            _rt.anchoredPosition += delta / scaleFactor;
        }
    }

    // ---------------------------------------------------------------
    // Pinch scale (mobile — two fingers)
    // ---------------------------------------------------------------

    private void HandlePinchScale()
    {
        if (Input.touchCount != 2)
        {
            // Pinch finished — release the claim so the next pinch can pick a new target.
            if (_currentlyPinching == this)
            {
                Debug.Log($"[HUDCustomize] Finished resizing '{ElementId}' at scale {_rt.localScale.x:0.00}.");
                _currentlyPinching = null;
                _initialPinchDistance = 0f;
            }
            return;
        }

        Touch t0 = Input.GetTouch(0);
        Touch t1 = Input.GetTouch(1);
        Vector2 pinchCenter = (t0.position + t1.position) * 0.5f;

        // Claim the pinch for whichever element is closest to the pinch centre.
        bool pinchStarting = t0.phase == TouchPhase.Began || t1.phase == TouchPhase.Began;
        if (pinchStarting || _currentlyPinching == null)
        {
            float distance = ScreenDistanceTo(pinchCenter);
            bool eligible = distance <= PinchClaimRadius;

            if (eligible && (_currentlyPinching == null || distance < _pinchOwnerDistance))
            {
                _currentlyPinching  = this;
                _pinchOwnerDistance = distance;
            }

            if (_currentlyPinching == this)
            {
                _initialPinchDistance = Vector2.Distance(t0.position, t1.position);
                _initialPinchScale    = _rt.localScale;
                Debug.Log($"[HUDCustomize] Pinch grabbed '{ElementId}' (distance to pinch centre {distance:0}px).");
                return;
            }
        }

        if (_currentlyPinching != this) return;
        if (_initialPinchDistance <= 0f) return;

        float currentDist = Vector2.Distance(t0.position, t1.position);
        float ratio       = currentDist / _initialPinchDistance;
        float newUniform  = Mathf.Clamp(_initialPinchScale.x * ratio, minScale, maxScale);
        _rt.localScale    = new Vector3(newUniform, newUniform, 1f);
    }

    /// <summary>
    /// 0 when the point is inside this element, otherwise the screen-space gap
    /// between the point and the element's centre.
    /// </summary>
    private float ScreenDistanceTo(Vector2 screenPoint)
    {
        Canvas canvas = GetRootCanvas();
        Camera cam = canvas != null ? canvas.worldCamera : null;

        if (RectTransformUtility.RectangleContainsScreenPoint(_rt, screenPoint, cam))
            return 0f;

        Vector2 center = RectTransformUtility.WorldToScreenPoint(cam, _rt.position);
        return Vector2.Distance(center, screenPoint);
    }

    // ---------------------------------------------------------------
    // Scroll-wheel scale (PC)
    // ---------------------------------------------------------------

    private void HandleScrollWheelScale()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) < 0.001f) return;
        if (!IsPointerOverThis()) return;

        float current  = _rt.localScale.x;
        float next     = Mathf.Clamp(current + scroll * 0.5f, minScale, maxScale);
        _rt.localScale = new Vector3(next, next, 1f);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private bool IsPointerOverThis()
    {
        Canvas canvas = GetRootCanvas();
        return RectTransformUtility.RectangleContainsScreenPoint(
            _rt, Input.mousePosition,
            canvas != null ? canvas.worldCamera : null);
    }
}
