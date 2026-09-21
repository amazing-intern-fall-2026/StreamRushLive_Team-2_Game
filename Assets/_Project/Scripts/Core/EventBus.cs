using System;
using System.Collections.Generic;

namespace SteamRush.Core
{
    // Pub/sub toi gian dung chung cho toan bo game - muc dich de cac module giao tiep ma KHONG
    // tham chieu truc tiep vao nhau (vd: CheckpointManager subscribe PlayerDeathEvent ma khong can
    // biet module HP/Heart nao ban su kien do). Static, khong ke thua MonoBehaviour.
    public static class EventBus
    {
        private static readonly Dictionary<Type, Delegate> _handlers = new Dictionary<Type, Delegate>();

        public static void Subscribe<TEvent>(Action<TEvent> handler)
        {
            Type eventType = typeof(TEvent);
            if (_handlers.TryGetValue(eventType, out Delegate existing))
            {
                _handlers[eventType] = Delegate.Combine(existing, handler);
            }
            else
            {
                _handlers[eventType] = handler;
            }
        }

        public static void Unsubscribe<TEvent>(Action<TEvent> handler)
        {
            Type eventType = typeof(TEvent);
            if (!_handlers.TryGetValue(eventType, out Delegate existing))
            {
                return;
            }

            Delegate remaining = Delegate.Remove(existing, handler);
            if (remaining == null)
            {
                _handlers.Remove(eventType);
            }
            else
            {
                _handlers[eventType] = remaining;
            }
        }

        public static void Publish<TEvent>(TEvent eventData)
        {
            Type eventType = typeof(TEvent);
            if (_handlers.TryGetValue(eventType, out Delegate existing))
            {
                ((Action<TEvent>)existing)?.Invoke(eventData);
            }
        }
    }

    // Ban khi Runner het tim. RunnerHealthSystem.cs (Truong, Jira S1-24) se KHONG tu publish
    // event nay - ho chi co "public event Action OnPlayerDeath" rieng theo dung task cua ho,
    // khong doi gi ca. Se co 1 adapter rieng (thuoc module Runner, KHONG dung toi
    // RunnerHealthSystem.cs) subscribe truc tiep vao OnPlayerDeath cua ho roi publish lai event
    // nay qua EventBus - viet sau khi S1-24 merge xong.
    public readonly struct PlayerDeathEvent
    {
    }

    // Yeu cau hoi tim ve gia tri chi dinh (module HP/Heart se subscribe de xu ly thuc te).
    public readonly struct RestoreHeartsRequestEvent
    {
        public readonly int HeartCount;

        public RestoreHeartsRequestEvent(int heartCount)
        {
            HeartCount = heartCount;
        }
    }

    // Ban khi phe Anti du 500 tim, yeu cau sinh 1 xe can duong (GDD v1.3 muc 5).
    // SingleObstacleSpawner.cs (DangHuy, Jira S1-23) co san ham public
    // "TriggerSpawnCarFromAntiLikes()" nhung KHONG tu subscribe event nay - se co 1 adapter rieng
    // (khong dung toi FactionTugOfWarManager.cs) noi 2 ben lai sau khi S1-23 co san, tuong tu
    // adapter cua PlayerDeathEvent voi RunnerHealthSystem (S1-24).
    public readonly struct RequestCarSpawnEvent
    {
        public readonly int LaneIndex; // 0 = Random, 1 = Left, 2 = Center, 3 = Right

        public RequestCarSpawnEvent(int laneIndex = 0)
        {
            LaneIndex = laneIndex;
        }
    }

    // Ban khi 1 nguoi xem chua Follow co gui lenh dieu khien (left/right/fast/slow) - GDD v1.3
    // muc 3.2: "he thong tu dong bo qua lenh (kem thong bao bot nhac nho neu can)". Module chat
    // that (StreamIntegration) se subscribe event nay de tra loi comment nhac Follow - chua ton
    // tai luc viet file nay nen chi Publish, khong tu gui tin nhan duoc.
    public readonly struct NonFollowerCommandRejectedEvent
    {
        public readonly string UserId;

        public NonFollowerCommandRejectedEvent(string userId)
        {
            UserId = userId;
        }
    }
}
