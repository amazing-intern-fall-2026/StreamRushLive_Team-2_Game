using DG.Tweening;
using UnityEngine;
using StreamRushLive.Features.Spawning;

namespace SteamRush.Features.Runner
{
    // Controls activation and animation of the protective shield hologram VFX around Runner.
    // Polls RunnerItemEffects.IsShieldActive matching ShieldTimerCircle pattern.
    public class ShieldVFXController : MonoBehaviour
    {
        [SerializeField] private RunnerItemEffects itemEffects;
        [SerializeField] private GameObject shieldVFXPrefab;

        [Tooltip("Relative position to Runner root to center around character torso.")]
        [SerializeField] private Vector3 localOffset = new Vector3(0f, 1.1f, 0f);

        [Tooltip("Scale multiplier relative to original prefab dimensions.")]
        [SerializeField] private float scaleMultiplier = 1f;

        [SerializeField] private float animDuration = 0.35f;

        private GameObject _instance;
        private bool _isActive;

        private void Awake()
        {
            if (itemEffects == null)
            {
                itemEffects = GetComponent<RunnerItemEffects>();
                if (itemEffects == null) itemEffects = GetComponentInParent<RunnerItemEffects>();
            }
        }

        private void Update()
        {
            if (itemEffects == null) return;

            bool active = itemEffects.IsShieldActive;
            if (active == _isActive) return;

            _isActive = active;
            if (active) Show();
            else Hide();
        }

        private void Show()
        {
            if (shieldVFXPrefab == null) return;

            if (_instance != null)
            {
                DOTween.Kill(_instance.transform);
                Destroy(_instance);
            }

            _instance = Instantiate(shieldVFXPrefab, transform);
            _instance.transform.localPosition = localOffset;
            _instance.transform.localRotation = Quaternion.identity;

            Vector3 fullScale = shieldVFXPrefab.transform.localScale * scaleMultiplier;
            _instance.transform.localScale = Vector3.zero;
            _instance.transform.DOScale(fullScale, animDuration).SetEase(Ease.OutBack);
        }

        private void Hide()
        {
            if (_instance == null) return;

            GameObject toDestroy = _instance;
            _instance = null;

            DOTween.Kill(toDestroy.transform);
            toDestroy.transform.DOScale(Vector3.zero, animDuration * 0.6f)
                .SetEase(Ease.InBack)
                .OnComplete(() =>
                {
                    if (toDestroy != null) Destroy(toDestroy);
                });
        }

        private void OnDisable()
        {
            if (_instance != null)
            {
                DOTween.Kill(_instance.transform);
                Destroy(_instance);
                _instance = null;
            }
            _isActive = false;
        }
    }
}
