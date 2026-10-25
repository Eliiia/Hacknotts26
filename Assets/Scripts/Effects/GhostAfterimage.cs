using UnityEngine;

public class GhostAfterimage : MonoBehaviour
{
    private SpriteRenderer image;
    private Color startColor;
    private float duration;
    private float age;

    public void Initialize(SpriteRenderer renderer, float lifetime)
    {
        image = renderer;
        startColor = image.color;
        duration = Mathf.Max(0.01f, lifetime);
        Destroy(gameObject, duration);
    }

    private void Update()
    {
        if (image == null)
            return;
        age += Time.deltaTime;
        Color color = startColor;
        color.a *= 1f - Mathf.Clamp01(age / duration);
        image.color = color;
    }
}
