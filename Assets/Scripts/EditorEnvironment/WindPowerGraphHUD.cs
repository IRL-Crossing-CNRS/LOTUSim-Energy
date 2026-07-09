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
//  WindPowerGraphHUD.cs
//
//  Description:
//  Toggleable popup window (default key: G) that records and plots PowerW (Watts) for every
//  WindTurbineController in the scene. The user manually starts/stops a recording and then
//  saves a CSV (one column per turbine, with time_s) and a PNG snapshot of the graph to
//  the Downloads folder.
//
//  Setup:
//  1. Add this component to any persistent GameObject in the scene.
//  2. Make sure the scene has WindTurbineController instances (auto-discovered on Start
//     and again whenever the panel is reopened or a new recording is started).
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Lotusim
{
    /// <summary>
    /// Records and plots PowerW for every wind turbine in the scene.
    /// Manual Start/Stop/Save workflow.
    /// </summary>
    public class WindPowerGraphHUD : MonoBehaviour
    {
        // ---------------------------------------------------------------------------------
        #region Inspector Fields


        [Header("Recording")]
        [Tooltip("Sampling rate (Hz). Each turbine's PowerW is captured at this rate.")]
        [SerializeField] private float sampleRateHz = 10f;

        [Header("Graph")]
        [SerializeField] private int graphWidth = 1000;
        [SerializeField] private int graphHeight = 500;
        [SerializeField] private Color bgColor = new Color(0.04f, 0.07f, 0.14f, 1.00f);
        [SerializeField] private Color gridColor = new Color(1.00f, 1.00f, 1.00f, 0.10f);
        [SerializeField] private Color axisColor = new Color(1.00f, 1.00f, 1.00f, 0.45f);

        [Header("Save")]
        [Tooltip("Subfolder name created inside the user's Downloads directory.")]
        [SerializeField] private string saveSubfolder = "FACET_WindPower";

        #endregion
        // ---------------------------------------------------------------------------------

        #region Types

        private class Series
        {
            public WindTurbineController controller;
            public string name;
            public Color color;
            public List<float> samples = new List<float>(2048);
        }

        private enum State { Idle, Recording, Stopped }

        #endregion
        // ---------------------------------------------------------------------------------

        #region State

        private State m_state = State.Idle;
        private readonly List<Series> m_series = new List<Series>();
        private float m_recordingTime = 0f;
        private float m_sampleAccum = 0f;
        private float m_maxPower = 1f;
        private bool m_graphDirty = true;
        private string m_toast = "";
        private float m_toastUntil = 0f;

        // UI
        private GameObject m_window;
        private RawImage m_graphImage;
        private Texture2D m_graphTex;
        private Color[] m_graphPixels;
        private TMP_Text m_statusText;
        private TMP_Text m_legendText;
        private TMP_Text m_toastText;
        private TMP_Text[] m_yTickLabels;
        private TMP_Text[] m_xTickLabels;

        private const int kTickCount = 5;          // 0%, 25%, 50%, 75%, 100%
        private const int kGridDivisions = 4;      // ticks line up with grid
        // Layout constants — must match the graph anchoring done in BuildUI.
        private const float kGraphAnchorX = 14f;
        private const float kGraphAnchorY = -36f;
        private const int kPadL = 48, kPadR = 12, kPadT = 12, kPadB = 24;

        private static readonly Color[] s_palette = {
            new Color(0.40f, 0.88f, 1.00f),
            new Color(1.00f, 0.55f, 0.20f),
            new Color(0.55f, 1.00f, 0.40f),
            new Color(1.00f, 0.40f, 0.85f),
            new Color(1.00f, 0.95f, 0.30f),
            new Color(0.55f, 0.70f, 1.00f),
            new Color(1.00f, 0.55f, 0.55f),
            new Color(0.85f, 0.65f, 1.00f),
        };

        #endregion
        // ---------------------------------------------------------------------------------

        #region Unity Lifecycle

        private void Start()
        {
            EnsureEventSystem();
            BuildUI();
            DiscoverTurbines();
            m_window.SetActive(false);
            RedrawGraph();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyBindings.TogglePowerGraph))
            {
                bool nowOpen = !m_window.activeSelf;
                SetWindowOpen(nowOpen);
            }

            if (!m_window.activeSelf) return;

            HandleKeyboardShortcuts();

            if (m_state == State.Recording)
            {
                m_recordingTime += Time.deltaTime;
                m_sampleAccum += Time.deltaTime;
                float interval = 1f / Mathf.Max(0.1f, sampleRateHz);
                while (m_sampleAccum >= interval)
                {
                    SampleOnce();
                    m_sampleAccum -= interval;
                    m_graphDirty = true;
                }
            }

            UpdateStatusText();
            UpdateLegendText();

            if (m_graphDirty)
            {
                RedrawGraph();
                m_graphDirty = false;
            }

            UpdateHintsOrToast();
        }

        private void OnDestroy()
        {
            if (m_graphTex != null) Destroy(m_graphTex);
        }

        #endregion
        // ---------------------------------------------------------------------------------

        #region Recording

        private void DiscoverTurbines()
        {
            // Preserve any in-progress recording by only refreshing when idle/stopped.
            if (m_state == State.Recording) return;

            m_series.Clear();
            var ctrls = UnityEngine.Object.FindObjectsByType<WindTurbineController>(
                FindObjectsSortMode.None);
            Array.Sort(ctrls, (a, b) => CompareNatural(a.name, b.name));
            for (int i = 0; i < ctrls.Length; i++)
            {
                m_series.Add(new Series
                {
                    controller = ctrls[i],
                    name = ctrls[i].name,
                    color = s_palette[i % s_palette.Length],
                });
            }
            m_graphDirty = true;
        }

        private void SampleOnce()
        {
            for (int i = 0; i < m_series.Count; i++)
            {
                float p = m_series[i].controller != null ? m_series[i].controller.PowerW : 0f;
                m_series[i].samples.Add(p);
                if (p > m_maxPower) m_maxPower = p;
            }
        }

        private void StartRecording()
        {
            DiscoverTurbines();
            for (int i = 0; i < m_series.Count; i++) m_series[i].samples.Clear();
            m_recordingTime = 0f;
            m_sampleAccum = 0f;
            m_maxPower = 1f;
            m_state = State.Recording;
            m_graphDirty = true;
            ShowToast("Recording started");
        }

        private void StopRecording()
        {
            m_state = State.Stopped;
            m_graphDirty = true;
            ShowToast($"Recording stopped — {m_recordingTime:F1}s captured");
        }

        private void NewRecording()
        {
            for (int i = 0; i < m_series.Count; i++) m_series[i].samples.Clear();
            m_recordingTime = 0f;
            m_sampleAccum = 0f;
            m_maxPower = 1f;
            m_state = State.Idle;
            m_graphDirty = true;
            ShowToast("Cleared — ready to record");
        }

        private void SaveRecording()
        {
            if (TotalSampleCount() == 0)
            {
                ShowToast("Nothing to save", 2.5f);
                return;
            }
            StartCoroutine(SaveRoutine());
        }

        // Captures the rendered window (including TMP tick labels, legend, status) so the saved
        // PNG matches what the user sees in-game. Buttons + toast are briefly hidden for a clean
        // snapshot, then restored.
        private System.Collections.IEnumerator SaveRoutine()
        {
            string folder = ResolveSaveFolder();
            string csvPath = Path.Combine(folder, "power.csv");
            string pngPath = Path.Combine(folder, "graph.png");

            bool csvOk = false;
            try
            {
                Directory.CreateDirectory(folder);
                File.WriteAllText(csvPath, BuildCsv());
                csvOk = true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[WindPowerGraphHUD] CSV save failed: {e}");
                ShowToast("Save failed — see console", 4f);
            }
            if (!csvOk) yield break;

            // Hide controls so the snapshot is just title + status + graph + axes + legend.
            SetControlsVisible(false);
            string prevToast = m_toastText.text;
            m_toastText.text = "";

            yield return new WaitForEndOfFrame();

            Texture2D full = null;
            try
            {
                full = ScreenCapture.CaptureScreenshotAsTexture();

                Vector3[] corners = new Vector3[4];
                ((RectTransform)m_window.transform).GetWorldCorners(corners);
                // ScreenSpaceOverlay → world corners are already in screen pixels (BL, TL, TR, BR).
                int x = Mathf.RoundToInt(corners[0].x);
                int y = Mathf.RoundToInt(corners[0].y);
                int w = Mathf.RoundToInt(corners[2].x - corners[0].x);
                int h = Mathf.RoundToInt(corners[2].y - corners[0].y);
                x = Mathf.Clamp(x, 0, full.width - 1);
                y = Mathf.Clamp(y, 0, full.height - 1);
                w = Mathf.Clamp(w, 1, full.width - x);
                h = Mathf.Clamp(h, 1, full.height - y);

                Color[] pixels = full.GetPixels(x, y, w, h);
                Texture2D crop = new Texture2D(w, h, TextureFormat.RGBA32, false);
                crop.SetPixels(pixels);
                crop.Apply(false);
                File.WriteAllBytes(pngPath, crop.EncodeToPNG());
                Destroy(crop);

                Debug.Log($"[WindPowerGraphHUD] Saved recording to: {folder}");
                ShowToast($"Saved to {folder}", 6f);
            }
            catch (Exception e)
            {
                Debug.LogError($"[WindPowerGraphHUD] PNG save failed: {e}");
                ShowToast("CSV saved, PNG failed — see console", 4f);
            }
            finally
            {
                if (full != null) Destroy(full);
                SetControlsVisible(true);
                m_toastText.text = prevToast;
            }
        }

        private void SetControlsVisible(bool _) { }

        private int TotalSampleCount()
        {
            int n = 0;
            for (int i = 0; i < m_series.Count; i++) n += m_series[i].samples.Count;
            return n;
        }

        private string BuildCsv()
        {
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();

            sb.Append("time_s");
            for (int i = 0; i < m_series.Count; i++)
                sb.Append(',').Append(EscapeCsv(m_series[i].name));
            sb.AppendLine();

            int maxLen = 0;
            for (int i = 0; i < m_series.Count; i++)
                if (m_series[i].samples.Count > maxLen) maxLen = m_series[i].samples.Count;

            float interval = 1f / Mathf.Max(0.1f, sampleRateHz);
            for (int row = 0; row < maxLen; row++)
            {
                float t = row * interval;
                sb.Append(t.ToString("F3", ci));
                for (int i = 0; i < m_series.Count; i++)
                {
                    sb.Append(',');
                    if (row < m_series[i].samples.Count)
                        sb.Append(m_series[i].samples[row].ToString("F3", ci));
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        // Natural-order compare: digit runs are compared as integers so "x_2" < "x_10".
        private static int CompareNatural(string a, string b)
        {
            int i = 0, j = 0;
            while (i < a.Length && j < b.Length)
            {
                bool da = char.IsDigit(a[i]);
                bool db = char.IsDigit(b[j]);
                if (da && db)
                {
                    int si = i, sj = j;
                    while (i < a.Length && char.IsDigit(a[i])) i++;
                    while (j < b.Length && char.IsDigit(b[j])) j++;
                    int la = i - si, lb = j - sj;
                    // strip leading zeros for length compare
                    int za = si; while (za < i - 1 && a[za] == '0') za++;
                    int zb = sj; while (zb < j - 1 && b[zb] == '0') zb++;
                    int ela = i - za, elb = j - zb;
                    if (ela != elb) return ela - elb;
                    for (int k = 0; k < ela; k++)
                    {
                        int d = a[za + k] - b[zb + k];
                        if (d != 0) return d;
                    }
                    if (la != lb) return la - lb; // shorter (more leading zeros) first
                }
                else
                {
                    int d = char.ToLowerInvariant(a[i]) - char.ToLowerInvariant(b[j]);
                    if (d != 0) return d;
                    i++; j++;
                }
            }
            return (a.Length - i) - (b.Length - j);
        }

        // Format Watts with a friendly unit (W / kW / MW) so big values stay readable.
        private static string FormatPower(float w)
        {
            float abs = Mathf.Abs(w);
            if (abs >= 1_000_000f) return $"{w / 1_000_000f:F2} MW";
            if (abs >= 1_000f) return $"{w / 1_000f:F1} kW";
            return $"{w:F0} W";
        }

        private static string EscapeCsv(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        private string ResolveSaveFolder()
        {
            string downloads = null;
            try
            {
                string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (!string.IsNullOrEmpty(profile))
                {
                    string candidate = Path.Combine(profile, "Downloads");
                    if (Directory.Exists(candidate)) downloads = candidate;
                }
            }
            catch { /* fall through to fallback */ }

            if (string.IsNullOrEmpty(downloads)) downloads = Application.persistentDataPath;

            string ts = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            return Path.Combine(downloads, saveSubfolder, $"recording_{ts}");
        }

        private void ShowToast(string msg, float seconds = 3f)
        {
            m_toast = msg;
            m_toastUntil = Time.unscaledTime + seconds;
        }

        #endregion
        // ---------------------------------------------------------------------------------

        #region Graph Drawing

        private void RedrawGraph()
        {
            int w = graphWidth, h = graphHeight;
            for (int i = 0; i < m_graphPixels.Length; i++) m_graphPixels[i] = bgColor;

            int plotW = w - kPadL - kPadR;
            int plotH = h - kPadT - kPadB;
            if (plotW < 4 || plotH < 4) { ApplyPixels(); return; }

            // NOTE: Texture2D pixel y=0 is the BOTTOM row. We work in that coordinate space here.
            int xLeft = kPadL;
            int xRight = kPadL + plotW;
            int yBottom = kPadB;            // plot baseline (low watts)
            int yTop = kPadB + plotH;    // plot top      (high watts)

            // Grid
            for (int g = 1; g < kGridDivisions; g++)
            {
                int yy = yBottom + (plotH * g) / kGridDivisions;
                DrawHLine(xLeft, xRight, yy, gridColor);
                int xx = xLeft + (plotW * g) / kGridDivisions;
                DrawVLine(xx, yBottom, yTop, gridColor);
            }
            // X axis (bottom) and Y axis (left)
            DrawHLine(xLeft, xRight, yBottom, axisColor);
            DrawVLine(xLeft, yBottom, yTop, axisColor);

            int maxLen = 0;
            for (int i = 0; i < m_series.Count; i++)
                if (m_series[i].samples.Count > maxLen) maxLen = m_series[i].samples.Count;

            float interval = 1f / Mathf.Max(0.1f, sampleRateHz);
            float duration = Mathf.Max(0.001f, (maxLen - 1) * interval);
            float yMax = Mathf.Max(1f, m_maxPower * 1.05f);

            UpdateTickLabels(yMax, duration);

            if (maxLen >= 2)
            {
                for (int i = 0; i < m_series.Count; i++)
                {
                    var s = m_series[i];
                    int n = s.samples.Count;
                    if (n < 2) continue;

                    int prevX = 0, prevY = 0;
                    for (int k = 0; k < n; k++)
                    {
                        float tNorm = (k * interval) / duration;
                        float vNorm = Mathf.Clamp01(s.samples[k] / yMax);
                        int xx = xLeft + Mathf.RoundToInt(tNorm * plotW);
                        int yy = yBottom + Mathf.RoundToInt(vNorm * plotH); // up = high watts
                        if (k > 0) DrawLine(prevX, prevY, xx, yy, s.color);
                        prevX = xx; prevY = yy;
                    }
                }
            }

            ApplyPixels();
        }

        private void UpdateTickLabels(float yMax, float duration)
        {
            if (m_yTickLabels == null) return;

            float plotW = graphWidth - kPadL - kPadR;
            float plotH = graphHeight - kPadT - kPadB;
            float rectBottom = kGraphAnchorY - graphHeight; // anchored y of rawimage bottom edge
            // Y label x position: just left of plot left edge
            float yLabelX = kGraphAnchorX + kPadL - 4f;
            // X label y position: just below plot bottom
            float xLabelY = rectBottom + kPadB - 4f;

            for (int i = 0; i < kTickCount; i++)
            {
                float t = i / (float)(kTickCount - 1); // 0, 0.25, 0.5, 0.75, 1
                // Y tick: rises from bottom to top
                float yPos = rectBottom + kPadB + t * plotH;
                m_yTickLabels[i].text = FormatPower(t * yMax);
                m_yTickLabels[i].rectTransform.anchoredPosition = new Vector2(yLabelX, yPos);

                // X tick
                float xPos = kGraphAnchorX + kPadL + t * plotW;
                m_xTickLabels[i].text = $"{t * duration:F1}s";
                m_xTickLabels[i].rectTransform.anchoredPosition = new Vector2(xPos, xLabelY);
            }
        }

        private void ApplyPixels()
        {
            m_graphTex.SetPixels(m_graphPixels);
            m_graphTex.Apply(false);
        }

        private void DrawHLine(int x0, int x1, int y, Color c)
        {
            if (y < 0 || y >= graphHeight) return;
            int a = Mathf.Clamp(Mathf.Min(x0, x1), 0, graphWidth - 1);
            int b = Mathf.Clamp(Mathf.Max(x0, x1), 0, graphWidth - 1);
            int row = y * graphWidth;
            for (int x = a; x <= b; x++)
                m_graphPixels[row + x] = BlendOver(m_graphPixels[row + x], c);
        }

        private void DrawVLine(int x, int y0, int y1, Color c)
        {
            if (x < 0 || x >= graphWidth) return;
            int a = Mathf.Clamp(Mathf.Min(y0, y1), 0, graphHeight - 1);
            int b = Mathf.Clamp(Mathf.Max(y0, y1), 0, graphHeight - 1);
            for (int y = a; y <= b; y++)
                m_graphPixels[y * graphWidth + x] = BlendOver(m_graphPixels[y * graphWidth + x], c);
        }

        private void DrawLine(int x0, int y0, int x1, int y1, Color c)
        {
            int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            while (true)
            {
                Plot(x0, y0, c);
                Plot(x0 + 1, y0, c); // 2px thickness
                Plot(x0, y0 + 1, c);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        private void Plot(int x, int y, Color c)
        {
            if (x < 0 || x >= graphWidth || y < 0 || y >= graphHeight) return;
            int idx = y * graphWidth + x;
            m_graphPixels[idx] = BlendOver(m_graphPixels[idx], c);
        }

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

        #region UI Construction

        private void EnsureEventSystem()
        {
            // If one already exists (configured by the project for whatever input system it uses),
            // don't touch it. Only create a barebones one if the scene has none at all.
            if (EventSystem.current != null) return;
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        private void BuildUI()
        {
            GameObject canvasGO = new GameObject("WindPowerGraph_Canvas");
            canvasGO.transform.SetParent(transform, false);
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            float widgetW = graphWidth + 40f + 150f;
            float widgetH = -kGraphAnchorY + graphHeight + 20f;

            m_window = new GameObject("WindPowerGraph_Window");
            m_window.transform.SetParent(canvasGO.transform, false);
            RectTransform winRT = m_window.AddComponent<RectTransform>();
            winRT.anchorMin = winRT.anchorMax = new Vector2(0f, 0f);
            winRT.pivot = new Vector2(0f, 0f);
            winRT.sizeDelta = new Vector2(widgetW, widgetH);
            winRT.anchoredPosition = new Vector2(16f, 16f);
            m_window.AddComponent<Image>().color = new Color(0.05f, 0.08f, 0.14f, 1f);

            // Title — leaves ~70px on right for status indicator
            TMP_Text title = CreateTMP("Title", m_window.transform);
            RectTransform tRT = title.rectTransform;
            tRT.anchorMin = new Vector2(0f, 1f); tRT.anchorMax = new Vector2(1f, 1f);
            tRT.pivot = new Vector2(0f, 1f);
            tRT.offsetMin = new Vector2(8f, -31f);
            tRT.offsetMax = new Vector2(-72f, -5f);
            title.text = "Wind Turbine Power (W)";
            title.fontSize = 13f;
            title.fontStyle = FontStyles.Bold;
            title.color = Color.white;
            title.alignment = TextAlignmentOptions.MidlineLeft;

            // Status indicator — top-right (tiny)
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

            // Graph texture
            m_graphTex = new Texture2D(graphWidth, graphHeight, TextureFormat.RGBA32, false);
            m_graphTex.filterMode = FilterMode.Point;
            m_graphTex.wrapMode = TextureWrapMode.Clamp;
            m_graphPixels = new Color[graphWidth * graphHeight];

            GameObject grGO = new GameObject("Graph");
            grGO.transform.SetParent(m_window.transform, false);
            m_graphImage = grGO.AddComponent<RawImage>();
            m_graphImage.texture = m_graphTex;
            m_graphImage.raycastTarget = false;
            RectTransform grRT = m_graphImage.rectTransform;
            grRT.anchorMin = new Vector2(0f, 1f); grRT.anchorMax = new Vector2(0f, 1f);
            grRT.pivot = new Vector2(0f, 1f);
            grRT.sizeDelta = new Vector2(graphWidth, graphHeight);
            grRT.anchoredPosition = new Vector2(kGraphAnchorX, kGraphAnchorY);

            // Tick labels — positioned per-frame in UpdateTickLabels()
            m_yTickLabels = new TMP_Text[kTickCount];
            m_xTickLabels = new TMP_Text[kTickCount];
            for (int i = 0; i < kTickCount; i++)
            {
                TMP_Text yt = CreateTMP("YTick" + i, m_window.transform);
                RectTransform yrt = yt.rectTransform;
                yrt.anchorMin = new Vector2(0f, 1f); yrt.anchorMax = new Vector2(0f, 1f);
                yrt.pivot = new Vector2(1f, 0.5f);
                yrt.sizeDelta = new Vector2(46f, 14f);
                yt.fontSize = 9f;
                yt.color = new Color(1f, 1f, 1f, 0.7f);
                yt.alignment = TextAlignmentOptions.MidlineRight;
                m_yTickLabels[i] = yt;

                TMP_Text xt = CreateTMP("XTick" + i, m_window.transform);
                RectTransform xrt = xt.rectTransform;
                xrt.anchorMin = new Vector2(0f, 1f); xrt.anchorMax = new Vector2(0f, 1f);
                xrt.pivot = new Vector2(0.5f, 1f);
                xrt.sizeDelta = new Vector2(60f, 14f);
                xt.fontSize = 9f;
                xt.color = new Color(1f, 1f, 1f, 0.7f);
                xt.alignment = TextAlignmentOptions.Top;
                m_xTickLabels[i] = xt;
            }

            // Legend (right of graph)
            m_legendText = CreateTMP("Legend", m_window.transform);
            RectTransform lRT = m_legendText.rectTransform;
            lRT.anchorMin = new Vector2(1f, 1f); lRT.anchorMax = new Vector2(1f, 1f);
            lRT.pivot = new Vector2(1f, 1f);
            lRT.sizeDelta = new Vector2(150f, graphHeight);
            lRT.anchoredPosition = new Vector2(-12f, kGraphAnchorY);
            m_legendText.fontSize = 9f;
            m_legendText.alignment = TextAlignmentOptions.TopLeft;
            m_legendText.color = Color.white;
            m_legendText.richText = true;
            m_legendText.enableWordWrapping = false;

            // Hints / toast strip — bottom of widget
            m_toastText = CreateTMP("Hints", m_window.transform);
            RectTransform htRT = m_toastText.rectTransform;
            htRT.anchorMin = new Vector2(0f, 0f); htRT.anchorMax = new Vector2(1f, 0f);
            htRT.pivot = new Vector2(0.5f, 0f);
            htRT.sizeDelta = new Vector2(-16f, 14f);
            htRT.anchoredPosition = new Vector2(0f, 4f);
            m_toastText.fontSize = 9f;
            m_toastText.alignment = TextAlignmentOptions.Center;
            m_toastText.color = new Color(1f, 1f, 1f, 0.28f);
            m_toastText.richText = true;
            m_toastText.enableWordWrapping = false;
        }

        private void SetWindowOpen(bool open)
        {
            if (open == m_window.activeSelf) return;
            m_window.SetActive(open);
            if (open) DiscoverTurbines();
        }

        private void HandleKeyboardShortcuts()
        {
            if (Input.GetKeyDown(KeyCode.S) && m_state != State.Recording && m_series.Count > 0) StartRecording();
            else if (Input.GetKeyDown(KeyCode.X) && m_state == State.Recording) StopRecording();
            else if (Input.GetKeyDown(KeyCode.V) && m_state == State.Stopped && TotalSampleCount() > 0) SaveRecording();
            else if (Input.GetKeyDown(KeyCode.N) && m_state != State.Recording && TotalSampleCount() > 0) NewRecording();
        }

        private void UpdateHintsOrToast()
        {
            if (Time.unscaledTime <= m_toastUntil)
            {
                m_toastText.text = m_toast;
                m_toastText.color = new Color(0.85f, 0.95f, 1f, 0.85f);
            }
            else
            {
                m_toastText.text = HintsText();
                m_toastText.color = new Color(1f, 1f, 1f, 0.28f);
            }
        }

        private string HintsText()
        {
            string dim = "#282828";
            string s = m_state != State.Recording && m_series.Count > 0 ? "[S]" : $"<color={dim}>[S]</color>";
            string x = m_state == State.Recording ? "[X]" : $"<color={dim}>[X]</color>";
            string v = m_state == State.Stopped && TotalSampleCount() > 0 ? "[V]" : $"<color={dim}>[V]</color>";
            string n = m_state != State.Recording && TotalSampleCount() > 0 ? "[N]" : $"<color={dim}>[N]</color>";
            return $"{s} start  ·  {x} stop  ·  {v} save  ·  {n} clear";
        }

        private TMP_Text CreateTMP(string n, Transform parent)
        {
            GameObject go = new GameObject(n);
            go.transform.SetParent(parent, false);
            TMP_Text t = go.AddComponent<TextMeshProUGUI>();
            t.raycastTarget = false;
            return t;
        }

        private Button CreateButton(string label, Transform parent, float x, float y,
            float w, float h, Color color, Action onClick, bool anchorRight = false)
        {
            GameObject go = new GameObject($"Btn_{label}");
            go.transform.SetParent(parent, false);
            Image img = go.AddComponent<Image>();
            img.color = color;
            Button btn = go.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = color;
            cb.highlightedColor = new Color(
                Mathf.Min(1f, color.r * 1.25f),
                Mathf.Min(1f, color.g * 1.25f),
                Mathf.Min(1f, color.b * 1.25f), 1f);
            cb.pressedColor = new Color(color.r * 0.7f, color.g * 0.7f, color.b * 0.7f, 1f);
            cb.disabledColor = new Color(color.r * 0.4f, color.g * 0.4f, color.b * 0.4f, 0.55f);
            btn.colors = cb;
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick());

            RectTransform rt = go.GetComponent<RectTransform>();
            if (anchorRight)
            {
                rt.anchorMin = new Vector2(1f, 1f); rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 1f);
            }
            else
            {
                rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
            }
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, y);

            TMP_Text t = CreateTMP("Label", go.transform);
            RectTransform trt = t.rectTransform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            t.text = label;
            t.richText = true;
            t.fontSize = 13f;
            t.fontStyle = FontStyles.Bold;
            t.color = Color.white;
            t.alignment = TextAlignmentOptions.Center;
            return btn;
        }

        #endregion
        // ---------------------------------------------------------------------------------

        #region UI Refresh

        private void UpdateStatusText()
        {
            switch (m_state)
            {
                case State.Recording:
                    int mm = (int)(m_recordingTime / 60f);
                    int ss = (int)(m_recordingTime % 60f);
                    m_statusText.text = $"<color=#FF6E5A>● {mm:00}:{ss:00}</color>";
                    break;
                case State.Stopped:
                    m_statusText.text = $"<color=#9DD5FF>■ {m_recordingTime:F1}s</color>";
                    break;
                default:
                    m_statusText.text = $"<color=#555555>○ {m_series.Count}T</color>";
                    break;
            }
        }

        private void UpdateLegendText()
        {
            if (m_series.Count == 0)
            {
                m_legendText.text = "<color=#A0A8B8>No turbines found in scene.\nPress G again after they spawn.</color>";
                return;
            }

            var sb = new StringBuilder();
            for (int i = 0; i < m_series.Count; i++)
            {
                var sr = m_series[i];
                string hex = ColorUtility.ToHtmlStringRGB(sr.color);
                float cur = sr.controller != null ? sr.controller.PowerW : 0f;
                sb.Append($"<color=#{hex}>■</color> {sr.name}  <color=#FFFFFF>{FormatPower(cur)}</color>\n");
            }
            m_legendText.text = sb.ToString();
        }

        #endregion
    }
}
