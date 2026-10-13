using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lotusim
{
    public class EnvironmentalWarningHUD : MonoBehaviour, IHudPanel
    {
        [Header("Content")]
        [SerializeField] private string title = "ENVIRONMENTAL WARNING";
        [SerializeField, TextArea] private string message =
            "Strong underwater current detected on BlueROV fleet. Battery consumption exceeding safety limits.";
        [SerializeField] private string icon = "[!]";

        [Header("Behaviour")]
        [SerializeField] private bool startVisible = false;
        [Tooltip("Key that toggles the box. Also shown in the hint text at the bottom right.")]
        [SerializeField] private KeyCode toggleKey = KeyCode.K;
        [SerializeField] private bool showHint = true;
        [Tooltip("Seconds before the box hides itself. 0 = stay until Hide() is called.")]
        [SerializeField] private float autoHideSeconds = 0f;

        [Header("Sound")]
        [Tooltip("Played once each time the box appears. Leave empty for no sound.")]
        [SerializeField] private AudioClip appearSound;
        [SerializeField, Range(0f, 1f)] private float soundVolume = 0.8f;

        [Header("Layout")]
        [Tooltip("Vertical position as a fraction of screen height (0 = bottom, 1 = top).")]
        [SerializeField, Range(0f, 1f)] private float verticalPosition = 0.75f;
        [SerializeField] private Vector2 panelSize = new Vector2(440f, 120f);
        [SerializeField] private int sortingOrder = 251;

        [Header("Colors")]
        [SerializeField] private Color panelColor   = new Color(0.09f, 0.07f, 0.11f, 0.86f);
        [SerializeField] private Color accentColor  = new Color(0.92f, 0.20f, 0.20f, 1f);
        [SerializeField] private Color titleColor   = new Color(0.95f, 0.26f, 0.26f, 1f);
        [SerializeField] private Color iconColor    = new Color(0.95f, 0.26f, 0.26f, 1f);
        [SerializeField] private Color messageColor = new Color(0.96f, 0.96f, 0.98f, 1f);
        [SerializeField] private Color hintColor    = new Color(1f, 1f, 1f, 0.40f);

        private GameObject m_panel;
        private TMP_Text m_titleText;
        private TMP_Text m_messageText;
        private AudioSource m_audio;
        private float m_hideAt = -1f;

        public bool IsVisible => m_panel != null && m_panel.activeSelf;
        public bool IsOpen => IsVisible;
        public void SetOpen(bool open) { if (open) Show(); else Hide(); }

        // ---------------------------------------------------------------------------------

        private void Awake()
        {
            m_audio = gameObject.AddComponent<AudioSource>();
            m_audio.playOnAwake = false;
            m_audio.spatialBlend = 0f;

            BuildUI();
            m_panel.SetActive(startVisible);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyBindings.ToggleWarning))
            {
                if (IsVisible) Hide();
                else Show();
            }

            if (m_hideAt > 0f && Time.unscaledTime >= m_hideAt)
                Hide();
        }

        // ---------------------------------------------------------------------------------

        /// <summary>Shows the box. Pass text to override the inspector title/message.</summary>
        [ContextMenu("Show")]
        public void Show() => Show(null, null);

        public void Show(string newMessage, string newTitle = null)
        {
            if (!string.IsNullOrEmpty(newTitle)) m_titleText.text = newTitle;
            if (!string.IsNullOrEmpty(newMessage)) m_messageText.text = newMessage;

            bool wasVisible = IsVisible;
            m_panel.SetActive(true);

            if (!wasVisible && appearSound != null)
                m_audio.PlayOneShot(appearSound, soundVolume);

            m_hideAt = autoHideSeconds > 0f ? Time.unscaledTime + autoHideSeconds : -1f;
        }

        [ContextMenu("Hide")]
        public void Hide()
        {
            m_hideAt = -1f;
            if (m_panel != null) m_panel.SetActive(false);
        }

        // ---------------------------------------------------------------------------------

        private void BuildUI()
        {
            GameObject canvasGO = new GameObject("EnvironmentalWarning_Canvas");
            canvasGO.transform.SetParent(transform, false);
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // Panel — anchored to (0.5, verticalPosition) so it stays centred on X at any resolution.
            m_panel = new GameObject("Panel");
            m_panel.transform.SetParent(canvasGO.transform, false);
            Image bg = m_panel.AddComponent<Image>();
            bg.color = panelColor;
            bg.raycastTarget = false;
            RectTransform pRT = bg.rectTransform;
            pRT.anchorMin = pRT.anchorMax = new Vector2(0.5f, verticalPosition);
            pRT.pivot = new Vector2(0.5f, 0.5f);
            pRT.sizeDelta = panelSize;
            pRT.anchoredPosition = Vector2.zero;

            // Accent bar — full-height strip on the left edge
            GameObject barGO = new GameObject("AccentBar");
            barGO.transform.SetParent(m_panel.transform, false);
            Image bar = barGO.AddComponent<Image>();
            bar.color = accentColor;
            bar.raycastTarget = false;
            RectTransform bRT = bar.rectTransform;
            bRT.anchorMin = new Vector2(0f, 0f);
            bRT.anchorMax = new Vector2(0f, 1f);
            bRT.pivot = new Vector2(0f, 0.5f);
            bRT.sizeDelta = new Vector2(6f, 0f);
            bRT.anchoredPosition = Vector2.zero;

            // Title — top, centred, wide letter spacing
            m_titleText = CreateTMP("Title", m_panel.transform);
            m_titleText.text = title;
            m_titleText.fontSize = 17f;
            m_titleText.fontStyle = FontStyles.Bold;
            m_titleText.characterSpacing = 3f;
            m_titleText.color = titleColor;
            m_titleText.alignment = TextAlignmentOptions.Center;
            RectTransform tRT = m_titleText.rectTransform;
            tRT.anchorMin = new Vector2(0f, 1f);
            tRT.anchorMax = new Vector2(1f, 1f);
            tRT.pivot = new Vector2(0.5f, 1f);
            tRT.offsetMin = new Vector2(20f, -38f);
            tRT.offsetMax = new Vector2(-12f, -12f);

            // Icon — left, vertically centred between title and hint
            TMP_Text iconText = CreateTMP("Icon", m_panel.transform);
            iconText.text = icon;
            iconText.fontSize = 32f;
            iconText.fontStyle = FontStyles.Bold;
            iconText.color = iconColor;
            iconText.alignment = TextAlignmentOptions.Center;
            iconText.enableWordWrapping = false;
            RectTransform iRT = iconText.rectTransform;
            iRT.anchorMin = Vector2.zero;
            iRT.anchorMax = new Vector2(0f, 1f);
            iRT.pivot = new Vector2(0f, 0.5f);
            iRT.offsetMin = new Vector2(20f, 24f);
            iRT.offsetMax = new Vector2(76f, -42f);

            m_messageText = CreateTMP("Message", m_panel.transform);
            m_messageText.text = message;
            m_messageText.fontSize = 14f;
            m_messageText.color = messageColor;
            m_messageText.alignment = TextAlignmentOptions.MidlineLeft;
            m_messageText.enableWordWrapping = true;
            RectTransform mRT = m_messageText.rectTransform;
            mRT.anchorMin = Vector2.zero;
            mRT.anchorMax = Vector2.one;
            mRT.offsetMin = new Vector2(84f, 26f);
            mRT.offsetMax = new Vector2(-16f, -42f);

            if (showHint)
            {
                TMP_Text hint = CreateTMP("Hint", m_panel.transform);
                hint.text = $"Press k to Dismiss";
                hint.fontSize = 10f;
                hint.fontStyle = FontStyles.Italic;
                hint.color = hintColor;
                hint.alignment = TextAlignmentOptions.BottomRight;
                hint.enableWordWrapping = false;
                RectTransform hRT = hint.rectTransform;
                hRT.anchorMin = new Vector2(0f, 0f);
                hRT.anchorMax = new Vector2(1f, 0f);
                hRT.pivot = new Vector2(0.5f, 0f);
                hRT.offsetMin = new Vector2(12f, 6f);
                hRT.offsetMax = new Vector2(-12f, 22f);
            }
        }

        private static TMP_Text CreateTMP(string n, Transform parent)
        {
            GameObject go = new GameObject(n);
            go.transform.SetParent(parent, false);
            TMP_Text t = go.AddComponent<TextMeshProUGUI>();
            t.raycastTarget = false;
            return t;
        }
    }
}