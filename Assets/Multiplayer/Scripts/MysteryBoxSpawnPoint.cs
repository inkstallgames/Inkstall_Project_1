using UnityEngine;

/// <summary>
/// Attach this script to empty GameObjects in your multiplayer scene to define where mystery boxes can spawn.
/// </summary>
public class MysteryBoxSpawnPoint : MonoBehaviour
{
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireCube(transform.position + Vector3.up * 0.5f, Vector3.one);
    }
}
