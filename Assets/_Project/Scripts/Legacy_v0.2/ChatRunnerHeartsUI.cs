using UnityEngine;
using UnityEngine.UI;
using SteamRush.Features.Runner;

namespace SteamRush.Features.UI
{
    /// <summary>
    /// Hiển thị 3 Tim Máu của Runner trên màn hình theo GDD ChatLand.
    /// </summary>
    public class ChatRunnerHeartsUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RunnerHealthSystem _healthSystem;
        [SerializeField] private Image[] _heartImages;

        [Header("Colors")]
        [SerializeField] private Color _activeHeartColor = new Color(1f, 0.2f, 0.3f, 1f); // Đỏ tươi
        [SerializeField] private Color _emptyHeartColor = new Color(0.2f, 0.2f, 0.2f, 0.4f); // Xám mờ

        private void Awake()
        {
            if (_healthSystem == null)
            {
                _healthSystem = FindFirstObjectByType<RunnerHealthSystem>();
            }
        }

        private void Update()
        {
            if (_healthSystem == null)
            {
                _healthSystem = FindFirstObjectByType<RunnerHealthSystem>();
                if (_healthSystem == null) return;
            }

            UpdateHeartsDisplay(_healthSystem.CurrentHealth);
        }

        private void UpdateHeartsDisplay(int currentHealth)
        {
            if (_heartImages == null) return;

            for (int i = 0; i < _heartImages.Length; i++)
            {
                if (_heartImages[i] != null)
                {
                    bool isAlive = (i < currentHealth);
                    _heartImages[i].color = isAlive ? _activeHeartColor : _emptyHeartColor;
                }
            }
        }
    }
}
