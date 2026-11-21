using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(CharacterController))]
public class CharacterAnimationInput : MonoBehaviour
{
    [Header("Input Actions")]
    [SerializeField] InputActionReference moveAction;
    [SerializeField] InputActionReference sprintAction;
    [SerializeField] InputActionReference jumpAction;
    [SerializeField] InputActionReference crouchAction;

    [Header("Camera")]
    [Tooltip("Leave empty to use the Main Camera automatically.")]
    [SerializeField] Transform cameraTransform;
    [SerializeField] float turnSpeed = 720f; // degrees per second

    [Header("Body Rotation (adjust these!)")]
    [Tooltip("Extra turn when NOT aiming. Usually 0.")]
    [Range(-90f, 90f)][SerializeField] float idleYawOffset = 0f;
    [Tooltip("Extra turn while holding RIGHT CLICK and standing still. Positive = right, negative = left.")]
    [Range(-90f, 90f)][SerializeField] float aimStandingYawOffset = 15f;
    [Tooltip("Extra turn while holding RIGHT CLICK and walking.")]
    [Range(-90f, 90f)][SerializeField] float aimWalkYawOffset = 30f;
    [Tooltip("Extra turn while holding RIGHT CLICK and sprinting.")]
    [Range(-90f, 90f)][SerializeField] float aimSprintYawOffset = 30f;
    [Tooltip("How quickly he turns into the offset.")]
    [SerializeField] float offsetSmooth = 12f;

