using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

namespace SteamRush.Features.Backend
{
    /// <summary>
    /// Manages the TikTok Live backend service lifecycle:
    /// - Checks whether backend ports (Socket.IO & HTTP) are open and responding.
    /// - Sets up environment variables (.env with PORT and EULER_API_KEY).
    /// - Clones the backend repository from GitHub if missing.
    /// - Launches and terminates the local backend process (via bun/npm).
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

        public const string DefaultGitRepo = "https://github.com/amazing-intern-fall-2026/TikTok-Live_Dev_Nhom5.git";
        public const string DefaultEulerApiKey = "euler_YTJkMTExNjY3ZjFiODZjZDczOWJhZGZjNzRiYTFhMDAzMzM5OGY1ZjQ3MGFkOTdiNzA0Mzgx";
        public const int DefaultHttpPort = 9091;
        public const int DefaultSocketPort = 3001;

        public static string DefaultLocalPath => GetDefaultBackendDirectory();

        public static string GetDefaultBackendDirectory()
        {
            string baseFolder = @"D:\Download";
            if (!Directory.Exists(baseFolder))
            {
                string userProfile = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
                string downloads = Path.Combine(userProfile, "Downloads");
                if (Directory.Exists(downloads))
                {
                    baseFolder = downloads;
                }
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

        /// <summary>
        /// Opens a native system folder picker dialog.
        /// In Unity Editor, uses EditorUtility.OpenFolderPanel.
        /// In standalone Windows, invokes FolderBrowserDialog via PowerShell fallback.
        /// </summary>
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

        private Process _runningBackendProcess;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            if (Application.isPlaying) DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        #region Port & Health Checking

        /// <summary>
        /// Asynchronously checks if a TCP port is open on localhost.
        /// </summary>
        public void CheckPort(int port, Action<bool, string> onComplete)
        {
            StartCoroutine(CheckPortCoroutine(port, onComplete));
        }

        private IEnumerator CheckPortCoroutine(int port, Action<bool, string> onComplete)
        {
            bool isOpen = false;
            string message = "";
            bool finished = false;

            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    using (var client = new TcpClient())
                    {
                        var asyncResult = client.BeginConnect("127.0.0.1", port, null, null);
                        bool success = asyncResult.AsyncWaitHandle.WaitOne(1200);
                        if (success && client.Connected)
                        {
                            client.EndConnect(asyncResult);
                            isOpen = true;
                            message = $"Port {port} is OPEN (Listening)";
                        }
                        else
                        {
                            isOpen = false;
                            message = $"Port {port} is CLOSED (No response)";
                        }
                    }
                }
                catch (Exception ex)
                {
                    isOpen = false;
                    message = $"Port {port} is CLOSED: {ex.Message}";
                }
                finally
                {
                    finished = true;
                }
            });

            float timeout = 2.0f;
            while (!finished && timeout > 0f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }

