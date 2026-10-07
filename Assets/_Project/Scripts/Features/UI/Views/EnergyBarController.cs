using UnityEngine;
using UnityEngine.UI;

namespace SteamRush.Features.UI.Views
{
    // View: displays energy bar adhering to SRP (normalized 0..1 value supplied by caller).
    public class EnergyBarController : MonoBehaviour
    {
        [SerializeField] private Image fillBar;

        public void SetEnergy(float currentEnergy)
        {
            if (fillBar != null)
            {
                // Modulate width instead of fillAmount to preserve 9-slice rounded corners
                var fillRect = fillBar.rectTransform;
                float fullWidth = ((RectTransform)fillRect.parent).rect.width;
                fillRect.sizeDelta = new Vector2(fullWidth * Mathf.Clamp01(currentEnergy), fillRect.sizeDelta.y);
            }
        }
    }
}
