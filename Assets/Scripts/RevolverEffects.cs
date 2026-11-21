using UnityEngine;

// Muzzle flash on shot, hit sparks on impact.
public class RevolverEffects : MonoBehaviour
{
    public VoidEventChannel shotFired;
    public HitEventChannel bulletHit;

    [Tooltip("Your muzzle flash particle system (child of the gun).")]
    public ParticleSystem muzzleFlash;
    [Tooltip("Particle prefab spawned where the bullet hits.")]
    public GameObject hitEffectPrefab;

    void OnEnable()
    {
        if (shotFired != null) shotFired.OnRaised += Flash;
        if (bulletHit != null) bulletHit.OnRaised += Spark;
    }

    void OnDisable()
    {
        if (shotFired != null) shotFired.OnRaised -= Flash;
        if (bulletHit != null) bulletHit.OnRaised -= Spark;
    }

    void Flash()
    {
        if (muzzleFlash != null) muzzleFlash.Play();
    }

    void Spark(Vector3 point, Vector3 normal)
    {
        if (hitEffectPrefab == null) return;
        var fx = Instantiate(hitEffectPrefab, point, Quaternion.LookRotation(normal));
        Destroy(fx, 3f);
    }
}