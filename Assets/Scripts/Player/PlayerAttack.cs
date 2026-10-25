using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;
using UnityEngine.Serialization;

public class PlayerAttack : MonoBehaviour
{
    public InputActionReference attackAction;
    public InputActionReference secondaryAttackAction;
    public Transform attackPoint;
    public LayerMask enemyLayer;

    public float attackRadius = 2f;
    [FormerlySerializedAs("attackCooldown")]
    [Min(0f)] public float primaryCooldown = 1f;
    [Min(0f)] public float secondaryCooldown = 1f;
    public int damage = 1;

    [Header("Primary Guitar Throw")]
    public GameObject primaryGuitarPrefab;
    [Min(1)] public int primaryRangeSlots = 4;
    [Min(0.05f)] public float primaryFlightTime = 0.45f;
    [Min(0f)] public float primaryReleaseDelay = 0.12f;
    [Min(0.01f)] public float primaryHitRadius = 1f;
    [Min(0.01f)] public float primaryGuitarWidth = 16f;

    [Header("Animation Events")]
    public UnityEvent onPrimaryAttack = new UnityEvent();
    public UnityEvent onSecondaryAttack = new UnityEvent();

    private float nextPrimaryAttackTime;
    private float nextSecondaryAttackTime;
    private PlayerMovement movement;
    private Collider2D body;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        body = GetComponent<Collider2D>();
    }

    public float PrimaryCooldownProgress =>
        GetCooldownProgress(nextPrimaryAttackTime, primaryCooldown);
    public float SecondaryCooldownProgress =>
        GetCooldownProgress(nextSecondaryAttackTime, secondaryCooldown);

    private static float GetCooldownProgress(float readyTime, float duration)
    {
        return duration <= 0f ? 1f :
            Mathf.Clamp01(1f - (readyTime - Time.time) / duration);
    }

    private void OnEnable()
    {
        if (attackAction != null)
            attackAction.action.Enable();
        if (secondaryAttackAction != null)
            secondaryAttackAction.action.Enable();
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        if (attackAction != null)
            attackAction.action.Disable();
        if (secondaryAttackAction != null)
            secondaryAttackAction.action.Disable();
    }

    private void Update()
    {
        if (attackAction != null && attackAction.action.WasPressedThisFrame())
            PrimaryAttack();
        if (secondaryAttackAction != null &&
            secondaryAttackAction.action.WasPressedThisFrame())
            SecondaryAttack();
    }

    public void PrimaryAttack()
    {
        if (!isActiveAndEnabled || movement == null || primaryGuitarPrefab == null ||
            Time.time < nextPrimaryAttackTime) return;
        nextPrimaryAttackTime = Time.time + Mathf.Max(0f, primaryCooldown);
        float direction = transform.localScale.x >= 0f ? 1f : -1f;
        onPrimaryAttack?.Invoke();
        StartCoroutine(LaunchPrimaryGuitar(direction));
    }

    private IEnumerator LaunchPrimaryGuitar(float direction)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, primaryReleaseDelay));
        Vector3 origin = body != null ? body.bounds.center : transform.position;
        float spacing = Mathf.Abs(movement.rightSlotX - movement.leftSlotX) /
            (PlayerMovement.SlotCount - 1);
        float distance = spacing * primaryRangeSlots;
        GameObject shot = Instantiate(primaryGuitarPrefab, origin, Quaternion.identity);
        var visual = shot.GetComponentInChildren<SpriteRenderer>();
        if (visual != null && visual.sprite != null && visual.bounds.size.x > 0f)
            shot.transform.localScale *= primaryGuitarWidth / visual.bounds.size.x;
        Vector3 scale = shot.transform.localScale;
        scale.x = Mathf.Abs(scale.x) * direction;
        shot.transform.localScale = scale;
        var projectile = shot.GetComponent<PlayerGuitarProjectile>() ?? shot.AddComponent<PlayerGuitarProjectile>();
        projectile.Initialize(direction, distance, primaryFlightTime, primaryHitRadius,
            damage, enemyLayer, gameObject);
        shot.SetActive(true);
    }

    public void SecondaryAttack()
    {
        TryAttack(onSecondaryAttack, ref nextSecondaryAttackTime, secondaryCooldown);
    }

    private void TryAttack(UnityEvent animationEvent, ref float readyTime, float cooldown)
    {
        if (!isActiveAndEnabled || attackPoint == null ||
            Time.time < readyTime)
            return;

        readyTime = Time.time + Mathf.Max(0f, cooldown);
        Attack();
        animationEvent?.Invoke();
    }

    private void Attack()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            attackPoint.position,
            attackRadius,
            enemyLayer
        );

        // An enemy can have multiple colliders.
        // Damage each Health component only once per punch.
        var damagedEnemies = new HashSet<Health>();

        foreach (Collider2D hit in hits)
        {
            Health health = hit.GetComponentInParent<Health>();

            if (health != null && damagedEnemies.Add(health))
                health.TakeDamage(damage);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null)
            return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(
            attackPoint.position,
            attackRadius
        );
    }
}
