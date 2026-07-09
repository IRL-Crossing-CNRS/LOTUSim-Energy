/*
 * Copyright (c) 2025 Naval Group
 *
 * This program and the accompanying materials are made available under the
 * terms of the Eclipse Public License 2.0 which is available at
 * https://www.eclipse.org/legal/epl-2.0.
 *
 * SPDX-License-Identifier: EPL-2.0
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using RosMessageTypes.WindPhysics;
using Unity.Robotics.ROSTCPConnector;

namespace Lotusim
{
    public class WindLcoeGraphHUD : MonoBehaviour
    {
        [Header("ROS")]
        [Tooltip("Topic pattern. Use {0} as placeholder for the world name.")]
        [SerializeField] private string topicPattern = "{0}/lcoe";

        [Header("Recording")]
        [Tooltip("Sampling rate (Hz).")]
        [SerializeField] private float sampleRateHz = 10f;

        [Header("Graph")]
        [SerializeField] private int graphWidth = 1000;
        [SerializeField] private int graphHeight = 500;
        [SerializeField] private Color bgColor = new Color(0.04f, 0.07f, 0.14f, 1.00f);
        [SerializeField] private Color gridColor = new Color(1.00f, 1.00f, 1.00f, 0.10f);
        [SerializeField] private Color axisColor = new Color(1.00f, 1.00f, 1.00f, 0.45f);
        [SerializeField] private Color farmAvgColor = new Color(1.00f, 0.88f, 0.10f, 1.00f);

        [Header("Save")]
        [Tooltip("Subfolder name created inside the user's Downloads directory.")]
        [SerializeField] private string saveSubfolder = "FACET_WindLCOE";

        private enum State { Idle, Recording, Stopped }

        private State m_state = State.Idle;
        private readonly List<LCOEStateMsg> m_samples = new List<LCOEStateMsg>(2048);
        private float m_recordingTime = 0f;
        private float m_sampleAccum = 0f;
        private float m_maxLcoe = 1f;
        private bool m_graphDirty = true;
        private string m_toast = "";
        private float m_toastUntil = 0f;

        private LCOEStateMsg m_latestMsg = null;
        private readonly Mutex m_lcoeMutex = new Mutex();
        private bool m_subscribed = false;

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
        private const int kTickCount = 5;
        private const int kGridDivisions = 4;
        private const float kGraphAnchorX = 14f;
        private const float kGraphAnchorY = -56f;
        private const int kPadL = 56, kPadR = 12, kPadT = 12, kPadB = 24;

        private void Start()
        {
            EnsureEventSystem();
            BuildUI();
            m_window.SetActive(false);
            RedrawGraph();
            SubscribeROS();
        }

        private void SubscribeROS()
        {
            if (m_subscribed) return;
            string ns = RosInterface.Instance != null ? RosInterface.Instance.RosNamespace : "energy";
            string topic = string.Format(topicPattern, ns ?? "energy");
            ROSConnection.GetOrCreateInstance().Subscribe<LCOEStateMsg>(topic, OnLcoeReceived);
            m_subscribed = true;
            Debug.Log($"[WindLcoeGraphHUD] Subscribed to {topic}");
        }

        private void OnLcoeReceived(LCOEStateMsg msg)
        {
            m_lcoeMutex.WaitOne();
            m_latestMsg = msg;
            m_lcoeMutex.ReleaseMutex();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyBindings.ToggleLcoeGraph))
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

        private void SampleOnce()
        {
            m_lcoeMutex.WaitOne();
            var snap = m_latestMsg;
            m_lcoeMutex.ReleaseMutex();

            if (snap == null) snap = new LCOEStateMsg();

            m_samples.Add(snap);
            if (snap.lcoe_aud_mwh > m_maxLcoe) m_maxLcoe = snap.lcoe_aud_mwh;
        }

        private void StartRecording()
        {
            m_samples.Clear();
            m_recordingTime = 0f;
            m_sampleAccum = 0f;
            m_maxLcoe = 1f;
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
            m_samples.Clear();
            m_recordingTime = 0f;
            m_sampleAccum = 0f;
            m_maxLcoe = 1f;
            m_state = State.Idle;
            m_graphDirty = true;
            ShowToast("Cleared — ready to record");
        }

        private void SaveRecording()
        {
            if (m_samples.Count == 0)
            {
                ShowToast("Nothing to save", 2.5f);
                return;
            }
            StartCoroutine(SaveRoutine());
        }

        private System.Collections.IEnumerator SaveRoutine()
        {
            string folder = ResolveSaveFolder();
            string csvPath = Path.Combine(folder, "lcoe.csv");
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
                Debug.LogError($"[WindLcoeGraphHUD] CSV save failed: {e}");
                ShowToast("Save failed — see console", 4f);
            }
            if (!csvOk) yield break;

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

                Debug.Log($"[WindLcoeGraphHUD] Saved recording to: {folder}");
                ShowToast($"Saved to {folder}", 6f);
            }
            catch (Exception e)
            {
                Debug.LogError($"[WindLcoeGraphHUD] PNG save failed: {e}");
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

        private string BuildCsv()
        {
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();

            sb.AppendLine("time_s,lcoe_aud_mwh,energy_produced_kwh,cost_total_aud,cost_maintenance_aud,cost_robot_time_aud,cost_robot_energy_aud,robot_operation_time_h,robot_energy_wh");

            float interval = 1f / Mathf.Max(0.1f, sampleRateHz);
            for (int row = 0; row < m_samples.Count; row++)
            {
                float t = row * interval;
                var s = m_samples[row];
                sb.Append(t.ToString("F3", ci)).Append(',')
                  .Append(s.lcoe_aud_mwh.ToString("F4", ci)).Append(',')
                  .Append(s.energy_produced_kwh.ToString("F4", ci)).Append(',')
                  .Append(s.cost_total_aud.ToString("F4", ci)).Append(',')
                  .Append(s.cost_maintenance_aud.ToString("F4", ci)).Append(',')
                  .Append(s.cost_robot_time_aud.ToString("F4", ci)).Append(',')
                  .Append(s.cost_robot_energy_aud.ToString("F4", ci)).Append(',')
                  .Append(s.robot_operation_time_h.ToString("F4", ci)).Append(',')
                  .Append(s.robot_energy_wh.ToString("F4", ci))
                  .AppendLine();
            }
            return sb.ToString();
        }

        private static string FormatLcoe(float audMwh)
        {
            return $"A${audMwh:F1}/MWh";
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

        private void RedrawGraph()
        {
            int w = graphWidth, h = graphHeight;
            for (int i = 0; i < m_graphPixels.Length; i++) m_graphPixels[i] = bgColor;

            int plotW = w - kPadL - kPadR;
            int plotH = h - kPadT - kPadB;
            if (plotW < 4 || plotH < 4) { ApplyPixels(); return; }

            int xLeft = kPadL;
            int xRight = kPadL + plotW;
            int yBottom = kPadB;
            int yTop = kPadB + plotH;

            // Grid
            for (int g = 1; g < kGridDivisions; g++)
            {
                int yy = yBottom + (plotH * g) / kGridDivisions;
                DrawHLine(xLeft, xRight, yy, gridColor);
                int xx = xLeft + (plotW * g) / kGridDivisions;
                DrawVLine(xx, yBottom, yTop, gridColor);
            }
            DrawHLine(xLeft, xRight, yBottom, axisColor);
            DrawVLine(xLeft, yBottom, yTop, axisColor);

            int maxLen = m_samples.Count;

            float interval = 1f / Mathf.Max(0.1f, sampleRateHz);
            float duration = Mathf.Max(0.001f, (maxLen - 1) * interval);
            float yMax = Mathf.Max(1f, m_maxLcoe * 1.05f);

            UpdateTickLabels(yMax, duration);

            if (maxLen >= 2)
            {
                // Farm average curve — thick yellow
                int prevAvgX = 0, prevAvgY = 0;
                for (int k = 0; k < maxLen; k++)
                {
                    float tNorm = (k * interval) / duration;
                    float val = m_samples[k].lcoe_aud_mwh;
                    float vNorm = Mathf.Clamp01(val / yMax);
                    int xx = xLeft + Mathf.RoundToInt(tNorm * plotW);
                    int yy = yBottom + Mathf.RoundToInt(vNorm * plotH);
                    if (k > 0) DrawLine(prevAvgX, prevAvgY, xx, yy, farmAvgColor);
                    prevAvgX = xx; prevAvgY = yy;
                }
            }

            ApplyPixels();
        }

        private void UpdateTickLabels(float yMax, float duration)
        {
            if (m_yTickLabels == null) return;

            float plotW = graphWidth - kPadL - kPadR;
            float plotH = graphHeight - kPadT - kPadB;
            float rectBottom = kGraphAnchorY - graphHeight;
            float yLabelX = kGraphAnchorX + kPadL - 4f;
            float xLabelY = rectBottom + kPadB - 4f;

            for (int i = 0; i < kTickCount; i++)
            {
                float t = i / (float)(kTickCount - 1);
                float yPos = rectBottom + kPadB + t * plotH;
                m_yTickLabels[i].text = FormatLcoe(t * yMax);
                m_yTickLabels[i].rectTransform.anchoredPosition = new Vector2(yLabelX, yPos);

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
                Plot(x0 + 1, y0, c);
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
            GameObject canvasGO = new GameObject("WindLcoeGraph_Canvas");
            canvasGO.transform.SetParent(transform, false);
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 201;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            float widgetW = graphWidth + 40f + 190f;
            float widgetH = -kGraphAnchorY + graphHeight + 20f;

            m_window = new GameObject("WindLcoeGraph_Window");
            m_window.transform.SetParent(canvasGO.transform, false);
            RectTransform winRT = m_window.AddComponent<RectTransform>();
            winRT.anchorMin = winRT.anchorMax = new Vector2(0f, 0f);
            winRT.pivot = new Vector2(0f, 0f);
            winRT.sizeDelta = new Vector2(widgetW, widgetH);
            winRT.anchoredPosition = new Vector2(16f, 16f);
            m_window.AddComponent<Image>().color = new Color(0.04f, 0.06f, 0.12f, 1f);

            // Title — left-aligned, leaves right gap for status indicator
            TMP_Text title = CreateTMP("Title", m_window.transform);
            RectTransform tRT = title.rectTransform;
            tRT.anchorMin = new Vector2(0f, 1f); tRT.anchorMax = new Vector2(1f, 1f);
            tRT.pivot = new Vector2(0f, 1f);
            tRT.offsetMin = new Vector2(8f, -31f);
            tRT.offsetMax = new Vector2(-80f, -5f);
            title.text = "Wind Farm LCOE  <size=9><color=#A09060>A$/MWh · AUS market</color></size>";
            title.fontSize = 13f;
            title.fontStyle = FontStyles.Bold;
            title.color = farmAvgColor;
            title.alignment = TextAlignmentOptions.MidlineLeft;
            title.richText = true;

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

            m_yTickLabels = new TMP_Text[kTickCount];
            m_xTickLabels = new TMP_Text[kTickCount];
            for (int i = 0; i < kTickCount; i++)
            {
                TMP_Text yt = CreateTMP("YTick" + i, m_window.transform);
                RectTransform yrt = yt.rectTransform;
                yrt.anchorMin = new Vector2(0f, 1f); yrt.anchorMax = new Vector2(0f, 1f);
                yrt.pivot = new Vector2(1f, 0.5f);
                yrt.sizeDelta = new Vector2(54f, 14f);
                yt.fontSize = 9f;
                yt.color = new Color(1f, 0.92f, 0.55f, 0.80f);
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
            lRT.sizeDelta = new Vector2(190f, graphHeight);
            lRT.anchoredPosition = new Vector2(-12f, kGraphAnchorY);
            m_legendText.fontSize = 11f;
            m_legendText.alignment = TextAlignmentOptions.TopLeft;
            m_legendText.color = Color.white;
            m_legendText.richText = true;

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
        }

        private void HandleKeyboardShortcuts()
        {
            if (Input.GetKeyDown(KeyCode.S) && m_state != State.Recording) StartRecording();
            else if (Input.GetKeyDown(KeyCode.X) && m_state == State.Recording) StopRecording();
            else if (Input.GetKeyDown(KeyCode.V) && m_state == State.Stopped && m_samples.Count > 0) SaveRecording();
            else if (Input.GetKeyDown(KeyCode.N) && m_state != State.Recording && m_samples.Count > 0) NewRecording();
        }

        private void UpdateHintsOrToast()
        {
            if (Time.unscaledTime <= m_toastUntil)
            {
                m_toastText.text = m_toast;
                m_toastText.color = new Color(1f, 0.95f, 0.70f, 0.85f);
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
            string s = m_state != State.Recording ? "[S]" : $"<color={dim}>[S]</color>";
            string x = m_state == State.Recording ? "[X]" : $"<color={dim}>[X]</color>";
            string v = m_state == State.Stopped && m_samples.Count > 0 ? "[V]" : $"<color={dim}>[V]</color>";
            string n = m_state != State.Recording && m_samples.Count > 0 ? "[N]" : $"<color={dim}>[N]</color>";
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
                    m_statusText.text = "<color=#555555>○ idle</color>";
                    break;
            }
        }

        private void UpdateLegendText()
        {
            m_lcoeMutex.WaitOne();
            var msg = m_latestMsg;
            m_lcoeMutex.ReleaseMutex();

            if (msg == null)
            {
                m_legendText.text = "<color=#A0A8B8>Waiting for LCOE data...</color>";
                return;
            }

            string avgHex = ColorUtility.ToHtmlStringRGB(farmAvgColor);
            var sb = new StringBuilder();
            sb.Append($"<color=#{avgHex}><b>Farm Avg LCOE</b></color>\n");
            sb.Append($"<color=#{avgHex}><size=15><b>{FormatLcoe(msg.lcoe_aud_mwh)}</b></size></color>\n\n");

            sb.Append("<color=#A0A8B8><size=9>─ details ─</size></color>\n");
            sb.Append($"<color=#FFFFFF>Energy Prod:</color> <color=#FFE97A>{msg.energy_produced_kwh:F1} kWh</color>\n");
            sb.Append($"<color=#FFFFFF>Total Cost:</color> <color=#FF9999>A${msg.cost_total_aud:F2}</color>\n");
            sb.Append($"<color=#A0A8B8>  ├ Maint:</color> <color=#A0A8B8>A${msg.cost_maintenance_aud:F2}</color>\n");
            sb.Append($"<color=#A0A8B8>  ├ Rbt Time:</color> <color=#A0A8B8>A${msg.cost_robot_time_aud:F2}</color>\n");
            sb.Append($"<color=#A0A8B8>  └ Rbt Nrg:</color> <color=#A0A8B8>A${msg.cost_robot_energy_aud:F3}</color>\n\n");
            sb.Append($"<color=#FFFFFF>Robot Ops:</color> <color=#9DD5FF>{msg.robot_operation_time_h:F2} h</color>\n");
            sb.Append($"<color=#A0A8B8>         ({msg.robot_energy_wh:F1} Wh)</color>\n");

            m_legendText.text = sb.ToString();
        }

    }
}
