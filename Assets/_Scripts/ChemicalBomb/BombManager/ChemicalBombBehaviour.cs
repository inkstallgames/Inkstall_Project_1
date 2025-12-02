using UnityEngine;

public class ChemicalBombBehaviour : MonoBehaviour
{
    [Header("Throwing Settings")]
    [SerializeField] private Transform throwPoint;         // Point from which the ball is thrown
    [SerializeField] private GameObject chemicalBallPrefab; // The chemical ball prefab to instantiate
    [SerializeField] private float throwForce = 15f;       // Force applied to the thrown ball
    [SerializeField] private float throwCooldown = 1.0f;   // Cooldown between throws
    [SerializeField] private Camera playerCamera;          // Reference to the player's camera
    [SerializeField] private float maxThrowDistance = 50f; // Maximum distance to throw
    [SerializeField] private LayerMask hitLayers = -1;     // Layers that can be hit by the ray

    [Header("Effects")]
    [SerializeField] private ParticleSystem throwEffect;   // Optional particle effect when throwing
    [SerializeField] private AudioClip throwSound;         // Sound played when throwing the bomb
    [SerializeField] private float throwSoundVolume = 1.0f; // Volume of the throw sound
    
    [Header("Visibility Settings")]
    [SerializeField] private float ballScale = 1.0f;       // Scale of the ball (increase for better visibility)
    [SerializeField] private Material ballMaterial;        // Optional custom material for the ball
    [SerializeField] private Color ballColor = Color.green; // Color of the ball
    
    // Reference to the BallsManager for ammo management
    private ChemicalBombManager chemicalBombManager;
    private float nextThrowTime = 0f;
    private Vector3 targetPoint;
    private bool hasTarget = false;

    private void Start()
    {
        // If no camera is assigned, try to find the main camera
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        // Try to get the BallsManager instance
        chemicalBombManager = ChemicalBombManager.Instance;
        
        if (chemicalBombManager == null)
        {
            Debug.LogWarning("ChemicalBombManager not found. Ammo counting will be disabled.");
        }
    }

    private void Update()
    {
        // Update target point based on raycast
        UpdateTargetPoint();
        
        if(ChemicalBombManager.Instance.currentBombs <= 0)
        {
            this.gameObject.SetActive(false);
        }

    }
    
    private void UpdateTargetPoint()
    {
        // Cast ray from camera center
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        RaycastHit hit;
        
        if (Physics.Raycast(ray, out hit, maxThrowDistance, hitLayers))
        {
            // We hit something, set it as our target
            targetPoint = hit.point;
            hasTarget = true;
        }
        else
        {
            // We didn't hit anything, set a point in the distance as target
            targetPoint = playerCamera.transform.position + playerCamera.transform.forward * maxThrowDistance;
            hasTarget = false;
        }
    }
    
    private Vector3 CalculateVelocityToHitTarget(Vector3 startPoint, Vector3 targetPoint, float force)
    {
        // Calculate direction to target
        Vector3 direction = targetPoint - startPoint;
        float distance = direction.magnitude;
        direction.Normalize();
        
        // For simplicity, we'll use a direct path with slight arc
        // In a more complex implementation, you could solve the projectile motion equation
        
        // Add a slight upward component to account for gravity
        direction.y += distance * 0.01f;
        direction.Normalize();
        
        return direction * force;
    }

    public void ThrowChemicalBall()
    {
        // Check if we have ammo available
        if (ChemicalBombManager.Instance != null && ChemicalBombManager.Instance.currentBombs <= 0)
        {
            Debug.Log("No chemical balls left!");
            return;
        }

        // Set cooldown
        nextThrowTime = Time.time + throwCooldown;

        // Instantiate the chemical ball at the throw point
        GameObject ball = Instantiate(chemicalBallPrefab, throwPoint.position, Quaternion.identity);
        
        // Scale the ball for better visibility
        ball.transform.localScale *= ballScale;
        
        // Apply custom material or color if provided
        Renderer ballRenderer = ball.GetComponent<Renderer>();
        if (ballRenderer != null)
        {
            if (ballMaterial != null)
            {
                ballRenderer.material = ballMaterial;
            }
            else
            {
                ballRenderer.material.color = ballColor;
            }
        }
        
        Rigidbody rb = ball.GetComponent<Rigidbody>();

        if (rb != null)
        {
            // Calculate velocity needed to hit the target
            Vector3 velocity = CalculateVelocityToHitTarget(throwPoint.position, targetPoint, throwForce);
            
            // Apply force to the ball in the calculated direction
            rb.AddForce(velocity, ForceMode.Impulse);
            
            // Play throw effect if available
            if (throwEffect != null)
            {
                throwEffect.Play();
            }
            
            // Play throw sound if available
            if (throwSound != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFXAtPoint(throwSound, throwPoint.position);
            }
            
            // Decrease ammo count
            if (ChemicalBombManager.Instance != null)
            {
                ChemicalBombManager.Instance.DecreaseBomb();
            }
        }
        else
        {
            Debug.LogError("Chemical ball prefab must have a Rigidbody component!");
        }
    }

    private void OnCollisionEnter(Collision other)
    {
        if (other.gameObject.CompareTag("Alien"))
        {
            Debug.Log("Alien hit by chemical ball!");
            other.gameObject.SetActive(false);
            Destroy(gameObject);
            // Play Partivle effects on hit alien
            // and also 

        }
        else
        {
            // play normal object hit partcle effect
        }
    }
}
