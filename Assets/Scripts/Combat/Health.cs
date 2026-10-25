using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class Health : MonoBehaviour
{
    [Header("Health")]
    public int maxHealth = 3;
    public string deathScene;

    [Header("Player Hearts")]
    public Image[] hearts;
    public Color fullHeartColor = Color.white;
    public Color emptyHeartColor =
        new Color(0.2f, 0.2f, 0.2f, 1f);

    [Header("Enemy Slider")]
    public Slider healthBar;

    [Header("Hit Flash")]
    public SpriteRenderer spriteRenderer;
    public Color hitColor = Color.red;
    public float flashDuration = 0.12f;

    private int currentHealth;
    private Color originalColor;
    private Coroutine flashRoutine;

    private void Awake()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        currentHealth = maxHealth;

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (spriteRenderer != null)
            originalColor = spriteRenderer.color;

        UpdateHealthDisplay();
    }

    public void TakeDamage(int damage)
    {
        if (currentHealth <= 0 || damage <= 0)
            return;

        currentHealth = Mathf.Max(0, currentHealth - damage);
        UpdateHealthDisplay();

        if (currentHealth == 0)
        {
            if (!string.IsNullOrEmpty(deathScene))
            {
                StartCoroutine(LoadDeathScene());
                return;
            }
            Destroy(gameObject);
            return;
        }

        if (spriteRenderer != null)
        {
            if (flashRoutine != null)
                StopCoroutine(flashRoutine);

            flashRoutine = StartCoroutine(Flash());
        }
    }

    private IEnumerator LoadDeathScene()
    {
        // Let the damage callback and this frame's meteor destruction finish
        // before starting scene loading or microphone teardown.
        yield return null;

        foreach (GuitarInput input in FindObjectsByType<GuitarInput>(FindObjectsSortMode.None))
            input.StopCapture();

        // Keep audio shutdown separate from scene unloading.
        yield return null;
        Time.timeScale = 1f;
        yield return SceneManager.LoadSceneAsync(deathScene);
    }

    private void UpdateHealthDisplay()
    {
        if (healthBar != null)
        {
            healthBar.minValue = 0f;
            healthBar.maxValue = 1f;
            healthBar.value = (float)currentHealth / maxHealth;
        }

        if (hearts != null)
        {
            for (int i = 0; i < hearts.Length; i++)
            {
                if (hearts[i] != null)
                {
                    hearts[i].color = i < currentHealth
                        ? fullHeartColor
                        : emptyHeartColor;
                }
            }
        }
    }

    private IEnumerator Flash()
    {
        spriteRenderer.color = hitColor;

        yield return new WaitForSeconds(flashDuration);

        spriteRenderer.color = originalColor;
        flashRoutine = null;
    }

    [ContextMenu("Test: Take 1 Damage")]
    private void TestDamage()
    {
        if (Application.isPlaying)
            TakeDamage(1);
    }
}
