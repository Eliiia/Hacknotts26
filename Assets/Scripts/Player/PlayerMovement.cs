using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Input")]
    public InputActionReference jumpAction;

    [Header("Horizontal Slots")]
    public float leftSlotX = -55f;
    public float rightSlotX = 55f;
    public const int SlotCount = 12;

    [Header("Jump")]
    public float jumpSpeed = 10f;
    [Min(1)] public int maxJumps = 2;

    [Header("Ground Detection")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.15f;
    public LayerMask groundLayer;

    [Header("Visuals")]
    public SpriteRenderer spriteRenderer;
    public Transform facingTarget;

    public Animator animator;

    private Rigidbody2D rb;
    private bool jumpRequested;
    private int jumpsUsed;
    private int currentSlot;
    private float lastMovementDirection = 1f;
    private readonly InputAction[] slotActions = new InputAction[SlotCount];
    private InputAction previousSlotAction;
    private InputAction nextSlotAction;
    private Collider2D bodyCollider;

    public int CurrentSlot => currentSlot + 1;

    public PlayerRangedAttack RangedAttack { get; private set; }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        lastMovementDirection = transform.localScale.x < 0f ? -1f : 1f;
        RangedAttack = GetComponent<PlayerRangedAttack>();
        rb.constraints |= RigidbodyConstraints2D.FreezePositionX;
        rb.interpolation = RigidbodyInterpolation2D.None;

        string[] keys = { "q", "w", "e", "r", "t", "y",
            "u", "i", "o", "p", "leftBracket", "rightBracket" };
        for (int i = 0; i < SlotCount; i++)
            slotActions[i] = new InputAction("Slot " + (i + 1),
                InputActionType.Button, "<Keyboard>/" + keys[i]);
        previousSlotAction = new InputAction("Previous slot",
            InputActionType.Button, "<Keyboard>/leftArrow");
        nextSlotAction = new InputAction("Next slot",
            InputActionType.Button, "<Keyboard>/rightArrow");

        currentSlot = Mathf.Clamp(Mathf.RoundToInt(
            Mathf.InverseLerp(leftSlotX, rightSlotX, bodyCollider.bounds.center.x) *
            (SlotCount - 1)), 0, SlotCount - 1);

        UpdateFacing();
        SnapToSlot();
    }

    private void OnEnable()
    {
        foreach (InputAction action in slotActions)
            action.Enable();
        previousSlotAction.Enable();
        nextSlotAction.Enable();

        if (jumpAction != null)
            jumpAction.action.Enable();
    }

    private void OnDisable()
    {
        foreach (InputAction action in slotActions)
            action.Disable();
        previousSlotAction.Disable();
        nextSlotAction.Disable();

        if (jumpAction != null)
            jumpAction.action.Disable();

        jumpRequested = false;
    }

    private void OnDestroy()
    {
        foreach (InputAction action in slotActions)
            action?.Dispose();
        previousSlotAction?.Dispose();
        nextSlotAction?.Dispose();
    }

    private void Update()
    {
        bool aiming = RangedAttack != null && RangedAttack.IsAiming;

        int requestedSlot = aiming
            ? RangedAttack.TargetSlot
            : currentSlot;

        if (previousSlotAction.WasPressedThisFrame())
            requestedSlot--;
        if (nextSlotAction.WasPressedThisFrame())
            requestedSlot++;
        for (int i = 0; i < SlotCount; i++)
        {
            if (slotActions[i].WasPressedThisFrame())
                requestedSlot = i;
        }

        if (aiming)
            RangedAttack.SelectTargetSlot(requestedSlot);
        else
            SelectSlot(requestedSlot);

        UpdateFacing();
        SnapToSlot();

        if (jumpAction != null &&
            jumpAction.action.WasPressedThisFrame())
        {
            jumpRequested = true;
        }
    }

    private void SelectSlot(int requestedSlot)
    {
        int next = Mathf.Clamp(requestedSlot, 0, SlotCount - 1);
        if (next != currentSlot)
        {
            lastMovementDirection = next < currentSlot ? -1f : 1f;
            currentSlot = next;
        }
    }

    public void RequestGuitarSlot(int slot)
    {
        if (!isActiveAndEnabled) return;
        if (RangedAttack != null && RangedAttack.IsAiming)
            RangedAttack.SelectTargetSlot(slot);
        else
            SelectSlot(slot);
    }

    public void RequestJump()
    {
        if (isActiveAndEnabled) jumpRequested = true;
    }

    private void UpdateFacing()
    {
        float direction = lastMovementDirection;
        if (facingTarget != null)
        {
            int enemySlot = Mathf.Clamp(Mathf.RoundToInt(
                Mathf.InverseLerp(leftSlotX, rightSlotX, facingTarget.position.x) *
                (SlotCount - 1)), 0, SlotCount - 1);
            if (enemySlot != currentSlot)
                direction = enemySlot < currentSlot ? -1f : 1f;
        }

        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * direction;
        transform.localScale = scale;
    }

    private void SnapToSlot()
    {
        float slotX = Mathf.Lerp(
            leftSlotX,
            rightSlotX,
            currentSlot / (float)(SlotCount - 1)
        );

        // Includes Transform scale and left/right flipping.
        float colliderOffsetX =
            transform.TransformVector(bodyCollider.offset).x;

        Vector2 position = rb.position;
        position.x = slotX - colliderOffsetX;

        rb.position = position;
        // Keep the rendered transform in sync when facing changes between
        // physics ticks; the mirrored collider offset must be corrected now.
        transform.position = new Vector3(position.x, position.y, transform.position.z);
    }

    private void FixedUpdate()
    {
        Vector2 velocity = rb.linearVelocity;
        velocity.x = 0f;
        UpdateFacing();
        SnapToSlot();

        bool grounded = groundCheck != null &&
            Physics2D.OverlapCircle(
                groundCheck.position,
                groundCheckRadius,
                groundLayer
            ) != null;

        // Do not reset while the feet still overlap the ground during takeoff.
        if (grounded && velocity.y <= 0f)
            jumpsUsed = 0;
        else if (!grounded && jumpsUsed == 0)
            jumpsUsed = 1;

        if (jumpRequested && jumpsUsed < Mathf.Max(1, maxJumps))
        {
            velocity.y = jumpSpeed;
            jumpsUsed++;

            if(animator != null)
                animator.SetTrigger("Jump");
        }

        jumpRequested = false;
        rb.linearVelocity = velocity;

        if(animator != null) {
            animator.SetBool(
                "Grounded",
                grounded && velocity.y <= 0f
            );
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        for (int i = 0; i < SlotCount; i++)
        {
            float x = Mathf.Lerp(leftSlotX, rightSlotX,
                i / (float)(SlotCount - 1));
            Gizmos.DrawLine(new Vector3(x, transform.position.y - 1f, 0f),
                new Vector3(x, transform.position.y + 1f, 0f));
        }

        if (groundCheck == null)
            return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(
            groundCheck.position,
            groundCheckRadius
        );
    }
}
