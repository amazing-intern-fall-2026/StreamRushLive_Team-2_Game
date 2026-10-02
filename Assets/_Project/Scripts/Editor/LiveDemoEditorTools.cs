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
                Debug.Log("[LiveDemoEditorTools] LiveSessionDemoRunner đã tồn tại trong scene: " + existing.gameObject.name);
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            GameObject go = new GameObject("LiveSessionDemoRunner", typeof(LiveSessionDemoRunner));
            Undo.RegisterCreatedObjectUndo(go, "Create LiveSessionDemoRunner");

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Debug.Log("<color=#00FF88>[LiveDemoEditorTools] Đã thêm LiveSessionDemoRunner vào scene thành công!</color>");
            Selection.activeGameObject = go;
        }

        [MenuItem("Tools/StreamRush/Live Demo/Toggle Live Demo")]
        public static void ToggleDemo()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[LiveDemoEditorTools] Chức năng Toggle Live Demo chỉ hoạt động trong Play Mode!");
                return;
            }

            var runner = LiveSessionDemoRunner.Instance ?? Object.FindFirstObjectByType<LiveSessionDemoRunner>();
            if (runner != null)
            {
                runner.ToggleLiveDemo();
            }
            else
            {
                Debug.LogWarning("[LiveDemoEditorTools] Không tìm thấy LiveSessionDemoRunner trong scene!");
            }
        }

        [MenuItem("Tools/StreamRush/Live Demo/Toggle Mock Followers (Follower Chuyền Gậy)")]
        public static void ToggleFollowers()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[LiveDemoEditorTools] Chức năng Toggle Mock Followers chỉ hoạt động trong Play Mode!");
                return;
            }

            var runner = LiveSessionDemoRunner.Instance ?? Object.FindFirstObjectByType<LiveSessionDemoRunner>();
            if (runner != null)
            {
                runner.ToggleMockFollowers();
            }
            else
            {
                Debug.LogWarning("[LiveDemoEditorTools] Không tìm thấy LiveSessionDemoRunner trong scene!");
            }
        }
    }
}
#endif
