using System;
using UnityEngine;
using SteamRush.Track;
using SteamRush.Core.Typography;

namespace SteamRush.Features.Runner
{
    /// <summary>
    /// Single Responsibility: Spawns, outfits, positions, and cleans up the roadside
    /// handover proxy character that waits on the sidewalk ahead of the runner.
    /// </summary>
    public class RoadsideHandoverProxySpawner
    {
        private GameObject _roadsideProxyPrefab;
        private GameObject _nameplatePrefab;
        private float _spawnAheadMeters = 35f;
        private float _leftSidewalkZ = -5.8f;
        private float _leftSidewalkRotY = -60f;
        private float _rightSidewalkZ = 5.8f;
        private float _rightSidewalkRotY = -120f;
        private float _sidewalkY = 0.2f;
        private Color _nameColor = Color.white;
        private Material _roadsideOutlineMaterial;
        private Color _roadsideOutlineColor = new Color(0.15f, 0.75f, 1f, 1f);
        private float _roadsideOutlineWidth = 0.015f;

        public void Initialize(
            GameObject roadsideProxyPrefab,
            GameObject nameplatePrefab,
            float spawnAheadMeters,
            float leftSidewalkZ,
            float leftSidewalkRotY,
            float rightSidewalkZ,
            float rightSidewalkRotY,
            float sidewalkY,
            Color nameColor,
            Material roadsideOutlineMaterial,
            Color roadsideOutlineColor,
            float roadsideOutlineWidth)
        {
            _roadsideProxyPrefab = roadsideProxyPrefab;
            _nameplatePrefab = nameplatePrefab;
            _spawnAheadMeters = spawnAheadMeters;
            _leftSidewalkZ = leftSidewalkZ;
            _leftSidewalkRotY = leftSidewalkRotY;
            _rightSidewalkZ = rightSidewalkZ;
            _rightSidewalkRotY = rightSidewalkRotY;
            _sidewalkY = sidewalkY;
            _nameColor = nameColor;
            _roadsideOutlineMaterial = roadsideOutlineMaterial;
            _roadsideOutlineColor = roadsideOutlineColor;
            _roadsideOutlineWidth = roadsideOutlineWidth;
        }

        public GameObject SpawnProxy(
            Transform runnerTransform,
            WorldSpeedManager worldSpeedManager,
            ChatRunnerQueueManager queueManager,
            GameObject outfitPrefab,
            string runnerId)
        {
            if (runnerTransform == null || outfitPrefab == null) return null;

            bool isLeft = UnityEngine.Random.value > 0.5f;
            float targetZ = isLeft ? _leftSidewalkZ : _rightSidewalkZ;
            Quaternion spawnRot = isLeft ? Quaternion.Euler(0f, _leftSidewalkRotY, 0f) : Quaternion.Euler(0f, _rightSidewalkRotY, 0f);

            Vector3 rootPos = new Vector3(runnerTransform.position.x + _spawnAheadMeters, 0f, 0f);

            GameObject proxyInstance;
            if (_roadsideProxyPrefab != null)
            {
                proxyInstance = UnityEngine.Object.Instantiate(_roadsideProxyPrefab, rootPos, Quaternion.identity);
                proxyInstance.name = $"RoadsideHandoverProxy_{outfitPrefab.name}";
            }
            else
            {
                proxyInstance = new GameObject($"RoadsideHandoverProxy_{outfitPrefab.name}");
                proxyInstance.transform.position = rootPos;
                proxyInstance.transform.rotation = Quaternion.identity;
            }

            MovingWorldObject mover = proxyInstance.GetComponent<MovingWorldObject>();
            if (mover == null) mover = proxyInstance.AddComponent<MovingWorldObject>();
            if (worldSpeedManager != null) mover.Initialize(worldSpeedManager);

            BoxCollider box = proxyInstance.GetComponent<BoxCollider>();
            if (box == null) box = proxyInstance.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(1.5f, 3.5f, 12f);
            box.center = new Vector3(0f, 1.75f, 0f);

            RoadsideHandoverTrigger trigger = proxyInstance.GetComponent<RoadsideHandoverTrigger>();
            if (trigger == null) trigger = proxyInstance.AddComponent<RoadsideHandoverTrigger>();
            trigger.Initialize(queueManager, 12f);

            Transform existingContainer = proxyInstance.transform.Find("ModelContainer");
            GameObject modelAnchor;
            if (existingContainer != null)
            {
                modelAnchor = existingContainer.gameObject;
            }
            else
            {
                modelAnchor = new GameObject("ModelAnchor");
                modelAnchor.transform.SetParent(proxyInstance.transform, false);
            }

            modelAnchor.transform.localPosition = new Vector3(0f, _sidewalkY, targetZ);
            modelAnchor.transform.localRotation = spawnRot;

            foreach (Transform c in modelAnchor.transform)
            {
                UnityEngine.Object.Destroy(c.gameObject);
            }

            GameObject characterInstance = UnityEngine.Object.Instantiate(outfitPrefab, modelAnchor.transform);
            characterInstance.name = outfitPrefab.name;
            characterInstance.transform.localPosition = Vector3.zero;
            characterInstance.transform.localRotation = Quaternion.identity;

            NeutralizeCharacterPhysics(characterInstance);
            ApplyNaturalStandingPose(characterInstance);
            AttachNameplate(characterInstance, runnerId);
            AttachOutline(characterInstance);

            return proxyInstance;
        }