    [Header("Roll / Dodge")]
    [Tooltip("Press this key to roll in the direction you're moving (N / S / E / W). No input = roll forward.")]
    [SerializeField] Key rollKey = Key.V;
    [Tooltip("How long the roll lasts. Match this to your roll animation's length.")]
    [SerializeField] float rollDuration = 0.6f;
    [Tooltip("How fast he moves while rolling (units per second).")]
    [SerializeField] float rollSpeed = 7f;
    [Tooltip("Shapes the roll speed over time: left = start of roll, right = end.")]
    [SerializeField] AnimationCurve rollSpeedCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0.4f);
    [Tooltip("Seconds at the start of the roll where he can't be hurt.")]
    [SerializeField] float rollInvulnerableTime = 0.35f;
    [Tooltip("Seconds before he can roll again.")]
    [SerializeField] float rollCooldown = 0.6f;

    [Header("Roll Rotation (adjust these!)")]
    [Tooltip("Turns his body during each roll WITHOUT changing the direction he travels. Positive = turn right, negative = turn left. Range is -180 to 180.")]
    [Range(-180f, 180f)][SerializeField] float rollNorthYawOffset = 0f;
    [Range(-180f, 180f)][SerializeField] float rollSouthYawOffset = 0f;
    [Range(-180f, 180f)][SerializeField] float rollEastYawOffset = 0f;
    [Range(-180f, 180f)][SerializeField] float rollWestYawOffset = 0f;

    [Header("Animation")]
    [SerializeField] float dampTime = 0.1f;

    [Header("Movement Speeds (units per second)")]
    [SerializeField] float walkSpeed = 2f;
    [SerializeField] float runSpeed = 5f;
    [SerializeField] float crouchSpeed = 1.2f;

    [Header("Jump & Gravity")]
    [SerializeField] float jumpHeight = 1.2f;     // meters
    [SerializeField] float gravity = -20f;        // more negative = snappier fall
    [SerializeField] float coyoteTime = 0.1f;     // grace period after leaving a ledge
    [SerializeField] float jumpBufferTime = 0.1f; // grace period if jump pressed just before landing

    [Header("Crouch Collider")]
    [SerializeField] float standHeight = 1.9f;
    [Tooltip("Very short hitbox for crouching. Unity won't allow less than 2x the radius.")]
    [SerializeField] float crouchHeight = 0.7f;
    [SerializeField] float crouchTransitionSpeed = 15f;
    [Tooltip("Smaller skin width = less invisible padding around the capsule.")]
    [SerializeField] float skinWidth = 0.02f;

    [Header("Effects")]
    [Tooltip("Particle System at his feet. Plays only while running on the ground.")]
    [SerializeField] ParticleSystem runDust;
    [SerializeField] float dustRateOverTime = 20f;

    [Header("Audio")]
    [Tooltip("Looping footstep sound while walking.")]
    [SerializeField] AudioClip walkClip;
    [Tooltip("Looping footstep sound while running.")]
    [SerializeField] AudioClip runClip;
    [Tooltip("Played once when he jumps.")]
    [SerializeField] AudioClip jumpClip;
    [SerializeField, Range(0f, 1f)] float walkVolume = 0.5f;
    [SerializeField, Range(0f, 1f)] float runVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] float jumpVolume = 1f;

    public bool IsRolling => isRolling;

    Animator animator;
    CharacterController controller;
    PlayerHealth health;        // optional: used for the roll's invulnerability
    AudioSource footstepSource; // loops walk/run
    AudioSource sfxSource;      // one-shots (jump)
    readonly HashSet<string> animParamNames = new HashSet<string>();

    float verticalVelocity;
    float lastGroundedTime;
    float lastJumpPressedTime = -10f;
    float currentYawOffset;

    // Roll state
    bool isRolling;
    float rollTimer;
    float nextRollTime;
    Vector3 rollDirWorld;

    static readonly int MoveX = Animator.StringToHash("MoveX");
    static readonly int MoveY = Animator.StringToHash("MoveY");
    static readonly int JumpTrigger = Animator.StringToHash("Jump");
    static readonly int IsCrouching = Animator.StringToHash("IsCrouching");
    const string RollTriggerName = "Roll"; // Trigger
    const string RollDirName = "RollDir";  // Int: 0 = N, 1 = S, 2 = E, 3 = W

    void Awake()
    {
        animator = GetComponent<Animator>();
        controller = GetComponent<CharacterController>();
        health = GetComponent<PlayerHealth>();
        if (health == null) health = FindFirstObjectByType<PlayerHealth>();
        animator.applyRootMotion = false; // the script moves the character instead

        foreach (var p in animator.parameters) animParamNames.Add(p.name);

        controller.skinWidth = skinWidth;
        controller.radius = 0.3f;
        controller.height = standHeight;
        controller.center = new Vector3(0f, standHeight * 0.5f, 0f);

        // Audio sources are created automatically, nothing to add by hand
        footstepSource = gameObject.AddComponent<AudioSource>();
        footstepSource.loop = true;
        footstepSource.playOnAwake = false;
        footstepSource.spatialBlend = 0f; // 2D sound

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.loop = false;
        sfxSource.playOnAwake = false;
        sfxSource.spatialBlend = 0f;

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void OnEnable()
    {
        moveAction.action.Enable();
        sprintAction.action.Enable();
        jumpAction.action.Enable();
        crouchAction.action.Enable();
    }

    void OnDisable()
    {
        moveAction.action.Disable();
        sprintAction.action.Disable();
        jumpAction.action.Disable();
        crouchAction.action.Disable();

        // Never leave the player stuck invulnerable if this script is switched off mid-roll
        if (isRolling)
        {
            isRolling = false;
            if (health != null) health.Invulnerable = false;
        }
    }

    void Update()
    {
        if (isRolling)
        {
            UpdateRoll();
            return;
        }

        Vector2 input = moveAction.action.ReadValue<Vector2>();
        bool moving = input.sqrMagnitude > 0.01f;
        bool crouching = crouchAction.action.IsPressed();

        // Stay crouched if there's something above his head
        if (!crouching && controller.height < standHeight - 0.01f && !CanStandUp())
            crouching = true;

        // Smoothly resize the controller, keeping the bottom of the capsule at his feet
        float targetHeight = crouching ? crouchHeight : standHeight;
        controller.height = Mathf.Lerp(controller.height, targetHeight, crouchTransitionSpeed * Time.deltaTime);
        controller.center = new Vector3(0f, controller.height * 0.5f, 0f);

        bool sprinting = sprintAction.action.IsPressed() && !crouching;

        // --- Roll ---
        if (RollPressed() && Time.time >= nextRollTime && controller.isGrounded && !crouching)
        {
            StartRoll(input);
            return;
        }

        // --- Body rotation: camera direction + an extra offset while aiming ---
        bool aiming = Mouse.current != null && Mouse.current.rightButton.isPressed;
        float targetOffset;
        if (!aiming) targetOffset = idleYawOffset;
        else if (!moving) targetOffset = aimStandingYawOffset;
        else if (sprinting) targetOffset = aimSprintYawOffset;
        else targetOffset = aimWalkYawOffset;

        currentYawOffset = Mathf.Lerp(currentYawOffset, targetOffset, 1f - Mathf.Exp(-offsetSmooth * Time.deltaTime));
        float yawOffset = currentYawOffset;

        if (cameraTransform != null)
        {
            Quaternion target = Quaternion.Euler(0f, cameraTransform.eulerAngles.y + yawOffset, 0f);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
        }

        // Animation: walk = magnitude 1, run = magnitude 2
        Vector2 animMove = sprinting ? input * 2f : input;
        animator.SetFloat(MoveX, animMove.x, dampTime, Time.deltaTime);
        animator.SetFloat(MoveY, animMove.y, dampTime, Time.deltaTime);

        // --- Ground check ---
        bool grounded = controller.isGrounded;
        if (grounded)
        {
            lastGroundedTime = Time.time;
            if (verticalVelocity < 0f)
                verticalVelocity = -2f; // small downward push keeps him stuck to the ground
        }

        // --- Run dust (only while sprinting, moving and on the ground) ---
        UpdateRunDust(sprinting && grounded && moving);

        // --- Footsteps (walk/run loops, silent when crouching or in the air) ---
        UpdateFootsteps(grounded && moving && !crouching, sprinting);

        // --- Jump ---
        if (jumpAction.action.WasPressedThisFrame())
            lastJumpPressedTime = Time.time;

        bool canJump = Time.time - lastGroundedTime <= coyoteTime;
        bool wantsJump = Time.time - lastJumpPressedTime <= jumpBufferTime;

        if (canJump && wantsJump && !crouching)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            animator.SetTrigger(JumpTrigger);
            lastJumpPressedTime = -10f;   // consume the press
            lastGroundedTime = -10f;      // no double jump from coyote time

            if (jumpClip != null)
                sfxSource.PlayOneShot(jumpClip, jumpVolume);
        }

        // --- Gravity ---
        verticalVelocity += gravity * Time.deltaTime;

        // --- Horizontal movement ---
        // Relative to the camera-facing direction (the aim offset is subtracted so W still goes where the camera looks)
        float speed = crouching ? crouchSpeed : (sprinting ? runSpeed : walkSpeed);
        Vector3 direction = new Vector3(input.x, 0f, input.y);
        direction = Vector3.ClampMagnitude(direction, 1f); // no faster diagonals
        Vector3 horizontal = Quaternion.Euler(0f, transform.eulerAngles.y - yawOffset, 0f) * direction * speed;

        // --- Apply everything in a single Move call ---
        Vector3 velocity = horizontal + Vector3.up * verticalVelocity;
        controller.Move(velocity * Time.deltaTime);

        // Crouch (hold to crouch)
        animator.SetBool(IsCrouching, crouching);
    }

    // ---------- Roll ----------
    bool RollPressed()
    {
        var kb = Keyboard.current;
        return kb != null && rollKey != Key.None && kb[rollKey].wasPressedThisFrame;
    }

    void StartRoll(Vector2 input)
    {
        // Pick one of four directions (relative to the camera)
        int dir;
        Vector3 local;
        if (Mathf.Abs(input.y) >= Mathf.Abs(input.x))
        {
            if (input.y >= 0f) { dir = 0; local = Vector3.forward; } // N (also the default with no input)
            else { dir = 1; local = Vector3.back; }    // S
        }
        else if (input.x > 0f) { dir = 2; local = Vector3.right; }   // E
        else { dir = 3; local = Vector3.left; }    // W

        float yaw = cameraTransform != null ? cameraTransform.eulerAngles.y : transform.eulerAngles.y;
        Quaternion yawRot = Quaternion.Euler(0f, yaw, 0f);
        float bodyOffset = dir == 0 ? rollNorthYawOffset
                         : dir == 1 ? rollSouthYawOffset
                         : dir == 2 ? rollEastYawOffset
                         : rollWestYawOffset;
        transform.rotation = Quaternion.Euler(0f, yaw + bodyOffset, 0f); // body facing (your adjustable offset)
        rollDirWorld = yawRot * local;                                   // travel direction stays camera-based
        currentYawOffset = 0f;

        isRolling = true;
        rollTimer = 0f;

        if (footstepSource.isPlaying) footstepSource.Stop();
        UpdateRunDust(false);
        animator.SetBool(IsCrouching, false);

        // Only set the roll animator parameters if they exist (so it still works before you set up the animations)
        if (animParamNames.Contains(RollDirName)) animator.SetInteger(RollDirName, dir);
        if (animParamNames.Contains(RollTriggerName)) animator.SetTrigger(RollTriggerName);

        if (health != null) health.Invulnerable = true;
    }

    void UpdateRoll()
    {
        rollTimer += Time.deltaTime;
        float t = Mathf.Clamp01(rollTimer / Mathf.Max(0.01f, rollDuration));

        Vector3 horizontal = rollDirWorld * rollSpeed * rollSpeedCurve.Evaluate(t);

        if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
        verticalVelocity += gravity * Time.deltaTime;

        controller.Move((horizontal + Vector3.up * verticalVelocity) * Time.deltaTime);

        if (health != null) health.Invulnerable = rollTimer < rollInvulnerableTime;

        if (rollTimer >= rollDuration)
        {
            isRolling = false;
            nextRollTime = Time.time + rollCooldown;
            if (health != null) health.Invulnerable = false;
        }
    }

    void UpdateFootsteps(bool shouldPlay, bool sprinting)
    {
        AudioClip wanted = sprinting ? runClip : walkClip;

        if (!shouldPlay || wanted == null)
        {
            if (footstepSource.isPlaying)
                footstepSource.Stop();
            return;
        }

        // Each loop has its own volume (updates live, so you can tweak it in Play mode)
        footstepSource.volume = sprinting ? runVolume : walkVolume;

        // Switch between walk and run loops, or start playing if stopped
        if (footstepSource.clip != wanted || !footstepSource.isPlaying)
        {
            footstepSource.clip = wanted;
            footstepSource.Play();
        }
    }

    void UpdateRunDust(bool shouldEmit)
    {
        if (runDust == null) return;

        var emission = runDust.emission;
        emission.rateOverTime = shouldEmit ? dustRateOverTime : 0f;

        if (shouldEmit && !runDust.isPlaying)
            runDust.Play();
        // When shouldEmit is false the rate is 0, so existing dust fades out naturally.
    }

    bool CanStandUp()
    {
        float radius = controller.radius * 0.9f;
        Vector3 origin = transform.position + Vector3.up * (controller.height - controller.radius);
        float distance = standHeight - controller.height + 0.05f;
        return !Physics.SphereCast(origin, radius, Vector3.up, out _, distance,
                                   ~0, QueryTriggerInteraction.Ignore);
    }
}