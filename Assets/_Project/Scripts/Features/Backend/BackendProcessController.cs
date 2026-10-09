using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace SteamRush.Features.Backend
{
    /// <summary>
    /// Manages launching, batch script execution, monitoring, and terminating the backend node/bun process.
    /// Single responsibility: Backend process lifecycle.
    /// </summary>
    public class BackendProcessController : MonoBehaviour
    {
        private Process _runningBackendProcess;

        public bool IsProcessRunning => _runningBackendProcess != null && !_runningBackendProcess.HasExited;

        private void OnApplicationQuit()
        {
            TerminateProcess();
        }

        public void TerminateProcess()
        {
            try
            {
                if (_runningBackendProcess != null && !_runningBackendProcess.HasExited)
                {
                    _runningBackendProcess.Kill();
                    _runningBackendProcess = null;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BackendProcessController] Terminate error: {ex.Message}");
            }
        }

        public void StartBackend(
            string backendDir,
            int httpPort,
            int socketPort,
            string eulerApiKey,
            BackendPortChecker portChecker,
            Action<bool, string> onComplete)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                var enumerator = StartBackendCoroutine(backendDir, httpPort, socketPort, eulerApiKey, portChecker, onComplete);
                UnityEditor.EditorApplication.CallbackFunction updateCallback = null;
                updateCallback = () =>
                {
                    if (enumerator == null || !enumerator.MoveNext())
                    {
                        UnityEditor.EditorApplication.update -= updateCallback;
                    }
                };
                UnityEditor.EditorApplication.update += updateCallback;
                return;
            }
#endif
            StartCoroutine(StartBackendCoroutine(backendDir, httpPort, socketPort, eulerApiKey, portChecker, onComplete));
        }

        private IEnumerator StartBackendCoroutine(
            string backendDir,
            int httpPort,
            int socketPort,
            string eulerApiKey,
            BackendPortChecker portChecker,
            Action<bool, string> onComplete)
        {
            if (string.IsNullOrWhiteSpace(backendDir))
            {
                backendDir = TikTokBackendManager.GetDefaultBackendDirectory();
            }

            if (!Directory.Exists(backendDir))
            {
                Directory.CreateDirectory(backendDir);
            }

            string serverFile = Path.Combine(backendDir, "src", "server.ts");
            if (!File.Exists(serverFile))
            {
                string existingRef = @"D:\Download\TikTok-Live_Dev_Nhom5-main\backend";
                if (Directory.Exists(existingRef) && File.Exists(Path.Combine(existingRef, "src", "server.ts")))
                {
                    BackendInstaller.CopyDirectory(existingRef, backendDir);
                }
            }

            if (!File.Exists(Path.Combine(backendDir, "src", "server.ts")))
            {
                onComplete?.Invoke(false, $"Backend source files not found in '{backendDir}'. Please click 'AUTO SETUP (GIT CLONE)' to install!");
                yield break;
            }

            // 1. Write .env
            if (!BackendInstaller.WriteEnvFile(backendDir, httpPort, eulerApiKey, out string envErr))
            {
                onComplete?.Invoke(false, $"Failed to write .env file: {envErr}");
                yield break;
            }

            // 2. Launch Process via run_backend.bat
            try
            {
                string batPath = EnsureRunBatFile(backendDir, httpPort, socketPort);
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/k \"{batPath}\"",
                    WorkingDirectory = backendDir,
                    UseShellExecute = true,
                    CreateNoWindow = false,
                    WindowStyle = ProcessWindowStyle.Normal
                };

                _runningBackendProcess = Process.Start(psi);
                Debug.Log($"[BackendProcessController] Launched backend process from {batPath}");
            }
            catch (Exception ex)
            {
                onComplete?.Invoke(false, $"Process execution error: {ex.Message}");
                yield break;
            }

            // 3. Poll ports for up to 8 seconds
            bool socketOpen = false;
            bool httpOpen = false;
            float elapsed = 0f;
            float maxWaitTime = 8f;

            yield return new WaitForSecondsRealtime(1.2f);
            elapsed += 1.2f;

            while (elapsed < maxWaitTime)
            {
                socketOpen = BackendPortChecker.ProbeTcpPort(socketPort, 400);
                httpOpen = BackendPortChecker.ProbeTcpPort(httpPort, 400);

                if (socketOpen || httpOpen)
                {
                    break;
                }

                yield return new WaitForSecondsRealtime(0.6f);
                elapsed += 0.6f;
            }

            if (socketOpen || httpOpen)
            {
                onComplete?.Invoke(true, $"Backend launched successfully! Socket.IO: {socketPort} ({(socketOpen ? "ONLINE" : "PENDING")}), HTTP: {httpPort} ({(httpOpen ? "ONLINE" : "PENDING")})");
            }
            else
            {
                onComplete?.Invoke(false, $"Console window opened, but ports are not responding yet (Socket: {socketPort}, HTTP: {httpPort}). Check terminal window.");
            }
        }

        public void StopBackend(int httpPort, int socketPort, BackendPortChecker portChecker, Action<bool, string> onComplete)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                var enumerator = StopBackendCoroutine(httpPort, socketPort, portChecker, onComplete);
                UnityEditor.EditorApplication.CallbackFunction updateCallback = null;
                updateCallback = () =>
                {
                    if (enumerator == null || !enumerator.MoveNext())
                    {
                        UnityEditor.EditorApplication.update -= updateCallback;
                    }
                };
                UnityEditor.EditorApplication.update += updateCallback;
                return;
            }
#endif
            StartCoroutine(StopBackendCoroutine(httpPort, socketPort, portChecker, onComplete));
        }

        private IEnumerator StopBackendCoroutine(int httpPort, int socketPort, BackendPortChecker portChecker, Action<bool, string> onComplete)
        {
            TerminateProcess();
            KillProcessOnPort(httpPort);
            KillProcessOnPort(socketPort);

            yield return new WaitForSecondsRealtime(1.0f);

            bool socketOpen = BackendPortChecker.ProbeTcpPort(socketPort, 400);

            if (!socketOpen)
            {
                onComplete?.Invoke(true, "Backend stopped and ports released successfully.");
            }
            else
            {
                onComplete?.Invoke(false, "Stop command executed, but port is still occupied.");
            }
        }

        private void KillProcessOnPort(int port)
        {
            try
            {
                var psi = new ProcessStartInfo("cmd.exe", $"/c for /f \"tokens=5\" %a in ('netstat -aon ^| findstr :{port}') do taskkill /F /PID %a")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                using (var p = Process.Start(psi))
                {
                    p?.WaitForExit(3000);
                }
            }
            catch { }
        }

        private string EnsureRunBatFile(string backendDir, int httpPort, int socketPort)
        {
            return BackendInstaller.EnsureRunBatFile(backendDir, httpPort, socketPort);
        }
    }
}
