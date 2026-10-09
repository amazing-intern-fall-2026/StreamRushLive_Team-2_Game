using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SteamRush.Features.Backend;

namespace SteamRush.Features.UI.PreGameConfig
{
    /// <summary>
    /// Manages the TikTok Live Backend Service settings view in PreGameConfig UI (Advanced Tab).
    /// Handles port configuration, directory browsing, Euler API key, port connectivity checks, and process controls.
    /// </summary>
    public class PreGameBackendSettingsView : MonoBehaviour
    {
        [Header("Backend Inputs")]
        [SerializeField] private TMP_InputField _inputBackendPort;
        [SerializeField] private TMP_InputField _inputBackendSocketPort;
        [SerializeField] private TMP_InputField _inputEulerApiKey;
        [SerializeField] private TMP_InputField _inputBackendDir;
        [SerializeField] private TextMeshProUGUI _txtBackendStatus;

        [Header("Backend Action Buttons")]
        [SerializeField] private Button _btnBrowseBackendDir;
        [SerializeField] private Button _btnCheckPort;
        [SerializeField] private Button _btnStartBackend;
        [SerializeField] private Button _btnSetupBackend;
        [SerializeField] private Button _btnStopBackend;
        [SerializeField] private Button _btnGetEulerKey;

        public void Initialize(Action onBrowseClicked, Action onCheckPortClicked, Action onStartClicked, Action onSetupClicked, Action onStopClicked)
        {
            if (_btnBrowseBackendDir != null)
            {
                _btnBrowseBackendDir.onClick.RemoveAllListeners();
                _btnBrowseBackendDir.onClick.AddListener(() => onBrowseClicked?.Invoke());
            }

            if (_btnCheckPort != null)
            {
                _btnCheckPort.onClick.RemoveAllListeners();
                _btnCheckPort.onClick.AddListener(() => onCheckPortClicked?.Invoke());
            }

            if (_btnStartBackend != null)
            {
                _btnStartBackend.onClick.RemoveAllListeners();
                _btnStartBackend.onClick.AddListener(() => onStartClicked?.Invoke());
            }

            if (_btnSetupBackend != null)
            {
                _btnSetupBackend.onClick.RemoveAllListeners();
                _btnSetupBackend.onClick.AddListener(() => onSetupClicked?.Invoke());
            }

            if (_btnStopBackend != null)
            {
                _btnStopBackend.onClick.RemoveAllListeners();
                _btnStopBackend.onClick.AddListener(() => onStopClicked?.Invoke());
            }

            if (_btnGetEulerKey != null)
            {
                _btnGetEulerKey.onClick.RemoveAllListeners();
                _btnGetEulerKey.onClick.AddListener(() =>
                {
                    Application.OpenURL("https://eulerstream.com");
                });
            }
        }

        public void BindConfig(PreGameConfigData data)
        {
            if (data == null) return;

            if (_inputBackendPort != null)
                _inputBackendPort.text = (data.backendPort > 0 ? data.backendPort : 9091).ToString();

            if (_inputBackendSocketPort != null)
                _inputBackendSocketPort.text = (data.backendSocketPort > 0 ? data.backendSocketPort : 3001).ToString();

            if (_inputEulerApiKey != null)
                _inputEulerApiKey.text = data.eulerApiKey ?? "";

            if (_inputBackendDir != null)
            {
                _inputBackendDir.text = !string.IsNullOrEmpty(data.backendDirectory)
                    ? data.backendDirectory
                    : TikTokBackendManager.DefaultLocalPath;
            }

            if (_txtBackendStatus != null)
            {
                _txtBackendStatus.text = "Status: Idle";
            }
        }

        public void ReadConfig(PreGameConfigData data)
        {
            if (data == null) return;

            if (_inputBackendPort != null && int.TryParse(_inputBackendPort.text, out int bPort))
                data.backendPort = bPort;

            if (_inputBackendSocketPort != null && int.TryParse(_inputBackendSocketPort.text, out int sPort))
                data.backendSocketPort = sPort;

            if (_inputEulerApiKey != null)
                data.eulerApiKey = _inputEulerApiKey.text.Trim();

            if (_inputBackendDir != null)
                data.backendDirectory = _inputBackendDir.text.Trim();
        }

        public void BrowseBackendDirectory(Action<string> onFolderSelected)
        {
            string current = _inputBackendDir != null ? _inputBackendDir.text : "";
            string selected = TikTokBackendManager.BrowseFolderDialog("Select TikTok Live Backend Folder", current);
            if (!string.IsNullOrEmpty(selected))
            {
                if (_inputBackendDir != null)
                {
                    _inputBackendDir.text = selected;
                }
                onFolderSelected?.Invoke(selected);
            }
        }

        public void CheckPorts(int httpPort, int socketPort)
        {
            UpdateStatus("<color=#FCDA21>Checking ports...</color>");

            TikTokBackendManager.Instance.CheckPort(socketPort, (socketOpen, _) =>
            {
                TikTokBackendManager.Instance.CheckPort(httpPort, (httpOpen, _) =>
                {
                    string sStatus = socketOpen ? "<color=#76D12C>OPEN</color>" : "<color=#FF4D57>CLOSED</color>";
                    string hStatus = httpOpen ? "<color=#76D12C>OPEN</color>" : "<color=#FF4D57>CLOSED</color>";
                    UpdateStatus($"Status: Socket {socketPort}: {sStatus} | HTTP {httpPort}: {hStatus}");
                });
            });
        }

