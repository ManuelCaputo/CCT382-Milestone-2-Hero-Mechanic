using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

// Put this on the PLAYER (same object as the controller).
// Hold RIGHT mouse = aim (zoom + aim animation). LEFT click while aiming = fire. R = reload.
// While riding the horse you have unlimited ammo and never need to reload.
public class Revolver : MonoBehaviour
{
    [Header("Data (all tunable stats live in this asset)")]
    public RevolverData data;

    [Header("Event channels (assets)")]
    public VoidEventChannel shotFired;
    public VoidEventChannel emptyClick;
    public VoidEventChannel reloadStarted;
    public VoidEventChannel reloadFinished;
    public IntEventChannel ammoChanged;
    public HitEventChannel bulletHit;

    [Header("Behaviour")]
    public bool autoReloadWhenEmpty = true;
    [Tooltip("No ammo used and no reloading while mounted on the horse.")]
    public bool infiniteAmmoWhileRiding = true;
    public LayerMask hitMask = ~0;
    public bool showCrosshair = true;

    [Header("Bullet (visible)")]
    [Tooltip("Optional: your own bullet prefab. Leave empty to use a glowing yellow ball with a trail.")]
    public GameObject bulletPrefab;
    [Tooltip("Size of the default bullet (only used if no prefab is set).")]
    public float bulletSize = 0.08f;
    public Color bulletColor = new Color(1f, 0.85f, 0.2f);
    [Tooltip("Optional: empty object at the gun barrel named 'Muzzle' (found automatically).")]
    public Transform muzzle;

    [Header("Camera")]
    [Tooltip("Leave empty to use the Main Camera.")]
    public Transform cameraTransform;
    [Tooltip("Leave empty to zoom the Main Camera. If you use Cinemachine and nothing zooms, drag your FreeLook Camera object here.")]
    public GameObject cinemachineCameraObject;

    [Header("Animator parameters (names in your Animator Controller)")]
    public string aimingBool = "IsAiming";
    public string shootTrigger = "Shoot";
    public string reloadTrigger = "Reload";

    public int CurrentAmmo { get; private set; }
    public bool IsReloading { get; private set; }
    public bool IsAiming => isAiming;
    public bool Riding => infiniteAmmoWhileRiding && horseMount != null && horseMount.IsRiding;

    Animator animator;
    HorseMount horseMount;
    CharacterAnimationInput walkScript;
    readonly Dictionary<string, AnimatorControllerParameterType> animParams = new();

    bool isAiming;
    float nextFireTime;

    // Zoom
    Camera cam;
    Component vcam;
    FieldInfo lensField;
    PropertyInfo lensProp;
    float baseFov, currentFov;
    bool wasZoomed;

    void Awake()
    {
        animator = GetComponent<Animator>();

        horseMount = GetComponent<HorseMount>();
        if (horseMount == null) horseMount = FindFirstObjectByType<HorseMount>();
        walkScript = GetComponent<CharacterAnimationInput>();
        if (walkScript == null) walkScript = FindFirstObjectByType<CharacterAnimationInput>();

        if (animator != null)
            foreach (var p in animator.parameters) animParams[p.name] = p.type;

        if (data == null)
        {
            Debug.LogError("Revolver: assign a RevolverData asset in the Inspector.", this);
            enabled = false;
            return;
        }

        CurrentAmmo = data.magazineSize;
    }

    void Start()
    {
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        cam = Camera.main;

        // Auto-find an object called "Muzzle" anywhere under the player (e.g. on the gun) if none was assigned.
        if (muzzle == null)
        {
            foreach (var t in transform.root.GetComponentsInChildren<Transform>(true))
                if (t.name == "Muzzle") { muzzle = t; break; }
        }

        FindVcam();
        baseFov = ReadFov();
        currentFov = baseFov;

        if (vcam == null && cam != null)
        {
            foreach (var c in cam.GetComponents<Component>())
                if (c != null && c.GetType().Name == "CinemachineBrain")
                {
                    Debug.LogWarning("Revolver: your camera uses Cinemachine. If the zoom doesn't work, drag your FreeLook Camera object into 'Cinemachine Camera Object'.");
                    break;
                }
        }

        // Tell the HUD the starting ammo.
        if (ammoChanged != null) ammoChanged.Raise(CurrentAmmo);
    }

