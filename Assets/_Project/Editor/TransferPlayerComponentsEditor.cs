#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SteamRush.Features.Runner;
using SteamRush.MinhHuy;

namespace SteamRush.EditorTools
{
    [InitializeOnLoad]
    public static class TransferPlayerComponentsEditor
    {
        private const string TransferExecutedKey = "TransferPlayerRunnerExecuted_v1";

        static TransferPlayerComponentsEditor()
        {
            EditorApplication.delayCall += AutoTransferOnce;
        }

        private static void AutoTransferOnce()
        {
            if (!SessionState.GetBool(TransferExecutedKey, false))
            {
                SessionState.SetBool(TransferExecutedKey, true);
                ExecuteTransfer();
            }
        }

        [MenuItem("Tools/Transfer PlayerRunner To Character_Male_Jacket_01")]
        public static void ExecuteTransfer()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.isLoaded) return;

            // 1. Tìm PlayerRunner
            GameObject playerRunner = GameObject.Find("PlayerRunner");
            if (playerRunner == null)
            {
                foreach (var root in activeScene.GetRootGameObjects())
                {
                    if (root.name.Contains("PlayerRunner") || (root.CompareTag("Player") && !root.name.Contains("Character_Male_Jacket")))
                    {
                        playerRunner = root;
                        break;
                    }
                }
            }

            // 2. Tìm hoặc Tạo Character_Male_Jacket_01
            GameObject targetChar = GameObject.Find("Character_Male_Jacket_01");
            if (targetChar == null)
            {
                foreach (var root in activeScene.GetRootGameObjects())
                {
                    if (root.name.Contains("Character_Male_Jacket"))
                    {
                        targetChar = root;
                        break;
                    }
                }
            }

            if (targetChar == null)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SyntyStudios/PolygonCity/Prefabs/Characters/Character_Male_Jacket_01.prefab");
                if (prefab != null)
                {
                    targetChar = (GameObject)PrefabUtility.InstantiatePrefab(prefab, activeScene);
                    targetChar.name = "Character_Male_Jacket_01";
                }
                else
                {
                    Debug.LogWarning("[Transfer] Không tìm thấy Character_Male_Jacket_01 trong Scene hoặc Assets!");
                    return;
                }
            }

            if (playerRunner == null)
            {
                Debug.LogWarning("[Transfer] Không tìm thấy PlayerRunner trong Scene!");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(targetChar, "Transfer Components to Character");
            Undo.RegisterFullObjectHierarchyUndo(playerRunner, "Transfer Components from PlayerRunner");

            // Sao chép Transform
            targetChar.transform.position = playerRunner.transform.position;
            targetChar.transform.rotation = playerRunner.transform.rotation;
            targetChar.transform.localScale = playerRunner.transform.localScale;

            // Sao chép Tag & Layer
            targetChar.tag = playerRunner.tag;
            targetChar.layer = playerRunner.layer;

            // Sao chép BoxCollider
            BoxCollider srcBox = playerRunner.GetComponent<BoxCollider>();
            if (srcBox != null)
            {
                BoxCollider dstBox = targetChar.GetComponent<BoxCollider>();
                if (dstBox == null) dstBox = targetChar.AddComponent<BoxCollider>();
                EditorUtility.CopySerialized(srcBox, dstBox);
            }

            // Sao chép Rigidbody
            Rigidbody srcRb = playerRunner.GetComponent<Rigidbody>();
            if (srcRb != null)
            {
                Rigidbody dstRb = targetChar.GetComponent<Rigidbody>();
                if (dstRb == null) dstRb = targetChar.AddComponent<Rigidbody>();
                EditorUtility.CopySerialized(srcRb, dstRb);
            }

            // Sao chép RunnerController
            RunnerController srcController = playerRunner.GetComponent<RunnerController>();
            if (srcController != null)
            {
                RunnerController dstController = targetChar.GetComponent<RunnerController>();
                if (dstController == null) dstController = targetChar.AddComponent<RunnerController>();
                EditorUtility.CopySerialized(srcController, dstController);
            }

            // Sao chép RunnerInputHandler
            RunnerInputHandler srcInput = playerRunner.GetComponent<RunnerInputHandler>();
            if (srcInput != null)
            {
                RunnerInputHandler dstInput = targetChar.GetComponent<RunnerInputHandler>();
                if (dstInput == null) dstInput = targetChar.AddComponent<RunnerInputHandler>();
                EditorUtility.CopySerialized(srcInput, dstInput);
            }

            // Sao chép RunnerCollisionHandler
            RunnerCollisionHandler srcCollision = playerRunner.GetComponent<RunnerCollisionHandler>();
            if (srcCollision != null)
            {
                RunnerCollisionHandler dstCollision = targetChar.GetComponent<RunnerCollisionHandler>();
                if (dstCollision == null) dstCollision = targetChar.AddComponent<RunnerCollisionHandler>();
                EditorUtility.CopySerialized(srcCollision, dstCollision);
            }

            // Sao chép HUDTestDriver nếu có
            HUDTestDriver srcHud = playerRunner.GetComponent<HUDTestDriver>();
            if (srcHud != null)
            {
                HUDTestDriver dstHud = targetChar.GetComponent<HUDTestDriver>();
                if (dstHud == null) dstHud = targetChar.AddComponent<HUDTestDriver>();
                EditorUtility.CopySerialized(srcHud, dstHud);

                SerializedObject so = new SerializedObject(dstHud);
                SerializedProperty dummyProp = so.FindProperty("dummyRunner");
                if (dummyProp != null)
                {
                    dummyProp.objectReferenceValue = targetChar.transform;
                    so.ApplyModifiedProperties();
                }
            }

            // Cập nhật các reference khác trong Scene đang trỏ tới PlayerRunner
            foreach (var root in activeScene.GetRootGameObjects())
            {
                foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null || mb.gameObject == playerRunner || mb.gameObject == targetChar) continue;

                    SerializedObject so = new SerializedObject(mb);
                    SerializedProperty sp = so.GetIterator();
                    bool modified = false;
                    while (sp.NextVisible(true))
                    {
                        if (sp.propertyType == SerializedPropertyType.ObjectReference)
                        {
                            if (sp.objectReferenceValue == playerRunner)
                            {
                                sp.objectReferenceValue = targetChar;
                                modified = true;
                            }
                            else if (sp.objectReferenceValue == playerRunner.transform)
                            {
                                sp.objectReferenceValue = targetChar.transform;
                                modified = true;
                            }
                        }
                    }
                    if (modified)
                    {
                        so.ApplyModifiedProperties();
                        EditorUtility.SetDirty(mb);
                    }
                }
            }

            // Xóa bỏ PlayerRunner cũ (capsule placeholder)
            Object.DestroyImmediate(playerRunner);

            EditorUtility.SetDirty(targetChar);
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            Debug.Log($"[Transfer] Đã chuyển thành công toàn bộ component và transform sang {targetChar.name} và lưu scene!");
        }
    }
}
#endif
