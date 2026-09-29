using TMPro;
using UnityEngine;

namespace SteamRush.Features.UI.Views
{
    // View: badge pill hiển thị thời gian trận đấu tăng dần (mm:ss), đặt ngay dưới ProgressBar.
    // Đếm từ khi component được bật (Awake) - chưa có sự kiện "bắt đầu trận" riêng trong codebase.
    public class ElapsedTimeStopwatch : MonoBehaviour
    {
        [SerializeField] private TMP_Text timeText;

        // Cap 999:59 theo spec (mm 3 chữ số tối đa) - tránh tràn hiển thị nếu live kéo dài bất thường.
        private const float MaxSeconds = 999 * 60 + 59;

        private float _elapsedSeconds;
        private bool _isRunning = true;

        // VictoryCeremonyController doc lai text mm:ss cuoi cung de hien trong popup vinh danh -
        // doc thang tu timeText da cap nhat san, khong lap lai logic format o day.
        public string CurrentDisplayText => timeText != null ? timeText.text : "00:00";

        private void Update()
        {
            if (!_isRunning) return;

            _elapsedSeconds = Mathf.Min(_elapsedSeconds + Time.deltaTime, MaxSeconds);
            UpdateDisplay();
        }

        // Goi tu VictoryCeremonyController de dong bang thoi gian hoan thanh cuoi cung khi qua dich.
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
