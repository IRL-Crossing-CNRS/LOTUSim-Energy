/*
 * Copyright (c) 2025 Naval Group
 *
 * This program and the accompanying materials are made available under the
 * terms of the Eclipse Public License 2.0 which is available at
 * https://www.eclipse.org/legal/epl-2.0.
 *
 * SPDX-License-Identifier: EPL-2.0
 */

using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Lotusim
{
    /// <summary>
    /// Displays a fake detection uncertainty warning on drone cameras.
    /// Toggled with the 'J' key (KeyBindings.ToggleDetectionWarning).
    /// </summary>
    public class DetectionWarningHUD : MonoBehaviour
    {
        [Header("Design Settings")]
        [SerializeField] private Color m_panelColor = new Color(0.12f, 0.10f, 0.02f, 0.85f);
        [SerializeField] private Color m_amberColor = new Color(1.00f, 0.75f, 0.10f, 1f);
        [SerializeField] private float m_panelWidth = 350f;
        [SerializeField] private float m_panelHeight = 80f;
        [SerializeField] private int m_sortingOrder = 280;

        private GameObject m_root;
        private bool m_isVisible = false;

        private void Start()
        {
            BuildUI();
        }

        private void Update()
        {
            // Only allow toggle/visibility if we are on a drone camera slot (Slot 2, 3, or 4 in DisplaySwitcher)
            if (DisplaySwitcher.Instance != null && (DisplaySwitcher.Instance.ActiveSlotIndex < 2 || DisplaySwitcher.Instance.ActiveSlotIndex > 4))
            {
                if (m_isVisible) SetVisible(false);
                return;
            }

            if (Input.GetKeyDown(KeyBindings.ToggleDetectionWarning))
            {
                SetVisible(!m_isVisible);
            }
        }

        public void SetVisible(bool visible)
        {
            m_isVisible = visible;
            if (m_root != null)
                m_root.SetActive(m_isVisible);
        }

        private void BuildUI()
        {
            // Canvas
            GameObject canvasGO = new GameObject("DetectionWarningHUD_Canvas");
            canvasGO.transform.SetParent(transform, false);
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = m_sortingOrder;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            // Root Container
            m_root = new GameObject("DetectionWarningHUD_Root");
            m_root.transform.SetParent(canvasGO.transform, false);
            RectTransform rootRT = m_root.AddComponent<RectTransform>();
            rootRT.anchorMin = Vector2.zero;
            rootRT.anchorMax = Vector2.one;
            rootRT.offsetMin = Vector2.zero;
            rootRT.offsetMax = Vector2.zero;

            // Main Panel
            GameObject panelGO = new GameObject("Panel");
            panelGO.transform.SetParent(m_root.transform, false);
            Image panelImg = panelGO.AddComponent<Image>();
            panelImg.color = m_panelColor;
            RectTransform panelRT = panelGO.GetComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.5f, 0.15f); // Bottom center
            panelRT.anchorMax = new Vector2(0.5f, 0.15f);
            panelRT.pivot = new Vector2(0.5f, 0f);
            panelRT.sizeDelta = new Vector2(m_panelWidth, m_panelHeight);
            
            // Subtle glow border
            GameObject borderGO = new GameObject("Border");
            borderGO.transform.SetParent(panelGO.transform, false);
            Image borderImg = borderGO.AddComponent<Image>();
            borderImg.color = new Color(m_amberColor.r, m_amberColor.g, m_amberColor.b, 0.4f);
            RectTransform borderRT = borderGO.GetComponent<RectTransform>();
            borderRT.anchorMin = Vector2.zero;
            borderRT.anchorMax = Vector2.one;
            borderRT.offsetMin = new Vector2(-1, -1);
            borderRT.offsetMax = new Vector2(1, 1);
            borderGO.transform.SetAsFirstSibling();

            // Icon
            TMP_Text iconText = CreateText("Icon", panelGO.transform, "[!]", 18, FontStyles.Bold, TextAlignmentOptions.Center);
            iconText.color = m_amberColor;
            RectTransform iconRT = iconText.rectTransform;
            iconRT.anchorMin = new Vector2(0, 0.5f);
            iconRT.anchorMax = new Vector2(0, 0.5f);
            iconRT.anchoredPosition = new Vector2(30, 0);
            iconRT.sizeDelta = new Vector2(30, 30);

            // Text content
            TMP_Text titleText = CreateText("Title", panelGO.transform, "DETECTION UNCERTAINTY", 12, FontStyles.Bold, TextAlignmentOptions.Left);
            titleText.color = m_amberColor;
            RectTransform titleRT = titleText.rectTransform;
            titleRT.anchorMin = new Vector2(0, 1);
            titleRT.anchorMax = new Vector2(1, 1);
            titleRT.anchoredPosition = new Vector2(60, -20);
            titleRT.sizeDelta = new Vector2(-70, 20);

            TMP_Text msgText = CreateText("Message", panelGO.transform, "Fault detected below confidence threshold. Manual verification required.", 10, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            msgText.color = Color.white;
            msgText.enableWordWrapping = true;
            RectTransform msgRT = msgText.rectTransform;
            msgRT.anchorMin = Vector2.zero;
            msgRT.anchorMax = Vector2.one;
            msgRT.offsetMin = new Vector2(60, 10);
            msgRT.offsetMax = new Vector2(-15, -35);

            m_root.SetActive(false);
        }

        private TMP_Text CreateText(string name, Transform parent, string content, float size, FontStyles style, TextAlignmentOptions alignment)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            TMP_Text text = go.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }
    }
}
