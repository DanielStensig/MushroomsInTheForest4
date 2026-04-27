using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class FirstPersonController : MonoBehaviour
{
    private Rigidbody rb;
    private CapsuleCollider capsule;

    [Header("Animator")]
    public Animator animator;
    public bool autoFindAnimator = true;
    public float animatorSpeedDamp = 10f;
    private float currentAnimSpeed;

    [Header("Camera")]
    public Camera playerCamera;
    public float fov = 60f;
    public bool invertCamera = false;
    public bool cameraCanMove = true;
    public float mouseSensitivity = 2f;
    public float maxLookAngle = 80f;

    [Header("Cursor / Crosshair")]
    public bool lockCursor = true;
    public bool crosshair = true;
    public Sprite crosshairImage;
    public Color crosshairColor = Color.white;
    private Image crosshairObject;

    [Header("Zoom")]
    public bool enableZoom = true;
    public bool holdToZoom = false;
    public KeyCode zoomKey = KeyCode.Mouse1;
    public float zoomFOV = 30f;
    public float zoomStepTime = 10f;
    private bool isZoomed = false;

    [Header("Movement")]
    public bool playerCanMove = true;
    public float walkSpeed = 5f;
    public float maxVelocityChange = 10f;
    private bool isWalking = false;

    [Header("Sprint")]
    public bool enableSprint = true;
    public bool unlimitedSprint = false;
    public KeyCode sprintKey = KeyCode.LeftShift;
    public float sprintSpeed = 7f;
    public float sprintDuration = 5f;
    public float sprintCooldown = 0.5f;
    public float sprintFOV = 80f;
    public float sprintFOVStepTime = 10f;

    private bool isSprinting = false;
    private float sprintRemaining;
    private bool isSprintCooldown = false;
    private float sprintCooldownReset;
    private float sprintCooldownTimer = 0f;

    [Header("Jump")]
    public bool enableJump = true;
    public KeyCode jumpKey = KeyCode.Space;
    public float jumpPower = 5f;
    public float groundCheckExtraDistance = 0.12f;
    public float coyoteTime = 0.12f;
    public float jumpBufferTime = 0.12f;
    public LayerMask groundMask = ~0;

    private bool isGrounded = false;
    private float lastGroundedTime = -999f;
    private float lastJumpPressedTime = -999f;

    [Header("Head Bob")]
    public bool enableHeadBob = true;
    public Transform joint;
    public float bobSpeed = 10f;
    public Vector3 bobAmount = new Vector3(0.15f, 0.05f, 0f);
    private Vector3 jointOriginalPos;
    private float timer = 0f;

    private float yaw = 0f;
    private float pitch = 0f;

    private float inputX;
    private float inputZ;
    private bool sprintHeld;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        crosshairObject = GetComponentInChildren<Image>();

        if (autoFindAnimator && animator == null)
            animator = GetComponentInChildren<Animator>();

        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        if (playerCamera != null)
            playerCamera.fieldOfView = fov;

        if (joint != null)
            jointOriginalPos = joint.localPosition;

        sprintRemaining = sprintDuration;
        sprintCooldownReset = sprintCooldown;
        sprintCooldownTimer = sprintCooldownReset;
    }

    private void Start()
    {
        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (crosshairObject != null)
        {
            if (crosshair)
            {
                crosshairObject.sprite = crosshairImage;
                crosshairObject.color = crosshairColor;
                crosshairObject.gameObject.SetActive(true);
            }
            else
            {
                crosshairObject.gameObject.SetActive(false);
            }
        }

        if (animator != null)
            animator.applyRootMotion = false;
    }

    private void Update()
    {
        HandleMouseLook();
        HandleInput();
        HandleZoom();
        CheckGround();
        HandleSprintState();

        if (enableHeadBob)
            HeadBob();

        UpdateAnimator();
    }

    private void FixedUpdate()
    {
        if (!playerCanMove)
            return;

        ApplyMovement();
        TryJump();
    }

    private void HandleInput()
    {
        inputX = Input.GetAxisRaw("Horizontal");
        inputZ = Input.GetAxisRaw("Vertical");
        sprintHeld = Input.GetKey(sprintKey);

        if (enableJump && Input.GetKeyDown(jumpKey))
            lastJumpPressedTime = Time.time;
    }

    private void HandleMouseLook()
    {
        if (!cameraCanMove || playerCamera == null)
            return;

        yaw += Input.GetAxis("Mouse X") * mouseSensitivity;

        if (!invertCamera)
            pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
        else
            pitch += Input.GetAxis("Mouse Y") * mouseSensitivity;

        pitch = Mathf.Clamp(pitch, -maxLookAngle, maxLookAngle);

        // Mouse controls the body yaw, so movement follows mouse direction.
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void HandleZoom()
    {
        if (!enableZoom || playerCamera == null)
            return;

        if (Input.GetKeyDown(zoomKey) && !holdToZoom && !isSprinting)
            isZoomed = !isZoomed;

        if (holdToZoom && !isSprinting)
        {
            if (Input.GetKeyDown(zoomKey))
                isZoomed = true;
            else if (Input.GetKeyUp(zoomKey))
                isZoomed = false;
        }

        if (isZoomed)
            playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, zoomFOV, zoomStepTime * Time.deltaTime);
        else if (!isSprinting)
            playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, fov, zoomStepTime * Time.deltaTime);
    }

    private void HandleSprintState()
    {
        if (!enableSprint)
        {
            isSprinting = false;
            return;
        }

        bool wantsToSprint = sprintHeld && inputZ > 0.1f && !isSprintCooldown;

        if (wantsToSprint && (unlimitedSprint || sprintRemaining > 0f))
        {
            isSprinting = true;

            if (!unlimitedSprint)
            {
                sprintRemaining -= Time.deltaTime;
                if (sprintRemaining <= 0f)
                {
                    sprintRemaining = 0f;
                    isSprinting = false;
                    isSprintCooldown = true;
                    sprintCooldownTimer = sprintCooldownReset;
                }
            }
        }
        else
        {
            isSprinting = false;

            if (!unlimitedSprint)
                sprintRemaining = Mathf.Clamp(sprintRemaining + Time.deltaTime, 0f, sprintDuration);
        }

        if (isSprintCooldown)
        {
            sprintCooldownTimer -= Time.deltaTime;
            if (sprintCooldownTimer <= 0f)
            {
                sprintCooldownTimer = sprintCooldownReset;
                isSprintCooldown = false;
            }
        }
    }

    private void ApplyMovement()
    {
        Vector3 input = new Vector3(inputX, 0f, inputZ).normalized;

        // Move relative to camera look direction instead of world / body axes
        Vector3 forward = playerCamera.transform.forward;
        Vector3 right = playerCamera.transform.right;

        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        Vector3 moveDirection = forward * input.z + right * input.x;
        Vector3 targetVelocity = moveDirection;
        float speed = isSprinting ? sprintSpeed : walkSpeed;
        targetVelocity *= speed;

        Vector3 velocity = rb.linearVelocity;
        Vector3 horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);
        Vector3 velocityChange = targetVelocity - horizontalVelocity;

        velocityChange.x = Mathf.Clamp(velocityChange.x, -maxVelocityChange, maxVelocityChange);
        velocityChange.z = Mathf.Clamp(velocityChange.z, -maxVelocityChange, maxVelocityChange);
        velocityChange.y = 0f;

        rb.AddForce(velocityChange, ForceMode.VelocityChange);

        isWalking = input.magnitude > 0.1f && isGrounded;
    }

    private void CheckGround()
    {
        Vector3 centerWorld = transform.TransformPoint(capsule.center);
        float radius = Mathf.Max(0.01f, capsule.radius * 0.95f);
        float sphereStart = centerWorld.y - (capsule.height * 0.5f) + radius + 0.02f;
        Vector3 origin = new Vector3(centerWorld.x, sphereStart, centerWorld.z);
        float castDistance = groundCheckExtraDistance;

        isGrounded = Physics.SphereCast(
            origin,
            radius,
            Vector3.down,
            out _,
            castDistance,
            groundMask,
            QueryTriggerInteraction.Ignore
        );

        if (isGrounded)
            lastGroundedTime = Time.time;

        Debug.DrawRay(origin, Vector3.down * castDistance, isGrounded ? Color.green : Color.red);
    }

    private void TryJump()
    {
        if (!enableJump)
            return;

        bool bufferedJump = Time.time - lastJumpPressedTime <= jumpBufferTime;
        bool coyoteJump = Time.time - lastGroundedTime <= coyoteTime;

        if (!bufferedJump || !coyoteJump)
            return;

        Vector3 velocity = rb.linearVelocity;
        velocity.y = 0f;
        rb.linearVelocity = velocity;

        rb.AddForce(Vector3.up * jumpPower, ForceMode.Impulse);

        isGrounded = false;
        lastGroundedTime = -999f;
        lastJumpPressedTime = -999f;

        if (animator != null)
            animator.SetTrigger("Jump");
    }

    private void UpdateAnimator()
    {
        if (animator == null)
            return;

        float horizontalSpeed = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z).magnitude;
        currentAnimSpeed = Mathf.Lerp(currentAnimSpeed, horizontalSpeed, animatorSpeedDamp * Time.deltaTime);

        animator.SetFloat("Speed", currentAnimSpeed);
        animator.SetBool("IsGrounded", isGrounded);
        animator.SetBool("IsSprinting", isSprinting);
    }

    private void HeadBob()
    {
        if (joint == null)
            return;

        if (isWalking)
        {
            float currentBobSpeed = bobSpeed;
            if (isSprinting)
                currentBobSpeed += sprintSpeed;

            timer += Time.deltaTime * currentBobSpeed;

            Vector3 targetPos = new Vector3(
                jointOriginalPos.x + Mathf.Sin(timer) * bobAmount.x,
                jointOriginalPos.y + Mathf.Sin(timer) * bobAmount.y,
                jointOriginalPos.z + Mathf.Sin(timer) * bobAmount.z
            );

            joint.localPosition = targetPos;
        }
        else
        {
            timer = 0f;
            joint.localPosition = Vector3.Lerp(joint.localPosition, jointOriginalPos, Time.deltaTime * bobSpeed);
        }
    }
}