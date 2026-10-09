using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace StreamRushLive.Features.VFX
{
    /// <summary>
    /// Điều khiển bật/tắt Speed Lines Full Screen Pass Renderer Feature.
    /// Speed Lines được cấu hình trong PC_Renderer.asset.
    /// </summary>
    [DisallowMultipleComponent]
    public class SpeedLinesController : MonoBehaviour
    {
        public static SpeedLinesController Instance { get; private set; }

        [Header("URP Renderer")]
        [Tooltip("PC_Renderer.asset đang chứa Full Screen Pass Renderer Feature.")]
        [SerializeField] private ScriptableRendererData _rendererData;

        [Header("Speed Lines")]
        [Tooltip("Material SpeedLines đang được gắn vào Pass Material.")]
        [SerializeField] private Material _speedLinesMaterial;

        private ScriptableRendererFeature _speedLinesFeature;

        public bool IsActive =>
            _speedLinesFeature != null && _speedLinesFeature.isActive;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            FindSpeedLinesFeature();

            // Speed Lines luôn tắt khi bắt đầu scene.
            SetSpeedLines(false);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void FindSpeedLinesFeature()
        {
            if (_rendererData == null)
            {
                Debug.LogError(
                    "[SpeedLinesController] Chưa gán PC_Renderer vào Renderer Data."
                );
                return;
            }

            if (_speedLinesMaterial == null)
            {
                Debug.LogError(
                    "[SpeedLinesController] Chưa gán SpeedLines Material."
                );
                return;
            }

            foreach (ScriptableRendererFeature feature in _rendererData.rendererFeatures)
            {
                if (feature == null)
                {
                    continue;
                }

                if (feature is FullScreenPassRendererFeature fullScreenFeature)
                {
                    if (fullScreenFeature.passMaterial == _speedLinesMaterial)
                    {
                        _speedLinesFeature = feature;

                        Debug.Log(
                            "[SpeedLinesController] Đã tìm thấy Full Screen Pass Renderer Feature của SpeedLines."
                        );

                        return;
                    }
                }
            }

            Debug.LogError(
                "[SpeedLinesController] Không tìm thấy Full Screen Pass Renderer Feature " +
                "đang dùng Material SpeedLines trong PC_Renderer."
            );
        }

        public void SetSpeedLines(bool active)
        {
            if (_speedLinesFeature == null)
            {
                FindSpeedLinesFeature();
            }

            if (_speedLinesFeature == null)
            {
                return;
            }

            _speedLinesFeature.SetActive(active);

            Debug.Log(
                $"[SpeedLinesController] Speed Lines = {(active ? "ON" : "OFF")}"
            );
        }

        public void Activate()
        {
            SetSpeedLines(true);
        }

        public void Deactivate()
        {
            SetSpeedLines(false);
        }
    }
}