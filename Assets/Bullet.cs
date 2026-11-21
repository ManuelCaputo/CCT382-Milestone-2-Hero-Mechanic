using UnityEngine;
using System.Linq;

// Spawned by Revolver. You don't need to add this yourself.
public class Bullet : MonoBehaviour
{
    float speed, damage, hitForce, trailLinger = 0.3f;
    LayerMask hitMask;
    HitEventChannel hitEvent;
    Transform ignoreRoot;
    bool finished;

    public void Init(RevolverData data, LayerMask hitMask, HitEventChannel hitEvent, Transform ignoreRoot)
    {
        speed = data.bulletSpeed;
        damage = data.damage;
        hitForce = data.hitForce;
        this.hitMask = hitMask;
        this.hitEvent = hitEvent;
        this.ignoreRoot = ignoreRoot;
        Destroy(gameObject, data.bulletLifetime);
    }

    void Update()
    {
        if (finished) return;

        float step = speed * Time.deltaTime;
        Vector3 dir = transform.forward;

        // Sweep along the path this frame so fast bullets can't skip through thin objects.
        var hits = Physics.RaycastAll(transform.position, dir, step, hitMask, QueryTriggerInteraction.Ignore)
                          .OrderBy(h => h.distance);

        foreach (var hit in hits)
        {
            if (ignoreRoot != null && hit.transform.root == ignoreRoot) continue; // don't shoot ourselves/horse

            var target = hit.collider.GetComponentInParent<IDamageable>();
            if (target != null) target.TakeDamage(damage);

            if (hit.rigidbody != null)
                hit.rigidbody.AddForceAtPosition(dir * hitForce, hit.point);

            if (hitEvent != null) hitEvent.Raise(hit.point, hit.normal);

            // Stop at the hit point and let the trail fade out.
            transform.position = hit.point;
            foreach (var r in GetComponentsInChildren<MeshRenderer>()) r.enabled = false;
            finished = true;
            Destroy(gameObject, trailLinger);
            return;
        }

        transform.position += dir * step;
    }
}