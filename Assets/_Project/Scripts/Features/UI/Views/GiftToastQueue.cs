using System.Collections.Generic;
using UnityEngine;

namespace SteamRush.Features.UI.Views
{
    // View: sinh toast quà tặng từ 1 template có sẵn, xếp hàng nhờ VerticalLayoutGroup trên container cha - không truy cập module khác (SRP).
    public class GiftToastQueue : MonoBehaviour
    {
        [SerializeField] private GiftToastController toastTemplate;

        // Gioi han so toast hien thi CUNG LUC. Donate/su kien don dap (spam) khong duoc de khung
        // toast phinh to vo han roi che mat Runner (bug thuc te da gap) - toast cu nhat bi ep
        // bien mat ngay khi vuot gioi han, nhuong cho toast moi nhat.
        [SerializeField] private int _maxConcurrentToasts = 3;

        private readonly List<GiftToastController> _active = new List<GiftToastController>();

        private void Awake()
        {
            _active.Clear();
            if (toastTemplate != null && toastTemplate.transform.parent != null)
            {
                Transform parent = toastTemplate.transform.parent;
                for (int i = parent.childCount - 1; i >= 0; i--)
                {
                    Transform child = parent.GetChild(i);
                    if (child != toastTemplate.transform && child.name.Contains("(Clone)"))
                    {
                        Destroy(child.gameObject);
                    }
                }
            }
        }

        public void Show(string viewerName, string itemName, Sprite icon, Color? iconColor = null, Color? accentColor = null, bool showItemName = true)
        {
            if (toastTemplate == null)
            {
                Debug.LogWarning("[GiftToastQueue] Chưa gán toastTemplate trong Inspector - bỏ qua Show.");
                return;
            }

            // Dọn sạch các toast đã bị huỷ hoặc null
            _active.RemoveAll(item => item == null);

            while (_active.Count >= _maxConcurrentToasts)
            {
                GiftToastController oldest = _active[0];
                _active.RemoveAt(0);
                if (oldest != null)
                {
                    oldest.ForceDismiss();
                }
            }

            GiftToastController instance = Instantiate(toastTemplate, toastTemplate.transform.parent);
            instance.gameObject.SetActive(true);
            instance.Dismissed += OnToastDismissed;
            _active.Add(instance);
            instance.Play(viewerName, itemName, icon, iconColor, accentColor, showItemName);
        }

        private void OnToastDismissed(GiftToastController toast)
        {
            if (toast != null)
            {
                toast.Dismissed -= OnToastDismissed;
            }
            _active.Remove(toast);
            _active.RemoveAll(item => item == null);
        }
    }
}
