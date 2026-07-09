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
//  WindHUDIndicator.cs
//
//  Description:
//  Wii-Sports-style wind indicator drawn pixel-by-pixel onto a Texture2D.
//  Renders a circular dark panel with a directional arrow (triangle head + shaft).
//  Placed bottom-left of the screen.
//
//  Setup:
//  1. Add this component to any GameObject.
//  2. Assign 'windController'.
//  3. Everything is created at runtime — no prefab or sprite needed.
// --------------------------------------------------------------------------------------------------------------------

using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Lotusim
{
    /// <summary>
    /// Pixel-drawn circular wind direction HUD panel in bottom-left corner.
    /// Shows a rotating arrow (direction) and speed label.
    /// </summary>
    public class WindHUDIndicator : MonoBehaviour
    {
        // ---------------------------------------------------------------------------------
        #region Inspector Fields

        [Header("Data Source")]
        [SerializeField] private WindSliderController windController;

        [Tooltip("Transform whose Y rotation drives the compass (usually the player or main camera). " +
                 "If null, falls back to Camera.main.")]
        [SerializeField] private Transform playerOrCamera;

        [Header("Layout")]
        [Tooltip("Position offset from the bottom-left corner.")]
        [SerializeField] private Vector2 panelOffset = new Vector2(28f, 28f);

        [Tooltip("Display size of the panel in pixels.")]
        [SerializeField] private float panelSize = 105f;

        [Header("Visual")]
        [SerializeField] private Color bgColor       = new Color(0.04f, 0.07f, 0.14f, 0.82f);
        [SerializeField] private Color ringColor     = new Color(0.35f, 0.70f, 1.00f, 0.50f);
        [SerializeField] private Color arrowColorLow  = new Color(0.40f, 0.88f, 1.00f, 1f);
        [SerializeField] private Color arrowColorHigh = new Color(1.00f, 0.32f, 0.10f, 1f);

        [Tooltip("Wind speed (m/s) at which color is fully shifted to arrowColorHigh.")]
        [SerializeField] private float maxWindForColor = 20f;

        [Tooltip("Arrow rotation smoothing (degrees/second).")]
        [SerializeField] private float rotationSmoothing = 200f;

        [Tooltip("Texture resolution in pixels (square). 180 is a good balance.")]
        [SerializeField] private int textureSize = 180;

        #endregion
        // ---------------------------------------------------------------------------------

        #region Private Fields

        private Texture2D m_texture;
        private Color[]   m_pixels;
        private RawImage  m_rawImage;
        private RectTransform m_rawRect;
        private TMP_Text  m_speedText;
        private TMP_Text  m_labelText;
        private TMP_Text[] m_cardinalLabels; // N, E, S, W

        // World-space compass angle (degrees, clockwise from north) for each cardinal label.
        private static readonly float[] s_cardinalAngles = { 0f, 90f, 180f, 270f };
        private static readonly string[] s_cardinalNames = { "N", "E", "S", "W" };

        private float m_currentAngle  = 0f;
        private float m_currentYaw    = 0f;
        private float m_windMagnitude = 0f;

        #endregion
        // ---------------------------------------------------------------------------------

        #region Unity Lifecycle

        private void Start()
        {
            BuildCanvas();
            // Draw initial static frame
            RedrawTexture(0f, 0f);
        }

        private void Update()
        {
            if (windController == null) return;

            Vector3 wind = windController.CurrentWindVector;
            float wx = wind.x;
            float wz = wind.z; // Slider Z = north/south -> Unity world Z

            m_windMagnitude = Mathf.Sqrt(wx * wx + wz * wz);

            // Smooth arrow rotation
            float targetAngle = m_windMagnitude > 0.05f
                ? Mathf.Atan2(wx, wz) * Mathf.Rad2Deg
                : m_currentAngle;

            m_currentAngle = Mathf.MoveTowardsAngle(
                m_currentAngle, targetAngle, rotationSmoothing * Time.deltaTime);

            // Track the assigned transform, or fall back to the main camera
            Transform target = playerOrCamera != null ? playerOrCamera : (Camera.main != null ? Camera.main.transform : null);
            
            bool shouldTrackCamera = true;
            
            // Reliably determine camera state from the CameraModeSwitcher
            CameraModeSwitcher modeSwitcher = Object.FindAnyObjectByType<CameraModeSwitcher>();
            if (modeSwitcher != null)
            {
                FreeFlyCamera ffc = modeSwitcher.GetComponent<FreeFlyCamera>();
                if (ffc != null)
                {
                    shouldTrackCamera = ffc.enabled;
                }
            }

            if (shouldTrackCamera)
            {
                m_currentYaw = ComputeStableYaw(target);
            }
            else
            {
                // In Auto mode or animations, force N to point to true World North
                m_currentYaw = 0f;
            }

            RedrawTexture(m_currentAngle - m_currentYaw, m_windMagnitude);
            UpdateCardinalLabels(m_currentYaw);
            UpdateLabels();
        }

        private void OnDestroy()
        {
            if (m_texture != null) Destroy(m_texture);
        }

        // Yaw derived from the forward vector projected onto the world XZ plane. Unlike
        // Transform.eulerAngles.y, this is invariant to pitch/roll discontinuities — it stays
        // numerically stable even when FreeFlyCamera's pitch math wraps past ±90° (where
        // eulerAngles can flip x/y/z by 180° while the camera visually faces the same direction).
        private static float ComputeStableYaw(Transform t)
        {
            if (t == null) return 0f;
            Vector3 fwd = t.forward;
            fwd.y = 0f;
            // Looking nearly straight up/down: fall back to projecting the up vector instead.
            if (fwd.sqrMagnitude < 1e-4f)
            {
                fwd = -t.up;
                fwd.y = 0f;
                if (fwd.sqrMagnitude < 1e-4f) return 0f;
            }
            // Atan2(x, z): 0° when forward = +Z (north), +90° when forward = +X (east).
            return Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
        }

        #endregion
        // ---------------------------------------------------------------------------------

        #region Canvas Construction

        private void BuildCanvas()
        {
            // ── Canvas ──────────────────────────────────────────────────────────────────
            GameObject canvasGO = new GameObject("WindHUD_Canvas");
            canvasGO.transform.SetParent(transform, false);

            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            // ── Outer container (bottom-left anchor) ───────────────────────────────────
            GameObject containerGO = new GameObject("WindHUD_Container");
            containerGO.transform.SetParent(canvasGO.transform, false);
            RectTransform container = containerGO.AddComponent<RectTransform>();
            container.anchorMin = container.anchorMax = Vector2.zero; // bottom-left
            container.pivot = Vector2.zero;
            container.sizeDelta = new Vector2(panelSize, panelSize + 28f);
            container.anchoredPosition = panelOffset;

            // ── RawImage for the drawn circle+arrow ────────────────────────────────────
            m_texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
            m_texture.filterMode = FilterMode.Bilinear;
            m_pixels = new Color[textureSize * textureSize];

            GameObject rawGO = new GameObject("WindHUD_Circle");
            rawGO.transform.SetParent(container, false);
            m_rawImage = rawGO.AddComponent<RawImage>();
            m_rawImage.texture = m_texture;

            m_rawRect = m_rawImage.rectTransform;
            m_rawRect.anchorMin = new Vector2(0f, 0.18f);
            m_rawRect.anchorMax = new Vector2(1f, 1f);
            m_rawRect.offsetMin = m_rawRect.offsetMax = Vector2.zero;

            // ── Cardinal letters (N / E / S / W) — children of the circle ──────────────
            m_cardinalLabels = new TMP_Text[4];
            for (int i = 0; i < 4; i++)
            {
                TMP_Text card = CreateTMPText("WindHUD_Card_" + s_cardinalNames[i], rawGO.transform);
                RectTransform crt = card.rectTransform;
                crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
                crt.pivot = new Vector2(0.5f, 0.5f);
                crt.sizeDelta = new Vector2(18f, 18f);
                card.text = s_cardinalNames[i];
                card.fontSize = 12f;
                card.fontStyle = FontStyles.Bold;
                card.color = (i == 0) // N gets a touch more emphasis
                    ? new Color(1f, 1f, 1f, 0.95f)
                    : new Color(1f, 1f, 1f, 0.65f);
                card.alignment = TextAlignmentOptions.Center;
                m_cardinalLabels[i] = card;
            }

            // ── Bottom row: "WIND" on the left, "x.x m/s" on the right ─────────────────
            m_labelText = CreateTMPText("WindHUD_Label", container);
            RectTransform labelRect = m_labelText.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = new Vector2(0.5f, 0.18f);
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            m_labelText.text = "WIND";
            m_labelText.fontSize = 13f;
            m_labelText.fontStyle = FontStyles.Bold;
            m_labelText.color = new Color(1f, 1f, 1f, 0.55f);
            m_labelText.alignment = TextAlignmentOptions.MidlineRight;
            m_labelText.margin = new Vector4(0f, 0f, 6f, 0f);

            m_speedText = CreateTMPText("WindHUD_Speed", container);
            RectTransform speedRect = m_speedText.rectTransform;
            speedRect.anchorMin = new Vector2(0.5f, 0f);
            speedRect.anchorMax = new Vector2(1f, 0.18f);
            speedRect.offsetMin = speedRect.offsetMax = Vector2.zero;
            m_speedText.text = "0.0 m/s";
            m_speedText.fontSize = 13f;
            m_speedText.fontStyle = FontStyles.Bold;
            m_speedText.color = Color.white;
            m_speedText.alignment = TextAlignmentOptions.MidlineLeft;
            m_speedText.margin = new Vector4(6f, 0f, 0f, 0f);
        }

        private void UpdateCardinalLabels(float yaw)
        {
            if (m_cardinalLabels == null || m_rawRect == null) return;

            // Place each cardinal at the compass ring radius, rotating with the player so
            // that the letter for world-north sits at world-north on the HUD.
            float radius = m_rawRect.rect.width * 0.5f * 0.78f; // matches compass ring
            for (int i = 0; i < m_cardinalLabels.Length; i++)
            {
                float a = (s_cardinalAngles[i] - yaw) * Mathf.Deg2Rad;
                Vector2 pos = new Vector2(Mathf.Sin(a) * radius, Mathf.Cos(a) * radius);
                m_cardinalLabels[i].rectTransform.anchoredPosition = pos;
            }
        }

        private TMP_Text CreateTMPText(string goName, Transform parent)
        {
            GameObject go = new GameObject(goName);
            go.transform.SetParent(parent, false);
            return go.AddComponent<TextMeshProUGUI>();
        }

        #endregion
        // ---------------------------------------------------------------------------------

        #region Texture Drawing

        private void RedrawTexture(float angleDeg, float magnitude)
        {
            int sz = textureSize;
            float cx = sz * 0.5f;
            float cy = sz * 0.5f;
            float outerR   = sz * 0.465f;
            float ringW     = sz * 0.025f;

            float t = Mathf.Clamp01(magnitude / maxWindForColor);
            Color arrowCol  = Color.Lerp(arrowColorLow, arrowColorHigh, t);

            // Arrow geometry (in local space, pointing UP = positive-y before rotation)
            float shaftHW = sz * 0.050f;   // shaft half-width
            float shaftLen = sz * 0.285f;   // shaft length
            float headW    = sz * 0.155f;   // arrowhead base half-width
            float headH    = sz * 0.190f;   // arrowhead height
            float totalH   = shaftLen + headH;
            float tailY    = -totalH * 0.5f;
            float neckY    = tailY + shaftLen;
            float tipY     = neckY + headH;

            float angleRad = angleDeg * Mathf.Deg2Rad;
            // Positive angle → clockwise rotation → arrow points right for positive X wind.
            float cosA = Mathf.Cos(angleRad);
            float sinA = Mathf.Sin(angleRad);

            for (int py = 0; py < sz; py++)
            {
                for (int px = 0; px < sz; px++)
                {
                    float lx = px - cx;
                    float ly = py - cy;
                    float dist = Mathf.Sqrt(lx * lx + ly * ly);

                    Color c = Color.clear;

                    if (dist <= outerR)
                    {
                        // Fill circle background
                        c = bgColor;

                        // Outer ring
                        if (dist >= outerR - ringW)
                        {
                            float blend = (dist - (outerR - ringW)) / ringW;
                            c = Color.Lerp(bgColor, ringColor, blend);
                        }

                        // When magnitude is near zero: draw a calm dot at center instead of arrow
                        if (magnitude < 0.05f)
                        {
                            float calmDotR = sz * 0.06f;
                            if (lx * lx + ly * ly <= calmDotR * calmDotR)
                                c = BlendOver(c, new Color(1f, 1f, 1f, 0.55f));
                        }
                        else
                        {
                            // Rotate pixel coords to arrow local space
                            float rx = lx * cosA - ly * sinA;
                            float ry = lx * sinA + ly * cosA;

                            // Shaft: rectangle
                            bool inShaft = Mathf.Abs(rx) <= shaftHW
                                        && ry >= tailY && ry <= neckY;

                            // Head: filled triangle — wider at neckY, point at tipY
                            bool inHead = false;
                            if (ry > neckY && ry <= tipY)
                            {
                                float progress = (tipY - ry) / headH; // 1 at base, 0 at tip
                                inHead = Mathf.Abs(rx) <= progress * headW;
                            }

                            if (inShaft || inHead)
                            {
                                Color ac = inHead
                                    ? arrowCol
                                    : new Color(arrowCol.r * 0.78f, arrowCol.g * 0.78f, arrowCol.b * 0.78f, arrowCol.a);

                                if (inShaft)
                                {
                                    float edgeFade = 1f - Mathf.Clamp01((Mathf.Abs(rx) - shaftHW * 0.7f) / (shaftHW * 0.3f));
                                    ac.a *= edgeFade;
                                }

                                c = BlendOver(c, ac);
                            }
                        }
                    }

                    m_pixels[py * sz + px] = c;
                }
            }

            m_texture.SetPixels(m_pixels);
            m_texture.Apply(false);
        }

        /// <summary>Alpha-composite src over dst.</summary>
        private static Color BlendOver(Color dst, Color src)
        {
            float a = src.a + dst.a * (1f - src.a);
            if (a < 0.0001f) return Color.clear;
            return new Color(
                (src.r * src.a + dst.r * dst.a * (1f - src.a)) / a,
                (src.g * src.a + dst.g * dst.a * (1f - src.a)) / a,
                (src.b * src.a + dst.b * dst.a * (1f - src.a)) / a,
                a);
        }

        #endregion
        // ---------------------------------------------------------------------------------

        #region Label Update

        private void UpdateLabels()
        {
            m_speedText.text = $"{m_windMagnitude:F1} m/s";
            float t = Mathf.Clamp01(m_windMagnitude / maxWindForColor);
            m_speedText.color = Color.Lerp(Color.white, arrowColorHigh, t * 0.55f);
        }

        #endregion
    }
}
