using System.Collections;
using UnityEngine;

// Put this on any object with a collider (like a Cube).
// Hit = white flash. Health 0 = dissolve away, then delete.
public class ShootableTarget : MonoBehaviour, IDamageable
{
    [Tooltip("Revolver damage is set in the RevolverData asset (25 by default), so 25 = one shot, 50 = two shots.")]
    public float health = 25f;

    [Header("Shader feedback")]
    public float flashTime = 0.12f;
    public float dissolveTime = 0.8f;
    [Tooltip("Slightly over 1 so the noise fully disappears.")]
    public float dissolveEnd = 1.3f;

    [Header("Optional")]
    [Tooltip("Spawned where the cube was destroyed (should delete itself).")]
    public GameObject destroyEffectPrefab;
    public AudioClip destroySound;
    [Range(0f, 1f)] public float destroyVolume = 1f;

    public event System.Action<ShootableTarget> Destroyed;

    static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
    static readonly int HitFlashId = Shader.PropertyToID("_HitFlash");

    Renderer[] renderers;
    Coroutine flashRoutine;
    bool dead;

    void Awake()
    {
        // .materials gives this cube its own copy so cubes don't flash together.
        renderers = GetComponentsInChildren<Renderer>();
        foreach (var r in renderers) { var _ = r.materials; }
    }

    // Called by the Bullet through the IDamageable interface.
    public void TakeDamage(float amount)
    {
        if (dead) return;

        health -= amount;

        if (health > 0f)
        {
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(FlashRoutine());
            return;
        }

        dead = true;
        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;

        if (destroyEffectPrefab != null)
            Instantiate(destroyEffectPrefab, transform.position, Quaternion.identity);
        if (destroySound != null)
            AudioSource.PlayClipAtPoint(destroySound, transform.position, destroyVolume);

        Destroyed?.Invoke(this);
        StartCoroutine(DissolveRoutine());
    }

    IEnumerator FlashRoutine()
    {
        SetFloat(HitFlashId, 1f);
        float t = 0f;
        while (t < flashTime)
        {
            t += Time.deltaTime;
            SetFloat(HitFlashId, 1f - Mathf.Clamp01(t / flashTime));
            yield return null;
        }
        SetFloat(HitFlashId, 0f);
    }

    IEnumerator DissolveRoutine()
    {
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        SetFloat(HitFlashId, 1f);

        float t = 0f;
        while (t < dissolveTime)
        {
            t += Time.deltaTime;
            SetFloat(DissolveId, Mathf.Lerp(0f, dissolveEnd, t / dissolveTime));
            SetFloat(HitFlashId, Mathf.Lerp(1f, 0f, t / (dissolveTime * 0.3f)));
            yield return null;
        }
        Destroy(gameObject);
    }

    void SetFloat(int id, float value)
    {
        foreach (var r in renderers)
            foreach (var m in r.materials)
                if (m.HasProperty(id)) m.SetFloat(id, value);
    }
}