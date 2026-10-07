using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace SteamRush.Features.Runner
{
    /// <summary>
    /// Manages an inverted-hull outline SkinnedMeshRenderer for characters.
    /// Automatically detects and binds to whichever character skin / SkinnedMeshRenderer
    /// is currently active (e.g. Runner outfit swaps or roadside waiting proxies).
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class CharacterVisualOutline : MonoBehaviour
    {
        [Header("Material & Shader")]
        [Tooltip("Material using Custom/Runner_Outline shader.")]
        [SerializeField] private Material _outlineMaterial;

        [Tooltip("Outline color tint.")]
        [SerializeField] private Color _outlineColor = new Color(1f, 0.85f, 0f, 1f); // Default Gold

        [Tooltip("Outline thickness width.")]
        [Range(0.001f, 0.08f)]
        [SerializeField] private float _outlineWidth = 0.015f;

        [Tooltip("Automatically track and sync to the currently active SkinnedMeshRenderer under this GameObject.")]
        [SerializeField] private bool _autoTrackActiveSkin = true;

        [SerializeField] private GameObject _outlineObject;
        [SerializeField] private SkinnedMeshRenderer _outlineRenderer;

        private SkinnedMeshRenderer _lastSourceSmr;
        private MaterialPropertyBlock _propBlock;

        public Material OutlineMaterial
        {
            get => _outlineMaterial;
            set
            {
                _outlineMaterial = value;
                ApplyMaterialProperties();
            }
        }

        public Color OutlineColor
        {
            get => _outlineColor;
            set
            {
                _outlineColor = value;
                ApplyMaterialProperties();
            }
        }

        public float OutlineWidth
        {
            get => _outlineWidth;
            set
            {
                _outlineWidth = value;
                ApplyMaterialProperties();
            }
        }

        private void OnEnable()
        {
            EnsureOutlineObject();
            RefreshOutline();
        }

        private void Start()
        {
            EnsureOutlineObject();
            RefreshOutline();
        }

        private void LateUpdate()
        {
            if (_autoTrackActiveSkin)
            {
                CheckAndSyncActiveSkin();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ApplyMaterialProperties();
        }
#endif

        /// <summary>
        /// Explicitly configures the outline material, color, and width.
        /// </summary>
        public void ConfigureOutline(Material material, Color color, float width = 0.015f)
        {
            _outlineMaterial = material;
            _outlineColor = color;
            _outlineWidth = width;
            ApplyMaterialProperties();
            RefreshOutline();
        }

        /// <summary>
        /// Finds the currently active character SkinnedMeshRenderer (ignoring outline meshes).
        /// </summary>
        public SkinnedMeshRenderer GetActiveSourceMeshRenderer()
        {
            var smrs = GetComponentsInChildren<SkinnedMeshRenderer>(false);
            foreach (var smr in smrs)
            {
                if (smr == null || !smr.gameObject.activeInHierarchy) continue;
                if (smr == _outlineRenderer) continue;
                if (smr.name.EndsWith("_OUTLINE", StringComparison.OrdinalIgnoreCase)) continue;
                if (smr.name.StartsWith("Visual_Outline", StringComparison.OrdinalIgnoreCase)) continue;

                return smr;
            }

            return null;
        }

        /// <summary>
        /// Synchronizes the outline renderer's mesh, bones, and transform with the active character mesh.
        /// </summary>
        public void RefreshOutline()
        {
            EnsureOutlineObject();

            SkinnedMeshRenderer activeSource = GetActiveSourceMeshRenderer();
            if (activeSource == null)
            {
                if (_outlineObject != null)
                {
                    _outlineObject.SetActive(false);
                }
                return;
            }

            SyncWithSource(activeSource);
        }

        private void CheckAndSyncActiveSkin()
        {
            SkinnedMeshRenderer currentSource = GetActiveSourceMeshRenderer();
            if (currentSource != _lastSourceSmr || (_outlineRenderer != null && _outlineRenderer.sharedMesh != currentSource?.sharedMesh))
            {
                if (currentSource == null)
                {
                    if (_outlineObject != null) _outlineObject.SetActive(false);
                    _lastSourceSmr = null;
                }
                else
                {
                    SyncWithSource(currentSource);
                }
            }
        }

        private void SyncWithSource(SkinnedMeshRenderer source)
        {
            EnsureOutlineObject();
            if (_outlineRenderer == null || source == null) return;

            _lastSourceSmr = source;
            _outlineRenderer.sharedMesh = source.sharedMesh;
            _outlineRenderer.bones = source.bones;
            _outlineRenderer.rootBone = source.rootBone;
            _outlineRenderer.updateWhenOffscreen = source.updateWhenOffscreen;

            ApplyMaterialProperties();

            if (!_outlineObject.activeSelf)
            {
                _outlineObject.SetActive(true);
            }
        }

        private void EnsureOutlineObject()
        {
            if (_outlineObject == null)
            {
                Transform existing = transform.Find("Visual_Outline_Renderer");
                if (existing != null)
                {
                    _outlineObject = existing.gameObject;
                }
                else
                {
                    _outlineObject = new GameObject("Visual_Outline_Renderer");
                    _outlineObject.transform.SetParent(transform, false);
                    _outlineObject.transform.localPosition = Vector3.zero;
                    _outlineObject.transform.localRotation = Quaternion.identity;
                    _outlineObject.transform.localScale = Vector3.one;
                }
            }

            if (_outlineRenderer == null && _outlineObject != null)
            {
                _outlineRenderer = _outlineObject.GetComponent<SkinnedMeshRenderer>();
                if (_outlineRenderer == null)
                {
                    _outlineRenderer = _outlineObject.AddComponent<SkinnedMeshRenderer>();
                }

                _outlineRenderer.shadowCastingMode = ShadowCastingMode.Off;
                _outlineRenderer.receiveShadows = false;
            }

            if (_outlineMaterial == null)
            {
                _outlineMaterial = Resources.Load<Material>("Runner_Outline_Mat_V2");
                if (_outlineMaterial == null)
                {
#if UNITY_EDITOR
                    _outlineMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Runner_Outline_Mat_V2.mat");
#endif
                }
            }

            ApplyMaterialProperties();
        }

        private void ApplyMaterialProperties()
        {
            if (_outlineRenderer == null) return;

            if (_outlineMaterial != null && _outlineRenderer.sharedMaterial != _outlineMaterial)
            {
                _outlineRenderer.sharedMaterial = _outlineMaterial;
            }

            if (_propBlock == null)
            {
                _propBlock = new MaterialPropertyBlock();
            }

            _outlineRenderer.GetPropertyBlock(_propBlock);
            _propBlock.SetColor("_OutlineColor", _outlineColor);
            _propBlock.SetFloat("_OutlineWidth", _outlineWidth);
            _outlineRenderer.SetPropertyBlock(_propBlock);
        }
    }
}
