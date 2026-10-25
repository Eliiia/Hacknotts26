using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class EnemySlotMovement : MonoBehaviour
{
    public const int PositionCount = PlayerMovement.SlotCount - 1;
    public PlayerMovement playerSlots;
    public Collider2D leftBoundary;
    public Collider2D rightBoundary;
    [Min(1)] public int minMoveSlots = 2;
    [Min(1)] public int maxMoveSlots = 12;
    [Min(0.1f)] public float minMoveDelay = 5f;
    [Min(0.1f)] public float maxMoveDelay = 10f;
    [Min(0.01f)] public float stepInterval = 0.3f;
    public AnimationClip chargeAnimation;

    public int CurrentSlot => currentSlot + 1;
    public bool IsDashing => remainingSteps > 0;

    private Rigidbody2D rb;
    private Collider2D body;
    private int currentSlot;
    private float nextMoveTime;
    private EnemyShooter shooter;
    private int remainingSteps;
    private int moveDirection;
    private float nextStepTime;
    private Animator animator;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        body = GetComponent<Collider2D>();
        shooter = GetComponent<EnemyShooter>();
        animator = GetComponent<Animator>();
        rb.constraints |= RigidbodyConstraints2D.FreezePositionX |
            RigidbodyConstraints2D.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation2D.None;
        if (playerSlots != null)
        {
            float spacing = SlotSpacing;
            currentSlot = spacing > 0f ? Mathf.Clamp(Mathf.RoundToInt(
                (body.bounds.center.x - FirstPositionX) / spacing),
                0, PositionCount - 1) : 0;
        }
        ScheduleMove();
    }

    private void FixedUpdate()
    {
        if (playerSlots == null)
            return;

        GetSlotLimits(out int first, out int last);
        currentSlot = Mathf.Clamp(currentSlot, first, last);
        if (remainingSteps > 0)
        {
            if (Time.time >= nextStepTime)
                StepForward(first, last);
        }
        else if (Time.time >= nextMoveTime &&
            (shooter == null || !shooter.IsPreparingShot))
        {
            BeginMove(first, last);
        }

        AlignCollider();
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    private float SlotSpacing => (playerSlots.rightSlotX - playerSlots.leftSlotX) /
        (PlayerMovement.SlotCount - 1);
    private float FirstPositionX => playerSlots.leftSlotX + SlotSpacing * 0.5f;

    private void LateUpdate()
    {
        // Facing can change after physics has run. Keep offset colliders on the mark.
        if (playerSlots != null)
            AlignCollider();
    }

    private void AlignCollider()
    {
        float slotX = FirstPositionX + currentSlot * SlotSpacing;
        float offsetX = transform.TransformVector(body.offset).x;
        rb.position = new Vector2(slotX - offsetX, rb.position.y);
        Vector3 position = transform.position;
        position.x = slotX - offsetX;
        transform.position = position;
    }

    private void GetSlotLimits(out int first, out int last, float halfWidth = -1f)
    {
        first = 0;
        last = PositionCount - 1;
        float spacing = SlotSpacing;
        if (spacing <= 0f)
            return;

        if (halfWidth < 0f) halfWidth = body.bounds.extents.x;
        if (leftBoundary != null)
            first = Mathf.Clamp(Mathf.CeilToInt(
                (leftBoundary.bounds.max.x + halfWidth + 0.05f -
                FirstPositionX) / spacing), 0, last);
        if (rightBoundary != null)
            last = Mathf.Clamp(Mathf.FloorToInt(
                (rightBoundary.bounds.min.x - halfWidth - 0.05f -
                FirstPositionX) / spacing), first, last);
    }

    public bool TryChooseLanding(float preferredCenterX, Bounds playerBounds,
        float halfWidth, float clearance, out int boundaryIndex, out float rootX)
    {
        boundaryIndex = -1;
        rootX = transform.position.x;
        if (playerSlots == null) return false;
        GetSlotLimits(out int first, out int last, halfWidth);
        float bestDistance = float.PositiveInfinity;
        for (int i = first; i <= last; i++)
        {
            float centerX = FirstPositionX + i * SlotSpacing;
            if (Mathf.Abs(centerX - playerBounds.center.x) <
                halfWidth + playerBounds.extents.x + clearance) continue;
            float distance = Mathf.Abs(centerX - preferredCenterX);
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            boundaryIndex = i;
            rootX = centerX - transform.TransformVector(body.offset).x;
        }
        return boundaryIndex >= 0;
    }

    public void SetLandingBoundary(int boundaryIndex)
    {
        currentSlot = Mathf.Clamp(boundaryIndex, 0, PositionCount - 1);
        remainingSteps = 0;
        ScheduleMove();
    }

    private int GetFacingDirection()
    {
        return shooter != null ? shooter.FacingDirection
            : transform.localScale.x >= 0f ? 1 : -1;
    }

    private void BeginMove(int first, int last)
    {
        int min = Mathf.Clamp(minMoveSlots, 1, PositionCount);
        int max = Mathf.Clamp(maxMoveSlots, min, PositionCount);
        int distance = Random.Range(min, max + 1);
        moveDirection = GetFacingDirection();
        int available = moveDirection > 0 ? last - currentSlot : currentSlot - first;
        remainingSteps = Mathf.Min(distance, available);
        if (remainingSteps > 0)
        {
            if (animator != null && chargeAnimation != null)
            {
                animator.Play("EnemyCharge", 0, 0f);
                nextStepTime = Time.time + chargeAnimation.length;
            }
            else
                StepForward(first, last);
        }
        else
            ScheduleMove();
    }

    private void StepForward(int first, int last)
    {
        int next = currentSlot + moveDirection;
        // A turn ends this sequence rather than making the enemy walk backwards.
        if (GetFacingDirection() != moveDirection || next < first || next > last)
        {
            remainingSteps = 0;
            ScheduleMove();
            return;
        }

        currentSlot = next;
        remainingSteps--;
        nextStepTime = Time.time + Mathf.Max(0.01f, stepInterval);
        if (remainingSteps == 0)
            ScheduleMove();
    }

    private void ScheduleMove()
    {
        float min = Mathf.Max(0.1f, minMoveDelay);
        nextMoveTime = Time.time + Random.Range(min, Mathf.Max(min, maxMoveDelay));
    }

    private void OnDisable()
    {
        remainingSteps = 0;
        ScheduleMove();
        if (animator != null && chargeAnimation != null)
            animator.Play("EnemyIdle", 0, 0f);
    }
}
