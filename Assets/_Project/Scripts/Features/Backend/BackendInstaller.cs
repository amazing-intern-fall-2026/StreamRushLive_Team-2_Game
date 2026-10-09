using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace SteamRush.Features.Backend
{
    /// <summary>
    /// Handles automated installation, Bun runtime checking, Git cloning, and .env creation for the TikTok Live backend.
    /// Single responsibility: Backend setup & dependency installation.
    /// </summary>
    public class BackendInstaller : MonoBehaviour
    {
        public const string DefaultGitRepo = "https://github.com/amazing-intern-fall-2026/TikTok-Live_Dev_Nhom5.git";

        public static string GetBunExecutablePath()
        {
            string userHome = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(userHome))
                userHome = System.Environment.GetEnvironmentVariable("USERPROFILE") ?? "";

            if (!string.IsNullOrEmpty(userHome))
            {
                string bunHome = Path.Combine(userHome, @".bun\bin\bun.exe");
                if (File.Exists(bunHome)) return bunHome;
            }

            string localApp = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(localApp) && !string.IsNullOrEmpty(userHome))
                localApp = Path.Combine(userHome, @"AppData\Local");

            if (!string.IsNullOrEmpty(localApp))
            {
                string bunLocal = Path.Combine(localApp, @"bun\bin\bun.exe");
                if (File.Exists(bunLocal)) return bunLocal;
            }

            return "bun";
        }

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
                catch { }
            }

            return false;
        }

        public static bool InstallBun(Action<string> onProgress, out string error)
        {
            error = string.Empty;
            onProgress?.Invoke("Downloading & installing Bun runtime via PowerShell (irm bun.sh/install.ps1 | iex)...");
            Debug.Log("[BackendInstaller] Starting automated Bun installation...");

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
                        string stdErr = p.StandardError.ReadToEnd();
                        p.WaitForExit(120000);

                        if (p.ExitCode != 0)
                        {
                            Debug.LogWarning($"[BackendInstaller] PowerShell Bun install exited with code {p.ExitCode}: {stdErr}. Trying npm fallback...");
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

                string userHome = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
                string bunBinDir = Path.Combine(userHome, @".bun\bin");
                string pathEnv = System.Environment.GetEnvironmentVariable("PATH") ?? "";
                if (!pathEnv.Contains(bunBinDir))
                {
                    System.Environment.SetEnvironmentVariable("PATH", $"{bunBinDir};{pathEnv}");
                }

                if (CheckBunVersion(out string ver))
                {
                    Debug.Log($"<color=#76D12C>[BackendInstaller] Bun installed and verified successfully! Version: {ver}</color>");
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
                Debug.LogError($"[BackendInstaller] Bun installation exception: {ex.Message}");
                return false;
            }
        }

        public static bool EnsureBunInstalled(Action<string> onProgress, out string versionOrMessage)
        {
            if (CheckBunVersion(out string ver))
            {
                versionOrMessage = ver;
                Debug.Log($"[BackendInstaller] Bun runtime detected: v{ver}");
                return true;
            }

            Debug.LogWarning("[BackendInstaller] Bun runtime not found. Starting automatic installation...");
            onProgress?.Invoke("Bun runtime not found. Auto-installing Bun...");

            if (InstallBun(onProgress, out string err))
            {
                CheckBunVersion(out string newVer);
                versionOrMessage = newVer;
                return true;
            }

            versionOrMessage = err;
            return false;
        }

        public static bool WriteEnvFile(string backendPath, int port, string eulerApiKey, out string error)
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

                string keyToWrite = eulerApiKey != null ? eulerApiKey.Trim() : "";
                string envPath = Path.Combine(backendPath, ".env");

                // If key is empty, check if existing .env has a non-empty key
                if (string.IsNullOrEmpty(keyToWrite) && File.Exists(envPath))
                {
                    try
                    {
                        string[] lines = File.ReadAllLines(envPath);
                        foreach (var line in lines)
                        {
                            if (line.StartsWith("EULER_API_KEY=", StringComparison.OrdinalIgnoreCase))
                            {
                                string existingVal = line.Substring("EULER_API_KEY=".Length).Trim();
                                if (!string.IsNullOrEmpty(existingVal))
                                {
                                    keyToWrite = existingVal;
                                    break;
                                }
                            }
                        }
                    }
                    catch { }
                }

                // If still empty, fallback to DefaultEulerApiKey
                if (string.IsNullOrEmpty(keyToWrite))
                {
                    keyToWrite = TikTokBackendManager.DefaultEulerApiKey;
                }

                string content = $"PORT={port}\nEULER_API_KEY={keyToWrite}\n";
                File.WriteAllText(envPath, content);
                Debug.Log($"[BackendInstaller] Updated .env at: {envPath} (EULER_API_KEY: {(keyToWrite.Length > 10 ? keyToWrite.Substring(0, 10) + "..." : "SET")})");
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Debug.LogError($"[BackendInstaller] Failed to write .env: {ex.Message}");
                return false;
            }
        }

        public static string EnsureRunBatFile(string backendDir, int httpPort, int socketPort)
        {
            try
            {
                if (!Directory.Exists(backendDir)) Directory.CreateDirectory(backendDir);
                string batPath = Path.Combine(backendDir, "run_backend.bat");
                string bunExe = GetBunExecutablePath();

                string content = $@"@echo off
title TikTok Live Backend - HTTP {httpPort} - Socket {socketPort}
echo ===================================================
echo   TikTok Live Backend Runner
echo   HTTP Port: {httpPort}
echo   Socket Port: {socketPort}
echo ===================================================
cd /d ""{backendDir}""
""{bunExe}"" run dev
pause
";
                File.WriteAllText(batPath, content);
                return batPath;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BackendInstaller] Could not create run_backend.bat: {ex.Message}");
                return string.Empty;
            }
        }

        public static void DeleteDirectorySafely(string targetDir)
        {
            if (string.IsNullOrEmpty(targetDir) || !Directory.Exists(targetDir)) return;
            try
            {
                var dir = new DirectoryInfo(targetDir);
                foreach (var file in dir.GetFiles("*", SearchOption.AllDirectories))
                {
                    file.Attributes = FileAttributes.Normal;
                }
                dir.Delete(true);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BackendInstaller] DeleteDirectorySafely warning: {ex.Message}. Falling back to cmd...");
                try
                {
                    var psi = new ProcessStartInfo("cmd.exe", $"/c rd /s /q \"{targetDir}\"")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    using (var p = Process.Start(psi))
                    {
                        p?.WaitForExit(5000);
                    }
                }
                catch { }
            }
        }

        public void SetupBackendFromGit(string destinationDir, int httpPort, int socketPort, string eulerApiKey, Action<bool, string> onComplete, Action<string> onProgress = null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                var enumerator = SetupBackendFromGitCoroutine(destinationDir, httpPort, socketPort, eulerApiKey, onComplete, onProgress);
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
            StartCoroutine(SetupBackendFromGitCoroutine(destinationDir, httpPort, socketPort, eulerApiKey, onComplete, onProgress));
        }

        private IEnumerator SetupBackendFromGitCoroutine(string destinationDir, int httpPort, int socketPort, string eulerApiKey, Action<bool, string> onComplete, Action<string> onProgress = null)
        {
            string targetFolder = string.IsNullOrWhiteSpace(destinationDir) ? TikTokBackendManager.GetDefaultBackendDirectory() : destinationDir;
            // Cache Application.dataPath on main thread before ThreadPool execution!
            string mainThreadDataPath = Application.dataPath;

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
                        ReportProgress($"Backend source files already exist in '{targetFolder}'.");
                        isSuccess = true;
                    }
                    else
                    {
                        string localRef = @"D:\Download\TikTok-Live_Dev_Nhom5-main\backend";
                        if (Directory.Exists(localRef) && File.Exists(Path.Combine(localRef, "src", "server.ts")))
                        {
                            ReportProgress("Copying backend source files from local reference folder...");
                            CopyDirectory(localRef, targetFolder);
                            isSuccess = true;
                        }
                        else
                        {
                            ReportProgress("Cloning backend repository from GitHub...");
                            string parentDir = Directory.GetParent(targetFolder)?.FullName;
                            if (string.IsNullOrEmpty(parentDir)) parentDir = mainThreadDataPath;
                            if (!Directory.Exists(parentDir)) Directory.CreateDirectory(parentDir);

                            string cloneDir = Path.Combine(parentDir, "TikTok-Live_Dev_Nhom5_Temp");
                            DeleteDirectorySafely(cloneDir);

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
                                var errOutput = new System.Text.StringBuilder();
                                p.ErrorDataReceived += (s, e) =>
                                {
                                    if (!string.IsNullOrEmpty(e.Data))
                                    {
                                        errOutput.AppendLine(e.Data);
                                        ReportProgress($"Git: {e.Data}");
                                    }
                                };
                                p.OutputDataReceived += (s, e) =>
                                {
                                    if (!string.IsNullOrEmpty(e.Data)) ReportProgress($"Git: {e.Data}");
                                };
                                p.BeginErrorReadLine();
                                p.BeginOutputReadLine();

                                bool exited = p.WaitForExit(90000);
                                if (!exited)
                                {
                                    try { p.Kill(); } catch { }
                                }

                                if (p.ExitCode == 0)
                                {
                                    string clonedBackend = Path.Combine(cloneDir, "backend");
                                    if (Directory.Exists(clonedBackend))
                                    {
                                        ReportProgress("Copying cloned backend files into target folder...");
                                        CopyDirectory(clonedBackend, targetFolder);
                                        DeleteDirectorySafely(cloneDir);
                                        isSuccess = true;
                                    }
                                    else
                                    {
                                        isSuccess = false;
                                        resultMessage = "Cloned repository does not contain 'backend' folder.";
                                    }
                                }
                                else
                                {
                                    isSuccess = false;
                                    resultMessage = $"Git clone failed: {errOutput}";
                                }
                            }
                        }
                    }

                    if (isSuccess)
                    {
                        ReportProgress("Updating configuration (.env & run_backend.bat)...");
                        EnsureBackendSourcePatches(targetFolder);
                        WriteEnvFile(targetFolder, httpPort, eulerApiKey, out string envErr);
                        EnsureRunBatFile(targetFolder, httpPort, socketPort);

                        string nodeModules = Path.Combine(targetFolder, "node_modules");
                        if (!Directory.Exists(nodeModules))
                        {
                            ReportProgress("Installing dependencies via Bun (bun install)...");
                            string bunExe = GetBunExecutablePath();
                            var bunPsi = new ProcessStartInfo("cmd.exe", $"/c cd /d \"{targetFolder}\" && \"{bunExe}\" install")
                            {
                                WorkingDirectory = targetFolder,
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true
                            };
                            using (var bp = Process.Start(bunPsi))
                            {
                                bp.OutputDataReceived += (s, e) =>
                                {
                                    if (!string.IsNullOrEmpty(e.Data)) ReportProgress($"Bun: {e.Data}");
                                };
                                bp.ErrorDataReceived += (s, e) =>
                                {
                                    if (!string.IsNullOrEmpty(e.Data)) ReportProgress($"Bun: {e.Data}");
                                };
                                bp.BeginOutputReadLine();
                                bp.BeginErrorReadLine();
                                bp?.WaitForExit(120000);
                            }
                        }

                        resultMessage = $"Auto Setup completed successfully! Backend ready (HTTP: {httpPort}, Socket: {socketPort}).";
                    }
                }
                catch (Exception ex)
                {
                    isSuccess = false;
                    resultMessage = $"Setup error: {ex.Message}";
                    Debug.LogError($"[BackendInstaller] Setup exception: {ex}");
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

        public static void CopyDirectory(string sourceDir, string targetDir)
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

        private static void EnsureBackendSourcePatches(string targetFolder)
        {
            try
            {
                string socketsIndexPath = Path.Combine(targetFolder, "src", "sockets", "index.ts");
                if (File.Exists(socketsIndexPath))
                {
                    string content = File.ReadAllText(socketsIndexPath);
                    // Remove duplicate connect() call that causes 'Already connecting!'
                    const string duplicateCallCrLf = "tikTokConnectionWrapper.connect();\r\n            } catch (err: any) {";
                    const string duplicateCallLf = "tikTokConnectionWrapper.connect();\n            } catch (err: any) {";
                    const string replacement = "} catch (err: any) {";
                    if (content.Contains(duplicateCallCrLf))
                    {
                        content = content.Replace(duplicateCallCrLf, replacement);
                        File.WriteAllText(socketsIndexPath, content);
                    }
                    else if (content.Contains(duplicateCallLf))
                    {
                        content = content.Replace(duplicateCallLf, replacement);
                        File.WriteAllText(socketsIndexPath, content);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BackendInstaller] Could not patch backend source: {ex.Message}");
            }
        }
    }
}
