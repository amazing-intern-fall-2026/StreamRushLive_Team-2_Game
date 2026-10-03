using System.Collections.Generic;
using UnityEngine;
using SteamRush.Core;

namespace SteamRush.Features.StreamIntegration
{
    /// <summary>
    /// Listens for TikTok like events via EventBus and converts cumulative likes into faction energy.
    /// Supports configurable step increments and audio feedback.
    /// </summary>
    [DisallowMultipleComponent]
    public class TikTokLikeAdapter : MonoBehaviour
    {
        [Header("Likes Conversion")]
        [Tooltip("Likes threshold to award energy (e.g. 20 likes = 1 step).")]
        [SerializeField] private int _likesPerEnergyStep = 20;

        [Tooltip("Energy points awarded per step reached.")]
        [SerializeField] private int _energyPerStep = 5;

        [Header("Gameplay Subsystems")]
        [SerializeField] private FactionTugOfWarManager _factionManager;

        [Header("Diagnostics")]
        [SerializeField] private bool _logEvents = true;

        private readonly Dictionary<string, int> _processedLikeStepsByUser = new Dictionary<string, int>();

        public int LikesPerEnergyStep
        {
            get => _likesPerEnergyStep;
            set => _likesPerEnergyStep = Mathf.Max(1, value);
        }

        public int EnergyPerStep
        {
            get => _energyPerStep;
            set => _energyPerStep = Mathf.Max(1, value);
        }

        private void Awake()
        {
            EnsureReferences();
        }

        private void OnEnable()
        {
            EnsureReferences();
            EventBus.Subscribe<TikTokLikeEvent>(OnLikeReceived);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<TikTokLikeEvent>(OnLikeReceived);
        }

        public void ResetProgress()
        {
            _processedLikeStepsByUser.Clear();
        }

        public void EnsureReferences()
        {
            if (_factionManager == null) _factionManager = FindFirstObjectByType<FactionTugOfWarManager>();
        }

        private void OnLikeReceived(TikTokLikeEvent evt)
        {
            EnsureReferences();

            if (string.IsNullOrEmpty(evt.UserId) || evt.TotalLike <= 0)
            {
                return;
            }

            int newSteps = _likesPerEnergyStep > 0 ? evt.TotalLike / _likesPerEnergyStep : 0;
            _processedLikeStepsByUser.TryGetValue(evt.UserId, out int oldSteps);

            if (newSteps <= oldSteps)
            {
                return;
            }

            _processedLikeStepsByUser[evt.UserId] = newSteps;
            int stepsGained = newSteps - oldSteps;
            int energyAmount = stepsGained * _energyPerStep;

            FactionType faction = _factionManager != null ? _factionManager.GetFaction(evt.UserId) : FactionType.Fan;
            string displayName = !string.IsNullOrEmpty(evt.DisplayName) ? evt.DisplayName : evt.UserId;

            if (_factionManager != null)
            {
                _factionManager.AddLikes(faction, energyAmount);
            }

            AudioManager.Instance?.PlaySFX(SFXType.StreamLike, 0.5f);

            if (_logEvents)
            {
                Debug.Log($"[TikTokLikeAdapter] LIKE: {displayName} ({evt.UserId}) reached {evt.TotalLike} likes (+{stepsGained} steps) -> +{energyAmount} energy for {faction}.");
            }
        }
    }
}