    void Update()
    {
        var mouse = Mouse.current;
        var keyboard = Keyboard.current;
        if (mouse == null || keyboard == null) return;

        // No aiming or shooting in the middle of a dodge roll
        bool rolling = walkScript != null && walkScript.IsRolling;
        isAiming = mouse.rightButton.isPressed && !rolling;

        // Mounted: cancel any reload in progress, you don't need one
        if (Riding && IsReloading)
        {
            StopAllCoroutines();
            IsReloading = false;
            if (reloadFinished != null) reloadFinished.Raise();
        }

        SetBool(aimingBool, isAiming);

        if (keyboard.rKey.wasPressedThisFrame) TryReload();

        // Must be holding right click to fire
        if (isAiming && mouse.leftButton.wasPressedThisFrame) TryShoot();
    }

    void LateUpdate()
    {
        if (baseFov <= 0f) return;

        float target = isAiming ? baseFov / Mathf.Max(1f, data.zoomAmount) : baseFov;
        currentFov = Mathf.Lerp(currentFov, target, 1f - Mathf.Exp(-data.zoomSpeed * Time.deltaTime));

        if (Mathf.Abs(currentFov - baseFov) > 0.01f)
        {
            WriteFov(currentFov);
            wasZoomed = true;
        }
        else if (wasZoomed)
        {
            WriteFov(baseFov);
            currentFov = baseFov;
            wasZoomed = false;
        }
    }

    // ---------- Shooting ----------
    void TryShoot()
    {
        if (IsReloading || Time.time < nextFireTime) return;

        nextFireTime = Time.time + data.fireCooldown;

        if (!Riding && CurrentAmmo <= 0)
        {
            if (emptyClick != null) emptyClick.Raise();
            if (autoReloadWhenEmpty) TryReload();
            return;
        }

        if (!Riding)
        {
            CurrentAmmo--;
            if (ammoChanged != null) ammoChanged.Raise(CurrentAmmo);
        }

        if (shotFired != null) shotFired.Raise();
        SetTrigger(shootTrigger);

        SpawnBullet();
    }

    void SpawnBullet()
    {
        if (cameraTransform == null) return;

        Vector3 spawnPos = muzzle != null
            ? muzzle.position
            : transform.position + Vector3.up * 1.3f + transform.forward * 0.6f;

        Vector3 aimPoint = GetAimPoint();
        Vector3 dir = (aimPoint - spawnPos).normalized;

        GameObject b = bulletPrefab != null
            ? Instantiate(bulletPrefab, spawnPos, Quaternion.LookRotation(dir))
            : CreateDefaultBullet(spawnPos, dir);

        var bullet = b.GetComponent<Bullet>();
        if (bullet == null) bullet = b.AddComponent<Bullet>();
        bullet.Init(data, hitMask, bulletHit, transform.root);
    }

    // Where the crosshair is pointing (ignores the player/horse so the camera ray isn't blocked by us).
    Vector3 GetAimPoint()
    {
        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
        var hits = Physics.RaycastAll(ray, data.range, hitMask, QueryTriggerInteraction.Ignore);

        float best = float.MaxValue;
        Vector3 point = ray.GetPoint(data.range);
        foreach (var h in hits)
        {
            if (h.transform.root == transform.root) continue;
            if (h.distance < best) { best = h.distance; point = h.point; }
        }
        return point;
    }

