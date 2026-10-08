using TMPro;
using UnityEngine;

namespace SteamRush.Features.UI.Views
{
    // View: badge pill displaying elapsed match time (mm:ss) below ProgressBar.
    public class ElapsedTimeStopwatch : MonoBehaviour
    {
        [SerializeField] private TMP_Text timeText;

        // Cap at 999:59 per design specification
        private const float MaxSeconds = 999 * 60 + 59;

        private float _elapsedSeconds;
        private bool _isRunning = true;

        // Read by VictoryCeremonyController to display final clear time in victory popup.
        public string CurrentDisplayText => timeText != null ? timeText.text : "00:00";

        public void SetDisplayVisible(bool isVisible)
        {
            var cg = GetComponent<CanvasGroup>();
            if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
            cg.alpha = isVisible ? 1f : 0f;
            cg.interactable = isVisible;
            cg.blocksRaycasts = isVisible;
        }

        private void Update()
        {
            if (!_isRunning) return;

            _elapsedSeconds = Mathf.Min(_elapsedSeconds + Time.deltaTime, MaxSeconds);
            UpdateDisplay();
        }

        // Called by VictoryCeremonyController to freeze final clear time when crossing finish line.
        public void StopTimer()
        {
            _isRunning = false;
        }

        public void ResumeTimer()
        {
            _isRunning = true;
        }

        public void ResetTimer()
        {
            _elapsedSeconds = 0f;
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            if (timeText == null) return;

            int totalSeconds = Mathf.FloorToInt(_elapsedSeconds);
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            timeText.text = $"{minutes:D2}:{seconds:D2}";
        }
    }
}
