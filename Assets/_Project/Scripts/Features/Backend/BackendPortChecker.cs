using System;
using System.Collections;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.Networking;

namespace SteamRush.Features.Backend
{
    /// <summary>
    /// Handles TCP socket port probes and HTTP health checks for the TikTok Live backend service.
    /// Single responsibility: Network connectivity & health verification.
    /// </summary>
    public class BackendPortChecker : MonoBehaviour
    {
        public static bool ProbeTcpPort(int port, int timeoutMs = 800)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    var asyncResult = client.BeginConnect("127.0.0.1", port, null, null);
                    bool success = asyncResult.AsyncWaitHandle.WaitOne(timeoutMs);
                    if (success && client.Connected)
                    {
                        client.EndConnect(asyncResult);
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        public void CheckPort(int port, Action<bool, string> onComplete)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                bool open = ProbeTcpPort(port, 1000);
                string msg = open ? $"Port {port} is OPEN (Listening)" : $"Port {port} is CLOSED (No response)";
                onComplete?.Invoke(open, msg);
                return;
            }
#endif
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
    }
}
