using System.Collections;
using UnityEngine;
using SteamRush.Track;
using StreamRushLive.Features.Spawning;

namespace SteamRush.Features.Runner
{
    [RequireComponent(typeof(PlayerCore))]
    public class PlayerCollisionFeature : MonoBehaviour
    {
        [Header("Stun Settings")]
        [SerializeField] private float _stunDuration = 1.5f;
        [SerializeField] private float _stunSpeedMultiplier = 0.3f; // Giảm 70% tốc độ (còn 30%)
        [SerializeField] private float _energyPenaltyPercent = 25f; // Trừ 25% năng lượng

        private bool _isStunned = false;
        private Coroutine _stunCoroutine;
        private Renderer[] _renderers;

        public bool IsStunned => _isStunned;

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>();
        }

        private void OnCollisionEnter(Collision collision)
        {
            HandleCollision(collision.gameObject);
        }

        private void OnTriggerEnter(Collider other)
        {
            HandleCollision(other.gameObject);
        }

        private void HandleCollision(GameObject obj)
        {
            if (_isStunned)
            {
                return;
            }

            // Xử lý nhặt Buff Item
            if (obj.CompareTag("Buff") || obj.name.Contains("Buff"))
            {
                EnergySystem energy = FindFirstObjectByType<EnergySystem>();
                if (energy != null)
                {
                    energy.AddEnergy(20f);
                    Debug.Log("[Player] Đã nhặt Buff Item! Hồi +20% năng lượng!");
                }
                Destroy(obj);
                return;
            }

            if (obj.CompareTag("Obstacle") || obj.name.Contains("Barrier") || obj.name.Contains("Obstacle"))
            {
                TriggerStun();
            }
        }

        public void TriggerStun()
        {
            if (_isStunned)
            {
                return;
            }

            if (_stunCoroutine != null)
            {
                StopCoroutine(_stunCoroutine);
            }

            _stunCoroutine = StartCoroutine(StunRoutine());
        }

        private IEnumerator StunRoutine()
        {
            _isStunned = true;
            Debug.LogWarning($"[StreamRush] Người theo dõi đã vấp ngã! Choáng {_stunDuration}s, tốc độ giảm 70%, trừ {_energyPenaltyPercent}% năng lượng!");

            // 1. Giảm tốc độ thế giới
            WorldSpeedManager speedManager = FindFirstObjectByType<WorldSpeedManager>();
            float originalSpeed = 0f;
            if (speedManager != null)
            {
                originalSpeed = speedManager.CurrentSpeed;
                speedManager.CurrentSpeed = originalSpeed * _stunSpeedMultiplier;
            }

            // 2. Trừ năng lượng
            EnergySystem energySystem = FindFirstObjectByType<EnergySystem>();
            if (energySystem != null)
            {
                energySystem.AddEnergy(-_energyPenaltyPercent);
            }

            // 3. Hiệu ứng nhấp nháy (i-frames)
            float elapsed = 0f;
            while (elapsed < _stunDuration)
            {
                SetRenderersVisible(elapsed % 0.2f < 0.1f);
                elapsed += Time.deltaTime;
                yield return null;
            }

            SetRenderersVisible(true);

            // 4. Khôi phục tốc độ
            if (speedManager != null)
            {
                // Nếu có EnergySystem, khôi phục theo tốc độ EnergySystem tính
                if (energySystem != null)
                {
                    speedManager.CurrentSpeed = energySystem.GetCurrentSpeed();
                }
                else
                {
                    speedManager.CurrentSpeed = originalSpeed;
                }
            }

            _isStunned = false;
            _stunCoroutine = null;
        }

        private void SetRenderersVisible(bool visible)
        {
            if (_renderers == null) return;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                {
                    _renderers[i].enabled = visible;
                }
            }
        }
    }
}