            onComplete?.Invoke(isOpen, message);
        }

        /// <summary>
        /// Checks HTTP health endpoint http://localhost:{port}/api/health
        /// </summary>
        public void CheckHttpHealth(int port, Action<bool, string> onComplete)
        {
            StartCoroutine(CheckHttpHealthCoroutine(port, onComplete));
        }

        private IEnumerator CheckHttpHealthCoroutine(int port, Action<bool, string> onComplete)
        {
            string url = $"http://localhost:{port}/api/health";
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = 2;
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    onComplete?.Invoke(true, $"HTTP API Healthy (200 OK): {req.downloadHandler.text}");
                }
                else
                {
                    onComplete?.Invoke(false, $"HTTP Error on port {port}: {req.error}");
                }
            }
        }

        #endregion

        #region Environment Configuration (.env)

        /// <summary>
        /// Writes or updates .env in the backend folder.
        /// </summary>
        public bool WriteEnvFile(string backendPath, int port, string eulerApiKey, out string error)
        {
            error = string.Empty;
            try
            {
                if (string.IsNullOrWhiteSpace(backendPath))
                {
                    error = "Backend path is empty.";
                    return false;
                }

                if (!Directory.Exists(backendPath))
                {
                    Directory.CreateDirectory(backendPath);
                }

                string envPath = Path.Combine(backendPath, ".env");
                string content = $"PORT={port}\nEULER_API_KEY={eulerApiKey.Trim()}\n";
                File.WriteAllText(envPath, content);
                Debug.Log($"[TikTokBackendManager] Updated .env at: {envPath}");
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Debug.LogError($"[TikTokBackendManager] Failed to write .env: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Setup & Launch Backend

        #region Bun Runtime Verification & Auto-Installation

        /// <summary>
        /// Gets the resolved absolute path to bun.exe if available, otherwise "bun".
        /// </summary>
        public static string GetBunExecutablePath()
        {
            string userHome = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
            string bunHome = Path.Combine(userHome, @".bun\bin\bun.exe");
            if (File.Exists(bunHome)) return bunHome;

            string localApp = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
            string bunLocal = Path.Combine(localApp, @"bun\bin\bun.exe");
            if (File.Exists(bunLocal)) return bunLocal;

            return "bun";
        }

        /// <summary>
        /// Checks if Bun is installed by executing 'bun --version' and reading output.
        /// </summary>
        public static bool CheckBunVersion(out string version)
        {
            version = string.Empty;
            string bunExe = GetBunExecutablePath();

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = bunExe,
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var p = Process.Start(psi))
                {
                    if (p != null)
                    {
                        string output = p.StandardOutput.ReadToEnd().Trim();
                        p.WaitForExit(5000);
                        if (p.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                        {
                            version = output;
                            return true;
                        }
                    }
                }
            }
            catch
            {
                // Fallback via cmd /c bun --version
                try
                {
                    var cmdPsi = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = "/c bun --version",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };
                    using (var cp = Process.Start(cmdPsi))
                    {
                        if (cp != null)
                        {
                            string output = cp.StandardOutput.ReadToEnd().Trim();
                            cp.WaitForExit(5000);
                            if (cp.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                            {
                                version = output;
                                return true;
                            }
                        }
                    }
                }
                catch
                {
                    // Ignored
                }
            }

            return false;
        }

        /// <summary>
        /// Automatically installs Bun on Windows via official PowerShell script:
        /// powershell -NoProfile -ExecutionPolicy Bypass -Command "irm bun.sh/install.ps1 | iex"
        /// If that fails, falls back to npm install -g bun.
        /// </summary>
        public static bool InstallBun(Action<string> onProgress, out string error)
        {
            error = string.Empty;
            onProgress?.Invoke("Downloading & installing Bun runtime via PowerShell (irm bun.sh/install.ps1 | iex)...");
            Debug.Log("[TikTokBackendManager] Starting automated Bun installation...");

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"irm bun.sh/install.ps1 | iex\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var p = Process.Start(psi))
                {
                    if (p != null)
                    {
                        string stdOut = p.StandardOutput.ReadToEnd();
                        string stdErr = p.StandardError.ReadToEnd();
                        p.WaitForExit(120000); // 2 minute timeout

                        if (p.ExitCode != 0)
                        {
                            Debug.LogWarning($"[TikTokBackendManager] PowerShell Bun install exited with code {p.ExitCode}: {stdErr}. Trying npm fallback...");
                            onProgress?.Invoke("PowerShell install attempt finished. Trying npm install -g bun fallback...");
                            var npmPsi = new ProcessStartInfo
                            {
                                FileName = "cmd.exe",
                                Arguments = "/c npm install -g bun",
                                UseShellExecute = false,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                CreateNoWindow = true
                            };
                            using (var np = Process.Start(npmPsi))
                            {
                                np?.WaitForExit(60000);
                            }
                        }
                    }
                }

                // Add ~/.bun/bin to current process PATH
                string userHome = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
                string bunBinDir = Path.Combine(userHome, @".bun\bin");
                string pathEnv = System.Environment.GetEnvironmentVariable("PATH") ?? "";
                if (!pathEnv.Contains(bunBinDir))
                {
                    System.Environment.SetEnvironmentVariable("PATH", $"{bunBinDir};{pathEnv}");
                }

                if (CheckBunVersion(out string ver))
                {
                    Debug.Log($"<color=#76D12C>[TikTokBackendManager] Bun installed and verified successfully! Version: {ver}</color>");
                    return true;
                }
                else
                {
                    error = "Bun installation script completed but 'bun --version' could not be verified. Please install manually from https://bun.sh";
                    return false;
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Debug.LogError($"[TikTokBackendManager] Bun installation exception: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Ensures Bun is installed. Checks bun --version first. If missing, auto-installs.
        /// </summary>
        public static bool EnsureBunInstalled(Action<string> onProgress, out string versionOrMessage)
        {
            if (CheckBunVersion(out string ver))
            {
                versionOrMessage = ver;
                Debug.Log($"[TikTokBackendManager] Bun runtime detected: v{ver}");
                return true;
            }

            Debug.LogWarning("[TikTokBackendManager] Bun runtime not found. Starting automatic installation...");
            onProgress?.Invoke("Bun runtime not found. Auto-installing Bun...");

            if (InstallBun(onProgress, out string err))
            {
                if (CheckBunVersion(out string newVer))
                {
                    versionOrMessage = newVer;
                    return true;
                }
                versionOrMessage = "Installed";
                return true;
            }

            versionOrMessage = err;
            return false;
        }

        #endregion

        private string EnsureRunBatFile(string backendDir, int httpPort, int socketPort)
        {
            string batPath = Path.Combine(backendDir, "run_backend.bat");
            string bunExe = GetBunExecutablePath();

            string script = "@echo off\r\n" +
                $"title TikTok Live Backend [HTTP {httpPort} - Socket {socketPort}]\r\n" +
                "cd /d \"%~dp0\"\r\n" +
                "echo ========================================================\r\n" +
                "echo Starting TikTok Live Backend...\r\n" +
                "echo Directory: %CD%\r\n" +
                "echo ========================================================\r\n" +
                (File.Exists(bunExe) ? $"if exist \"{bunExe}\" ( \"{bunExe}\" run dev ) else ( bun run dev )\r\n" : "bun run dev\r\n") +
                "if errorlevel 1 (\r\n" +
                "    echo.\r\n" +
                "    echo [ERROR] Backend crashed or failed to start.\r\n" +
                "    pause\r\n" +
                ")\r\n";

            try
            {
                File.WriteAllText(batPath, script);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TikTokBackendManager] Could not write run_backend.bat: {ex.Message}");
            }
            return batPath;
        }

        /// <summary>
        /// Resolves the bun or npm executable path.
        /// </summary>
        private string ResolveRuntimeCommand(string backendDir)
        {
            string bunExe = GetBunExecutablePath();
            if (File.Exists(bunExe)) return $"\"{bunExe}\"";

            return "bun";
        }

        /// <summary>
        /// Launches the backend server via bun run dev in a new terminal window.
        /// After launch, automatically waits and tests the ports.
        /// </summary>
        public void StartBackend(string backendDir, int httpPort, int socketPort, string eulerApiKey, Action<bool, string> onComplete)
        {
            StartCoroutine(StartBackendCoroutine(backendDir, httpPort, socketPort, eulerApiKey, onComplete));
        }

        private IEnumerator StartBackendCoroutine(string backendDir, int httpPort, int socketPort, string eulerApiKey, Action<bool, string> onComplete)
        {
            if (string.IsNullOrWhiteSpace(backendDir))
            {
                backendDir = GetDefaultBackendDirectory();
            }

            // Auto-create folder if missing
            if (!Directory.Exists(backendDir))
            {
                Directory.CreateDirectory(backendDir);
            }

            // Auto-populate from reference folder if present
            string serverFile = Path.Combine(backendDir, "src", "server.ts");
            if (!File.Exists(serverFile))
            {
                string existingRef = @"D:\Download\TikTok-Live_Dev_Nhom5-main\backend";
                if (Directory.Exists(existingRef) && File.Exists(Path.Combine(existingRef, "src", "server.ts")))
                {
                    CopyDirectory(existingRef, backendDir);
                }
            }

            if (!File.Exists(Path.Combine(backendDir, "src", "server.ts")))
            {
                onComplete?.Invoke(false, $"Backend source files not found in '{backendDir}'. Please click 'AUTO SETUP (GIT CLONE)' to install!");
                yield break;
            }

            // 1. Write .env
            if (!WriteEnvFile(backendDir, httpPort, eulerApiKey, out string envErr))
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
                Debug.Log($"[TikTokBackendManager] Launched backend process from {batPath}");
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
                yield return CheckPortCoroutine(socketPort, (ok, _) => socketOpen = ok);
                yield return CheckPortCoroutine(httpPort, (ok, _) => httpOpen = ok);

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

        /// <summary>
        /// Automatically checks/installs Bun runtime, clones the GitHub repository and prepares the backend.
        /// </summary>
        public void SetupBackendFromGit(string destinationDir, int httpPort, int socketPort, string eulerApiKey, Action<bool, string> onComplete, Action<string> onProgress = null)
        {
            StartCoroutine(SetupBackendFromGitCoroutine(destinationDir, httpPort, socketPort, eulerApiKey, onComplete, onProgress));
        }

        private IEnumerator SetupBackendFromGitCoroutine(string destinationDir, int httpPort, int socketPort, string eulerApiKey, Action<bool, string> onComplete, Action<string> onProgress = null)
        {
            string targetFolder = string.IsNullOrWhiteSpace(destinationDir) ? GetDefaultBackendDirectory() : destinationDir;

            bool isDone = false;
            bool isSuccess = false;
            string resultMessage = "";
            string lastProgress = "";
            object progressLock = new object();

            void ReportProgress(string msg)
            {
                lock (progressLock)
                {
                    lastProgress = msg;
                }
            }

            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    // 1. Check Bun runtime and auto-install if missing
                    ReportProgress("Checking Bun runtime (bun --version)...");
                    if (!EnsureBunInstalled(ReportProgress, out string bunVersion))
                    {
                        isSuccess = false;
                        resultMessage = $"Bun check/installation failed: {bunVersion}";
                        return;
                    }

                    ReportProgress($"Bun runtime ready (v{bunVersion}). Preparing backend directory...");

                    if (!Directory.Exists(targetFolder))
                    {
                        Directory.CreateDirectory(targetFolder);
                    }

                    string serverTsPath = Path.Combine(targetFolder, "src", "server.ts");
                    if (File.Exists(serverTsPath))
                    {
                        resultMessage = $"Backend files already exist in target directory. Bun v{bunVersion} ready.";
                        isSuccess = true;
                    }
                    else
                    {
                        // Check if existing local reference folder is available to copy quickly
                        string localRef = @"D:\Download\TikTok-Live_Dev_Nhom5-main\backend";
                        if (Directory.Exists(localRef) && File.Exists(Path.Combine(localRef, "src", "server.ts")))
                        {
                            ReportProgress("Copying backend files from local reference folder...");
                            CopyDirectory(localRef, targetFolder);
                            isSuccess = true;
                            resultMessage = "Backend copied from local reference directory.";
                        }
                        else
                        {
                            // Clone from GitHub
                            ReportProgress("Cloning backend repository from GitHub...");
                            string parentDir = Directory.GetParent(targetFolder)?.FullName;
                            if (string.IsNullOrEmpty(parentDir)) parentDir = Application.dataPath;
                            if (!Directory.Exists(parentDir)) Directory.CreateDirectory(parentDir);

                            string cloneDir = Path.Combine(parentDir, "TikTok-Live_Dev_Nhom5_Temp");
                            if (Directory.Exists(cloneDir)) Directory.Delete(cloneDir, true);

                            string cloneCmd = $"clone {DefaultGitRepo} \"{cloneDir}\"";
                            var psi = new ProcessStartInfo("git", cloneCmd)
                            {
                                WorkingDirectory = parentDir,
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                RedirectStandardError = true,
                                RedirectStandardOutput = true
                            };

                            using (var p = Process.Start(psi))
                            {
                                p.WaitForExit(60000); // 60s timeout
                                if (p.ExitCode == 0)
                                {
                                    string clonedBackend = Path.Combine(cloneDir, "backend");
                                    if (Directory.Exists(clonedBackend))
                                    {
                                        ReportProgress("Copying cloned backend files into target folder...");
                                        CopyDirectory(clonedBackend, targetFolder);
                                        isSuccess = true;
                                        resultMessage = "Backend cloned from GitHub successfully.";
                                    }
                                    else
                                    {
                                        isSuccess = false;
                                        resultMessage = "Cloned repository does not contain 'backend' folder.";
                                    }
                                }
                                else
                                {
                                    string err = p.StandardError.ReadToEnd();
                                    isSuccess = false;
                                    resultMessage = $"Git clone failed: {err}";
                                }
                            }
                        }
                    }

                    if (isSuccess)
                    {
                        // Write .env
                        ReportProgress("Updating .env configuration...");
                        WriteEnvFile(targetFolder, httpPort, eulerApiKey, out string _);

                        // Check node_modules
                        string nodeModules = Path.Combine(targetFolder, "node_modules");
                        if (!Directory.Exists(nodeModules))
                        {
                            ReportProgress("Installing dependencies via Bun (bun install)...");
                            string bunExe = GetBunExecutablePath();
                            var bunPsi = new ProcessStartInfo("cmd.exe", $"/c cd /d \"{targetFolder}\" && \"{bunExe}\" install")
                            {
                                WorkingDirectory = targetFolder,
                                UseShellExecute = false,
                                CreateNoWindow = true
                            };
                            using (var bp = Process.Start(bunPsi))
                            {
                                bp?.WaitForExit(120000);
                            }
                        }
                        resultMessage = $"Backend setup finished successfully! Bun v{bunVersion} is ready.";
                    }
                }
                catch (Exception ex)
                {
                    isSuccess = false;
                    resultMessage = $"Setup error: {ex.Message}";
                }
                finally
                {
                    isDone = true;
                }
            });

            string prevReported = "";
            while (!isDone)
            {
                string p;
                lock (progressLock)
                {
                    p = lastProgress;
                }
                if (!string.IsNullOrEmpty(p) && p != prevReported)
                {
                    prevReported = p;
                    onProgress?.Invoke(p);
                }
                yield return null;
            }

            onComplete?.Invoke(isSuccess, resultMessage);
        }

        private static void CopyDirectory(string sourceDir, string targetDir)
        {
            if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(targetDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                string dirName = Path.GetFileName(subDir);
                if (dirName == "node_modules" || dirName == ".git") continue;
                string destSubDir = Path.Combine(targetDir, dirName);
                CopyDirectory(subDir, destSubDir);
            }
        }

        /// <summary>
        /// Kills any running node/bun process or frees the ports.
        /// </summary>
        public void StopBackend(int httpPort, int socketPort, Action<bool, string> onComplete)
        {
            StartCoroutine(StopBackendCoroutine(httpPort, socketPort, onComplete));
        }

        private IEnumerator StopBackendCoroutine(int httpPort, int socketPort, Action<bool, string> onComplete)
        {
            try
            {
                if (_runningBackendProcess != null && !_runningBackendProcess.HasExited)
                {
                    _runningBackendProcess.Kill();
                    _runningBackendProcess = null;
                }

                // Force kill any process holding port httpPort or socketPort
                KillProcessOnPort(httpPort);
                KillProcessOnPort(socketPort);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TikTokBackendManager] StopBackend warning: {ex.Message}");
            }

            yield return new WaitForSecondsRealtime(1.0f);

            bool socketOpen = false;
            yield return CheckPortCoroutine(socketPort, (ok, _) => socketOpen = ok);

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

        #endregion
    }
}
