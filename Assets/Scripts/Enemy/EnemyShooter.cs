using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class EnemyShooter : MonoBehaviour
{
    public Transform target;
    [Tooltip("A projectile prefab or an inactive projectile object in this scene.")]
    public EnemyProjectile projectilePrefab;
    public float fireInterval = 3f;
    public float projectileSpeed = 25f;
    public float spawnClearance = 0.75f;
    public float projectileForwardOffset = 10f;
    [Tooltip("World-space height above the enemy collider's feet. Independent of enemy scale.")]
    public float shotHeightAboveFeet = 7.5f;
    public bool spriteFacesRight = true;
    public AnimationClip rangedAnimation;
    public bool IsPreparingShot { get; private set; }

    public int FacingDirection =>
        ((transform.localScale.x >= 0f) == spriteFacesRight) ? 1 : -1;

    private Collider2D body;
    private float nextShotTime;
    private EnemySlotMovement slotMovement;
    private Animator animator;
    private float shotReleaseTime;

    private void Awake()
    {
        body = GetComponent<Collider2D>();
        nextShotTime = Time.time + fireInterval;
        slotMovement = GetComponent<EnemySlotMovement>();
        animator = GetComponent<Animator>();
    }

    private void Update()
    {
        if (target == null)
            return;

        if (!IsPreparingShot && (slotMovement == null || !slotMovement.IsDashing))
        {
            Physics2D.SyncTransforms();
            Collider2D targetBody = target.GetComponent<Collider2D>();
            float targetX = targetBody != null ? targetBody.bounds.center.x : target.position.x;
            bool targetIsRight = targetX >= body.bounds.center.x;

            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) *
                (targetIsRight == spriteFacesRight ? 1f : -1f);

            transform.localScale = scale;
        }

        bool facingRight = FacingDirection > 0;

        if (IsPreparingShot)
        {
            if (Time.time >= shotReleaseTime)
            {
                IsPreparingShot = false;
                if (projectilePrefab != null)
                    Shoot(facingRight);
            }
            return;
        }

        if (projectilePrefab != null && Time.time >= nextShotTime &&
            (slotMovement == null || !slotMovement.IsDashing))
        {
            nextShotTime = Time.time + Mathf.Max(0.1f, fireInterval);
            if (animator != null && rangedAnimation != null)
            {
                animator.Play("EnemyRange", 0, 0f);
                IsPreparingShot = true;
                shotReleaseTime = Time.time + rangedAnimation.length;
            }
            else
                Shoot(facingRight);
        }
    }

    private void OnDisable()
    {
        IsPreparingShot = false;
        nextShotTime = Time.time + Mathf.Max(0.1f, fireInterval);
        if (animator != null && rangedAnimation != null)
            animator.Play("EnemyIdle", 0, 0f);
    }

    private void Shoot(bool facingRight)
    {
        Physics2D.SyncTransforms();
        Bounds bounds = body.bounds;
        Vector2 origin = new Vector2(
            facingRight ? bounds.max.x + spawnClearance
                        : bounds.min.x - spawnClearance,
            bounds.min.y + shotHeightAboveFeet
        );
        Vector2 direction = facingRight ? Vector2.right : Vector2.left;
        origin += direction * projectileForwardOffset;

        EnemyProjectile shot = Instantiate(projectilePrefab, origin,
            Quaternion.Euler(0f, 0f,
                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg));
        shot.Initialize(direction * projectileSpeed, gameObject, target);
        shot.gameObject.SetActive(true);
    }
}
