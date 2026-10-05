using System.Collections.Generic;
using UnityEngine;

namespace SteamRush.Features.UI.Views
{
    // View: instantiates and queues gift toasts in VerticalLayoutGroup.
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
                Debug.LogWarning("[GiftToastQueue] toastTemplate not assigned in Inspector - skipping Show.");
                return;
            }

            // Clean up destroyed or null toast references
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
            instance.transform.SetAsFirstSibling(); // Chen len dau de thong bao cu troi dan xuong duoi
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
