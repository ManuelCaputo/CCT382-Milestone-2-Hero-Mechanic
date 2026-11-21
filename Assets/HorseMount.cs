using UnityEngine;
using UnityEngine.InputSystem;

// Put this on the SAME object as CharacterAnimationInput (the one with the CharacterController).
public class HorseMount : MonoBehaviour
{
    [Header("Horse")]
    [Tooltip("The Horse from your scene (leave it unchecked/inactive), or a horse PREFAB.")]
    public GameObject horse;
    public float spawnDistance = 2f;
    public float dismountDistance = 1.5f;

    [Header("Ride Limits")]
    [Tooltip("How many seconds you keep the horse after summoning it. 0 = no limit.")]
    public float rideDuration = 30f;
    [Tooltip("Seconds before you can summon the horse again after it leaves. 0 = instantly.")]
    public float summonCooldown = 10f;

    [Header("Horse Speed")]
    public float walkSpeed = 4f;
    [Tooltip("Speed while holding Left Shift.")]
    public float gallopSpeed = 12f;

    [Header("Aiming While Riding")]
    [Tooltip("While riding and holding RIGHT CLICK, the player turns to face where the camera is aiming.")]
    public bool faceAimDirectionWhileRiding = true;
    [Tooltip("Extra turn while aiming with the horse standing still. Positive = turn right, negative = turn left.")]
    [Range(-180f, 180f)] public float aimYawOffsetStanding = 15f;
    [Tooltip("Extra turn while aiming with the horse walking.")]
    [Range(-180f, 180f)] public float aimYawOffsetWalking = 15f;
    [Tooltip("Extra turn while aiming with the horse galloping (Shift).")]
    [Range(-180f, 180f)] public float aimYawOffsetGalloping = 15f;
    [Tooltip("How quickly the turn changes when the horse changes speed.")]
    public float aimOffsetSmooth = 12f;
    [Tooltip("How quickly the player turns to aim, and back to sitting forward afterwards.")]
    public float riderTurnSpeed = 540f;

    [Header("Rider Bobbing (metres up and down)")]
    [Tooltip("Bobbing while the horse stands still. 0 = none.")]
    [Range(0f, 0.5f)] public float idleBob = 0f;
    [Tooltip("Bobbing while the horse walks. Keep this small.")]
    [Range(0f, 0.5f)] public float walkBob = 0.03f;
    [Tooltip("Bobbing while the horse gallops (Shift). Much bigger.")]
    [Range(0f, 0.5f)] public float gallopBob = 0.12f;
    [Tooltip("Bounces per second while walking.")]
    [Range(0.5f, 12f)] public float walkBobSpeed = 2.5f;
    [Tooltip("Bounces per second while galloping.")]
    [Range(0.5f, 12f)] public float gallopBobSpeed = 5.5f;
    [Tooltip("How quickly the bobbing fades between standing, walking and galloping.")]
    public float bobBlendSpeed = 8f;

    [Header("Optional: extra scripts to switch off while riding")]
    public Behaviour[] extraScriptsToDisable;

    [Header("UI")]
    public bool showTimer = true;

    public bool IsRiding => isRiding;

    CharacterAnimationInput walkScript;
    CharacterController controller;
    Animator animator;
    Revolver revolver;

    bool isRiding;
    float rideTimer;
    float nextSummonTime;
    GameObject horseInstance;
    HorseRider currentRider;
    Transform originalParent;
    Transform aimCamera;

    float bobPhase, bobAmplitude, bobFrequency;
    float currentAimOffset;

    static readonly int MoveX = Animator.StringToHash("MoveX");
    static readonly int MoveY = Animator.StringToHash("MoveY");
    static readonly int IsCrouching = Animator.StringToHash("IsCrouching");

    void Awake()
    {
        walkScript = GetComponent<CharacterAnimationInput>();
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        revolver = GetComponent<Revolver>();
        if (revolver == null) revolver = FindFirstObjectByType<Revolver>();
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame)
        {
            if (isRiding) Dismount();
            else if (Time.time >= nextSummonTime) Mount();
        }

