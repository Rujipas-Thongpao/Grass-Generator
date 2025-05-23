using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    // Camera
    public float mouseSensitivity = 100f;
    public Transform playerCamera;
    private float xRotation = 0f; // For camera pitch

    // Movement
    public float speed = 12f;
    public float gravity = -9.81f * 2; // Multiplied for a slightly stronger gravity
    public float jumpHeight = 3f;

    // RenderTexture Height Sampling
    public RenderTexture heightRT;
    public Vector3 terrainWorldSize = new Vector3(1000, 100, 1000); // X=Width, Y=MaxHeight, Z=Length
    // The world position of the terrain's corner corresponding to UV (0,0) on the heightRT.
    public Vector3 terrainOrigin = Vector3.zero; 
    public Vector2Int heightRTSolution = new Vector2Int(1024, 1024); // Resolution of heightRT

    private CharacterController controller;
    private Vector3 velocity; // For gravity and jumping
    private bool isGrounded; // True if on CharacterController ground or RT surface

    private Texture2D tempHeightTexture; // For reading pixels from heightRT
    private bool useSampledHeight = false; // Flag to indicate if we should use heightRT

    // Offset from the CharacterController's transform.position to its actual bottom (feet).
    // This is used to align the player's feet with the sampled terrain height.
    private float playerFeetOffset; 

    void Start()
    {
        // Camera Setup
        if (playerCamera == null)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null) 
            {
                playerCamera = mainCamera.transform;
            }
            else 
            {
                // Critical: No camera found. Disable script to prevent runtime errors.
                Debug.LogError("PlayerController: CRITICAL - Player camera not assigned and Main Camera not found! Disabling script.");
                this.enabled = false; // Disable the script
                return; // Stop further execution in Start()
            }
        }
        Cursor.lockState = CursorLockMode.Locked;

        // Character Controller
        controller = GetComponent<CharacterController>();
        // Calculate the offset from the transform's pivot to the character's feet.
        playerFeetOffset = controller.height * 0.5f - controller.center.y;

        // RenderTexture Height Sampling Setup
        InitializeHeightSampling();
    }

    void InitializeHeightSampling() {
        if (heightRT != null) {
            // Check if texture needs to be recreated (null, wrong size, or wrong format)
            if (tempHeightTexture == null || tempHeightTexture.width != 1 || tempHeightTexture.height != 1 || tempHeightTexture.format != heightRT.format) {
                if (tempHeightTexture != null) Destroy(tempHeightTexture);
                tempHeightTexture = new Texture2D(1, 1, heightRT.format, false);
                // Debug.Log($"PlayerController: tempHeightTexture recreated. Format: {heightRT.format}");
            }
            useSampledHeight = true;
        } else {
            if (tempHeightTexture != null) Destroy(tempHeightTexture);
            tempHeightTexture = null;
            useSampledHeight = false;
        }
    }

    float GetTerrainHeightFromRT(Vector3 worldPosition)
    {
        if (!useSampledHeight || tempHeightTexture == null) { 
            return worldPosition.y - playerFeetOffset; // Fallback
        }

        // --- UV Calculation ---
        // Convert world position to UV coordinates relative to the terrainOrigin and terrainWorldSize.
        float u = (worldPosition.x - terrainOrigin.x) / terrainWorldSize.x;
        float v = (worldPosition.z - terrainOrigin.z) / terrainWorldSize.z;

        // Clamp UVs to [0, 1] range to prevent reading outside the texture boundaries.
        u = Mathf.Clamp01(u);
        v = Mathf.Clamp01(v);

        // Convert UV to pixel coordinates for the RenderTexture.
        // Subtract 1 because pixel coordinates are 0-indexed (0 to width-1 or height-1).
        // Mathf.Max ensures the index is not negative if resolution is very small.
        int texX = Mathf.FloorToInt(u * Mathf.Max(0, heightRTSolution.x - 1));
        int texY = Mathf.FloorToInt(v * Mathf.Max(0, heightRTSolution.y - 1));
        
        // --- Pixel Reading ---
        // Store the currently active RenderTexture to restore it later.
        RenderTexture prevActive = RenderTexture.active;
        // Set the target RenderTexture as active to read from it.
        RenderTexture.active = heightRT;

        try {
            // ReadPixels is a synchronous operation and can have a performance impact,
            // especially with larger textures or frequent calls. For future optimization,
            // consider asynchronous readback if supported and necessary.
            tempHeightTexture.ReadPixels(new Rect(texX, texY, 1, 1), 0, 0);
            tempHeightTexture.Apply(); // Apply the read pixel data to tempHeightTexture.
        } catch (UnityException e) {
            Debug.LogError($"Error reading pixel from heightRT: {e.Message}. Make sure format ({heightRT.format}) is readable and RT is properly initialized.");
            RenderTexture.active = prevActive; // Crucial: Restore previous active RT in case of error.
            return worldPosition.y - playerFeetOffset; // Fallback on error to prevent incorrect height.
        }
        
        // Restore the previously active RenderTexture.
        RenderTexture.active = prevActive;
        
        // --- Height Interpretation ---
        // Get the color from our 1x1 temporary texture.
        Color heightColor = tempHeightTexture.GetPixel(0, 0);
        // Height is assumed to be stored in the R (red) channel, normalized (0-1).
        // Scale this normalized value by the terrain's maximum height (terrainWorldSize.y).
        return heightColor.r * terrainWorldSize.y;
    }

    void Update()
    {
        // --- Camera Look --- 
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;
        transform.Rotate(Vector3.up * mouseX); 
        xRotation -= mouseY; 
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);
        if (playerCamera != null) playerCamera.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        // --- Initialize Height Sampling (Check if RT changed at runtime) ---
        bool currentRtAssigned = heightRT != null;
        if (currentRtAssigned != useSampledHeight || (currentRtAssigned && (tempHeightTexture == null || tempHeightTexture.format != heightRT.format)))
        {
            InitializeHeightSampling();
        }
        
        // --- Player Movement ---
        float inputX = Input.GetAxis("Horizontal"); 
        float inputZ = Input.GetAxis("Vertical"); 
        Vector3 moveDirection = (transform.right * inputX + transform.forward * inputZ).normalized; 

        // If useSampledHeight is true, player height is adjusted based on the RenderTexture.
        // Otherwise, standard CharacterController gravity and grounding are used.
        if (useSampledHeight) {
            // Sample terrain height at the player's current XZ position.
            float targetTerrainHeight = GetTerrainHeightFromRT(transform.position);
            // Calculate current Y position of the player's feet.
            float currentFeetY = transform.position.y - playerFeetOffset;
            // Determine the vertical error to correct player's height.
            float heightError = targetTerrainHeight - currentFeetY;
            
            // Construct the combined movement vector.
            // Horizontal movement is based on input, scaled by speed and deltaTime.
            // Vertical movement directly applies the calculated heightError to snap to terrain.
            Vector3 combinedMove = new Vector3(moveDirection.x * speed * Time.deltaTime, heightError, moveDirection.z * speed * Time.deltaTime);
            controller.Move(combinedMove);

            // Determine if grounded based on proximity to the target terrain height.
            isGrounded = Mathf.Abs(heightError) < 0.05f; 
            if (isGrounded && velocity.y < 0) {
                velocity.y = -2f; // Reset vertical velocity to stick to ground.
            }
        } else {
            // Standard CharacterController movement without RT height adjustment.
            controller.Move(moveDirection * speed * Time.deltaTime); // Apply horizontal movement.
            isGrounded = controller.isGrounded; // Use CharacterController's built-in ground check.
            if (isGrounded && velocity.y < 0) {
                velocity.y = -2f; // Reset vertical velocity if grounded.
            }
        }

        // --- Jumping ---
        if(Input.GetButtonDown("Jump") && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            isGrounded = false; // Player is no longer grounded after initiating a jump.
        }
        
        // --- Apply Gravity ---
        // Apply gravity if:
        // 1. The player is not grounded (applies to both standard and RT sampled movement).
        // 2. Or, if not using sampled height (ensuring standard CC gravity works as expected).
        if (!isGrounded || !useSampledHeight) { 
            velocity.y += gravity * Time.deltaTime;
        } else if (useSampledHeight && isGrounded) {
            // If using sampled height and grounded, ensure velocity.y is at a 'stick to ground' value.
            // This prevents accumulation of downward velocity if heightError was minimal but not zero.
            velocity.y = Mathf.Max(velocity.y, -2f); 
        }
        
        // Apply any accumulated vertical velocity (from gravity or jumping).
        controller.Move(velocity * Time.deltaTime); 
    }

    void OnDestroy() {
        if (tempHeightTexture != null) Destroy(tempHeightTexture);
    }
}
