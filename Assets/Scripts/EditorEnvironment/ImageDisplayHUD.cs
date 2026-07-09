/*
 * Copyright (c) 2025 Naval Group
 *
 * This program and the accompanying materials are made available under the
 * terms of the Eclipse Public License 2.0 which is available at
 * https://www.eclipse.org/legal/epl-2.0.
 *
 * SPDX-License-Identifier: EPL-2.0
 */

// --------------------------------------------------------------------------------------------------------------------
//  ImageDisplayHUD.cs
// Description:
//  Displays a full-screen (or sized) image overlay when the user presses the configured key (default: I).
//  Assign the target Sprite in the Inspector. The panel is toggled on/off each key press.
// --------------------------------------------------------------------------------------------------------------------

using UnityEngine;
using UnityEngine.UI;

namespace Lotusim
{
    public class ImageDisplayHUD : MonoBehaviour
    {
        [Header("Image Settings")]
        [Tooltip("The sprite/image to display when toggled on.")]
        public Sprite displaySprite;

        [Tooltip("Max width of the image panel in pixels (0 = full screen width).")]
        public float maxWidth = 0f;

        [Tooltip("Max height of the image panel in pixels (0 = full screen height).")]
        public float maxHeight = 0f;

        [Tooltip("Background overlay opacity (0 = transparent, 1 = opaque black).")]
        [Range(0f, 1f)]
        public float backgroundAlpha = 0.7f;

        [Header("UI Settings")]
        [Tooltip("Canvas sorting order (higher = on top of other HUDs).")]
        public int sortingOrder = 200;

        // ── Runtime references ────────────────────────────────────────────────
        private GameObject _panel;
        private Image _imageComponent;
        private bool _isVisible;

        // ─────────────────────────────────────────────────────────────────────

        private void Start()
        {
            BuildUI();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyBindings.ToggleImage))
                SetVisible(!_isVisible);
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Force-show or hide the image panel.</summary>
        public void SetVisible(bool visible)
        {
            _isVisible = visible;
            _panel.SetActive(_isVisible);
        }

        /// <summary>Swap the displayed sprite at runtime.</summary>
        public void SetSprite(Sprite sprite)
        {
            displaySprite = sprite;
            if (_imageComponent != null)
                _imageComponent.sprite = sprite;
        }

        // ── UI Construction ───────────────────────────────────────────────────

        private void BuildUI()
        {
            // Canvas
            var canvasGO = new GameObject("ImageDisplay_Canvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            // Dim overlay (click-through — no Raycaster needed on this layer)
            var overlayGO = new GameObject("DimOverlay");
            overlayGO.transform.SetParent(canvasGO.transform, false);
            var overlayImg = overlayGO.AddComponent<Image>();
            overlayImg.color = new Color(0f, 0f, 0f, backgroundAlpha);
            var overlayRT = overlayGO.GetComponent<RectTransform>();
            overlayRT.anchorMin = Vector2.zero;
            overlayRT.anchorMax = Vector2.one;
            overlayRT.offsetMin = Vector2.zero;
            overlayRT.offsetMax = Vector2.zero;

            // Image panel — centred, respects maxWidth/maxHeight
            var panelGO = new GameObject("ImagePanel");
            panelGO.transform.SetParent(canvasGO.transform, false);
            var panelRT = panelGO.AddComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.5f, 0.5f);
            panelRT.anchorMax = new Vector2(0.5f, 0.5f);
            panelRT.pivot = new Vector2(0.5f, 0.5f);
            panelRT.anchoredPosition = Vector2.zero;

            // Size: use sprite native size clamped to max; fall back to full screen
            Vector2 size = ComputeSize();
            panelRT.sizeDelta = size;

            _imageComponent = panelGO.AddComponent<Image>();
            _imageComponent.sprite = displaySprite;
            _imageComponent.color = Color.white;
            _imageComponent.preserveAspect = true;

            _panel = panelGO;

            // Wrap both overlay and panel so we can hide them together
            _panel = new GameObject("ImageDisplay_Root");
            _panel.transform.SetParent(canvasGO.transform, false);
            var rootRT = _panel.AddComponent<RectTransform>();
            rootRT.anchorMin = Vector2.zero;
            rootRT.anchorMax = Vector2.one;
            rootRT.offsetMin = Vector2.zero;
            rootRT.offsetMax = Vector2.zero;

            overlayGO.transform.SetParent(_panel.transform, false);
            panelGO.transform.SetParent(_panel.transform, false);

            _panel.SetActive(false);
        }

        private Vector2 ComputeSize()
        {
            float w = (maxWidth > 0f) ? maxWidth : Screen.width;
            float h = (maxHeight > 0f) ? maxHeight : Screen.height;

            if (displaySprite != null)
            {
                // Fit sprite into the available box while preserving aspect ratio
                float spriteW = displaySprite.rect.width;
                float spriteH = displaySprite.rect.height;
                float scale = Mathf.Min(w / spriteW, h / spriteH);
                return new Vector2(spriteW * scale, spriteH * scale);
            }

            return new Vector2(w, h);
        }
    }
}
