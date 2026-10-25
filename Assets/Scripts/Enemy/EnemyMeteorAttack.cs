using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class EnemyMeteorAttack : MonoBehaviour
{
    public PlayerMovement player;
    public Collider2D ground;
    public GameObject meteorPrefab;
    public Sprite[] meteorSprites;
    public Material meteorIconMaterial;
    public Material[] meteorIconMaterials;
    public GameObject warningPrefab;
    public float minAttackDelay = 8f;
    public float maxAttackDelay = 12f;
    public float warningDuration = 1.2f;
    public float dropHeight = 35f;
    public float fallSpeed = 35f;
    public float impactRadius = 4f;
    public int damage = 1;
    [Header("Successive Waves")]
    [Min(1)] public int wavesPerAttack = 4;
    [Min(0f)] public float previewFlashGap = 0.12f;
    [Min(0f)] public float timeBetweenWaves = 0.35f;
    [Range(1, 11)] public int minimumSafeSlotChange = 2;
    [Range(1, 6)] public int meteorsPerSlot = 3;
    public Color dangerColor = new Color(1f, 0.2f, 0.2f, 0.325f);
    public Color safeColor = new Color(0.2f, 1f, 0.45f, 0.425f);
    [Min(0.1f)] public float warningCircleHeight = 3f;
    public float safeKeyFontSize = 14f;
    public Color safeKeyColor = new Color(0f, 0f, 0f, 1f);
    private static readonly string[] SlotKeys =
        { "E1", "E2", "E3", "E4", "E5", "E6", "E7", "E8", "E9", "E10", "E11", "E12" };
    public int SafeSlot => safeSlot + 1;
    public int CurrentWave { get; private set; }

    private int safeSlot = -1;
    private int previousSafeSlot = -1;
    private GameObject patternRoot;
    private SlotCamera lockedCamera;

    [Header("Flight")]
    public float flightHeight = 70f;
    public float flightDuration = 0.6f;
    [Min(0f)] public float landingClearance = 0.5f;

    private bool inFlight;
    private Vector3 groundPosition;

    private Rigidbody2D flightBody;
    private EnemySlotMovement slotMovement;
    private EnemyShooter shooter;
    private Collider2D flightCollider;
    private float flightHalfWidth;
    private float preferredLandingCenterX;
    private int landingBoundary = -1;

    private bool wasSimulated;
    private bool movementWasEnabled;
    private bool shooterWasEnabled;

    private class FallingMeteor
    {
        public GameObject visual;
        public Vector3 landing;
        public float delay;
        public float speed;
        public bool landed;
    }

    private void OnEnable() { StartCoroutine(AttackLoop()); }

    private IEnumerator AttackLoop()
    {
        while (true)
        {
            float min = Mathf.Max(0.1f, minAttackDelay);
            yield return new WaitForSeconds(Random.Range(min, Mathf.Max(min, maxAttackDelay)));
            if (player != null && ground != null && meteorPrefab != null &&
                warningPrefab != null && Camera.main != null)
                yield return AttackWithFlight();
        }
    }

    private float SlotX(int slot) => Mathf.Lerp(player.leftSlotX, player.rightSlotX,
        slot / (float)(PlayerMovement.SlotCount - 1));

    private List<int> GetVisibleSlots(Camera camera)
    {
        var slots = new List<int>();
        float half = camera.orthographicSize * camera.aspect;
        for (int i = 0; i < PlayerMovement.SlotCount; i++)
            if (SlotX(i) > camera.transform.position.x - half &&
                SlotX(i) < camera.transform.position.x + half)
                slots.Add(i);
        return slots;
    }

    private void ShowWarning(List<int> slots)
    {
        float spacing = (player.rightSlotX - player.leftSlotX) / (PlayerMovement.SlotCount - 1);
        patternRoot = new GameObject("Meteor Preview Pattern");
        float groundY = ground.bounds.max.y + 0.1f;
        foreach (int slot in slots)
        {
            GameObject marker = Instantiate(warningPrefab,
                new Vector3(SlotX(slot), groundY, 0), Quaternion.identity, patternRoot.transform);
            marker.name = (slot == safeSlot ? "Safe Slot " : "Danger Slot ") + (slot + 1);
            marker.SetActive(true);
            SpriteRenderer image = marker.GetComponentInChildren<SpriteRenderer>();
            if (image == null) continue;
            image.color = slot == safeSlot ? safeColor : dangerColor;
            image.sortingOrder = 15;
            Vector3 scale = marker.transform.localScale;
            scale.x *= spacing * 0.92f / Mathf.Max(0.01f, image.bounds.size.x);
            scale.y *= warningCircleHeight / Mathf.Max(0.01f, image.bounds.size.y);
            marker.transform.localScale = scale;
            if (slot == safeSlot)
                CreateSafeKeyLabel(image.bounds.center, slot);
        }
    }

    private IEnumerator RainPattern(Camera camera, List<int> slots, int plannedSafeSlot)
    {
        safeSlot = plannedSafeSlot;
        patternRoot = new GameObject("Meteor Rain Pattern");
        float spacing = (player.rightSlotX - player.leftSlotX) / (PlayerMovement.SlotCount - 1);
        float half = camera.orthographicSize * camera.aspect;
        float left = camera.transform.position.x - half;
        float right = camera.transform.position.x + half;
        float groundY = ground.bounds.max.y + 0.1f;
        var rain = new List<FallingMeteor>();
        foreach (int slot in slots)
        {
            if (slot == safeSlot) continue;
            int count = Mathf.Max(1, meteorsPerSlot);
            for (int i = 0; i < count; i++)
            {
                float x = SlotX(slot) + ((i + 0.5f) / count - 0.5f) * spacing * 0.8f;
                x = Mathf.Clamp(x, left + 0.2f, right - 0.2f);
                Vector3 landing = new Vector3(x, groundY, 0);
                float startY = Mathf.Max(groundY + dropHeight,
                    camera.transform.position.y + camera.orthographicSize + 2f);
                var visual = Instantiate(meteorPrefab, new Vector3(x, startY, 0),
                    Quaternion.identity, patternRoot.transform);
                RandomizeMeteorVisual(visual);
                visual.SetActive(false);
                rain.Add(new FallingMeteor { visual = visual, landing = landing,
                    delay = Random.Range(0f, 0.3f),
                    speed = Mathf.Max(0.1f, fallSpeed) * Random.Range(0.9f, 1.1f) });
            }
        }
        var damaged = new HashSet<Health>();
        int remaining = rain.Count;
        float elapsed = 0;
        while (remaining > 0)
        {
            foreach (FallingMeteor meteor in rain)
            {
                if (meteor.landed || elapsed < meteor.delay) continue;
                meteor.visual.SetActive(true);
                meteor.visual.transform.position = Vector3.MoveTowards(
                    meteor.visual.transform.position, meteor.landing, meteor.speed * Time.deltaTime);
                if (Vector3.Distance(meteor.visual.transform.position, meteor.landing) > 0.01f) continue;
                meteor.landed = true;
                remaining--;
                Destroy(meteor.visual);
                ApplyImpact(meteor.landing, damaged);
            }
            elapsed += Time.deltaTime;
            yield return null;
        }
        previousSafeSlot = safeSlot;
        ClearWave();
    }

    private void RandomizeMeteorVisual(GameObject visual)
    {
        if (meteorSprites == null || meteorSprites.Length == 0) return;
        int iconIndex = Random.Range(0, meteorSprites.Length);
        Sprite sprite = meteorSprites[iconIndex];
        SpriteRenderer image = visual.GetComponentInChildren<SpriteRenderer>();
        if (sprite == null || image == null) return;
        float previousSize = Mathf.Max(image.bounds.size.x, image.bounds.size.y);
        image.sprite = sprite;
        if (meteorIconMaterials != null && iconIndex < meteorIconMaterials.Length &&
            meteorIconMaterials[iconIndex] != null)
            image.sharedMaterial = meteorIconMaterials[iconIndex];
        else if (meteorIconMaterial != null) image.sharedMaterial = meteorIconMaterial;
        float size = Mathf.Max(image.bounds.size.x, image.bounds.size.y);
        if (size > 0f && previousSize > 0f)
            visual.transform.localScale *= previousSize / size;
    }

    private void CreateSafeKeyLabel(Vector3 position, int slot)
    {
        var labelObject = new GameObject("Safe Slot Key");
        labelObject.transform.SetParent(patternRoot.transform, false);
        labelObject.transform.position = position;
        var label = labelObject.AddComponent<TextMeshPro>();
        label.font = TMP_Settings.defaultFontAsset;
        label.text = SlotKeys[slot];
        label.fontSize = safeKeyFontSize;
        label.color = safeKeyColor;
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = false;
        label.rectTransform.sizeDelta = new Vector2(8f, 3f);
        label.renderer.sortingOrder = 16;
    }

    private int ChooseSafeSlot(List<int> candidates)
    {
        int current = player.CurrentSlot - 1;
        int reference = previousSafeSlot >= 0 ? previousSafeSlot : current;
        var options = candidates.FindAll(i => i != current &&
            Mathf.Abs(i - reference) >= minimumSafeSlotChange);
        if (options.Count == 0)
            options = candidates.FindAll(i => i != current && i != previousSafeSlot);
        if (options.Count == 0)
            options = candidates.FindAll(i => i != previousSafeSlot);
        if (options.Count == 0)
            options = candidates;
        return options[Random.Range(0, options.Count)];
    }

    private IEnumerator RainSequence()
    {
        Camera camera = Camera.main;
        List<int> slots = GetVisibleSlots(camera);
        if (slots.Count < 2) yield break;
        float spacing = (player.rightSlotX - player.leftSlotX) / (PlayerMovement.SlotCount - 1);
        float half = camera.orthographicSize * camera.aspect;
        float left = camera.transform.position.x - half;
        float right = camera.transform.position.x + half;
        var candidates = slots.FindAll(i => SlotX(i) - spacing / 2f >= left &&
            SlotX(i) + spacing / 2f <= right);
        if (candidates.Count == 0) candidates = slots;
        lockedCamera = camera.GetComponent<SlotCamera>();
        if (lockedCamera != null) lockedCamera.FramingLocked = true;

        previousSafeSlot = -1;
        int waves = Mathf.Max(1, wavesPerAttack);
        var plannedSlots = new List<int>(waves);
        for (int i = 0; i < waves; i++)
        {
            int chosen = ChooseSafeSlot(candidates);
            plannedSlots.Add(chosen);
            previousSafeSlot = chosen;
        }

        // Preview the complete sequence before any meteors spawn.
        for (int i = 0; i < waves; i++)
        {
            if (player == null) break;
            safeSlot = plannedSlots[i];
            ShowWarning(slots);
            yield return new WaitForSeconds(Mathf.Max(0f, warningDuration));
            ClearWave();
            yield return new WaitForSeconds(Mathf.Max(0f, previewFlashGap));
        }

        for (int i = 0; i < waves; i++)
        {
            if (player == null) break;
            CurrentWave = i + 1;
            yield return RainPattern(camera, slots, plannedSlots[i]);
            if (i < waves - 1)
                yield return new WaitForSeconds(Mathf.Max(0f, timeBetweenWaves));
        }
        ClearPattern();
    }

    private IEnumerator AttackWithFlight()
    {
        groundPosition = transform.position;

        flightBody = GetComponent<Rigidbody2D>();
        slotMovement = GetComponent<EnemySlotMovement>();
        shooter = GetComponent<EnemyShooter>();
        flightCollider = GetComponent<Collider2D>();
        Physics2D.SyncTransforms();
        flightHalfWidth = flightCollider.bounds.extents.x;
        preferredLandingCenterX = flightCollider.bounds.center.x;
        landingBoundary = -1;

        wasSimulated = flightBody != null && flightBody.simulated;
        movementWasEnabled =
            slotMovement != null && slotMovement.enabled;
        shooterWasEnabled =
            shooter != null && shooter.enabled;

        inFlight = true;

        if (slotMovement != null)
            slotMovement.enabled = false;

        if (shooter != null)
            shooter.enabled = false;

        if (flightBody != null)
        {
            flightBody.linearVelocity = Vector2.zero;
            flightBody.simulated = false;
        }

        Vector3 skyPosition =
            groundPosition + Vector3.up * flightHeight;

        yield return FlyTo(skyPosition);

        yield return RainSequence();

        yield return FlyDownSafely();

        RestoreAfterFlight();
    }

    private bool UpdateLandingPosition()
    {
        if (player == null || slotMovement == null) return true;
        Collider2D playerBody = player.GetComponent<Collider2D>();
        if (playerBody == null) return true;
        Physics2D.SyncTransforms();
        if (!slotMovement.TryChooseLanding(preferredLandingCenterX, playerBody.bounds,
            flightHalfWidth, landingClearance, out int boundary, out float x)) return false;
        landingBoundary = boundary;
        groundPosition.x = x;
        return true;
    }

    private IEnumerator FlyDownSafely()
    {
        Vector3 origin = transform.position;
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, flightDuration);
        while (elapsed < duration)
        {
            // Recheck throughout descent because the player can change slots.
            if (!UpdateLandingPosition()) { yield return null; continue; }
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            transform.position = Vector3.Lerp(origin, groundPosition, t);
            yield return null;
        }
        while (!UpdateLandingPosition()) yield return null;
        transform.position = groundPosition;
    }

    private IEnumerator FlyTo(Vector3 destination)
    {
        Vector3 origin = transform.position;
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, flightDuration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.SmoothStep(
                0f, 1f, Mathf.Clamp01(elapsed / duration)
            );

            transform.position = Vector3.Lerp(
                origin, destination, t
            );

            yield return null;
        }

        transform.position = destination;
    }

    private void RestoreAfterFlight()
    {
        if (!inFlight)
            return;

        inFlight = false;
        UpdateLandingPosition();
        if (slotMovement != null && landingBoundary >= 0)
            slotMovement.SetLandingBoundary(landingBoundary);
        transform.position = groundPosition;

        if (flightBody != null)
        {
            flightBody.position = groundPosition;
            flightBody.linearVelocity = Vector2.zero;
            flightBody.simulated = wasSimulated;
        }

        if (slotMovement != null)
            slotMovement.enabled = movementWasEnabled;

        if (shooter != null)
            shooter.enabled = shooterWasEnabled;
    }

    private void ApplyImpact(Vector3 landing, HashSet<Health> damaged)
    {
        if (player == null || player.CurrentSlot - 1 == safeSlot) return;
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(landing, impactRadius))
        {
            PlayerMovement hitPlayer = hit.GetComponentInParent<PlayerMovement>();
            if (hitPlayer != player) continue;
            Health health = hitPlayer.GetComponent<Health>();
            if (health != null && damaged.Add(health)) health.TakeDamage(damage);
        }
    }

    private void ClearPattern()
    {
        if (lockedCamera != null) lockedCamera.FramingLocked = false;
        lockedCamera = null;
        ClearWave();
        previousSafeSlot = -1;
        CurrentWave = 0;
    }

    private void ClearWave()
    {
        if (patternRoot != null)
        {
            patternRoot.SetActive(false);
            Destroy(patternRoot);
        }
        patternRoot = null;
        safeSlot = -1;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        ClearPattern();
        RestoreAfterFlight();
    }
}
