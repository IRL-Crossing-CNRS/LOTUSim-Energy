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

        private void Start()
        {
            BuildUI();
            UpdateHelpContent();
        }

        private void Update()
        {
            // Dynamically update prompt in case key changes
            togglePromptText.text = $"Press [{KeyBindings.ToggleHelp}] for Help";

            if (Input.GetKeyDown(KeyBindings.ToggleHelp))
            {
                helpPanel.SetActive(!helpPanel.activeSelf);
                if (helpPanel.activeSelf)
                {
                    // Refresh text when opening
                    UpdateHelpContent();
                }
            }
        }

        private void UpdateHelpContent()
        {
            StringBuilder sb = new StringBuilder();
            var fields = typeof(KeyBindings).GetFields(BindingFlags.Public | BindingFlags.Static);
            
            // Group by category, keep insertion order for categories
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
                sb.AppendLine(); // Empty line between categories
            }

            contentText.text = sb.ToString();
        }

        private void BuildUI()
        {
            GameObject canvasGO = new GameObject("HelpMenu_Canvas");
            canvasGO.transform.SetParent(transform, false);
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300; // Above graphs
            
            // Force Canvas to stretch if it's placed as a child of another UI element
            RectTransform canvasRT = canvasGO.GetComponent<RectTransform>();
            if (canvasRT != null)
            {
                canvasRT.anchorMin = Vector2.zero;
                canvasRT.anchorMax = Vector2.one;
                canvasRT.offsetMin = Vector2.zero;
                canvasRT.offsetMax = Vector2.zero;
            }

            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            // Prompt Text
            GameObject promptGO = new GameObject("TogglePrompt");
            promptGO.transform.SetParent(canvasGO.transform, false);
            togglePromptText = promptGO.AddComponent<TextMeshProUGUI>();
            RectTransform pRT = togglePromptText.rectTransform;
            pRT.anchorMin = new Vector2(1f, 0f); pRT.anchorMax = new Vector2(1f, 0f);
            pRT.pivot = new Vector2(1f, 0f);
            pRT.sizeDelta = new Vector2(300f, 30f);
            pRT.anchoredPosition = new Vector2(-20f, 20f); // 20px from bottom right corner
            togglePromptText.fontSize = 16f;
            togglePromptText.alignment = TextAlignmentOptions.BottomRight;
            togglePromptText.color = new Color(1f, 1f, 1f, 0.7f);
            togglePromptText.fontStyle = FontStyles.Bold;

            // Help Panel
            helpPanel = new GameObject("HelpPanel");
            helpPanel.transform.SetParent(canvasGO.transform, false);
            RectTransform hRT = helpPanel.AddComponent<RectTransform>();
            hRT.anchorMin = new Vector2(1f, 0f); hRT.anchorMax = new Vector2(1f, 0f);
            hRT.pivot = new Vector2(1f, 0f);
            hRT.anchoredPosition = new Vector2(-20f, 60f); // Above the prompt
            
            Image hBg = helpPanel.AddComponent<Image>();
            hBg.color = new Color(0.05f, 0.08f, 0.14f, 0.95f);

            // Layout Group for Auto-Resizing
            VerticalLayoutGroup vlg = helpPanel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(20, 20, 20, 20);
            vlg.spacing = 15f;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;

            ContentSizeFitter csf = helpPanel.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            helpPanel.SetActive(false);

            // Title
            TMP_Text title = CreateText("Title", helpPanel.transform, 18f, FontStyles.Bold, TextAlignmentOptions.Center);
            title.text = "LOTUSim Controls";

            // Content
            contentText = CreateText("Content", helpPanel.transform, 14f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
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
            t.enableWordWrapping = false; // Forces exact width for ContentSizeFitter
            return t;
        }
    }
}
