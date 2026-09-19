using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using StreamRushLive.Features.Spawning;

namespace SteamRush.Features.Runner
{
    public class MockChatConsole : MonoBehaviour
    {
        [Header("Chat Input")]
        [SerializeField] private TMP_InputField chatInputField;

        [Header("Runner Components")]
        [SerializeField] private RunnerItemEffects itemEffects;
        [SerializeField] private InstantHealItem instantHealItem;

        [Header("Mock Settings")]
        [SerializeField] private float shieldDuration = 20f;

        private void Awake()
        {
            if (itemEffects == null)
            {
                itemEffects = GetComponent<RunnerItemEffects>();

                if (itemEffects == null)
                {
                    itemEffects = GetComponentInParent<RunnerItemEffects>();
                }
            }

            if (instantHealItem == null)
            {
                instantHealItem = GetComponent<InstantHealItem>();

                if (instantHealItem == null)
                {
                    instantHealItem = GetComponentInParent<InstantHealItem>();
                }
            }

            if (chatInputField != null)
            {
                chatInputField.onSubmit.AddListener(OnChatSubmitted);
            }
            else
            {
                Debug.LogWarning("[MockChatConsole] Chưa gán Chat Input Field!");
            }
        }

        private void OnDestroy()
        {
            if (chatInputField != null)
            {
                chatInputField.onSubmit.RemoveListener(OnChatSubmitted);
            }
        }

        private void Update()
        {
            if (Keyboard.current == null)
                return;

            if (Keyboard.current.f1Key.wasPressedThisFrame)
                MockDonateShield();

            if (Keyboard.current.f2Key.wasPressedThisFrame)
                MockDonateHeal();

            if (Keyboard.current.f3Key.wasPressedThisFrame)
                MockFanLikes();

            if (Keyboard.current.f4Key.wasPressedThisFrame)
                MockAntiLikes();

            if (Keyboard.current.f5Key.wasPressedThisFrame)
                MockNewFollower();
        }

        private void OnChatSubmitted(string rawInput)
        {
            Debug.Log($"[MockChatConsole] Raw chat: \"{rawInput}\"");

            if (string.IsNullOrWhiteSpace(rawInput))
            {
                ClearInputField();
                return;
            }

            // ChatCommandSanitizer sẽ được tích hợp ở bước sau.
            // Hiện tại chỉ nhận và log raw chat để kiểm tra InputField.

            ClearInputField();
        }

        private void MockDonateShield()
        {
            if (itemEffects == null)
            {
                Debug.LogWarning(
                    "[MockChatConsole] Không tìm thấy RunnerItemEffects trên Runner."
                );
                return;
            }

            itemEffects.ActivateShield(shieldDuration);

            Debug.Log(
                $"[MockChatConsole] F1 -> Donate Shield. Shield hoạt động {shieldDuration}s."
            );
        }

        private void MockDonateHeal()
        {
            if (instantHealItem == null)
            {
                Debug.LogWarning(
                    "[MockChatConsole] Không tìm thấy InstantHealItem trên Runner."
                );
                return;
            }

            instantHealItem.ActivateHeal();

            Debug.Log("[MockChatConsole] F2 -> Donate Instant Heal.");
        }

        private void MockFanLikes()
        {
            Debug.Log("[MockChatConsole] F3 -> +20 tim Fan.");
        }

        private void MockAntiLikes()
        {
            Debug.Log("[MockChatConsole] F4 -> +50 tim Anti.");
        }

        private void MockNewFollower()
        {
            Debug.Log("[MockChatConsole] F5 -> Thêm follower mới.");
        }

        private void ClearInputField()
        {
            if (chatInputField == null)
            {
                return;
            }

            chatInputField.text = "";
            chatInputField.ActivateInputField();
        }
    }
}