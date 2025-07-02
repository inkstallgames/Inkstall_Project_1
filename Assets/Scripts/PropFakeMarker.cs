using UnityEngine;
using System.Collections.Generic;

public class PropFakeMarker : MonoBehaviour
{
    [Header("Assign Props Manually OR via Parent")]
    public List<PropIdentity> allProps = new List<PropIdentity>();
    public Transform propsParent; // optional: auto-collect from parent

    [Header("Fake Prop Settings")]
    public int fakeCount = 2;

    void Start()
    {
        AutoFillIfEmpty();
        AssignFakeProps();
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
            Debug.LogWarning("❌ No props found for fake selection.");
        }
    }

    public void AssignFakeProps()
    {
        if (allProps.Count == 0) return;

        fakeCount = Mathf.Clamp(fakeCount, 0, allProps.Count);

        // Reset all
        foreach (var prop in allProps)
        {
            prop.isFake = false;
        }

        // Shuffle
        List<PropIdentity> shuffled = new List<PropIdentity>(allProps);
        ShuffleList(shuffled);

        // Assign fake
        for (int i = 0; i < fakeCount; i++)
        {
            shuffled[i].isFake = true;
            Debug.Log($"✅ Marked {shuffled[i].name} as FAKE");
        }

        // Optional log
        Debug.Log($"Fake Props Assigned: {fakeCount}/{allProps.Count}");
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
