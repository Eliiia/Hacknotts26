using UnityEngine;
using TMPro;

[ExecuteAlways]
public class SlotGroundRuler : MonoBehaviour
{
    public PlayerMovement player;
    public Collider2D ground;
    public SpriteRenderer[] marks;
    public Color color = new Color(1f, 0.92f, 0.7f, 1f);
    public float width = 0.12f;
    public float tickLength = 1f;

    [Header("Slot Note Labels")]
    public TextMeshPro[] keyLabels;
    public bool showKeyLabels = true;
    public Color labelColor = new Color(1f, 0.9f, 0.2f, 1f);
    public float labelOffsetBelowGround = 1.6f;
    public float labelFontSize = 10f;
    private static readonly string[] Keys =
        { "E1", "E2", "E3", "E4", "E5", "E6", "E7", "E8", "E9", "E10", "E11", "E12" };

    private void LateUpdate()
    {
        Refresh();
    }

    public void Refresh()
    {
        if (player == null || ground == null || marks == null)
            return;

        float spacing = (player.rightSlotX - player.leftSlotX) /
            (PlayerMovement.SlotCount - 1);
        float groundY = ground.bounds.max.y;

        for (int i = 0; i < marks.Length && i < PlayerMovement.SlotCount - 1; i++)
        {
            SpriteRenderer mark = marks[i];
            if (mark == null || mark.sprite == null)
                continue;

            float x = player.leftSlotX + (i + 0.5f) * spacing;
            float length = tickLength;
            mark.transform.position = new Vector3(x, groundY - length / 2f, 0f);
            Vector3 size = mark.sprite.bounds.size;
            mark.transform.localScale = new Vector3(width / size.x, length / size.y, 1f);
            mark.color = color;
        }

        if (keyLabels == null)
            return;
        for (int i = 0; i < keyLabels.Length && i < PlayerMovement.SlotCount; i++)
        {
            TextMeshPro label = keyLabels[i];
            if (label == null)
                continue;
            label.gameObject.SetActive(showKeyLabels);
            if (!showKeyLabels)
                continue;
            float x = Mathf.Lerp(player.leftSlotX, player.rightSlotX,
                i / (float)(PlayerMovement.SlotCount - 1));
            label.transform.position = new Vector3(x, groundY - labelOffsetBelowGround, 0f);
            label.text = Keys[i];
            label.color = labelColor;
            label.fontSize = labelFontSize;
        }
    }
}
