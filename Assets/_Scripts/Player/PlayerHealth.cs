using Fusion;
using UnityEngine;

public class PlayerHealth : NetworkBehaviour
{
    [Networked] public float Health { get; set; } = 100f;
    private const float MaxHealth = 100f;

    public void TakeDamage(float damage)
    {
        if (Health <= 0) return;

        Health -= damage;
        if (Health <= 0)
        {
            Health = 0;
            // Handle player death logic here, e.g., by calling a method on a game manager
            Debug.Log($"Player {Object.Id} has died.");
        }
    }

    public void ResetHealth()
    {
        Health = MaxHealth;
    }
}
