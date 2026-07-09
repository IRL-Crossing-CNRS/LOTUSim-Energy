/*
 * Copyright (c) 2025 Naval Group
 *
 * This program and the accompanying materials are made available under the
 * terms of the Eclipse Public License 2.0 which is available at
 * https://www.eclipse.org/legal/epl-2.0.
 *
 * SPDX-License-Identifier: EPL-2.0
 */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Lotusim
{
    /// <summary>
    /// Displays a maintenance overview of the drone fleet.
    /// Toggled with the 'O' key (KeyBindings.ToggleMaintenance).
    /// </summary>
    public class MaintenanceHUD : MonoBehaviour
    {
        [Header("Design Settings")]
        [SerializeField] private Color m_panelColor = new Color(0.04f, 0.06f, 0.10f, 0.85f);
        [SerializeField] private Color m_accentColor = new Color(0.22f, 0.62f, 1.00f, 1f);
        [SerializeField] private float m_panelWidth = 450f;
        [SerializeField] private float m_panelHeight = 350f;
        [SerializeField] private int m_sortingOrder = 250;

        private GameObject m_root;
        private bool m_isVisible = false;

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

            if (Input.GetKeyDown(KeyBindings.ToggleMaintenance))
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
            GameObject canvasGO = new GameObject("MaintenanceHUD_Canvas");
            canvasGO.transform.SetParent(transform, false);
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = m_sortingOrder;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            // Root Container
            m_root = new GameObject("MaintenanceHUD_Root");
            m_root.transform.SetParent(canvasGO.transform, false);
            RectTransform rootRT = m_root.AddComponent<RectTransform>();
            rootRT.anchorMin = Vector2.zero;
            rootRT.anchorMax = Vector2.one;
            rootRT.offsetMin = Vector2.zero;
            rootRT.offsetMax = Vector2.zero;

            // Background Dim (Subtle)
            GameObject dimGO = new GameObject("DimOverlay");
            dimGO.transform.SetParent(m_root.transform, false);
            Image dimImg = dimGO.AddComponent<Image>();
            dimImg.color = new Color(0, 0, 0, 0.4f);
            RectTransform dimRT = dimGO.GetComponent<RectTransform>();
            dimRT.anchorMin = Vector2.zero;
            dimRT.anchorMax = Vector2.one;
            dimRT.offsetMin = Vector2.zero;
            dimRT.offsetMax = Vector2.zero;

            // Main Panel
            GameObject panelGO = new GameObject("MainPanel");
            panelGO.transform.SetParent(m_root.transform, false);
            Image panelImg = panelGO.AddComponent<Image>();
            panelImg.color = m_panelColor;
            RectTransform panelRT = panelGO.GetComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.5f, 0.5f);
            panelRT.anchorMax = new Vector2(0.5f, 0.5f);
            panelRT.pivot = new Vector2(0.5f, 0.5f);
            panelRT.sizeDelta = new Vector2(m_panelWidth, m_panelHeight);
            
            // Add a subtle border
            GameObject borderGO = new GameObject("Border");
            borderGO.transform.SetParent(panelGO.transform, false);
            Image borderImg = borderGO.AddComponent<Image>();
            borderImg.color = new Color(m_accentColor.r, m_accentColor.g, m_accentColor.b, 0.3f);
            RectTransform borderRT = borderGO.GetComponent<RectTransform>();
            borderRT.anchorMin = Vector2.zero;
            borderRT.anchorMax = Vector2.one;
            borderRT.offsetMin = new Vector2(-2, -2);
            borderRT.offsetMax = new Vector2(2, 2);
            // Move border behind panel content
            borderGO.transform.SetAsFirstSibling();

            // Header
            TMP_Text headerText = CreateText("Header", panelGO.transform, "FLEET MAINTENANCE OVERVIEW", 20, FontStyles.Bold, TextAlignmentOptions.Center);
            RectTransform headerRT = headerText.rectTransform;
            headerRT.anchorMin = new Vector2(0, 1);
            headerRT.anchorMax = new Vector2(1, 1);
            headerRT.pivot = new Vector2(0.5f, 1);
            headerRT.anchoredPosition = new Vector2(0, -20);
            headerRT.sizeDelta = new Vector2(0, 40);

            // Divider
            GameObject dividerGO = new GameObject("Divider");
            dividerGO.transform.SetParent(panelGO.transform, false);
            Image dividerImg = dividerGO.AddComponent<Image>();
            dividerImg.color = m_accentColor;
            RectTransform divRT = dividerGO.GetComponent<RectTransform>();
            divRT.anchorMin = new Vector2(0.1f, 1);
            divRT.anchorMax = new Vector2(0.9f, 1);
            divRT.anchoredPosition = new Vector2(0, -60);
            divRT.sizeDelta = new Vector2(0, 2);

            // Content Area
            GameObject contentGO = new GameObject("Content");
            contentGO.transform.SetParent(panelGO.transform, false);
            RectTransform contentRT = contentGO.AddComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0.1f, 0);
            contentRT.anchorMax = new Vector2(0.9f, 0.8f);
            contentRT.offsetMin = Vector2.zero;
            contentRT.offsetMax = Vector2.zero;

            // Drone Entries
            CreateDroneEntry(contentGO.transform, "X500 FLEET", "5 UNITS ACTIVE", "12.5 MIN", "OPTIMAL", 0);
            CreateDroneEntry(contentGO.transform, "WAMV FLEET", "5 UNITS ACTIVE", "42.0 MIN", "PENDING", 1);
            CreateDroneEntry(contentGO.transform, "BLUEROV FLEET", "5 UNITS ACTIVE", "28.5 MIN", "OPTIMAL", 2);

            // Footer / Hint
            TMP_Text hintText = CreateText("Hint", panelGO.transform, $"Press [{KeyBindings.ToggleMaintenance}] to Close", 12, FontStyles.Italic, TextAlignmentOptions.Center);
            hintText.color = new Color(0.6f, 0.6f, 0.6f);
            RectTransform hintRT = hintText.rectTransform;
            hintRT.anchorMin = new Vector2(0, 0);
            hintRT.anchorMax = new Vector2(1, 0);
            hintRT.pivot = new Vector2(0.5f, 0);
            hintRT.anchoredPosition = new Vector2(0, 10);
            hintRT.sizeDelta = new Vector2(0, 20);

            m_root.SetActive(false);
        }

        private void CreateDroneEntry(Transform parent, string type, string count, string time, string status, int index)
        {
            float yOffset = -index * 80;

            GameObject entryGO = new GameObject(type + "_Entry");
            entryGO.transform.SetParent(parent, false);
            RectTransform entryRT = entryGO.AddComponent<RectTransform>();
            entryRT.anchorMin = new Vector2(0, 1);
            entryRT.anchorMax = new Vector2(1, 1);
            entryRT.pivot = new Vector2(0.5f, 1);
            entryRT.anchoredPosition = new Vector2(0, yOffset);
            entryRT.sizeDelta = new Vector2(0, 70);

            // Type Name
            TMP_Text typeText = CreateText("Type", entryGO.transform, type, 16, FontStyles.Bold, TextAlignmentOptions.Left);
            typeText.rectTransform.anchorMin = new Vector2(0, 1);
            typeText.rectTransform.anchorMax = new Vector2(0.6f, 1);
            typeText.rectTransform.anchoredPosition = new Vector2(0, -10);
            typeText.rectTransform.sizeDelta = new Vector2(0, 25);

            // Unit Count
            TMP_Text countText = CreateText("Count", entryGO.transform, count, 11, FontStyles.Normal, TextAlignmentOptions.Left);
            countText.color = new Color(0.7f, 0.7f, 0.7f);
            countText.rectTransform.anchorMin = new Vector2(0, 1);
            countText.rectTransform.anchorMax = new Vector2(0.6f, 1);
            countText.rectTransform.anchoredPosition = new Vector2(0, -35);
            countText.rectTransform.sizeDelta = new Vector2(0, 20);

            // Maintenance Time Label
            TMP_Text timeLabel = CreateText("TimeLabel", entryGO.transform, "EST. MAINT:", 10, FontStyles.Normal, TextAlignmentOptions.Right);
            timeLabel.color = new Color(0.5f, 0.5f, 0.5f);
            timeLabel.rectTransform.anchorMin = new Vector2(0.6f, 1);
            timeLabel.rectTransform.anchorMax = new Vector2(1, 1);
            timeLabel.rectTransform.anchoredPosition = new Vector2(0, -10);
            timeLabel.rectTransform.sizeDelta = new Vector2(0, 20);

            // Maintenance Time
            TMP_Text timeValue = CreateText("TimeValue", entryGO.transform, time, 18, FontStyles.Bold, TextAlignmentOptions.Right);
            timeValue.color = m_accentColor;
            timeValue.rectTransform.anchorMin = new Vector2(0.6f, 1);
            timeValue.rectTransform.anchorMax = new Vector2(1, 1);
            timeValue.rectTransform.anchoredPosition = new Vector2(0, -35);
            timeValue.rectTransform.sizeDelta = new Vector2(0, 30);
            
            // Status Tag
            GameObject statusGO = new GameObject("StatusTag");
            statusGO.transform.SetParent(entryGO.transform, false);
            Image statusImg = statusGO.AddComponent<Image>();
            statusImg.color = status == "OPTIMAL" ? new Color(0.1f, 0.6f, 0.1f, 0.4f) : new Color(0.8f, 0.5f, 0.1f, 0.4f);
            RectTransform statusRT = statusGO.GetComponent<RectTransform>();
            statusRT.anchorMin = new Vector2(0, 0);
            statusRT.anchorMax = new Vector2(0.2f, 0);
            statusRT.anchoredPosition = new Vector2(0, 5);
            statusRT.sizeDelta = new Vector2(70, 18);
            
            TMP_Text statusText = CreateText("Status", statusGO.transform, status, 9, FontStyles.Bold, TextAlignmentOptions.Center);
            statusText.rectTransform.anchorMin = Vector2.zero;
            statusText.rectTransform.anchorMax = Vector2.one;
            statusText.rectTransform.offsetMin = Vector2.zero;
            statusText.rectTransform.offsetMax = Vector2.zero;
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
