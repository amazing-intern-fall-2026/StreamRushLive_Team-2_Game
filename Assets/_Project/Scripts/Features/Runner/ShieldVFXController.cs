using DG.Tweening;
using UnityEngine;
using StreamRushLive.Features.Spawning;

namespace SteamRush.Features.Runner
{
    // Bat/tat VFX khien bao ve (Shield Shader FREE - hologram sphere) quanh Runner theo RunnerItemEffects.IsShieldActive.
    // RunnerItemEffects khong co event san (chi co IsShieldActive + ActivateShield()/ConsumeShield()),
    // nen poll moi frame giong het pattern ShieldTimerCircle.Update() da dung cho khung dem nguoc -
    // tranh sua RunnerItemEffects.cs (file core dung chung cho ShieldItem/MockChatConsole/collision).
    public class ShieldVFXController : MonoBehaviour
    {
        [SerializeField] private RunnerItemEffects itemEffects;
        [SerializeField] private GameObject shieldVFXPrefab;

        [Tooltip("Vi tri tuong doi so voi Runner root - can chinh de khien bao quanh than nguoi, khong lech len dau/xuong chan.")]
        [SerializeField] private Vector3 localOffset = new Vector3(0f, 1.1f, 0f);

        [Tooltip("Nhan them vao scale goc cua prefab (mac dinh 1 - unit sphere) de tinh chinh kich thuoc khien ma khong can sua prefab.")]
        [SerializeField] private float scaleMultiplier = 1f;

        [SerializeField] private float animDuration = 0.35f;

        private GameObject _instance;
        private bool _isActive;

        private void Awake()
        {
            if (itemEffects == null)
            {
                itemEffects = GetComponent<RunnerItemEffects>();
                if (itemEffects == null) itemEffects = GetComponentInParent<RunnerItemEffects>();
            }
        }

        private void Update()
        {
            if (itemEffects == null) return;

            bool active = itemEffects.IsShieldActive;
            if (active == _isActive) return;

            _isActive = active;
            if (active) Show();
            else Hide();
        }

        private void Show()
        {
            if (shieldVFXPrefab == null) return;

            if (_instance != null)
            {
                DOTween.Kill(_instance.transform);
                Destroy(_instance);
            }

            _instance = Instantiate(shieldVFXPrefab, transform);
            _instance.transform.localPosition = localOffset;
            _instance.transform.localRotation = Quaternion.identity;

            Vector3 fullScale = shieldVFXPrefab.transform.localScale * scaleMultiplier;
            _instance.transform.localScale = Vector3.zero;
            _instance.transform.DOScale(fullScale, animDuration).SetEase(Ease.OutBack);
        }

        private void Hide()
        {
            if (_instance == null) return;

            GameObject toDestroy = _instance;
            _instance = null;

            DOTween.Kill(toDestroy.transform);
            toDestroy.transform.DOScale(Vector3.zero, animDuration * 0.6f)
                .SetEase(Ease.InBack)
                .OnComplete(() =>
                {
                    if (toDestroy != null) Destroy(toDestroy);
                });
        }

        private void OnDisable()
        {
            if (_instance != null)
            {
                DOTween.Kill(_instance.transform);
                Destroy(_instance);
                _instance = null;
            }
            _isActive = false;
        }
    }
}
