using UnityEngine;
using UnityEngine.UI;

public class CooldownHUD : MonoBehaviour
{
    public PlayerAttack melee;
    public PlayerRangedAttack ranged;
    public Slider primarySlider;
    public Slider secondarySlider;
    public Slider rangedSlider;

    private void Update()
    {
        if (melee != null)
        {
            SetProgress(primarySlider, melee.PrimaryCooldownProgress);
            SetProgress(secondarySlider, melee.SecondaryCooldownProgress);
        }
        if (ranged != null)
            SetProgress(rangedSlider, ranged.CooldownProgress);
    }

    private static void SetProgress(Slider slider, float progress)
    {
        if (slider == null)
            return;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.value = progress;
    }
}
