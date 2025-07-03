using UnityEngine;
using System.Collections.Generic;

public class PropFakeMarker : MonoBehaviour
{
    [Header("Assign Props Manually OR via Parent")]
    public List<PropIdentity> allProps = new List<PropIdentity>();
    public Transform propsParent; // optional: auto-collect from parent

    [Header("Prop Settings")]
    public int fakeCount = 2;

    void Start()
    {
        AutoFillIfEmpty();
        AssignPropTypes();
    }

    void AutoFillIfEmpty()
    {
        if ((allProps == null || allProps.Count == 0) && propsParent != null)
        {
            allProps = new List<PropIdentity>(propsParent.GetComponentsInChildren<PropIdentity>());
            Debug.Log($"🔁 Auto-filled {allProps.Count} props from {propsParent.name}");
        }

        if (allProps == null || allProps.Count == 0)
        {
            Debug.LogWarning("❌ No props found for assignment.");
        }
    }

    public void AssignPropTypes()
    {
        if (allProps.Count == 0) return;

        // Ensure we don't try to assign more props than available
        fakeCount = Mathf.Clamp(fakeCount, 0, allProps.Count);

        // Reset all props
        foreach (var prop in allProps)
        {
            prop.isFake = false;
        }

        // Shuffle
        List<PropIdentity> shuffled = new List<PropIdentity>(allProps);
        ShuffleList(shuffled);

        // Assign fake props
        for (int i = 0; i < fakeCount; i++)
        {
            shuffled[i].isFake = true;
            Debug.Log($"✅ Marked {shuffled[i].name} as FAKE");
            
            // Register fake prop with GameManager
            if (GameManager.Instance != null)
            {
                GameManager.Instance.RegisterFakeProp();
            }
        }

        // Optional log
        Debug.Log($"Props Assigned: {fakeCount} fake, {allProps.Count - fakeCount} real out of {allProps.Count} total");
    }

    private void ShuffleList<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int rand = Random.Range(i, list.Count);
            (list[i], list[rand]) = (list[rand], list[i]);
        }
    }
}
