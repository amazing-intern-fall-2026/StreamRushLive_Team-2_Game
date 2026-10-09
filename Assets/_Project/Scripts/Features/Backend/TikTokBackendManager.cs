using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace SteamRush.Features.Backend
{
    /// <summary>
    /// Central Manager & Facade for the TikTok Live backend service lifecycle.
    /// Coordinates modular sub-components according to the Single Responsibility Principle (SRP):
    /// - BackendPortChecker: Checks socket and HTTP health endpoints.
    /// - BackendProcessController: Launches, monitors, and terminates the backend process.
    /// - BackendInstaller: Auto-installs Bun, clones from Git, and manages .env files.
    /// </summary>
    public class TikTokBackendManager : MonoBehaviour
    {
        private static TikTokBackendManager _instance;
        public static TikTokBackendManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<TikTokBackendManager>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("TikTokBackendManager");
                        _instance = go.AddComponent<TikTokBackendManager>();
                        if (Application.isPlaying) DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }

        public const string DefaultGitRepo = BackendInstaller.DefaultGitRepo;
        public const string DefaultEulerApiKey = "";
        public const int DefaultHttpPort = 9091;
        public const int DefaultSocketPort = 3001;

        public static string DefaultLocalPath => GetDefaultBackendDirectory();

        [Header("Modular Sub-Components (SRP)")]
        [SerializeField] private BackendPortChecker _portChecker;
        [SerializeField] private BackendProcessController _processController;
        [SerializeField] private BackendInstaller _installer;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            if (Application.isPlaying) DontDestroyOnLoad(gameObject);

            EnsureSubComponents();
        }

        private void EnsureSubComponents()
        {
            if (_portChecker == null) _portChecker = GetComponent<BackendPortChecker>() ?? gameObject.AddComponent<BackendPortChecker>();
            if (_processController == null) _processController = GetComponent<BackendProcessController>() ?? gameObject.AddComponent<BackendProcessController>();
            if (_installer == null) _installer = GetComponent<BackendInstaller>() ?? gameObject.AddComponent<BackendInstaller>();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        public static string GetDefaultBackendDirectory()
        {
            string baseFolder = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(baseFolder) || !Directory.Exists(baseFolder))
            {
                string userProfile = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
                baseFolder = Path.Combine(userProfile, "AppData", "Local");
            }

            string targetDir = Path.Combine(baseFolder, "backendLiveGame");
            try
            {
                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TikTokBackendManager] Could not auto-create directory '{targetDir}': {ex.Message}");
            }

            return targetDir;
        }

        public static string BrowseFolderDialog(string title = "Select Folder", string defaultPath = "")
        {
            string startDir = !string.IsNullOrEmpty(defaultPath) && Directory.Exists(defaultPath)
                ? defaultPath
                : GetDefaultBackendDirectory();

#if UNITY_EDITOR
            string selected = UnityEditor.EditorUtility.OpenFolderPanel(title, startDir, "");
            if (!string.IsNullOrEmpty(selected))
            {
                return selected.Replace('/', '\\');
            }
            return null;
#else
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -Command \"Add-Type -AssemblyName System.Windows.Forms; $f = New-Object System.Windows.Forms.FolderBrowserDialog; $f.Description = '{title}'; $f.SelectedPath = '{startDir.Replace("'", "''")}'; if($f.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK){{ [Console]::Out.Write($f.SelectedPath) }}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                using (var p = Process.Start(psi))
                {
                    string path = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(30000);
                    if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path.Trim()))
                    {
                        return path.Trim();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TikTokBackendManager] Folder picker error: {ex.Message}");
            }
            return null;
#endif
        }

        #region Facade Delegation Methods

        public void CheckPort(int port, Action<bool, string> onComplete)
        {
            EnsureSubComponents();
            _portChecker.CheckPort(port, onComplete);
        }

        public void CheckHttpHealth(int port, Action<bool, string> onComplete)
        {
            EnsureSubComponents();
            _portChecker.CheckHttpHealth(port, onComplete);
        }

        public void StartBackend(string backendDir, int httpPort, int socketPort, string eulerApiKey, Action<bool, string> onComplete)
        {
            EnsureSubComponents();
            _processController.StartBackend(backendDir, httpPort, socketPort, eulerApiKey, _portChecker, onComplete);
        }

        public void SetupBackendFromGit(string destinationDir, int httpPort, int socketPort, string eulerApiKey, Action<bool, string> onComplete, Action<string> onProgress = null)
        {
            EnsureSubComponents();
            _installer.SetupBackendFromGit(destinationDir, httpPort, socketPort, eulerApiKey, onComplete, onProgress);
        }

        public void StopBackend(int httpPort, int socketPort, Action<bool, string> onComplete)
        {
            EnsureSubComponents();
            _processController.StopBackend(httpPort, socketPort, _portChecker, onComplete);
        }

        #endregion
    }
}
