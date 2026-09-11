namespace SteamRush.Features.Runner
{
    using UnityEngine;

    /// Chịu trách nhiệm DUY NHẤT: tính độ dịch chuyển lùi (knockback) mỗi frame khi va chạm vật
    /// cản, giảm dần theo easing (mạnh lúc đầu, nhẹ dần cuối). Plain C# class, không phụ thuộc
    /// MonoBehaviour, dễ test độc lập.

    public class KnockbackHandler
    {
        private float _totalDistance;
        private float _duration;
        private float _elapsed;

        public bool IsKnockingBack => _elapsed < _duration;

        public void BeginKnockback(float distance, float duration)
        {
            _totalDistance = distance;
            _duration = Mathf.Max(0.01f, duration);
            _elapsed = 0f;
        }

        /// <summary>Trả về độ dịch chuyển lùi (giá trị dương) cần áp dụng trong frame này.</summary>
        public float GetBackwardDelta(float deltaTime)
        {
            if (!IsKnockingBack) return 0f;

            float previous = _totalDistance * EaseOut(_elapsed / _duration);
            _elapsed = Mathf.Min(_duration, _elapsed + deltaTime);
            float current = _totalDistance * EaseOut(_elapsed / _duration);

            return current - previous;
        }

        private float EaseOut(float t)
        {
            float clamped = Mathf.Clamp01(t);
            return 1f - Mathf.Pow(1f - clamped, 2f);
        }
    }
}