using UnityEngine;

public enum PropType { Real, Fake }

public class PropBehavior : MonoBehaviour
{
    public PropType propType;

    public void Init(PropType type)
    {
        propType = type;
    }

    public bool IsFake()
    {
        return propType == PropType.Fake;
    }
}