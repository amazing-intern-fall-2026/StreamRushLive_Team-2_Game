using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SteamRush.Core;
using SteamRush.Features.StreamIntegration;
using SteamRush.Features.Backend;

namespace SteamRush.Features.UI.PreGameConfig
{
    /// <summary>
    /// Handles TikTok Live room connectivity verification and Go Live authorization.
    /// Manages network handshakes, event listeners, status visual cues, and Go Live button states.
    /// </summary>
    public class PreGameLiveVerifier : MonoBehaviour
    {
        [Header("UI Controls")]
        [SerializeField] private Button _btnCheckLive;
        [SerializeField] private TextMeshProUGUI _txtLiveStatus;
        [SerializeField] private Button _btnGoLive;

        private bool _isLiveVerified = false;
        private string _verifiedUsername = "";
        private Coroutine _checkLiveCoroutine;

        public bool IsLiveVerified => _isLiveVerified;
        public string VerifiedUsername => _verifiedUsername;

        public void Initialize(Action onCheckLiveClicked, Action onGoLiveClicked)
        {
            if (_btnCheckLive != null)
            {
                _btnCheckLive.onClick.RemoveAllListeners();
                _btnCheckLive.onClick.AddListener(() => onCheckLiveClicked?.Invoke());
            }

            if (_btnGoLive != null)
            {
                _btnGoLive.onClick.RemoveAllListeners();
                _btnGoLive.onClick.AddListener(() => onGoLiveClicked?.Invoke());
            }

            SetLiveVerifiedUI(false, "NOT VERIFIED");
        }

        private void OnEnable()
        {
            if (!_isLiveVerified)
            {
                SetLiveVerifiedUI(false, "NOT VERIFIED");
            }
        }

        public void OnUsernameChanged(string newUsername)
        {
            string clean = newUsername?.Trim().TrimStart('@') ?? "";
            if (_isLiveVerified && !string.Equals(clean, _verifiedUsername, StringComparison.OrdinalIgnoreCase))
            {
                _isLiveVerified = false;
                _verifiedUsername = "";
                SetLiveVerifiedUI(false, "NOT VERIFIED");
            }
        }

        public void SetLiveVerifiedUI(bool isVerified, string statusText)
        {
            _isLiveVerified = isVerified;
            if (_txtLiveStatus != null)
            {
                if (isVerified)
                {
                    _txtLiveStatus.text = $"<color=#30E070>{statusText}</color>";
                }
                else if (statusText.Contains("CHECKING"))
                {
                    _txtLiveStatus.text = $"<color=#FCBA03>{statusText}</color>";
                }
                else if (statusText.Contains("OFFLINE") || statusText.Contains("FAILED") || statusText.Contains("NOT FOUND") || statusText.Contains("ERROR") || statusText.Contains("TIMEOUT") || statusText.Contains("OFF") || statusText.Contains("VERIFY") || statusText.Contains("USER"))
                {
                    _txtLiveStatus.text = $"<color=#FF4050>{statusText}</color>";
                }
                else
                {
                    _txtLiveStatus.text = $"<color=#88A0B0>{statusText}</color>";
                }
            }

            if (_btnGoLive != null)
            {
                _btnGoLive.interactable = isVerified;
                var btnImg = _btnGoLive.GetComponent<Image>();
                var btnTxt = _btnGoLive.GetComponentInChildren<TextMeshProUGUI>();
                if (btnImg != null)
                {
                    btnImg.color = isVerified ? Color.white : new Color(0.45f, 0.45f, 0.45f, 0.65f);
                }
                if (btnTxt != null)
                {
                    btnTxt.color = isVerified ? Color.black : new Color(0.3f, 0.3f, 0.3f, 0.7f);
                }
            }
        }

        public void VerifyLive(string rawUsername, int socketPort, Action<bool, string> onComplete = null)
        {
            if (_checkLiveCoroutine != null)
            {
                StopCoroutine(_checkLiveCoroutine);
            }
            _checkLiveCoroutine = StartCoroutine(CheckLiveRoutine(rawUsername, socketPort, onComplete));
        }

