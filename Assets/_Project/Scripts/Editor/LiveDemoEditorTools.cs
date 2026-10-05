#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using SteamRush.Features.Runner;

namespace SteamRush.EditorTools
{
    public static class LiveDemoEditorTools
    {
        [MenuItem("Tools/StreamRush/Live Demo/Attach Live Demo To Scene")]
        public static void AttachToScene()
        {
            var existing = Object.FindFirstObjectByType<LiveSessionDemoRunner>();
            if (existing != null)
            {
                Debug.Log("[LiveDemoEditorTools] LiveSessionDemoRunner already exists in scene: " + existing.gameObject.name);
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            GameObject go = new GameObject("LiveSessionDemoRunner", typeof(LiveSessionDemoRunner));
            Undo.RegisterCreatedObjectUndo(go, "Create LiveSessionDemoRunner");

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Debug.Log("<color=#00FF88>[LiveDemoEditorTools] LiveSessionDemoRunner added to scene successfully!</color>");
            Selection.activeGameObject = go;
        }

        [MenuItem("Tools/StreamRush/Live Demo/Toggle Live Demo")]
        public static void ToggleDemo()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[LiveDemoEditorTools] Toggle Live Demo is only available in Play Mode!");
                return;
            }

            var runner = LiveSessionDemoRunner.Instance ?? Object.FindFirstObjectByType<LiveSessionDemoRunner>();
            if (runner != null)
            {
                runner.ToggleLiveDemo();
            }
            else
            {
                Debug.LogWarning("[LiveDemoEditorTools] LiveSessionDemoRunner not found in scene!");
            }
        }

        [MenuItem("Tools/StreamRush/Live Demo/Toggle Mock Followers (Baton Relay)")]
        public static void ToggleFollowers()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[LiveDemoEditorTools] Toggle Mock Followers is only available in Play Mode!");
                return;
            }

            var runner = LiveSessionDemoRunner.Instance ?? Object.FindFirstObjectByType<LiveSessionDemoRunner>();
            if (runner != null)
            {
                runner.ToggleMockFollowers();
            }
            else
            {
                Debug.LogWarning("[LiveDemoEditorTools] LiveSessionDemoRunner not found in scene!");
            }
        }
    }
}
#endif
