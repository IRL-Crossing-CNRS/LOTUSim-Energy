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
    /// Displays a fake emergency warning popup.
    /// Toggled with the 'K' key (KeyBindings.ToggleWarning).
    /// </summary>
    public class WarningHUD : MonoBehaviour
    {
        [Header("Design Settings")]
        [SerializeField] private Color m_panelColor = new Color(0.12f, 0.02f, 0.02f, 0.92f);
        [SerializeField] private Color m_warningColor = new Color(1.00f, 0.25f, 0.25f, 1f);
        [SerializeField] private float m_panelWidth = 400f;
        [SerializeField] private float m_panelHeight = 120f;
        [SerializeField] private int m_sortingOrder = 300;

        private GameObject m_root;
        private Image m_panelImg;
        private bool m_isVisible = false;
        private float m_pulseTimer = 0f;

        private void Start()
        {
            BuildUI();
        }

        private void Update()
        {
            // Only allow toggle/visibility if we are on Display 1 (Slot 0 in DisplaySwitcher)
            if (DisplaySwitcher.Instance != null && DisplaySwitcher.Instance.ActiveSlotIndex != 0)
            {
                if (m_isVisible) SetVisible(false);
                return;
            }

            if (Input.GetKeyDown(KeyBindings.ToggleWarning))
            {
                SetVisible(!m_isVisible);
            }

            if (m_isVisible)
            {
                // Subtle pulse animation for the warning color
                m_pulseTimer += Time.deltaTime * 2.5f;
                float alpha = 0.7f + Mathf.PingPong(m_pulseTimer, 0.3f);
                if (m_panelImg != null)
                {
                    // Update border or icon glow if we had one, for now just pulse the panel alpha slightly
                    m_panelImg.color = new Color(m_panelColor.r, m_panelColor.g, m_panelColor.b, alpha);
                }
            }
        }

        public void SetVisible(bool visible)
        {
            m_isVisible = visible;
            if (m_root != null)
            {
                m_root.SetActive(m_isVisible);
                if (m_isVisible)
                {
                    m_pulseTimer = 0f;
                    // Reset scale for a simple "pop" effect
                    m_root.transform.GetChild(1).localScale = Vector3.one * 0.8f;
                }
            }
        }

        private void LateUpdate()
        {
            if (m_isVisible && m_root != null)
            {
                // Smooth scale-up
                Transform panel = m_root.transform.GetChild(1);
                panel.localScale = Vector3.Lerp(panel.localScale, Vector3.one, Time.deltaTime * 10f);
            }
        }

        private void BuildUI()
        {
            // Canvas
            GameObject canvasGO = new GameObject("WarningHUD_Canvas");
            canvasGO.transform.SetParent(transform, false);
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = m_sortingOrder;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            // Root Container
            m_root = new GameObject("WarningHUD_Root");
            m_root.transform.SetParent(canvasGO.transform, false);
            RectTransform rootRT = m_root.AddComponent<RectTransform>();
            rootRT.anchorMin = Vector2.zero;
            rootRT.anchorMax = Vector2.one;
            rootRT.offsetMin = Vector2.zero;
            rootRT.offsetMax = Vector2.zero;

            // Background Dim (Very subtle for warning)
            GameObject dimGO = new GameObject("DimOverlay");
            dimGO.transform.SetParent(m_root.transform, false);
            Image dimImg = dimGO.AddComponent<Image>();
            dimImg.color = new Color(0.2f, 0, 0, 0.2f);
            RectTransform dimRT = dimGO.GetComponent<RectTransform>();
            dimRT.anchorMin = Vector2.zero;
            dimRT.anchorMax = Vector2.one;
            dimRT.offsetMin = Vector2.zero;
            dimRT.offsetMax = Vector2.zero;

            // Main Panel
            GameObject panelGO = new GameObject("WarningPanel");
            panelGO.transform.SetParent(m_root.transform, false);
            m_panelImg = panelGO.AddComponent<Image>();
            m_panelImg.color = m_panelColor;
            RectTransform panelRT = panelGO.GetComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.5f, 0.85f); // Top center
            panelRT.anchorMax = new Vector2(0.5f, 0.85f);
            panelRT.pivot = new Vector2(0.5f, 1f);
            panelRT.sizeDelta = new Vector2(m_panelWidth, m_panelHeight);
            
            // Red accent side bar
            GameObject accentGO = new GameObject("AccentBar");
            accentGO.transform.SetParent(panelGO.transform, false);
            Image accentImg = accentGO.AddComponent<Image>();
            accentImg.color = m_warningColor;
            RectTransform accentRT = accentGO.GetComponent<RectTransform>();
            accentRT.anchorMin = new Vector2(0, 0);
            accentRT.anchorMax = new Vector2(0, 1);
            accentRT.pivot = new Vector2(0, 0.5f);
            accentRT.anchoredPosition = Vector2.zero;
            accentRT.sizeDelta = new Vector2(6, 0);

            // Warning Icon Placeholder (Text based for now)
            TMP_Text iconText = CreateText("Icon", panelGO.transform, "[!]", 24, FontStyles.Bold, TextAlignmentOptions.Center);
            iconText.color = m_warningColor;
            RectTransform iconRT = iconText.rectTransform;
            iconRT.anchorMin = new Vector2(0, 0.5f);
            iconRT.anchorMax = new Vector2(0, 0.5f);
            iconRT.anchoredPosition = new Vector2(35, 0);
            iconRT.sizeDelta = new Vector2(40, 40);

            // Header
            TMP_Text headerText = CreateText("Header", panelGO.transform, "ENVIRONMENTAL WARNING", 14, FontStyles.Bold, TextAlignmentOptions.Left);
            headerText.color = m_warningColor;
            RectTransform headerRT = headerText.rectTransform;
            headerRT.anchorMin = new Vector2(0, 1);
            headerRT.anchorMax = new Vector2(1, 1);
            headerRT.anchoredPosition = new Vector2(70, -30);
            headerRT.sizeDelta = new Vector2(-80, 20);

            // Message
            TMP_Text msgText = CreateText("Message", panelGO.transform, "Strong underwater current detected on BlueROV fleet. Battery consumption exceeding safety limits.", 11, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            msgText.color = Color.white;
            msgText.enableWordWrapping = true;
            RectTransform msgRT = msgText.rectTransform;
            msgRT.anchorMin = new Vector2(0, 0);
            msgRT.anchorMax = new Vector2(1, 1);
            msgRT.offsetMin = new Vector2(70, 15);
            msgRT.offsetMax = new Vector2(-20, -45);

            // Close Hint
            TMP_Text hintText = CreateText("Hint", panelGO.transform, "Press [K] to Dismiss", 9, FontStyles.Italic, TextAlignmentOptions.Right);
            hintText.color = new Color(0.6f, 0.6f, 0.6f);
            RectTransform hintRT = hintText.rectTransform;
            hintRT.anchorMin = new Vector2(1, 0);
            hintRT.anchorMax = new Vector2(1, 0);
            hintRT.pivot = new Vector2(1, 0);
            hintRT.anchoredPosition = new Vector2(-10, 5);
            hintRT.sizeDelta = new Vector2(100, 15);

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
