namespace SteamRush.Features.UI.Views
{
    using UnityEngine;
    using UnityEngine.UI;

    /// <summary>
    /// Controls the How-To-Play Guide ("Command To Play") banner.
    /// Can display in two modes:
    /// - Dual Column: Team Blue (runner control) and Team Red (obstacle spawner).
    /// - Red Only: When manual keyboard control is enabled, Blue chat control is locked and only Team Red instructions are displayed.
    /// </summary>
    public class HowToPlayGuideController : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private RectTransform _modalCard;
        [SerializeField] private RectTransform _body;
        [SerializeField] private GameObject _fanColumn;
        [SerializeField] private GameObject _vDivider;
        [SerializeField] private GameObject _antiColumn;

        private readonly Vector2 _defaultAnchorMin = new Vector2(0f, 0.21f);
        private readonly Vector2 _defaultAnchorMax = new Vector2(1f, 0.29f);
        private readonly Vector2 _defaultSizeDelta = new Vector2(-40f, 136f);

        private readonly Vector2 _singleColumnAnchorMin = new Vector2(0.5f, 0.21f);
        private readonly Vector2 _singleColumnAnchorMax = new Vector2(0.5f, 0.29f);
        private readonly Vector2 _singleColumnSizeDelta = new Vector2(540f, 136f);

        private void Awake()
        {
            AutoWireIfNull();
        }

        private void OnEnable()
        {
            var mgr = SteamRush.Features.UI.PreGameConfig.PreGameConfigManager.Instance;
            if (mgr != null && mgr.CurrentConfig != null)
            {
                SetRedOnlyMode(mgr.CurrentConfig.enableKeyboardControl);
            }

            SteamRush.Core.Localization.LocalizationManager.OnLanguageChanged += UpdateLanguage;
            UpdateLanguage(SteamRush.Core.Localization.LocalizationManager.Instance != null
                ? SteamRush.Core.Localization.LocalizationManager.Instance.CurrentLanguage
                : SteamRush.Core.Localization.AppLanguage.English);
        }

        private void OnDisable()
        {
            SteamRush.Core.Localization.LocalizationManager.OnLanguageChanged -= UpdateLanguage;
        }

        public void UpdateLanguage(SteamRush.Core.Localization.AppLanguage lang)
        {
            var loc = SteamRush.Core.Localization.LocalizationManager.Instance;
            if (loc == null) return;

            AutoWireIfNull();

            // Header Title
            var title = transform.Find("ModalCard/Header/Follow to play")?.GetComponent<TMPro.TMP_Text>();
            if (title != null) title.text = loc.Get("HTP_TITLE", title.text);

            // Fan Column
            if (_fanColumn != null)
            {
                var fanTexts = _fanColumn.GetComponentsInChildren<TMPro.TMP_Text>(true);
                if (fanTexts.Length >= 4)
                {
                    fanTexts[0].text = loc.Get("HTP_FAN_TITLE", fanTexts[0].text);
                    fanTexts[1].text = loc.Get("HTP_FAN_JOIN", fanTexts[1].text);
                    fanTexts[2].text = loc.Get("HTP_FAN_MOVE", fanTexts[2].text);
                    fanTexts[3].text = loc.Get("HTP_FAN_JUMP", fanTexts[3].text);
                }
            }

            // Anti Column
            if (_antiColumn != null)
            {
                var antiTexts = _antiColumn.GetComponentsInChildren<TMPro.TMP_Text>(true);
                if (antiTexts.Length >= 3)
                {
                    antiTexts[0].text = loc.Get("HTP_ANTI_TITLE", antiTexts[0].text);
                    antiTexts[1].text = loc.Get("HTP_ANTI_JOIN", antiTexts[1].text);
                    antiTexts[2].text = loc.Get("HTP_ANTI_SPAWN", antiTexts[2].text);
                }
            }
        }

        public void AutoWireIfNull()
        {
            if (_modalCard == null)
            {
                var mc = transform.Find("ModalCard");
                if (mc != null) _modalCard = mc.GetComponent<RectTransform>();
            }

            if (_body == null && _modalCard != null)
            {
                var b = _modalCard.Find("Body");
                if (b != null) _body = b.GetComponent<RectTransform>();
            }

            if (_fanColumn == null && _body != null)
            {
                var f = _body.Find("FanColumn");
                if (f != null) _fanColumn = f.gameObject;
            }

            if (_vDivider == null && _body != null)
            {
                var vd = _body.Find("VDivider");
                if (vd != null) _vDivider = vd.gameObject;
            }

            if (_antiColumn == null && _body != null)
            {
                var a = _body.Find("AntiColumn");
                if (a != null) _antiColumn = a.gameObject;
            }
        }

        public void SetRedOnlyMode(bool redOnly)
        {
            AutoWireIfNull();

            if (_fanColumn != null)
            {
                _fanColumn.SetActive(!redOnly);
            }

            if (_vDivider != null)
            {
                _vDivider.SetActive(false);
            }

            if (_antiColumn != null)
            {
                _antiColumn.SetActive(true);
            }

            if (_modalCard != null)
            {
                if (redOnly)
                {
                    _modalCard.anchorMin = _singleColumnAnchorMin;
                    _modalCard.anchorMax = _singleColumnAnchorMax;
                    _modalCard.sizeDelta = _singleColumnSizeDelta;
                    _modalCard.anchoredPosition = new Vector2(0f, _modalCard.anchoredPosition.y);
                }
                else
                {
                    _modalCard.anchorMin = _defaultAnchorMin;
                    _modalCard.anchorMax = _defaultAnchorMax;
                    _modalCard.sizeDelta = _defaultSizeDelta;
                    _modalCard.anchoredPosition = new Vector2(0f, _modalCard.anchoredPosition.y);
                }

                if (_body != null)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(_body);
                }
                LayoutRebuilder.ForceRebuildLayoutImmediate(_modalCard);
            }
        }
    }
}
