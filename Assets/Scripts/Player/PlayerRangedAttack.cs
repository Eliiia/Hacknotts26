using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(PlayerMovement))]
public class PlayerRangedAttack : MonoBehaviour
{
    public Transform targetMarker;
    public Collider2D ground;
    public GameObject guitarPrefab;
    public LayerMask enemyLayer;

    public int damage = 1;
    public float blastRadius = 4f;
    public float cooldown = 1f;
    public float flightTime = 0.6f;
    public float arcHeight = 8f;
    public float projectileWidth = 2f;
    public float throwReleaseDelay = 0.12f;

    public bool IsAiming { get; private set; }
    public int TargetSlot { get; private set; }

    private PlayerMovement movement;
    private float nextFireTime;
    private float normalFixedDeltaTime;

    public float CooldownProgress => cooldown <= 0f ? 1f :
        Mathf.Clamp01(1f - (nextFireTime - Time.time) / cooldown);

    private void Awake()
    {
        normalFixedDeltaTime = Time.fixedDeltaTime;
        movement = GetComponent<PlayerMovement>();

        if (targetMarker != null)
            targetMarker.gameObject.SetActive(false);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return;

        if (IsAiming && keyboard.escapeKey.wasPressedThisFrame)
        {
            StopAiming();
            return;
        }

        if (!keyboard.lKey.wasPressedThisFrame)
            return;

        ToggleAimAndFire();
    }

    public void ToggleAimAndFire()
    {
        if (!isActiveAndEnabled) return;
        if (IsAiming)
        {
            Fire();
        }
        else if (Time.time >= nextFireTime &&
                 targetMarker != null &&
                 ground != null &&
                 guitarPrefab != null)
        {
            IsAiming = true;
            Time.timeScale = 0.5f;
            Time.fixedDeltaTime = normalFixedDeltaTime * 0.5f;
            targetMarker.gameObject.SetActive(true);
            SelectTargetSlot(movement.CurrentSlot - 1);
        }
    }

    public void SelectTargetSlot(int slot)
    {
        TargetSlot = Mathf.Clamp(
            slot, 0, PlayerMovement.SlotCount - 1
        );

        float x = Mathf.Lerp(
            movement.leftSlotX,
            movement.rightSlotX,
            TargetSlot / (float)(PlayerMovement.SlotCount - 1)
        );

        targetMarker.position = new Vector3(
            x, ground.bounds.max.y + 0.2f, 0f
        );
    }

    private void Fire()
    {
        Vector3 destination = targetMarker.position;

        nextFireTime = Time.time + cooldown;
        StopAiming();

        StartCoroutine(ThrowProjectile(destination));
    }

    private IEnumerator ThrowProjectile(Vector3 destination)
    {
        Animator animator = movement != null ? movement.animator : GetComponent<Animator>();
        if (animator != null)
            animator.SetTrigger("Throw");
        yield return new WaitForSeconds(Mathf.Max(0f, throwReleaseDelay));
        yield return FlyProjectile(destination);
    }

    private void StopAiming()
    {
        IsAiming = false;
        Time.timeScale = 1f;
        Time.fixedDeltaTime = normalFixedDeltaTime;

        if (targetMarker != null)
            targetMarker.gameObject.SetActive(false);
    }

    private IEnumerator FlyProjectile(Vector3 destination)
    {
        GameObject shot = Instantiate(
            guitarPrefab,
            transform.position + Vector3.up * 2f,
            Quaternion.identity
        );

        SpriteRenderer visual =
            shot.GetComponentInChildren<SpriteRenderer>();

        if (visual != null && visual.sprite != null)
        {
            float width = visual.bounds.size.x;

            if (width > 0f)
                shot.transform.localScale *= projectileWidth / width;
        }

        Vector3 origin = transform.position + Vector3.up * 2f;
        float duration = Mathf.Max(0.05f, flightTime);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;

            shot.transform.position =
                Vector3.Lerp(origin, destination, t) +
                Vector3.up * (4f * arcHeight * t * (1f - t));

            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(shot);

        Collider2D[] hits = Physics2D.OverlapCircleAll(
            destination, blastRadius, enemyLayer
        );

        var damaged = new HashSet<Health>();

        foreach (Collider2D hit in hits)
        {
            Health health = hit.GetComponentInParent<Health>();

            if (health != null && damaged.Add(health))
                health.TakeDamage(damage);
        }
    }

    private void OnDisable()
    {
        StopAiming();
    }
}
