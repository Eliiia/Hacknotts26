using UnityEngine;

[RequireComponent(typeof(Health))]
public class PlayerContactDamage : MonoBehaviour
{
    public LayerMask enemyLayers;
    public int damage = 1;
    public float damageCooldown = 1f;

    private Health health;
    private float nextDamageTime;

    private void Awake()
    {
        health = GetComponent<Health>();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryTakeDamage(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        TryTakeDamage(collision);
    }

    private void TryTakeDamage(Collision2D collision)
    {
        if ((enemyLayers.value & (1 << collision.gameObject.layer)) == 0 ||
            Time.time < nextDamageTime || damage <= 0)
            return;

        nextDamageTime = Time.time + Mathf.Max(0.01f, damageCooldown);
        health.TakeDamage(damage);
    }
}