        private void NeutralizeCharacterPhysics(GameObject characterInstance)
        {
            foreach (var rb in characterInstance.GetComponentsInChildren<Rigidbody>(true))
            {
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                UnityEngine.Object.Destroy(rb);
            }

            foreach (var cc in characterInstance.GetComponentsInChildren<CharacterController>(true))
            {
                UnityEngine.Object.Destroy(cc);
            }

            foreach (var col in characterInstance.GetComponentsInChildren<Collider>(true))
            {
                col.enabled = false;
                UnityEngine.Object.Destroy(col);
            }

            foreach (var mb in characterInstance.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb is CharacterVisualOutline || mb is SteamRush.Relay.HandoverProxyController) continue;
                mb.enabled = false;
                UnityEngine.Object.Destroy(mb);
            }

            var anims = characterInstance.GetComponentsInChildren<Animator>(true);
            foreach (var a in anims)
            {
                a.enabled = false;
            }
        }

        private void AttachNameplate(GameObject proxyObj, string displayName)
        {
            if (_nameplatePrefab == null) return;

            GameObject nameplate = UnityEngine.Object.Instantiate(_nameplatePrefab, proxyObj.transform);
            nameplate.name = "RelayNameplate";
            nameplate.transform.localPosition = new Vector3(0f, 2.1f, 0f);

            var tmp = nameplate.GetComponentInChildren<TMPro.TMP_Text>();
            if (tmp != null)
            {
                tmp.text = ViewerNameSanitizer.SanitizeDisplayName(displayName);
                tmp.color = _nameColor;
            }

            var proxyCtrl = proxyObj.AddComponent<SteamRush.Relay.HandoverProxyController>();
            proxyCtrl.SetNameplate(nameplate.transform, tmp);
        }

        private void AttachOutline(GameObject proxyObj)
        {
            if (proxyObj == null) return;
            var outline = proxyObj.GetComponent<CharacterVisualOutline>();
            if (outline == null)
            {
                outline = proxyObj.AddComponent<CharacterVisualOutline>();
            }

            if (_roadsideOutlineMaterial == null)
            {
#if UNITY_EDITOR
                _roadsideOutlineMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Roadside_Outline_Mat.mat");
#endif
            }

            outline.ConfigureOutline(_roadsideOutlineMaterial, _roadsideOutlineColor, _roadsideOutlineWidth);
        }

        private void ApplyNaturalStandingPose(GameObject proxy)
        {
            if (proxy == null) return;
            var sL = proxy.transform.Find("Root/Hips/Spine_01/Spine_02/Spine_03/Clavicle_L/Shoulder_L");
            var sR = proxy.transform.Find("Root/Hips/Spine_01/Spine_02/Spine_03/Clavicle_R/Shoulder_R");
            if (sL != null) sL.localRotation = Quaternion.Euler(15f, 10f, 65f);
            if (sR != null) sR.localRotation = Quaternion.Euler(15f, -10f, -65f);

            var eL = proxy.transform.Find("Root/Hips/Spine_01/Spine_02/Spine_03/Clavicle_L/Shoulder_L/Elbow_L");
            var eR = proxy.transform.Find("Root/Hips/Spine_01/Spine_02/Spine_03/Clavicle_R/Shoulder_R/Elbow_R");
            if (eL != null) eL.localRotation = Quaternion.Euler(0f, 15f, 15f);
            if (eR != null) eR.localRotation = Quaternion.Euler(0f, -15f, -15f);
        }
    }
}