        public void StartBackend(string dir, int httpPort, int socketPort, string apiKey)
        {
            UpdateStatus("<color=#FCDA21>Launching backend & testing ports...</color>");
            Debug.Log($"[PreGameBackendSettingsView] Starting backend: dir='{dir}', http={httpPort}, socket={socketPort}");

            TikTokBackendManager.Instance.StartBackend(dir, httpPort, socketPort, apiKey, (success, msg) =>
            {
                UpdateStatus(success ? $"<color=#76D12C>[OK] {msg}</color>" : $"<color=#FF4D57>[ERROR] {msg}</color>");
                if (success)
                    Debug.Log($"<color=#76D12C>[PreGameBackendSettingsView] {msg}</color>");
                else
                    Debug.LogError($"[PreGameBackendSettingsView] {msg}");
            });
        }

        public void SetupBackend(string dir, int httpPort, int socketPort, string apiKey)
        {
            UpdateStatus("<color=#FCDA21>Checking Bun runtime (bun --version)...</color>");
            Debug.Log($"[PreGameBackendSettingsView] Auto Setup started: dir='{dir}', http={httpPort}, socket={socketPort}");

            TikTokBackendManager.Instance.SetupBackendFromGit(dir, httpPort, socketPort, apiKey, (success, msg) =>
            {
                UpdateStatus(success ? $"<color=#76D12C>[OK] {msg}</color>" : $"<color=#FF4D57>[ERROR] {msg}</color>");
                if (success)
                    Debug.Log($"<color=#76D12C>[PreGameBackendSettingsView] Setup complete: {msg}</color>");
                else
                    Debug.LogError($"[PreGameBackendSettingsView] Setup failed: {msg}");
            }, progressMsg =>
            {
                UpdateStatus($"<color=#FCDA21>{progressMsg}</color>");
                Debug.Log($"[PreGameBackendSettingsView] Setup progress: {progressMsg}");
            });
        }

        public void StopBackend(int httpPort, int socketPort)
        {
            UpdateStatus("<color=#FCDA21>Stopping backend...</color>");

            TikTokBackendManager.Instance.StopBackend(httpPort, socketPort, (success, msg) =>
            {
                UpdateStatus(success ? $"<color=#76D12C>[OK] {msg}</color>" : $"<color=#FF4D57>[ERROR] {msg}</color>");
            });
        }

        private void UpdateStatus(string message)
        {
            if (_txtBackendStatus != null)
            {
                _txtBackendStatus.text = message;
            }
        }

        public void AutoWireIfNull(Transform root)
        {
            if (root == null) root = transform;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;

                // 1. Search by row containers and extract input components
                if (_inputBackendPort == null && n.Contains("Backend HTTP Port"))
                {
                    _inputBackendPort = t.GetComponentInChildren<TMP_InputField>(true);
                }

                if (_inputBackendSocketPort == null && (n.Contains("Socket.IO Port") || n.Contains("SocketPort")))
                {
                    _inputBackendSocketPort = t.GetComponentInChildren<TMP_InputField>(true);
                }

                if (_inputEulerApiKey == null && (n.Contains("Euler API Key") || n.Contains("EulerKey") || n.Contains("ApiKey")))
                {
                    _inputEulerApiKey = t.GetComponentInChildren<TMP_InputField>(true);
                }

                if (_inputBackendDir == null && (n.Contains("Backend Folder Path") || n.Contains("BackendDir") || n.Contains("Directory")))
                {
                    _inputBackendDir = t.GetComponentInChildren<TMP_InputField>(true);
                }

                if (_txtBackendStatus == null && (n == "TxtStatus" || n.Contains("BackendStatus")))
                {
                    _txtBackendStatus = t.GetComponent<TextMeshProUGUI>() ?? t.GetComponentInChildren<TextMeshProUGUI>(true);
                }

                // 2. Fallbacks for direct names
                if (_inputBackendPort == null && n.Contains("Port") && !n.Contains("Socket") && !n.Contains("Btn"))
                    _inputBackendPort = t.GetComponent<TMP_InputField>();
                if (_inputBackendSocketPort == null && n.Contains("SocketPort"))
                    _inputBackendSocketPort = t.GetComponent<TMP_InputField>();
                if (_inputEulerApiKey == null && (n.Contains("EulerKey") || n.Contains("ApiKey")))
                    _inputEulerApiKey = t.GetComponent<TMP_InputField>();
                if (_inputBackendDir == null && (n.Contains("BackendDir") || n.Contains("Directory")))
                    _inputBackendDir = t.GetComponent<TMP_InputField>();

                // 3. Action Buttons
                if (_btnBrowseBackendDir == null && (n.Contains("Browse") || n == "BtnBrowseDir"))
                    _btnBrowseBackendDir = t.GetComponent<Button>();
                if (_btnCheckPort == null && n.Contains("CheckPort"))
                    _btnCheckPort = t.GetComponent<Button>();
                if (_btnStartBackend == null && n.Contains("StartBackend"))
                    _btnStartBackend = t.GetComponent<Button>();
                if (_btnSetupBackend == null && (n.Contains("SetupBackend") || n.Contains("Setup")))
                    _btnSetupBackend = t.GetComponent<Button>();
                if (_btnStopBackend == null && n.Contains("StopBackend"))
                    _btnStopBackend = t.GetComponent<Button>();
                if (_btnGetEulerKey == null && (n.Contains("GetEulerKey") || n == "BtnOpenWeb"))
                    _btnGetEulerKey = t.GetComponent<Button>();
            }
        }
    }
}
