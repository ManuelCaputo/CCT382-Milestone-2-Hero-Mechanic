using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Put this on the PLAYER (same object as the controller). The health bar builds itself, no UI setup needed.
// Anything that wants to hurt the player can call TakeDamage(float) or SendMessage("TakeDamage", amount).
public class PlayerHealth : MonoBehaviour
{
    [Header("Health")]
    public float maxHealth = 100f;
    [Tooltip("Health restored per second. 0 = no regeneration.")]
    public float regenPerSecond = 0f;
    [Tooltip("Seconds after the last hit before regeneration starts.")]
    public float regenDelay = 5f;

    [Header("Health Bar (built automatically)")]
    public bool showHealthBar = true;
    public Vector2 barSize = new Vector2(320f, 28f);
    [Tooltip("Distance from the top-left corner of the screen. Use a negative Y to move it down.")]
    public Vector2 barPosition = new Vector2(30f, -30f);
    public Color fullColor = new Color(0.25f, 0.85f, 0.3f);
    public Color lowColor = new Color(0.9f, 0.15f, 0.15f);
    public Color backgroundColor = new Color(0f, 0f, 0f, 0.65f);
    public float barSmooth = 10f;

    [Header("Death")]
    [Tooltip("Reload the scene after dying. The scene must be added in File > Build Profiles for this to work.")]
    public bool restartSceneOnDeath = false;
    public float restartDelay = 3f;
    [Tooltip("Optional Trigger parameter in the Animator to play a death animation.")]
    public string deathTrigger = "Die";
    public UnityEvent onDeath;

    public float CurrentHealth { get; private set; }
    public bool IsDead { get; private set; }
    // Set by the roll so the player can't be hurt during the dodge.
    public bool Invulnerable { get; set; }

    Animator animator;
    GameObject canvasObject;
    RectTransform fillRect;
    Image fillImage;
    float displayedFraction = 1f;
    float lastDamageTime = -999f;

    void Awake()
    {
        animator = GetComponent<Animator>();
        CurrentHealth = maxHealth;
        if (showHealthBar) BuildBar();
    }

    void OnDestroy()
    {
        if (canvasObject != null) Destroy(canvasObject);
    }

    void Update()
    {
        if (!IsDead && regenPerSecond > 0f && CurrentHealth < maxHealth &&
            Time.time - lastDamageTime >= regenDelay)
        {
            CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + regenPerSecond * Time.deltaTime);
        }

        float target = maxHealth > 0f ? CurrentHealth / maxHealth : 0f;
        displayedFraction = Mathf.Lerp(displayedFraction, target, 1f - Mathf.Exp(-barSmooth * Time.deltaTime));
        if (fillRect != null) ApplyBar(displayedFraction);
    }

    public void TakeDamage(float amount)
    {
        if (IsDead || Invulnerable || amount <= 0f) return;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
        lastDamageTime = Time.time;

        if (CurrentHealth <= 0f) Die();
    }

    public void Heal(float amount)
    {
        if (IsDead) return;
        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
    }

    // Right-click the component header in Play mode to test the bar.
    [ContextMenu("Test: Take 10 Damage")]
    void TestDamage() => TakeDamage(10f);

    void Die()
    {
        IsDead = true;

        if (animator != null && !string.IsNullOrEmpty(deathTrigger))
        {
            foreach (var p in animator.parameters)
                if (p.name == deathTrigger && p.type == AnimatorControllerParameterType.Trigger)
                {
                    animator.SetTrigger(deathTrigger);
                    break;
                }
        }

        // Stop the player from acting.
        DisableScript<CharacterAnimationInput>();
        DisableScript<Revolver>();
        DisableScript<HorseMount>();

        onDeath?.Invoke();

        if (restartSceneOnDeath) StartCoroutine(RestartRoutine());
    }

    void DisableScript<T>() where T : Behaviour
    {
        var c = GetComponent<T>();
        if (c != null) c.enabled = false;
    }

    IEnumerator RestartRoutine()
    {
        yield return new WaitForSeconds(restartDelay);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // ---------- Health bar UI (built in code so there's nothing to set up) ----------
    void BuildBar()
    {
        canvasObject = new GameObject("PlayerHealthBarCanvas");
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(canvasObject.transform, false);
        var bgRect = bg.GetComponent<RectTransform>();
        bgRect.anchorMin = bgRect.anchorMax = bgRect.pivot = new Vector2(0f, 1f);
        bgRect.anchoredPosition = barPosition;
        bgRect.sizeDelta = barSize;
        bg.GetComponent<Image>().color = backgroundColor;

        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(bg.transform, false);
        fillRect = fill.GetComponent<RectTransform>();
        fillImage = fill.GetComponent<Image>();

        ApplyBar(1f);
    }

    void ApplyBar(float fraction)
    {
        fraction = Mathf.Clamp01(fraction);
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(fraction, 1f);
        fillRect.offsetMin = new Vector2(4f, 4f);
        fillRect.offsetMax = new Vector2(0f, -4f);
        fillImage.enabled = fraction * barSize.x > 8f;
        fillImage.color = Color.Lerp(lowColor, fullColor, fraction);
    }

    void OnGUI()
    {
        if (!IsDead) return;
        var style = new GUIStyle(GUI.skin.label) { fontSize = 64, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        style.normal.textColor = new Color(0.9f, 0.15f, 0.15f);
        GUI.Label(new Rect(0, 0, Screen.width, Screen.height), "YOU DIED", style);
    }
}