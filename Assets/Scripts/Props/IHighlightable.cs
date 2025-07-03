using UnityEngine;

/// <summary>
/// Interface for objects that can be highlighted when player looks at them
/// </summary>
public interface IHighlightable
{
    void Highlight();
    void Unhighlight();
}
