using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SteamRush.Features.UI.Views
{
    /// <summary>
    /// Quản lý sinh popup thông báo buff/debuff theo hàng chờ (Queue):
    /// - Nhận các thông báo từ HUDManager.ShowStatusPopup.
    /// - Xếp hàng (FIFO): Mỗi thông báo hiển thị tuần tự, không đè chồng lên nhau.
    /// - Tự động điều tiết nhịp hiển thị:
    ///   + Khi hàng chờ rảnh rỗi: hiển thị đủ thời lượng (3.0s) để người xem đọc thoải mái.
    ///   + Khi hàng chờ có nhiều tin nhắn dồn dập: tăng tốc hiển thị (1.8s) để không bị trễ thông tin.
    /// - Lọc bỏ thông báo rỗng hoặc các thông báo trùng lặp liên tiếp trong thời gian cực ngắn (< 0.5s).
    /// </summary>
    public class StatusPopupSpawner : MonoBehaviour
    {
        [SerializeField] private StatusPopupController popupTemplate;

        [Header("Queue Timing Settings")]
        [Tooltip("Thời lượng hiển thị chuẩn khi hàng chờ ít thông báo (giây).")]
        [SerializeField] private float normalDuration = 3.0f;
        [SerializeField] private float normalHoldDuration = 2.2f;

        [Tooltip("Thời lượng hiển thị tăng tốc khi hàng chờ có nhiều thông báo đang đợi (giây).")]
        [SerializeField] private float fastDuration = 1.8f;
        [SerializeField] private float fastHoldDuration = 1.2f;

        [Tooltip("Khoảng dừng ngắn giữa 2 thông báo liên tiếp (giây).")]
        [SerializeField] private float interPopupDelay = 0.15f;

        [Tooltip("Giới hạn số lượng thông báo tối đa trong hàng chờ.")]
        [SerializeField] private int maxQueueSize = 25;

        private struct PopupRequest
        {
            public string Message;
            public bool IsBuff;
            public Sprite Icon;
            public Color? IconColor;
        }

        private readonly Queue<PopupRequest> _queue = new Queue<PopupRequest>();
        private Coroutine _processQueueCoroutine;
        private string _lastEnqueuedMessage;
        private float _lastEnqueueTime = -999f;

        private void OnDisable()
        {
            if (_processQueueCoroutine != null)
            {
                StopCoroutine(_processQueueCoroutine);
                _processQueueCoroutine = null;
            }
            _queue.Clear();
        }

        /// <summary>
        /// Thêm thông báo mới vào hàng chờ.
        /// </summary>
        public void Spawn(string message, bool isBuff, Sprite icon = null, Color? iconColor = null)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            if (popupTemplate == null)
            {
                Debug.LogWarning("[StatusPopupSpawner] Chưa gán popupTemplate trong Inspector - bỏ qua Spawn.");
                return;
            }

            // Tránh enqueue liên tục các thông báo trùng lặp y hệt nhau trong vòng 0.5s
            if (message == _lastEnqueuedMessage && (Time.time - _lastEnqueueTime) < 0.5f)
            {
                return;
            }

            // Giới hạn độ dài hàng chờ tránh spam tràn bộ nhớ
            if (_queue.Count >= maxQueueSize)
            {
                _queue.Dequeue();
            }

            _lastEnqueuedMessage = message;
            _lastEnqueueTime = Time.time;

            _queue.Enqueue(new PopupRequest
            {
                Message = message,
                IsBuff = isBuff,
                Icon = icon,
                IconColor = iconColor
            });

            if (_processQueueCoroutine == null && gameObject.activeInHierarchy)
            {
                _processQueueCoroutine = StartCoroutine(ProcessQueue());
            }
        }

        private IEnumerator ProcessQueue()
        {
            while (_queue.Count > 0)
            {
                PopupRequest request = _queue.Dequeue();

                if (popupTemplate == null)
                {
                    break;
                }

                StatusPopupController instance = Instantiate(popupTemplate, popupTemplate.transform.parent);
                instance.gameObject.SetActive(true);

                // Nếu còn nhiều tin đang đợi trong hàng chờ -> tăng tốc để đuổi kịp diễn biến game
                bool hasPending = _queue.Count > 0;
                float duration = hasPending ? fastDuration : normalDuration;
                float hold = hasPending ? fastHoldDuration : normalHoldDuration;

                bool isFinished = false;
                instance.Play(request.Message, request.IsBuff, request.Icon, request.IconColor, () =>
                {
                    isFinished = true;
                }, duration, hold);

                // Chờ cho đến khi popup hoàn thành hiển thị và biến mất
                float timeout = duration + 0.6f;
                float timer = 0f;
                while (!isFinished && timer < timeout && instance != null)
                {
                    timer += Time.deltaTime;
                    yield return null;
                }

                // Khoảng đệm ngắn trước khi thông báo tiếp theo xuất hiện
                if (_queue.Count > 0 && interPopupDelay > 0f)
                {
                    yield return new WaitForSeconds(interPopupDelay);
                }
            }

            _processQueueCoroutine = null;
        }

        /// <summary>
        /// Xóa sạch hàng chờ (dùng khi reset game hoặc chuyển scene).
        /// </summary>
        public void ClearQueue()
        {
            _queue.Clear();
            if (_processQueueCoroutine != null)
            {
                StopCoroutine(_processQueueCoroutine);
                _processQueueCoroutine = null;
            }
        }
    }
}
