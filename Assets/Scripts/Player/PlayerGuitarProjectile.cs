using System.Collections.Generic;
using UnityEngine;

public class PlayerGuitarProjectile : MonoBehaviour
{
    private float direction, remainingDistance, speed, hitRadius;
    private int damage;
    private LayerMask enemyLayer;
    private GameObject owner;
    private bool initialized;
    private readonly HashSet<Health> damagedEnemies = new HashSet<Health>();

    public void Initialize(float direction, float distance, float flightTime, float radius,
        int damage, LayerMask enemyLayer, GameObject owner)
    {
        this.direction = Mathf.Sign(direction);
        remainingDistance = Mathf.Max(0f, distance);
        speed = remainingDistance / Mathf.Max(0.05f, flightTime);
        hitRadius = Mathf.Max(0.01f, radius);
        this.damage = damage;
        this.enemyLayer = enemyLayer;
        this.owner = owner;
        damagedEnemies.Clear();
        initialized = true;
        Destroy(gameObject, Mathf.Max(0.05f, flightTime) + 1f);
    }

    private void FixedUpdate()
    {
        if (!initialized) return;
        float distance = Mathf.Min(remainingDistance, speed * Time.fixedDeltaTime);
        Vector2 origin = transform.position;
        Vector2 forward = Vector2.right * direction;
        // Sweep the entire step so fast guitars cannot skip an enemy.
        foreach (var hit in Physics2D.CircleCastAll(origin, hitRadius, forward, distance, enemyLayer))
        {
            if (owner != null && hit.collider.transform.IsChildOf(owner.transform)) continue;
            Health health = hit.collider.GetComponentInParent<Health>();
            if (health != null && damagedEnemies.Add(health)) health.TakeDamage(damage);
        }
        transform.position += (Vector3)(forward * distance);
        remainingDistance -= distance;
        if (remainingDistance <= 0.0001f)
        {
            initialized = false;
            Destroy(gameObject);
        }
    }
}
