using UnityEngine;

[RequireComponent(typeof(Camera))]
public class SlotCamera : MonoBehaviour
{
    public PlayerMovement player;
    public float leftArenaEdge = -62.22222f;
    public float rightArenaEdge = 62.22222f;
    public float followSmoothTime = 0.12f;
    public bool FramingLocked { get; set; }

    private const float Aspect = 4f / 3f;
    private const int VisibleSlots = 8;
    private Camera view;
    private float horizontalVelocity;

    private void Awake()
    {
        view = GetComponent<Camera>();
        ConfigureViewport();
        Follow(true);
    }

    private void LateUpdate()
    {
        ConfigureViewport();
        Follow(false);
    }

    private void ConfigureViewport()
    {
        if (player == null)
            return;

        float spacing = Mathf.Abs(player.rightSlotX - player.leftSlotX) /
            (PlayerMovement.SlotCount - 1);
        view.orthographic = true;
        view.orthographicSize = spacing * VisibleSlots / (2f * Aspect);

        // Preserve a cropped 4:3 play area even in a wider/taller window.
        float windowAspect = Screen.width / (float)Mathf.Max(1, Screen.height);
        if (windowAspect > Aspect)
        {
            float width = Aspect / windowAspect;
            view.rect = new Rect((1f - width) / 2f, 0f, width, 1f);
        }
        else
        {
            float height = windowAspect / Aspect;
            view.rect = new Rect(0f, (1f - height) / 2f, 1f, height);
        }
        view.aspect = Aspect;
    }

    private void Follow(bool instantly)
    {
        if (player == null || FramingLocked)
            return;

        float followX = player.transform.position.x;

        if(
            player.RangedAttack != null &&
            player.RangedAttack.IsAiming &&
            player.RangedAttack.targetMarker != null
        ) {
            followX = player.RangedAttack.targetMarker.position.x;
        }

        float halfWidth = view.orthographicSize * Aspect;
        float minX = leftArenaEdge + halfWidth;
        float maxX = rightArenaEdge - halfWidth;
        if (minX > maxX)
            minX = maxX = (leftArenaEdge + rightArenaEdge) / 2f;

        float targetX = Mathf.Clamp(followX, minX, maxX);
        Vector3 position = transform.position;
        position.x = instantly || followSmoothTime <= 0f
            ? targetX
            : Mathf.SmoothDamp(position.x, targetX,
                ref horizontalVelocity, followSmoothTime);

        // A direct key can jump across the arena: keep the player visible
        // immediately, then let the camera finish centring smoothly.
        float spacing = Mathf.Abs(player.rightSlotX - player.leftSlotX) /
            (PlayerMovement.SlotCount - 1);
        float inset = Mathf.Min(spacing, halfWidth);
        position.x = Mathf.Clamp(position.x,
            followX - halfWidth + inset,
            followX + halfWidth - inset);
        position.x = Mathf.Clamp(position.x, minX, maxX);
        transform.position = position;
    }
}
