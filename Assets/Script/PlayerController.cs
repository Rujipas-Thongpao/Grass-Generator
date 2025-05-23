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
    public Vector2Int heightRTSolution = new Vector2Int(1024, 1024); // Resolution of heightRT

    private CharacterController controller;
    private Vector3 velocity; // For gravity and jumping
    private bool isGrounded; // True if on CharacterController ground or RT surface

    private Texture2D tempHeightTexture; // For reading pixels from heightRT
    private bool useSampledHeight = false; // Flag to indicate if we should use heightRT

    // Offset from the controller's transform.position to its actual bottom (feet)
    private float playerFeetOffset; 

    void Start()
    {
        // Camera Setup
        if (playerCamera == null)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null) playerCamera = mainCamera.transform;
            else Debug.LogError("PlayerController: Player camera not assigned and Main Camera not found!");
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
        if (!useSampledHeight || tempHeightTexture == null) { // tempHeightTexture null check is important
             // Debug.LogWarning("GetTerrainHeightFromRT: Sampling not possible. Returning current feet Y.");
            return worldPosition.y - playerFeetOffset; // Fallback
        }

        float u = worldPosition.x / terrainWorldSize.x;
        float v = worldPosition.z / terrainWorldSize.z;
        u = Mathf.Clamp01(u);
        v = Mathf.Clamp01(v);

        int texX = Mathf.FloorToInt(u * Mathf.Max(0, heightRTSolution.x - 1));
        int texY = Mathf.FloorToInt(v * Mathf.Max(0, heightRTSolution.y - 1));
        
        RenderTexture prevActive = RenderTexture.active;
        RenderTexture.active = heightRT;

        try {
            tempHeightTexture.ReadPixels(new Rect(texX, texY, 1, 1), 0, 0);
            tempHeightTexture.Apply();
        } catch (UnityException e) {
            Debug.LogError($"Error reading pixel from heightRT: {e.Message}. Make sure format ({heightRT.format}) is readable and RT is properly initialized.");
            RenderTexture.active = prevActive;
            return worldPosition.y - playerFeetOffset; // Fallback on error
        }
        
        RenderTexture.active = prevActive;
        Color heightColor = tempHeightTexture.GetPixel(0, 0);
        return heightColor.r * terrainWorldSize.y;
    }

    void Update()
    {
        // --- Camera Look --- (Existing logic)
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;
        transform.Rotate(Vector3.up * mouseX); 
        xRotation -= mouseY; 
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);
        if (playerCamera != null) playerCamera.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        // --- Initialize Height Sampling (Check if RT changed) ---
        // This logic was slightly different in the instructions, ensuring it's called if heightRT assignment changes
        bool currentRtAssigned = heightRT != null;
        if (currentRtAssigned != useSampledHeight || (currentRtAssigned && (tempHeightTexture == null || tempHeightTexture.format != heightRT.format)))
        {
            InitializeHeightSampling();
        }
        
        // --- Player Movement ---
        float inputX = Input.GetAxis("Horizontal"); // Renamed from 'x' to 'inputX' for clarity
        float inputZ = Input.GetAxis("Vertical"); // Renamed from 'z' to 'inputZ' for clarity
        Vector3 moveDirection = (transform.right * inputX + transform.forward * inputZ).normalized; // Use normalized for consistent speed

        if (useSampledHeight) {
            float targetTerrainHeight = GetTerrainHeightFromRT(transform.position);
            float currentFeetY = transform.position.y - playerFeetOffset;
            float heightError = targetTerrainHeight - currentFeetY;
            
            // Construct move vector: horizontal part from input, vertical part is heightError
            Vector3 combinedMove = new Vector3(moveDirection.x * speed * Time.deltaTime, heightError, moveDirection.z * speed * Time.deltaTime);
            controller.Move(combinedMove);

            isGrounded = Mathf.Abs(heightError) < 0.05f; 
            if (isGrounded && velocity.y < 0) {
                velocity.y = -2f; 
            }
        } else {
            controller.Move(moveDirection * speed * Time.deltaTime); // Apply horizontal movement
            isGrounded = controller.isGrounded; // Use CharacterController's ground check
            if (isGrounded && velocity.y < 0) {
                velocity.y = -2f;
            }
        }

        // --- Jumping ---
        if(Input.GetButtonDown("Jump") && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            isGrounded = false; // Ensure not grounded immediately after jump
        }
        
        // --- Apply Gravity ---
        // The condition "!isGrounded || !useSampledHeight" means:
        // - If NOT grounded (standard or RT), apply gravity.
        // - OR, if NOT using sampled height (meaning standard physics path), apply gravity (which is normal for standard CC).
        // This logic is slightly different from previous, ensuring gravity applies if jumping off RT surface.
        if (!isGrounded || !useSampledHeight) { 
            velocity.y += gravity * Time.deltaTime;
        } else if (useSampledHeight && isGrounded) {
            // If on RT ground, ensure velocity.y doesn't accumulate excessively downwards.
            // -2f helps stick, but don't let it get much lower if somehow it does.
            velocity.y = Mathf.Max(velocity.y, -2f); 
        }
        // If !useSampledHeight, gravity is handled by the standard path's velocity accumulation.
        // If useSampledHeight and isGrounded, velocity.y is already managed (-2f).
        // If useSampledHeight and NOT isGrounded (e.g. jumped or fell off RT edge), gravity is added above.

        controller.Move(velocity * Time.deltaTime); // Apply accumulated vertical velocity (gravity/jump)
    }

    void OnDestroy() {
        if (tempHeightTexture != null) Destroy(tempHeightTexture);
    }
}
