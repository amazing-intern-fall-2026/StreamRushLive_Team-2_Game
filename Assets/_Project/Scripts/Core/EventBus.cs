using System;
using System.Collections.Generic;

namespace SteamRush.Core
{
    // Minimal pub/sub event bus decoupling modules across the project.
    // Static, does not inherit from MonoBehaviour.
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

    // Obsolete death event (deprecated in GDD v0.3/v1.2 - kept for backward compatibility if needed).
    [System.Obsolete("Deprecated in GDD v0.3/v1.2: Runner no longer has HP or death on collision.")]
    public readonly struct PlayerDeathEvent
    {
    }

    // Obsolete heart restore event (deprecated in GDD v0.3/v1.2).
    [System.Obsolete("Deprecated in GDD v0.3/v1.2: HP/Heart mechanic removed.")]
    public readonly struct RestoreHeartsRequestEvent
    {
        public readonly int HeartCount;

        public RestoreHeartsRequestEvent(int heartCount)
        {
            HeartCount = heartCount;
        }
    }

    // Triggered when Anti faction accumulates sufficient energy to spawn an obstacle vehicle (GDD v1.3).
    public readonly struct RequestCarSpawnEvent
    {
        public readonly int LaneIndex; // 0 = Random, 1 = Left, 2 = Center, 3 = Right

        public RequestCarSpawnEvent(int laneIndex = 0)
        {
            LaneIndex = laneIndex;
        }
    }

    // Published when a non-follower attempts to send runner control commands (GDD v1.3 Section 3.2).
    public readonly struct NonFollowerCommandRejectedEvent
    {
        public readonly string UserId;

        public NonFollowerCommandRejectedEvent(string userId)
        {
            UserId = userId;
        }
    }
}