    GameObject CreateDefaultBullet(Vector3 pos, Vector3 dir)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Bullet";
        Destroy(go.GetComponent<Collider>());
        go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(dir));
        go.transform.localScale = Vector3.one * bulletSize;

        var mat = new Material(Shader.Find("Sprites/Default")) { color = bulletColor };
        go.GetComponent<MeshRenderer>().material = mat;

        var trail = go.AddComponent<TrailRenderer>();
        trail.time = 0.15f;
        trail.startWidth = bulletSize;
        trail.endWidth = 0f;
        trail.material = mat;
        trail.startColor = bulletColor;
        trail.endColor = new Color(bulletColor.r, bulletColor.g, bulletColor.b, 0f);
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        return go;
    }

    // ---------- Reload ----------
    void TryReload()
    {
        if (Riding || IsReloading || CurrentAmmo == data.magazineSize) return;
        StartCoroutine(ReloadRoutine());
    }

    IEnumerator ReloadRoutine()
    {
        IsReloading = true;
        if (reloadStarted != null) reloadStarted.Raise();
        SetTrigger(reloadTrigger);

        yield return new WaitForSeconds(data.reloadTime);

        CurrentAmmo = data.magazineSize;
        IsReloading = false;
        if (ammoChanged != null) ammoChanged.Raise(CurrentAmmo);
        if (reloadFinished != null) reloadFinished.Raise();
    }

    // ---------- Zoom helpers (works with a normal camera or Cinemachine) ----------
    void FindVcam()
    {
        if (cinemachineCameraObject == null) return;

        foreach (var c in cinemachineCameraObject.GetComponents<Component>())
        {
            if (c == null) continue;
            var t = c.GetType();
            if (t.Name == "CinemachineCamera" || t.Name == "CinemachineVirtualCamera" || t.Name == "CinemachineFreeLook")
            {
                vcam = c;
                lensField = t.GetField("Lens") ?? t.GetField("m_Lens");
                if (lensField == null) lensProp = t.GetProperty("Lens") ?? t.GetProperty("m_Lens");
                return;
            }
        }
        Debug.LogWarning("Revolver: couldn't find a Cinemachine camera component on '" + cinemachineCameraObject.name + "'.");
    }

    float ReadFov()
    {
        try
        {
            if (vcam != null)
            {
                object lens = lensField != null ? lensField.GetValue(vcam) : lensProp.GetValue(vcam);
                var f = lens.GetType().GetField("FieldOfView");
                if (f != null) return (float)f.GetValue(lens);
                var p = lens.GetType().GetProperty("FieldOfView");
                if (p != null) return (float)p.GetValue(lens);
            }
        }
        catch { /* fall through to the normal camera */ }

        return cam != null ? cam.fieldOfView : 0f;
    }

    void WriteFov(float fov)
    {
        try
        {
            if (vcam != null)
            {
                object lens = lensField != null ? lensField.GetValue(vcam) : lensProp.GetValue(vcam);
                var f = lens.GetType().GetField("FieldOfView");
                var p = f == null ? lens.GetType().GetProperty("FieldOfView") : null;
                if (f != null) f.SetValue(lens, fov);
                else if (p != null) p.SetValue(lens, fov);
                if (lensField != null) lensField.SetValue(vcam, lens);
                else lensProp.SetValue(vcam, lens);
                return;
            }
        }
        catch { /* fall through to the normal camera */ }

        if (cam != null) cam.fieldOfView = fov;
    }

    // ---------- Small helpers ----------
    bool Has(string name, AnimatorControllerParameterType type) =>
        !string.IsNullOrEmpty(name) && animParams.TryGetValue(name, out var t) && t == type;

    void SetBool(string name, bool v) { if (Has(name, AnimatorControllerParameterType.Bool)) animator.SetBool(name, v); }
    void SetTrigger(string name) { if (animator != null && Has(name, AnimatorControllerParameterType.Trigger)) animator.SetTrigger(name); }

    // Only the crosshair stays here. The ammo text is now in RevolverHUD.
    void OnGUI()
    {
        if (showCrosshair && isAiming)
        {
            float s = 6f;
            GUI.DrawTexture(new Rect(Screen.width / 2f - s / 2f, Screen.height / 2f - s / 2f, s, s), Texture2D.whiteTexture);
        }
    }
}