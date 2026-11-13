using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Lotusim
{
    public class CameraViewHUD : MonoBehaviour, IHudPanel
    {
        // ---------------------------------------------------------------------------------
        #region Inspector Fields

        [Header("Source")]
        [Tooltip("Camera whose view is shown in the HUD window. Assign a dedicated camera.")]
        [SerializeField] private Camera sourceCamera;

        [Header("Render Texture")]
        [SerializeField] private int textureWidth = 640;
        [SerializeField] private int textureHeight = 360;
        [SerializeField] private int depthBits = 16;

        [Header("Window")]
        [SerializeField] private string windowTitle = "Camera View";
        [Tooltip("If true, sourceCamera.enabled is turned off while the panel is closed.")]
        [SerializeField] private bool disableCameraWhenClosed = true;

        #endregion
        // ---------------------------------------------------------------------------------

        #region State

        private GameObject m_window;
        private RawImage m_feedImage;
        private TMP_Text m_titleText;
        private TMP_Text m_statusText;
        private RenderTexture m_renderTexture;
        private bool m_cameraWasEnabledBeforeOpen;

        #endregion
        // ---------------------------------------------------------------------------------

        #region Unity Lifecycle

        private void Start()
        {
            EnsureEventSystem();
            BuildUI();
            m_window.SetActive(false);
        }

        private void OnDestroy()
        {
            ReleaseRenderTexture();
        }

        #endregion
        // ---------------------------------------------------------------------------------

        #region IHudPanel

        public bool IsOpen => m_window != null && m_window.activeSelf;

        public void SetOpen(bool open)
        {
            if (m_window == null || open == m_window.activeSelf) return;

            m_window.SetActive(open);

            if (open) OpenFeed();
            else CloseFeed();
        }

        #endregion
        // ---------------------------------------------------------------------------------

        #region Feed Control

        private void OpenFeed()
        {
            if (sourceCamera == null)
            {
                m_statusText.text = "<color=#FF6E5A>No camera assigned</color>";
                m_feedImage.texture = null;
                return;
            }

            if (m_renderTexture == null)
            {
                m_renderTexture = new RenderTexture(textureWidth, textureHeight, depthBits)
                {
                    name = $"{name}_CameraViewHUD_RT",
                };
            }

            m_cameraWasEnabledBeforeOpen = sourceCamera.enabled;
            sourceCamera.targetTexture = m_renderTexture;
            if (disableCameraWhenClosed) sourceCamera.enabled = true;

            m_feedImage.texture = m_renderTexture;
            m_titleText.text = string.IsNullOrEmpty(windowTitle) ? sourceCamera.name : windowTitle;
            m_statusText.text = "<color=#55FF9D>● live</color>";
        }

        private void CloseFeed()
        {
            if (sourceCamera != null)
            {
                sourceCamera.targetTexture = null;
                if (disableCameraWhenClosed) sourceCamera.enabled = m_cameraWasEnabledBeforeOpen;
            }
            ReleaseRenderTexture();
            if (m_feedImage != null) m_feedImage.texture = null;
        }

        private void ReleaseRenderTexture()
        {
            if (m_renderTexture == null) return;
            if (sourceCamera != null && sourceCamera.targetTexture == m_renderTexture)
                sourceCamera.targetTexture = null;
            m_renderTexture.Release();
            Destroy(m_renderTexture);
            m_renderTexture = null;
        }

        #endregion
        // ---------------------------------------------------------------------------------

        #region UI Construction

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        private void BuildUI()
        {
            GameObject canvasGO = new GameObject("CameraViewHUD_Canvas");
            canvasGO.transform.SetParent(transform, false);
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            float widgetW = textureWidth + 16f;
            float widgetH = textureHeight + 40f;

            m_window = new GameObject("CameraViewHUD_Window");
            m_window.transform.SetParent(canvasGO.transform, false);
            RectTransform winRT = m_window.AddComponent<RectTransform>();
            winRT.anchorMin = winRT.anchorMax = new Vector2(0f, 0f);
            winRT.pivot = new Vector2(0f, 0f);
            winRT.sizeDelta = new Vector2(widgetW, widgetH);
            winRT.anchoredPosition = new Vector2(16f, 16f);
            m_window.AddComponent<Image>().color = new Color(0.05f, 0.08f, 0.14f, 1f);

            // Title
            m_titleText = CreateTMP("Title", m_window.transform);
            RectTransform tRT = m_titleText.rectTransform;
            tRT.anchorMin = new Vector2(0f, 1f); tRT.anchorMax = new Vector2(1f, 1f);
            tRT.pivot = new Vector2(0f, 1f);
            tRT.offsetMin = new Vector2(8f, -31f);
            tRT.offsetMax = new Vector2(-72f, -5f);
            m_titleText.text = windowTitle;
            m_titleText.fontSize = 13f;
            m_titleText.fontStyle = FontStyles.Bold;
            m_titleText.color = Color.white;
            m_titleText.alignment = TextAlignmentOptions.MidlineLeft;

            // Status indicator — top-right
            m_statusText = CreateTMP("Status", m_window.transform);
            RectTransform stRT = m_statusText.rectTransform;
            stRT.anchorMin = new Vector2(1f, 1f); stRT.anchorMax = new Vector2(1f, 1f);
            stRT.pivot = new Vector2(1f, 1f);
            stRT.sizeDelta = new Vector2(68f, 21f);
            stRT.anchoredPosition = new Vector2(-6f, -8f);
            m_statusText.fontSize = 10f;
            m_statusText.alignment = TextAlignmentOptions.MidlineRight;
            m_statusText.color = Color.white;
            m_statusText.richText = true;

            // Camera feed image
            GameObject feedGO = new GameObject("Feed");
            feedGO.transform.SetParent(m_window.transform, false);
            m_feedImage = feedGO.AddComponent<RawImage>();
            m_feedImage.raycastTarget = false;
            RectTransform feedRT = m_feedImage.rectTransform;
            feedRT.anchorMin = new Vector2(0f, 1f); feedRT.anchorMax = new Vector2(0f, 1f);
            feedRT.pivot = new Vector2(0f, 1f);
            feedRT.sizeDelta = new Vector2(textureWidth, textureHeight);
            feedRT.anchoredPosition = new Vector2(8f, -36f);
        }

        private TMP_Text CreateTMP(string n, Transform parent)
        {
            GameObject go = new GameObject(n);
            go.transform.SetParent(parent, false);
            TMP_Text t = go.AddComponent<TextMeshProUGUI>();
            t.raycastTarget = false;
            return t;
        }

        #endregion
    }
}