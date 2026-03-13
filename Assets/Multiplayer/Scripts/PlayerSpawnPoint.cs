using UnityEngine;

public class PlayerSpawnPoint : MonoBehaviour
{
    public int teamId = 0; // 0 = Blue team, 1 = Red team, -1 = Free for all
    public bool isOccupied = false;
    
    private void OnDrawGizmos()
    {
        // Draw a colored sphere to visualize the spawn point in the editor
        Gizmos.color = teamId switch
        {
            0 => Color.blue,
            1 => Color.red,
            _ => Color.gray
        };
        
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1.5f);
    }
}
