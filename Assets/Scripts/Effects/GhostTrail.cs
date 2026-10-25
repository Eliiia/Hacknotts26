using UnityEngine;

public class GhostTrail : MonoBehaviour
{
    public SpriteRenderer source;
    public Color tint = new Color(0.3f, 0.9f, 1f, 0.4f);
    [Min(0.01f)] public float fadeDuration = 0.45f;
    [Min(0.01f)] public float emissionInterval = 0.05f;
    [Min(0.01f)] public float minimumDistance = 0.4f;
    [Min(0.1f)] public float ghostSpacing = 2f;
    [Range(1, 12)] public int maxGhostsPerEmission = 6;

    private Vector3 previousPosition;
    private Quaternion previousRotation;
    private Vector3 previousScale;
    private Sprite previousSprite;
    private bool previousFlipX;
    private bool previousFlipY;
    private float nextEmissionTime;
    private bool hasSnapshot;
    private Color previousColor;
    private EnemySlotMovement enemyMovement;
    private int previousEnemySlot;

    private void Awake()
    {
        if (source == null)
            source = GetComponentInChildren<SpriteRenderer>();
        enemyMovement = GetComponent<EnemySlotMovement>();
    }

    private void OnEnable()
    {
        hasSnapshot = false;
        nextEmissionTime = 0f;
    }

    private void LateUpdate()
    {
        if (source == null || !source.enabled || !source.gameObject.activeInHierarchy)
        {
            hasSnapshot = false;
            return;
        }

        Vector3 position = source.transform.position;
        if (hasSnapshot && previousSprite != null && Time.time >= nextEmissionTime)
        {
            float distance = Mathf.Abs(position.x - previousPosition.x);
            // Turning shifts an offset sprite even though the enemy stays
            // in the same slot. Only real steps or vertical movement emit.
            if (enemyMovement != null && enemyMovement.CurrentSlot == previousEnemySlot)
                distance = 0f;
            if (distance >= minimumDistance)
            {
                int count = Mathf.Clamp(Mathf.CeilToInt(distance / ghostSpacing),
                    1, maxGhostsPerEmission);
                for (int i = 0; i < count; i++)
                    Emit(Vector3.Lerp(previousPosition, position, i / (float)count));
                nextEmissionTime = Time.time + emissionInterval;
            }
        }

        previousPosition = position;
        previousRotation = source.transform.rotation;
        previousScale = source.transform.lossyScale;
        previousSprite = source.sprite;
        previousFlipX = source.flipX;
        previousFlipY = source.flipY;
        previousColor = source.color;
        if (enemyMovement != null)
            previousEnemySlot = enemyMovement.CurrentSlot;
        hasSnapshot = true;
    }

    private void Emit(Vector3 position)
    {
        var ghost = new GameObject(name + " Afterimage");
        ghost.transform.SetPositionAndRotation(position, previousRotation);
        ghost.transform.localScale = previousScale;
        var image = ghost.AddComponent<SpriteRenderer>();
        image.sprite = previousSprite;
        image.flipX = previousFlipX;
        image.flipY = previousFlipY;
        image.sharedMaterial = source.sharedMaterial;
        image.sortingLayerID = source.sortingLayerID;
        image.sortingOrder = source.sortingOrder - 1;
        image.drawMode = source.drawMode;
        image.size = source.size;
        image.color = previousColor * tint;
        ghost.AddComponent<GhostAfterimage>().Initialize(image, fadeDuration);
    }
}
