using UnityEngine;
using UnityEngine.InputSystem;

// Put this on the HORSE (the parent object). Only runs while the player is mounted.
public class HorseRider : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 3f;
    public float gallopSpeed = 9f;
    public float turnSpeed = 360f; // degrees per second
    public float gravity = -20f;

    [Header("Collider")]
    [Tooltip("The horse gets a CharacterController automatically (a vertical capsule that stops it walking through walls). If you add your own CharacterController to the Horse, yours is used instead.")]
    public float colliderRadius = 0.6f;
    public float colliderHeight = 1.8f;
    [Tooltip("Centre of the capsule relative to the horse's origin. Y is usually half the height if the origin is at the hooves.")]
    public Vector3 colliderCenter = new Vector3(0f, 0.9f, 0f);
    [Tooltip("Highest step the horse can walk up.")]
    public float stepOffset = 0.3f;

    [Header("Animation")]
    [Tooltip("Leave empty to find the Animator on this object or its children.")]
    public Animator animator;
    [Tooltip("Name of the float parameter in the horse's Animator Controller (0 = idle, 1 = walk, 2 = gallop).")]
    public string speedParameter = "Speed";
    public float dampTime = 0.1f;

    [Header("Audio (drag your clips in here)")]
    [Tooltip("Looping hoof sound while the horse walks.")]
    public AudioClip walkClip;
    [Tooltip("Looping hoof sound while the horse gallops (holding Shift).")]
    public AudioClip gallopClip;
    [Range(0f, 1f)] public float walkVolume = 0.6f;
    [Range(0f, 1f)] public float gallopVolume = 0.9f;

    [Tooltip("Leave empty to use the Main Camera.")]
    public Transform cameraTransform;

    // 0 = standing still, 1 = walking, 2 = galloping. Read by HorseMount for the rider bobbing.
    public int GaitState { get; private set; }

    CharacterController controller;
    AudioSource hoofSource;
    int speedHash;
    float verticalVelocity;

    void Awake()
    {
        speedHash = Animator.StringToHash(speedParameter);
        if (animator == null) animator = GetComponentInChildren<Animator>();

        controller = GetComponent<CharacterController>();
        if (controller == null)
        {
            controller = gameObject.AddComponent<CharacterController>();
            controller.radius = colliderRadius;
            controller.height = colliderHeight;
            controller.center = colliderCenter;
            controller.stepOffset = stepOffset;
        }

        hoofSource = gameObject.AddComponent<AudioSource>();
        hoofSource.loop = true;
        hoofSource.playOnAwake = false;
        hoofSource.spatialBlend = 0f; // 2D: the player is on top of it

        enabled = false; // HorseMount turns this on when you mount
    }

    void OnDisable()
    {
        GaitState = 0;
        verticalVelocity = 0f;
        if (animator != null) animator.SetFloat(speedHash, 0f);
        if (hoofSource != null && hoofSource.isPlaying) hoofSource.Stop();
    }

    void Update()
    {
        var k = Keyboard.current;
        if (k == null) return;

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        Vector2 input = Vector2.zero;
        if (k.wKey.isPressed) input.y += 1f;
        if (k.sKey.isPressed) input.y -= 1f;
        if (k.dKey.isPressed) input.x += 1f;
        if (k.aKey.isPressed) input.x -= 1f;
        input = Vector2.ClampMagnitude(input, 1f);

        bool moving = input.sqrMagnitude > 0.01f && cameraTransform != null;
        bool galloping = k.leftShiftKey.isPressed;

        GaitState = !moving ? 0 : (galloping ? 2 : 1);

        if (animator != null)
            animator.SetFloat(speedHash, GaitState, dampTime, Time.deltaTime);

        UpdateAudio();

        // --- Movement (with collision + gravity via the CharacterController) ---
        Vector3 move = Vector3.zero;

        if (moving)
        {
            Vector3 camForward = cameraTransform.forward; camForward.y = 0f; camForward.Normalize();
            Vector3 camRight = cameraTransform.right; camRight.y = 0f; camRight.Normalize();
            Vector3 direction = camForward * input.y + camRight * input.x;

            Quaternion target = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);

            move = transform.forward * (galloping ? gallopSpeed : walkSpeed);
        }

        if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
        verticalVelocity += gravity * Time.deltaTime;
        move.y = verticalVelocity;

        controller.Move(move * Time.deltaTime);
    }

    void UpdateAudio()
    {
        AudioClip wanted = GaitState == 2 ? gallopClip : (GaitState == 1 ? walkClip : null);

        if (wanted == null)
        {
            if (hoofSource.isPlaying) hoofSource.Stop();
            return;
        }

        hoofSource.volume = GaitState == 2 ? gallopVolume : walkVolume;

        if (hoofSource.clip != wanted || !hoofSource.isPlaying)
        {
            hoofSource.clip = wanted;
            hoofSource.Play();
        }
    }
}