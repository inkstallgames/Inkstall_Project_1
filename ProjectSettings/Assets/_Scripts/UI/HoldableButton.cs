using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Simple helper component that tracks whether a UI button is being held down.
/// Uses IPointerDownHandler/IPointerUpHandler for reliable touch and mouse support.
/// Attach to any UI Button GameObject.
/// </summary>
public class HoldableButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    /// <summary>
    /// True while the button is being held down (pointer/touch is active on it).
    /// </summary>
    public bool IsHeld { get; private set; }

    public void OnPointerDown(PointerEventData eventData)
    {
        IsHeld = true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        IsHeld = false;
    }

    private void OnDisable()
    {
        IsHeld = false;
    }
}
