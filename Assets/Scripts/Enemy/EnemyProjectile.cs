using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    public int damage = 1;
    public float hitRadius = 0.3f;
    public float lifetime = 6f;

    private Vector2 velocity;
    private GameObject owner;
    private Transform target;

    public void Initialize(Vector2 velocity, GameObject owner, Transform target)
    {
        this.velocity = velocity;
        this.owner = owner;
        this.target = target;
        Destroy(gameObject, lifetime);
    }

    private void FixedUpdate()
    {
        Vector2 position = transform.position;
        Vector2 step = velocity * Time.fixedDeltaTime;
        // Sweep the whole step so fast shots cannot skip through the player.
        RaycastHit2D[] hits = Physics2D.CircleCastAll(
            position, hitRadius, step.normalized, step.magnitude);

        foreach (RaycastHit2D hit in hits)
        {
            Transform other = hit.collider.transform;
            if (other == transform || other.IsChildOf(transform) ||
                (owner != null && other.IsChildOf(owner.transform)))
                continue;
            if (hit.collider.isTrigger)
                continue;

            if (target != null && other.IsChildOf(target))
            {
                Health health = hit.collider.GetComponentInParent<Health>();
                if (health != null)
                    health.TakeDamage(damage);
            }

            Destroy(gameObject);
            return;
        }

        transform.position += (Vector3)step;
    }
}
