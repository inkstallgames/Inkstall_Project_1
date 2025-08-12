using UnityEngine;

public class ChemicalBombScript : MonoBehaviour
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
    
    [Header("Visibility Settings")]
    [SerializeField] private float ballScale = 1.0f;       // Scale of the ball (increase for better visibility)
    [SerializeField] private Material ballMaterial;        // Optional custom material for the ball
    [SerializeField] private Color ballColor = Color.green; // Color of the ball
    [SerializeField] private bool showTrajectory = true;   // Whether to show trajectory prediction
    [SerializeField] private int trajectorySteps = 10;     // Number of steps in trajectory prediction
    [SerializeField] private GameObject trajectoryPointPrefab; // Prefab for trajectory points
    
    // Reference to the BallsManager for ammo management
    private BallsManager ballsManager;
    private float nextThrowTime = 0f;
    private GameObject[] trajectoryPoints;
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
        ballsManager = BallsManager.Instance;
        
        if (ballsManager == null)
        {
            Debug.LogWarning("BallsManager not found. Ammo counting will be disabled.");
        }
        
        // Initialize trajectory visualization
        if (showTrajectory && trajectoryPointPrefab != null)
        {
            InitializeTrajectoryPoints();
        }
    }

    private void Update()
    {
        // Update target point based on raycast
        UpdateTargetPoint();
        
        // Check for throw input (can be changed to any input you prefer)
        if (Input.GetMouseButtonDown(1) && Time.time >= nextThrowTime)
        {
            ThrowChemicalBall();
        }
        
        // Update trajectory visualization
        if (showTrajectory && trajectoryPoints != null && hasTarget)
        {
            UpdateTrajectoryVisualization();
        }
        else if (trajectoryPoints != null)
        {
            HideTrajectory();
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
    
    private void InitializeTrajectoryPoints()
    {
        trajectoryPoints = new GameObject[trajectorySteps];
        
        for (int i = 0; i < trajectorySteps; i++)
        {
            trajectoryPoints[i] = Instantiate(trajectoryPointPrefab);
            trajectoryPoints[i].SetActive(false);
        }
    }
    
    private void UpdateTrajectoryVisualization()
    {
        // Only show trajectory if we have ammo
        if (ballsManager != null && ballsManager.currentBombs <= 0)
        {
            HideTrajectory();
            return;
        }
        
        // Calculate direction to target
        Vector3 throwDirection = (targetPoint - throwPoint.position).normalized;
        
        // Calculate initial velocity needed to hit the target
        float distance = Vector3.Distance(throwPoint.position, targetPoint);
        Vector3 velocity = CalculateVelocityToHitTarget(throwPoint.position, targetPoint, throwForce);
        
        // Visualize trajectory
        Vector3 position = throwPoint.position;
        float timeStep = 0.1f;
        
        for (int i = 0; i < trajectorySteps; i++)
        {
            float timeOffset = timeStep * i;
            Vector3 predictedPosition = position + velocity * timeOffset + 0.5f * Physics.gravity * timeOffset * timeOffset;
            
            trajectoryPoints[i].transform.position = predictedPosition;
            trajectoryPoints[i].SetActive(true);
            
            // Scale down the points as they get further along the trajectory
            float scale = 1.0f - ((float)i / trajectorySteps) * 0.5f;
            trajectoryPoints[i].transform.localScale = new Vector3(scale, scale, scale);
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
    
    private void HideTrajectory()
    {
        if (trajectoryPoints != null)
        {
            foreach (GameObject point in trajectoryPoints)
            {
                if (point != null)
                {
                    point.SetActive(false);
                }
            }
        }
    }

    public void ThrowChemicalBall()
    {
        // Check if we have ammo available
        if (ballsManager != null && ballsManager.currentBombs <= 0)
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
            
            // Decrease ammo count
            if (ballsManager != null)
            {
                ballsManager.DecreaseBomb();
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
