using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SteamRush.Features.UI.Views
{
    /// <summary>
    /// FIFO queue spawner for status popups:
    /// - Queues and displays status toasts sequentially without overlap.
    /// - Dynamically throttles display speed when queue backs up.
    /// - Deduplicates identical consecutive messages within 0.5s.
    /// </summary>
    public class StatusPopupSpawner : MonoBehaviour
    {
        // Floating status text disabled to avoid obstructing Runner visibility and nametag.
        // Queue logic is retained for optional reuse.
        [SerializeField] private bool _popupsEnabled = false;

        [SerializeField] private StatusPopupController popupTemplate;

        [Header("Queue Timing Settings")]
        [Tooltip("Standard popup display duration when queue is low (seconds).")]
        [SerializeField] private float normalDuration = 3.0f;
        [SerializeField] private float normalHoldDuration = 2.2f;

        [Tooltip("Accelerated display duration when queue is backed up (seconds).")]
        [SerializeField] private float fastDuration = 1.8f;
        [SerializeField] private float fastHoldDuration = 1.2f;

        [Tooltip("Interval pause between consecutive popups (seconds).")]
        [SerializeField] private float interPopupDelay = 0.15f;

        [Tooltip("Maximum queue capacity limit.")]
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
        /// Enqueues new status notification.
        /// </summary>
        public void Spawn(string message, bool isBuff, Sprite icon = null, Color? iconColor = null)
        {
            if (!_popupsEnabled) return;
            if (string.IsNullOrWhiteSpace(message)) return;

            if (popupTemplate == null)
            {
                Debug.LogWarning("[StatusPopupSpawner] popupTemplate not assigned in Inspector - skipping Spawn.");
                return;
            }

            // Deduplicate identical consecutive messages within 0.5s
            if (message == _lastEnqueuedMessage && (Time.time - _lastEnqueueTime) < 0.5f)
            {
                return;
            }

            // Enforce queue capacity limit to avoid unbounded growth
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

                Transform container = (popupTemplate.transform.parent != null) ? popupTemplate.transform.parent : transform;
                StatusPopupController instance = Instantiate(popupTemplate, container);
                instance.gameObject.SetActive(true);

                // Accelerate pacing if queue is backed up
                bool hasPending = _queue.Count > 0;
                float duration = hasPending ? fastDuration : normalDuration;
                float hold = hasPending ? fastHoldDuration : normalHoldDuration;

                bool isFinished = false;
                instance.Play(request.Message, request.IsBuff, request.Icon, request.IconColor, () =>
                {
                    isFinished = true;
                }, duration, hold);

                // Wait until active popup finishes display cycle
                float timeout = duration + 0.6f;
                float timer = 0f;
                while (!isFinished && timer < timeout && instance != null)
                {
                    timer += Time.deltaTime;
                    yield return null;
                }

                // Short breathing interval before next popup appears
                if (_queue.Count > 0 && interPopupDelay > 0f)
                {
                    yield return new WaitForSeconds(interPopupDelay);
                }
            }

            _processQueueCoroutine = null;
        }

        /// <summary>
        /// Clears all queued notifications (used on reset or scene transition).
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
