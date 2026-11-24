using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class FPSController : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 3f;
    public float sprintSpeed = 5.5f;
    public float crouchSpeed = 1.5f;
    public float gravity = -20f;
    public float jumpHeight = 1.2f;

    [Header("Mouse Look")]
    public Transform playerCamera;
    public float mouseSensitivity = 2f;
    public float maxLookAngle = 80f;

    [Header("Crouch")]
    public float standingHeight = 2f;
    public float crouchHeight = 1f;
    public float crouchTransitionSpeed = 8f;

    [Header("Stamina")]
    public float maxStamina = 5f;
    public float staminaDrain = 1f;
    public float staminaRecovery = 0.8f;

    [Header("Capture Settings")]
    public float captureCameraSpeed = 5f;

    [Header("Camera Shake")]
    public float shakeDuration = 2f;
    public float shakeMagnitude = 0.08f;
    public float shakeSpeed = 25f;
    private float shakeTimer;
    private Vector3 originalCameraPosition;
    private bool isCameraShaking;

    [Header("Debug Console")]
    public bool enableDebug = true;
    public float debugInterval = 0.5f;

    private CharacterController controller;
    private Vector3 velocity;

    private float xRotation = 0f;
    private float currentStamina;

    private float inputX;
    private float inputZ;
    private float currentSpeed;
    private float nextDebugTime;

    private bool isGrounded;
    private bool isCrouching;
    private bool isSprinting;
    private bool isMoving;
    private bool isJumping;

    private bool isCaught;
    private Transform capturingEnemy;

    private string lastDebugStatus = "";

    void Start()
    {
        controller = GetComponent<CharacterController>();

        if (playerCamera != null)
        {
            originalCameraPosition = playerCamera.localPosition;
        }

        currentStamina = maxStamina;

        controller.height = standingHeight;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Debug.Log("========== FPS PLAYER INITIALIZED ==========");
        Debug.Log("Walk Speed: " + walkSpeed + " m/s");
        Debug.Log("Sprint Speed: " + sprintSpeed + " m/s");
        Debug.Log("Crouch Speed: " + crouchSpeed + " m/s");
        Debug.Log("Max Stamina: " + maxStamina);
        Debug.Log("Mouse Sensitivity: " + mouseSensitivity);
        Debug.Log("=============================================");
    }

    void Update()
    {
        // Jika Player tertangkap, hanya jalankan kamera.
        if (isCaught)
        {
            HandleCaptureCamera();
            HandleCameraShake();
            return;
        }

        HandleMouseLook();
        ReadMovementInput();
        HandleMovement();
        HandleCrouch();
        HandleStamina();
        HandleDebug();
    }

    // Dipanggil oleh EnemyAI ketika menangkap Player.
    public void CaptureByEnemy(Transform enemy)
    {
        if (isCaught || enemy == null)
            return;

        isCaught = true;
        capturingEnemy = enemy;

        velocity = Vector3.zero;

        inputX = 0f;
        inputZ = 0f;

        currentSpeed = 0f;
        isMoving = false;
        isSprinting = false;
        isJumping = false;

        isCrouching = false;
        controller.height = standingHeight;

        Debug.Log("[FPS] PLAYER CAUGHT!");

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void TriggerCameraShake()
    {
        if (playerCamera == null)
            return;

        shakeTimer = shakeDuration;
        isCameraShaking = true;

        Debug.Log("[FPS] CAMERA SHAKE TRIGGERED!");
    }

    void HandleCameraShake()
    {
        if (playerCamera == null || !isCameraShaking)
            return;

        if (shakeTimer > 0f)
        {
            shakeTimer -= Time.deltaTime;

            float shakeX = Random.Range(-1f, 1f) * shakeMagnitude;
            float shakeY = Random.Range(-1f, 1f) * shakeMagnitude;

            Vector3 shakeOffset = new Vector3(
                shakeX,
                shakeY,
                0f
            );

            playerCamera.localPosition =
                originalCameraPosition + shakeOffset;
        }
        else
        {
            playerCamera.localPosition = originalCameraPosition;
            isCameraShaking = false;
        }
    }

    void HandleCaptureCamera()
    {
        if (capturingEnemy == null || playerCamera == null)
            return;

        // Arah dari kamera menuju Enemy.
        Vector3 direction =
            capturingEnemy.position - playerCamera.position;

        // Putar badan Player secara horizontal.
        Vector3 horizontalDirection = direction;
        horizontalDirection.y = 0f;

        if (horizontalDirection.sqrMagnitude > 0.001f)
        {
            Quaternion bodyRotation =
                Quaternion.LookRotation(horizontalDirection);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                bodyRotation,
                captureCameraSpeed * Time.deltaTime
            );
        }

        // Hitung sudut vertikal kamera berdasarkan posisi Enemy.
        Vector3 directionFromPlayer =
            capturingEnemy.position - playerCamera.position;

        float targetXRotation = -Mathf.Atan2(
            directionFromPlayer.y,
            new Vector2(
                directionFromPlayer.x,
                directionFromPlayer.z
            ).magnitude
        ) * Mathf.Rad2Deg;

        targetXRotation = Mathf.Clamp(
            targetXRotation,
            -maxLookAngle,
            maxLookAngle
        );

        xRotation = Mathf.Lerp(
            xRotation,
            targetXRotation,
            captureCameraSpeed * Time.deltaTime
        );

        playerCamera.localRotation =
            Quaternion.Euler(xRotation, 0f, 0f);
    }

    void HandleMouseLook()
    {
        if (playerCamera == null)
        {
            Debug.LogError("FPSController: Player Camera belum diisi!");
            return;
        }

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -maxLookAngle, maxLookAngle);

        playerCamera.localRotation =
            Quaternion.Euler(xRotation, 0f, 0f);

        transform.Rotate(Vector3.up * mouseX);
    }

    void ReadMovementInput()
    {
        inputX = 0f;
        inputZ = 0f;

        if (Input.GetKey(KeyCode.A)) inputX -= 1f;
        if (Input.GetKey(KeyCode.D)) inputX += 1f;
        if (Input.GetKey(KeyCode.W)) inputZ += 1f;
        if (Input.GetKey(KeyCode.S)) inputZ -= 1f;

        isMoving = inputX != 0 || inputZ != 0;
    }

    void HandleMovement()
    {
        isGrounded = controller.isGrounded;

        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
            isJumping = false;
        }

        Vector3 move = transform.right * inputX +
                       transform.forward * inputZ;

        move = Vector3.ClampMagnitude(move, 1f);

        isSprinting =
            Input.GetKey(KeyCode.LeftShift) &&
            inputZ > 0 &&
            !isCrouching &&
            currentStamina > 0.01f &&
            isMoving;

        if (isCrouching)
        {
            currentSpeed = crouchSpeed;
        }
        else if (isSprinting)
        {
            currentSpeed = sprintSpeed;
        }
        else
        {
            currentSpeed = walkSpeed;
        }

        if (!isMoving)
        {
            currentSpeed = 0f;
        }

        controller.Move(move * currentSpeed * Time.deltaTime);

        if (Input.GetKeyDown(KeyCode.Space) &&
            isGrounded &&
            !isCrouching)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            isJumping = true;

            Debug.Log("[FPS] JUMP ACTIVATED");
        }

        velocity.y += gravity * Time.deltaTime;

        controller.Move(velocity * Time.deltaTime);
    }

    void HandleCrouch()
    {
        bool previousCrouch = isCrouching;

        isCrouching = Input.GetKey(KeyCode.LeftControl);

        float targetHeight = isCrouching
            ? crouchHeight
            : standingHeight;

        controller.height = Mathf.Lerp(
            controller.height,
            targetHeight,
            crouchTransitionSpeed * Time.deltaTime
        );

        if (previousCrouch != isCrouching)
        {
            Debug.Log(isCrouching
                ? "[FPS] CROUCH ACTIVATED"
                : "[FPS] CROUCH DEACTIVATED");
        }
    }

    void HandleStamina()
    {
        if (isSprinting)
        {
            currentStamina -= staminaDrain * Time.deltaTime;

            if (currentStamina <= 0f)
            {
                currentStamina = 0f;
                isSprinting = false;

                Debug.Log("[FPS] STAMINA EMPTY - SPRINT DISABLED");
            }
        }
        else
        {
            currentStamina += staminaRecovery * Time.deltaTime;
        }

        currentStamina = Mathf.Clamp(
            currentStamina,
            0f,
            maxStamina
        );
    }

    void HandleDebug()
    {
        if (!enableDebug)
            return;

        if (Time.time < nextDebugTime)
            return;

        nextDebugTime = Time.time + debugInterval;

        string movementState = "IDLE";

        if (isCrouching)
            movementState = "CROUCHING";
        else if (isSprinting)
            movementState = "SPRINTING";
        else if (isMoving)
            movementState = "WALKING";

        string status =
            $"State: {movementState} | " +
            $"Speed: {currentSpeed:F2} m/s | " +
            $"Stamina: {currentStamina:F2}/{maxStamina:F2} | " +
            $"Grounded: {isGrounded} | " +
            $"Jumping: {isJumping}";

        if (status != lastDebugStatus)
        {
            Debug.Log("[FPS DEBUG] " + status);
            lastDebugStatus = status;
        }
    }

    void OnDisable()
    {
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
