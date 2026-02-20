using Fusion;
using UnityEngine;

public class NetworkPlayerMovement : NetworkBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;
    public float maxHealth = 100f;
    
    [Networked] public float CurrentHealth { get; set; }
    [Networked] public Vector3 AimDirection { get; set; }
    [Networked] public bool IsDead { get; set; }
    
    private CharacterController characterController;
    private PlayerCameraController cameraController;
    private Vector3 movement;

    public override void Spawned()
    {
        if (Object.HasInputAuthority)
        {
            cameraController = GetComponent<PlayerCameraController>();
            CurrentHealth = maxHealth;
            IsDead = false;
        }
        
        characterController = GetComponent<CharacterController>();
        if (characterController == null)
        {
            characterController = gameObject.AddComponent<CharacterController>();
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (GetInput<PlayerInputData>(out var input) && !IsDead)
        {
            // Movement - move relative to where player is looking
            if (input.movement.sqrMagnitude > 0.01f)
            {
                movement = (transform.forward * input.movement.y + transform.right * input.movement.x).normalized;
                characterController.Move(movement * moveSpeed * Runner.DeltaTime);
            }
            
            // Handle Shooting
            if (input.isShooting)
            {
                Shoot();
            }
            
            // Rotation - face where camera is looking
            if (input.aimDirection != Vector3.zero)
            {
                AimDirection = input.aimDirection;
                Vector3 lookDirection = AimDirection;
                lookDirection.y = 0; // Keep only horizontal rotation
                
                if (lookDirection.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                    transform.rotation = Quaternion.Slerp(transform.rotation, 
                        targetRotation, 
                        rotationSpeed * Runner.DeltaTime);
                }
            }
        }
    }
    
    public void TakeDamage(float damage)
    {
        if (IsDead) return;
        
        CurrentHealth -= damage;
        
        if (CurrentHealth <= 0)
        {
            Die();
        }
    }
    
    private void Shoot()
    {
        // Simple raycast shooting
        if (Runner.IsServer)
        {
            RaycastHit hit;
            Vector3 shootDirection = cameraController.GetCameraForward();
            
            if (Physics.Raycast(transform.position + Vector3.up, shootDirection, out hit, 100f))
            {
                Debug.Log($"Hit: {hit.collider.name}");
                
                // Check if we hit another player
                var hitPlayer = hit.collider.GetComponent<NetworkPlayerMovement>();
                if (hitPlayer != null && hitPlayer != this)
                {
                    hitPlayer.TakeDamage(25f); // 25 damage per shot
                    
                    // Notify GameManager of kill
                    if (hitPlayer.CurrentHealth <= 0)
                    {
                        var gameManager = FindObjectOfType<NetworkGameManager>();
                        if (gameManager != null)
                        {
                            gameManager.OnPlayerKilled(hitPlayer.Object.InputAuthority, Object.InputAuthority);
                        }
                    }
                }
            }
        }
    }
    
    private void Die()
    {
        IsDead = true;
        if (characterController != null)
        {
            characterController.Move(Vector3.zero);
        }
        
        // Start respawn timer
        StartCoroutine(RespawnAfterDelay(5f));
    }
    
    private System.Collections.IEnumerator RespawnAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (Runner.IsServer)
        {
            // Respawn player
            IsDead = false;
            CurrentHealth = maxHealth;
            
            // Move to spawn point
            var gameManager = FindObjectOfType<NetworkGameManager>();
            if (gameManager != null)
            {
                var spawnPoint = gameManager.GetSpawnPoint(0); // Team 0 for now
                if (spawnPoint != null)
                {
                    characterController.enabled = false;
                    transform.position = spawnPoint.position;
                    characterController.enabled = true;
                }
            }
        }
    }
}
