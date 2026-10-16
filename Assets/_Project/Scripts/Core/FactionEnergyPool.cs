using System;
using UnityEngine;
using UnityEngine.Events;

namespace SteamRush.Features.StreamIntegration
{
    /// <summary>
    /// Reusable energy resource pool component for both Team Blue (Fan) and Team Red (Anti) factions.
    /// Manages capacity, accumulation from likes/gifts, and consumption for abilities and obstacles.
    /// </summary>
    public class FactionEnergyPool : MonoBehaviour
    {
        [Header("Faction Configuration")]
        [SerializeField] private FactionType _faction = FactionType.Blue;
        [SerializeField] private int _maxEnergy = 1000;
        [SerializeField] private int _initialEnergy = 100;

        [Header("Runtime State")]
        [SerializeField] private int _currentEnergy;

        [Serializable] public class EnergyChangedEvent : UnityEvent<int, int> { }
        [SerializeField] private EnergyChangedEvent _onEnergyChanged = new EnergyChangedEvent();

        /// <summary>Invoked whenever energy changes: (currentEnergy, maxEnergy)</summary>
        public EnergyChangedEvent OnEnergyChangedEvent => _onEnergyChanged;

        /// <summary>C# event for high-performance decoupled listeners: (currentEnergy, maxEnergy)</summary>
        public event Action<int, int> OnEnergyChanged;

        public FactionType Faction
        {
            get => _faction;
            set => _faction = value;
        }

        public int CurrentEnergy => _currentEnergy;
        public int MaxEnergy => _maxEnergy;
        public float NormalizedEnergy => _maxEnergy > 0 ? Mathf.Clamp01((float)_currentEnergy / _maxEnergy) : 0f;
        public bool IsFull => _currentEnergy >= _maxEnergy;

        private void Awake()
        {
            if (_currentEnergy <= 0 && _initialEnergy > 0)
            {
                _currentEnergy = Mathf.Clamp(_initialEnergy, 0, _maxEnergy);
            }
        }

        /// <summary>
        /// Explicit initialization when dynamically instantiated by FactionTugOfWarManager.
        /// </summary>
        public void Initialize(FactionType faction, int initialEnergy, int maxEnergy)
        {
            _faction = faction;
            _maxEnergy = Mathf.Max(1, maxEnergy);
            _currentEnergy = Mathf.Clamp(initialEnergy, 0, _maxEnergy);
            NotifyChanged();
        }

        /// <summary>
        /// Sets maximum energy capacity.
        /// </summary>
        public void SetMaxEnergy(int max)
        {
            _maxEnergy = Mathf.Max(1, max);
            _currentEnergy = Mathf.Clamp(_currentEnergy, 0, _maxEnergy);
            NotifyChanged();
        }

        /// <summary>
        /// Directly sets energy level clamped to [0, MaxEnergy].
        /// </summary>
        public void SetEnergy(int amount)
        {
            _currentEnergy = Mathf.Clamp(amount, 0, _maxEnergy);
            NotifyChanged();
        }

        /// <summary>
        /// Adds energy to the pool (likes, gifts, positive events).
        /// </summary>
        public void AddEnergy(int amount)
        {
            if (amount <= 0) return;

            _currentEnergy = Mathf.Clamp(_currentEnergy + amount, 0, _maxEnergy);
            NotifyChanged();
        }

        /// <summary>
        /// Checks and consumes a specific amount of energy if available.
        /// </summary>
        public bool TryConsumeEnergy(int amount)
        {
            if (amount <= 0) return true;
            if (_currentEnergy < amount) return false;

            _currentEnergy -= amount;
            NotifyChanged();
            return true;
        }

        /// <summary>
        /// Deducts a percentage of maximum capacity (0..100%).
        /// E.g. 25% on 1000 max capacity deducts 250 points.
        /// </summary>
        public bool TryConsumePercent(float percent)
        {
            if (percent <= 0f) return true;

            int pointsToDeduct = Mathf.RoundToInt((percent / 100f) * _maxEnergy);
            int toDeduct = Mathf.Min(pointsToDeduct, _currentEnergy);
            _currentEnergy = Mathf.Max(0, _currentEnergy - toDeduct);
            NotifyChanged();
            return _currentEnergy > 0;
        }

        private void NotifyChanged()
        {
            _onEnergyChanged?.Invoke(_currentEnergy, _maxEnergy);
            OnEnergyChanged?.Invoke(_currentEnergy, _maxEnergy);
        }
    }
}
