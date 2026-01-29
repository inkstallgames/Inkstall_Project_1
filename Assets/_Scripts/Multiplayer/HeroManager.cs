using UnityEngine;
using Fusion;

public class HeroManager : MonoBehaviour
{
    public static HeroManager Instance { get; private set; }

    public NetworkObject[] heroPrefabs;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public NetworkObject GetHeroPrefab(int heroId)
    {
        if (heroId >= 0 && heroId < heroPrefabs.Length)
        {
            return heroPrefabs[heroId];
        }
        return null;
    }
}
