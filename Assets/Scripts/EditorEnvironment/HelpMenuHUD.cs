using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Lotusim
{
    public class HelpMenuHUD : MonoBehaviour
    {
        private GameObject helpPanel;
        private TMP_Text togglePromptText;
        private TMP_Text contentText;
        private RectTransform contentRT;

        [SerializeField] private float maxPanelHeightRatio = 0.75f;
        [SerializeField, Range(0.1f, 0.9f)] private float panelWidthRatio = 0.28f;

        private void Start()
        {
            BuildUI();
            UpdateHelpContent();
        }

        private void Update()
        {
            togglePromptText.text = $"Press [{KeyBindings.ToggleHelp}] for Help";

            if (Input.GetKeyDown(KeyBindings.ToggleHelp))
            {
                helpPanel.SetActive(!helpPanel.activeSelf);
                if (helpPanel.activeSelf)
                {
                    UpdateHelpContent();
                }
            }
        }

        private void UpdateHelpContent()
        {
            StringBuilder sb = new StringBuilder();
            var fields = typeof(KeyBindings).GetFields(BindingFlags.Public | BindingFlags.Static);

            Dictionary<string, List<string>> categories = new Dictionary<string, List<string>>();
            List<string> categoryOrder = new List<string>();

            foreach (var field in fields)
            {
                var attr = field.GetCustomAttribute<KeyDescriptionAttribute>();
                if (attr != null)
                {
                    if (!categories.ContainsKey(attr.Category))
                    {
                        categories[attr.Category] = new List<string>();
                        categoryOrder.Add(attr.Category);
                    }

                    KeyCode key = (KeyCode)field.GetValue(null);
                    categories[attr.Category].Add($"{attr.Description}: [{key}]");
                }
            }

            foreach (var cat in categoryOrder)
            {
                sb.AppendLine($"<color=#55A0FF><b>{cat}</b></color>");
                foreach (var line in categories[cat])
                {
                    sb.AppendLine(line);
                }
                sb.AppendLine();
            }

            contentText.text = sb.ToString();

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentText.rectTransform);
            float textHeight = LayoutUtility.GetPreferredHeight(contentText.rectTransform);
            contentRT.sizeDelta = new Vector2(0f, textHeight + 40f);

            RectTransform textRT = contentText.rectTransform;
            textRT.sizeDelta = new Vector2(textRT.sizeDelta.x, textHeight);
        }

        private void BuildUI()
        {
            GameObject canvasGO = new GameObject("HelpMenu_Canvas");
            canvasGO.transform.SetParent(transform, false);
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;

            RectTransform canvasRT = canvasGO.GetComponent<RectTransform>();
            if (canvasRT != null)
            {
                canvasRT.anchorMin = Vector2.zero;
                canvasRT.anchorMax = Vector2.one;
                canvasRT.offsetMin = Vector2.zero;
                canvasRT.offsetMax = Vector2.zero;
            }

            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            GameObject promptGO = new GameObject("TogglePrompt");
            promptGO.transform.SetParent(canvasGO.transform, false);
            togglePromptText = promptGO.AddComponent<TextMeshProUGUI>();
            RectTransform pRT = togglePromptText.rectTransform;
            pRT.anchorMin = new Vector2(1f, 0f); pRT.anchorMax = new Vector2(1f, 0f);
            pRT.pivot = new Vector2(1f, 0f);
            pRT.sizeDelta = new Vector2(300f, 30f);
            pRT.anchoredPosition = new Vector2(-20f, 20f);
            togglePromptText.fontSize = 16f;
            togglePromptText.alignment = TextAlignmentOptions.BottomRight;
            togglePromptText.color = new Color(1f, 1f, 1f, 0.7f);
            togglePromptText.fontStyle = FontStyles.Bold;

            helpPanel = new GameObject("HelpPanel");
            helpPanel.transform.SetParent(canvasGO.transform, false);
            RectTransform hRT = helpPanel.AddComponent<RectTransform>();
            hRT.anchorMin = new Vector2(1f - panelWidthRatio, 0f);
            hRT.anchorMax = new Vector2(1f, 1f);
            hRT.pivot = new Vector2(1f, 0f);
            hRT.offsetMin = new Vector2(10f, 60f);
            hRT.offsetMax = new Vector2(-20f, -40f);

            Image hBg = helpPanel.AddComponent<Image>();
            hBg.color = new Color(0.05f, 0.08f, 0.14f, 0.95f);
            helpPanel.SetActive(false);

            GameObject titleGO = new GameObject("Title");
            titleGO.transform.SetParent(helpPanel.transform, false);
            TMP_Text title = titleGO.AddComponent<TextMeshProUGUI>();
            title.text = "LOTUSim Controls";
            title.fontSize = 18f;
            title.fontStyle = FontStyles.Bold;
            title.alignment = TextAlignmentOptions.Center;
            title.color = Color.white;
            RectTransform titleRT = title.rectTransform;
            titleRT.anchorMin = new Vector2(0f, 1f);
            titleRT.anchorMax = new Vector2(1f, 1f);
            titleRT.pivot = new Vector2(0.5f, 1f);
            titleRT.anchoredPosition = new Vector2(0f, -10f);
            titleRT.sizeDelta = new Vector2(-20f, 30f);

            GameObject scrollGO = new GameObject("ScrollView");
            scrollGO.transform.SetParent(helpPanel.transform, false);
            RectTransform scrollRT = scrollGO.AddComponent<RectTransform>();
            scrollRT.anchorMin = new Vector2(0f, 0f);
            scrollRT.anchorMax = new Vector2(1f, 1f);
            scrollRT.offsetMin = new Vector2(10f, 10f);
            scrollRT.offsetMax = new Vector2(-10f, -50f);

            ScrollRect scrollRect = scrollGO.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollGO.AddComponent<RectMask2D>();

            GameObject viewportGO = new GameObject("Viewport");
            viewportGO.transform.SetParent(scrollGO.transform, false);
            RectTransform viewportRT = viewportGO.AddComponent<RectTransform>();
            viewportRT.anchorMin = Vector2.zero;
            viewportRT.anchorMax = Vector2.one;
            viewportRT.offsetMin = Vector2.zero;
            viewportRT.offsetMax = Vector2.zero;
            viewportGO.AddComponent<Image>().color = new Color(0, 0, 0, 0.01f);
            scrollRect.viewport = viewportRT;

            GameObject contentGO = new GameObject("Content");
            contentGO.transform.SetParent(viewportGO.transform, false);
            contentRT = contentGO.AddComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0f, 1f);
            contentRT.anchorMax = new Vector2(1f, 1f);
            contentRT.pivot = new Vector2(0.5f, 1f);
            contentRT.anchoredPosition = Vector2.zero;
            contentRT.sizeDelta = new Vector2(0f, 0f);

            scrollRect.content = contentRT;

            contentText = CreateText("KeybindText", contentGO.transform, 14f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            RectTransform textRT = contentText.rectTransform;
            textRT.anchorMin = new Vector2(0f, 1f);
            textRT.anchorMax = new Vector2(1f, 1f);
            textRT.pivot = new Vector2(0.5f, 1f);
            textRT.offsetMin = new Vector2(20f, textRT.offsetMin.y);
            textRT.offsetMax = new Vector2(-20f, textRT.offsetMax.y);
            textRT.anchoredPosition = new Vector2(0f, -20f);
        }

        private TMP_Text CreateText(string name, Transform parent, float fontSize, FontStyles style, TextAlignmentOptions align)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            TMP_Text t = go.AddComponent<TextMeshProUGUI>();
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.alignment = align;
            t.color = Color.white;
            t.richText = true;
            t.enableWordWrapping = true;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }
    }
}