using UnityEngine;

namespace SteamRush.Features.UI.Views
{
    // View: sinh popup buff/debuff phía trên bảng tên runner từ 1 template có sẵn - không truy cập module khác (SRP).
    public class StatusPopupSpawner : MonoBehaviour
    {
        [SerializeField] private StatusPopupController popupTemplate;

        public void Spawn(string message, bool isBuff, Sprite icon = null, Color? iconColor = null)
        {
            if (popupTemplate == null)
            {
                Debug.LogWarning("[StatusPopupSpawner] Chưa gán popupTemplate trong Inspector - bỏ qua Spawn.");
                return;
            }

            StatusPopupController instance = Instantiate(popupTemplate, popupTemplate.transform.parent);
            instance.gameObject.SetActive(true);
            instance.Play(message, isBuff, icon, iconColor);
        }
    }
}
