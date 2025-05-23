using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    // Camera
    public float MouseSensitivity = 100f;
    public Transform PlayerCamera;
    private float xRotation = 0f; // For camera pitch

    // Movement
    public float Speed = 12f;
    public float Gravity = -9.81f * 2; // Multiplied for a slightly stronger gravity
    public float JumpHeight = 3f;

    // RenderTexture Height Sampling
    public RenderTexture HeightRT;
    public Vector3 TerrainWorldSize = new Vector3(1000, 100, 1000); // X=Width, Y=MaxHeight, Z=Length
    // The world position of the terrain's corner corresponding to UV (0,0) on the heightRT.
    public Vector3 TerrainOrigin = Vector3.zero; 
    public Vector2Int HeightRTSolution = new Vector2Int(1024, 1024); // Resolution of heightRT

    private CharacterController characterController;
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
        if (PlayerCamera == null)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null) 
            {
                PlayerCamera = mainCamera.transform;
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
        characterController = GetComponent<CharacterController>();
        // Calculate the offset from the transform's pivot to the character's feet.
        playerFeetOffset = characterController.height * 0.5f - characterController.center.y;

        // RenderTexture Height Sampling Setup
        InitializeHeightSampling();
    }

    void InitializeHeightSampling() {
        if (HeightRT != null) {
            // If tempHeightTexture is null, or if we want to ensure it's a 1x1 texture
            if (tempHeightTexture == null || tempHeightTexture.width != 1 || tempHeightTexture.height != 1) {
                if (tempHeightTexture != null) Destroy(tempHeightTexture);
                // Create with a common, readable format. ReadPixels will handle conversion if possible.
                tempHeightTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                // Debug.Log($"PlayerController: tempHeightTexture recreated with RGBA32 format.");
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
        // Convert world position to UV coordinates relative to the TerrainOrigin and TerrainWorldSize.
        float u = (worldPosition.x - TerrainOrigin.x) / TerrainWorldSize.x;
        float v = (worldPosition.z - TerrainOrigin.z) / TerrainWorldSize.z;

        // Clamp UVs to [0, 1] range to prevent reading outside the texture boundaries.
        u = Mathf.Clamp01(u);
        v = Mathf.Clamp01(v);

        // Convert UV to pixel coordinates for the RenderTexture.
        // Subtract 1 because pixel coordinates are 0-indexed (0 to width-1 or height-1).
        // Mathf.Max ensures the index is not negative if resolution is very small.
        int texX = Mathf.FloorToInt(u * Mathf.Max(0, HeightRTSolution.x - 1));
        int texY = Mathf.FloorToInt(v * Mathf.Max(0, HeightRTSolution.y - 1));
        
        // --- Pixel Reading ---
        // Store the currently active RenderTexture to restore it later.
        RenderTexture prevActive = RenderTexture.active;
        // Set the target RenderTexture as active to read from it.
        RenderTexture.active = HeightRT;

        try {
            // ReadPixels is a synchronous operation and can have a performance impact,
            // especially with larger textures or frequent calls. For future optimization,
            // consider asynchronous readback if supported and necessary.
            tempHeightTexture.ReadPixels(new Rect(texX, texY, 1, 1), 0, 0);
            tempHeightTexture.Apply(); // Apply the read pixel data to tempHeightTexture.
        } catch (UnityException e) {
            Debug.LogError($"Error reading pixel from HeightRT: {e.Message}. Make sure format ({HeightRT.format}) is readable and RT is properly initialized.");
            RenderTexture.active = prevActive; // Crucial: Restore previous active RT in case of error.
            return worldPosition.y - playerFeetOffset; // Fallback on error to prevent incorrect height.
        }
        
        // Restore the previously active RenderTexture.
        RenderTexture.active = prevActive;
        
        // --- Height Interpretation ---
        // Get the color from our 1x1 temporary texture.
        Color heightColor = tempHeightTexture.GetPixel(0, 0);
        // Height is assumed to be stored in the R (red) channel, normalized (0-1).
        // Scale this normalized value by the terrain's maximum height (TerrainWorldSize.y).
        return heightColor.r * TerrainWorldSize.y;
    }

    void Update()
    {
        // --- Camera Look --- 
        float mouseX = Input.GetAxis("Mouse X") * MouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * MouseSensitivity * Time.deltaTime;
        transform.Rotate(Vector3.up * mouseX); 
        xRotation -= mouseY; 
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);
        if (PlayerCamera != null) PlayerCamera.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        // --- Initialize Height Sampling (Check if RT changed at runtime) ---
        bool currentRtAssigned = HeightRT != null;
        // If the assignment status of HeightRT has changed OR if it's assigned but our temp texture is somehow null
        if (currentRtAssigned != useSampledHeight || (currentRtAssigned && tempHeightTexture == null) )
        {
            InitializeHeightSampling();
        }
        
        // --- Player Movement ---
        float inputX = Input.GetAxis("Horizontal");
        float inputZ = Input.GetAxis("Vertical");
        Vector3 moveDirection = (transform.right * inputX + transform.forward * inputZ).normalized;

        if (useSampledHeight) {
            // 1. Apply horizontal movement
            characterController.Move(new Vector3(moveDirection.x * Speed * Time.deltaTime, 0, moveDirection.z * Speed * Time.deltaTime));

            // 2. Determine target height and apply vertical correction
            float targetTerrainHeight = GetTerrainHeightFromRT(transform.position);
            float currentFeetY = transform.position.y - playerFeetOffset;
            float heightError = targetTerrainHeight - currentFeetY;
            
            characterController.Move(new Vector3(0, heightError, 0)); // Apply vertical correction

            isGrounded = Mathf.Abs(heightError) < 0.1f; // Adjusted threshold

            if (isGrounded) {
                velocity.y = -2f; // Stick to ground
            } else {
                // Not grounded on RT (e.g. large height error, or fell off an edge)
                velocity.y += Gravity * Time.deltaTime;
            }
        } else { // Standard CharacterController movement
            // Apply horizontal movement
            characterController.Move(moveDirection * Speed * Time.deltaTime); 
            isGrounded = characterController.isGrounded;

            if (isGrounded && velocity.y < 0) {
                velocity.y = -2f; // Stick to ground
            }
            // Apply gravity if not grounded
            if (!isGrounded) { 
                velocity.y += Gravity * Time.deltaTime;
            }
        }

        // --- Jumping ---
        if (Input.GetButtonDown("Jump") && isGrounded) {
            velocity.y = Mathf.Sqrt(JumpHeight * -2f * Gravity);
            isGrounded = false; // Player is now airborne
        }

        // --- Apply Final Vertical Velocity (Gravity/Jump/Sticking) ---
        characterController.Move(velocity * Time.deltaTime);
    }

    void OnDestroy() {
        if (tempHeightTexture != null) Destroy(tempHeightTexture);
    }
}
