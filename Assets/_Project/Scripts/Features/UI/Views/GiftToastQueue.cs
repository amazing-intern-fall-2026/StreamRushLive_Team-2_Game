using UnityEngine;

namespace SteamRush.Features.UI.Views
{
    // View: sinh toast quà tặng từ 1 template có sẵn, xếp hàng nhờ VerticalLayoutGroup trên container cha - không truy cập module khác (SRP).
    public class GiftToastQueue : MonoBehaviour
    {
        [SerializeField] private GiftToastController toastTemplate;

        public void Show(string viewerName, string itemName, Sprite icon, Color? iconColor = null)
        {
            if (toastTemplate == null)
            {
                Debug.LogWarning("[GiftToastQueue] Chưa gán toastTemplate trong Inspector - bỏ qua Show.");
                return;
            }

            GiftToastController instance = Instantiate(toastTemplate, toastTemplate.transform.parent);
            instance.gameObject.SetActive(true);
            instance.Play(viewerName, itemName, icon, iconColor);
        }
    }
}