        private IEnumerator CheckLiveRoutine(string rawUsername, int socketPort, Action<bool, string> onComplete)
        {
            string username = rawUsername?.Trim().TrimStart('@') ?? "";
            if (string.IsNullOrEmpty(username))
            {
                SetLiveVerifiedUI(false, "ENTER @USER");
                onComplete?.Invoke(false, "ENTER @USER");
                yield break;
            }

            SetLiveVerifiedUI(false, "CHECKING...");
            if (_btnCheckLive != null) _btnCheckLive.interactable = false;

            // 1. Check if backend port is responding
            bool portChecked = false;
            bool portOpen = false;
            TikTokBackendManager.Instance.CheckPort(socketPort, (isOpen, _) =>
            {
                portOpen = isOpen;
                portChecked = true;
            });

            float portWait = 2.5f;
            while (!portChecked && portWait > 0f)
            {
                portWait -= Time.unscaledDeltaTime;
                yield return null;
            }

            if (!portOpen)
            {
                SetLiveVerifiedUI(false, "BACKEND OFF");
                if (_btnCheckLive != null) _btnCheckLive.interactable = true;
                Debug.LogWarning($"[PreGameLiveVerifier] Backend socket port {socketPort} is closed. Please start backend in Advanced tab!");
                onComplete?.Invoke(false, "BACKEND OFF");
                yield break;
            }

            // 2. Connect client to test room connection
            var client = FindFirstObjectByType<TikTokLiveClient>();
            if (client == null)
            {
                var go = new GameObject("TikTokLiveClient");
                client = go.AddComponent<TikTokLiveClient>();
            }

            client.SetServerUrl($"http://localhost:{socketPort}");

            bool connected = false;
            bool disconnected = false;
            string disconnectReason = "";
            string roomId = "";

            Action<TikTokConnectedEvent> onConn = evt =>
            {
                connected = true;
                roomId = evt.RoomId;
            };
            Action<TikTokDisconnectedEvent> onDisc = evt =>
            {
                disconnected = true;
                disconnectReason = evt.Reason;
            };

            EventBus.Subscribe(onConn);
            EventBus.Subscribe(onDisc);

            client.ConnectWithUsername(username);

            float timeout = 12f;
            float elapsed = 0f;
            while (!connected && !disconnected && elapsed < timeout)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            EventBus.Unsubscribe(onConn);
            EventBus.Unsubscribe(onDisc);

            if (connected)
            {
                _verifiedUsername = username;
                SetLiveVerifiedUI(true, "CONNECTED");
                Debug.Log($"<color=#30E070>[PreGameLiveVerifier] TikTok Live connection verified for @{username} (Room: {roomId})</color>");
                onComplete?.Invoke(true, "CONNECTED");
            }
            else if (disconnected)
            {
                SetLiveVerifiedUI(false, "OFFLINE");
                Debug.LogWarning($"[PreGameLiveVerifier] TikTok Live connection failed for @{username}: {disconnectReason}");
                onComplete?.Invoke(false, "OFFLINE");
            }
            else
            {
                SetLiveVerifiedUI(false, "TIMEOUT");
                Debug.LogWarning($"[PreGameLiveVerifier] TikTok Live connection check timed out for @{username}. Verify user is live.");
                onComplete?.Invoke(false, "TIMEOUT");
            }

            if (_btnCheckLive != null) _btnCheckLive.interactable = true;
            _checkLiveCoroutine = null;
        }

        public void AutoWireIfNull(Transform root)
        {
            if (root == null) root = transform;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                if (_btnCheckLive == null && (n == "Btn_CheckLive" || n == "BtnCheckLive")) _btnCheckLive = t.GetComponent<Button>();
                if (_txtLiveStatus == null && (n == "Txt_LiveStatus" || n == "TxtLiveStatus")) _txtLiveStatus = t.GetComponent<TextMeshProUGUI>();
                if (_btnGoLive == null && (n == "Btn_GoLive" || n == "BtnGoLive")) _btnGoLive = t.GetComponent<Button>();
            }
        }
    }
}