        // Time limit
        if (isRiding && rideDuration > 0f)
        {
            rideTimer -= Time.deltaTime;
            if (rideTimer <= 0f) Dismount();
        }
    }

    // Runs after everything else so it can adjust the player's pose on the horse.
    void LateUpdate()
    {
        if (!isRiding) return;

        int gait = currentRider != null ? currentRider.GaitState : 0;

        // --- Turn to face where the camera is aiming (only while aiming) ---
        bool aiming = faceAimDirectionWhileRiding && revolver != null && revolver.IsAiming && aimCamera != null;
        if (aiming)
        {
            float targetOffset = gait == 0 ? aimYawOffsetStanding
                               : gait == 1 ? aimYawOffsetWalking
                               : aimYawOffsetGalloping;
            currentAimOffset = Mathf.Lerp(currentAimOffset, targetOffset, 1f - Mathf.Exp(-aimOffsetSmooth * Time.deltaTime));

            Quaternion target = Quaternion.Euler(0f, aimCamera.eulerAngles.y + currentAimOffset, 0f);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, riderTurnSpeed * Time.deltaTime);
        }
        else
        {
            // Sit facing forward on the horse again
            transform.localRotation = Quaternion.RotateTowards(transform.localRotation, Quaternion.identity, riderTurnSpeed * Time.deltaTime);
        }

        // --- Bobbing: none standing, a little walking, a lot galloping ---
        float targetAmp = gait == 0 ? idleBob : (gait == 1 ? walkBob : gallopBob);
        float targetFreq = gait == 2 ? gallopBobSpeed : (gait == 1 ? walkBobSpeed : walkBobSpeed * 0.5f);

        float blend = 1f - Mathf.Exp(-bobBlendSpeed * Time.deltaTime);
        bobAmplitude = Mathf.Lerp(bobAmplitude, targetAmp, blend);
        bobFrequency = Mathf.Lerp(bobFrequency, targetFreq, blend);

        bobPhase += bobFrequency * Mathf.PI * 2f * Time.deltaTime;
        transform.localPosition = new Vector3(0f, Mathf.Sin(bobPhase) * bobAmplitude, 0f);
    }

    void Mount()
    {
        if (horse == null)
        {
            Debug.LogWarning("HorseMount: assign the Horse.");
            return;
        }

        horseInstance = horse.scene.IsValid() ? horse : Instantiate(horse);

        Vector3 pos = transform.position + transform.forward * spawnDistance;
        horseInstance.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
        Physics.SyncTransforms(); // make sure the horse's collider sees its new position
        horseInstance.SetActive(true);

        Transform seat = horseInstance.transform.Find("Seat");
        if (seat == null)
        {
            Debug.LogWarning("HorseMount: the horse needs a child object named 'Seat'.");
            if (horseInstance == horse) horseInstance.SetActive(false); else Destroy(horseInstance);
            return;
        }

        SetPlayerControl(false);
        if (animator != null)
        {
            animator.SetFloat(MoveX, 0f);
            animator.SetFloat(MoveY, 0f);
            animator.SetBool(IsCrouching, false);
        }

        originalParent = transform.parent;
        transform.SetParent(seat);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        currentRider = horseInstance.GetComponent<HorseRider>();
        if (currentRider != null)
        {
            currentRider.walkSpeed = walkSpeed;     // speeds set here override the ones on HorseRider
            currentRider.gallopSpeed = gallopSpeed;
            currentRider.enabled = true;
        }

        aimCamera = Camera.main != null ? Camera.main.transform : null;
        bobPhase = bobAmplitude = bobFrequency = 0f;
        currentAimOffset = aimYawOffsetStanding;

        rideTimer = rideDuration;
        isRiding = true;
    }

    void Dismount()
    {
        if (currentRider != null) currentRider.enabled = false;

        // Move him BEFORE re-enabling the CharacterController, or it will fight the teleport.
        transform.SetParent(originalParent);
        Vector3 pos = horseInstance.transform.position + horseInstance.transform.right * dismountDistance;
        transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, horseInstance.transform.eulerAngles.y, 0f));
        Physics.SyncTransforms();

        SetPlayerControl(true);

        if (horseInstance == horse) horseInstance.SetActive(false);
        else Destroy(horseInstance);

        currentRider = null;
        isRiding = false;
        nextSummonTime = Time.time + summonCooldown;
    }

    void SetPlayerControl(bool on)
    {
        if (controller != null) controller.enabled = on;
        if (walkScript != null) walkScript.enabled = on;
        foreach (var s in extraScriptsToDisable)
            if (s != null) s.enabled = on;
    }

    void OnGUI()
    {
        if (!showTimer) return;

        string text = null;
        if (isRiding && rideDuration > 0f)
            text = $"Horse: {Mathf.CeilToInt(Mathf.Max(0f, rideTimer))}s";
        else if (!isRiding && Time.time < nextSummonTime)
            text = $"Horse ready in {Mathf.CeilToInt(nextSummonTime - Time.time)}s";

        if (text == null) return;

        var style = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        style.normal.textColor = Color.white;
        GUI.Label(new Rect(Screen.width / 2f - 150f, 20f, 300f, 40f), text, style);
    }
}