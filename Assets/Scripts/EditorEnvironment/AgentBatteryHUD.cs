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
//  AgentBatteryHUD.cs
//
//  Description:
//  Toggleable popup panel (default key: B) that displays real-time battery state for every
//  agent discovered at runtime. Agents are detected dynamically: whenever the LotusimConnector
//  spawns a new GameObject via renderer_cmd, this HUD automatically subscribes to
//      /model/<AGENT_NAME>/battery/linear_battery/state
//  and renders a labelled progress bar that transitions green → orange → red as the charge drops.
//
//  Because agent names change every run (x500inspection0, x500inspection1, …), discovery is
//  driven by watching the scene's object list each frame (same strategy as WindPowerGraphHUD
//  uses for turbines, but via GameObject polling).
//
//  Setup:
//  1. Add this component to any persistent GameObject in the scene (e.g. the same one that
//     hosts WindPowerGraphHUD).
//  2. Ensure ROSConnection is initialised before this script starts (it always is, since
//     LotusimConnector.Start() runs first).
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using Unity.Robotics.ROSTCPConnector;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using RosMessageTypes.Sensor;

namespace Lotusim
{
    /// <summary>
    /// Real-time battery monitor for dynamically spawned agents.
    /// </summary>
    public class AgentBatteryHUD : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        #region Inspector Fields

        [Header("ROS")]
        [Tooltip("Topic pattern. Use {0} for world name and {1} for agent name.")]
        [SerializeField] private string topicPattern = "{0}/{1}/battery/state";

        [Header("Discovery")]
        [Tooltip("How often (seconds) to scan the scene for new RendererPosesWaypointFollower components (= agents spawned by LotusimConnector).")]
        [SerializeField] private float discoveryIntervalSec = 2f;

        [Header("Layout")]
        [SerializeField] private float panelWidth  = 420f;
        [SerializeField] private float panelHeight = 460f;

        #endregion
        // -----------------------------------------------------------------------

        #region Types

        private class AgentEntry
        {
            public string   agentName;
            public string   topic;
            public float    percentage   = -1f;   // -1 = not yet received
            public float    voltage;
            public float    current;
            public byte     status;
            public bool     subscribed   = false;
            public readonly Mutex mutex  = new Mutex();

            // Cached values for UI optimization
            public float    lastPercentage = -2f;
            public float    lastVoltage = -1f;
            public float    lastCurrent = -1f;
            public byte     lastStatus = 255;

            // UI refs
            public TMP_Text labelText;
            public TMP_Text valueText;
            public RawImage barFill;
            public RawImage barBg;
            public GameObject row;
        }

        #endregion
        // -----------------------------------------------------------------------

        #region State

        private readonly List<AgentEntry> m_agents = new List<AgentEntry>();
        private readonly HashSet<string>  m_knownNames = new HashSet<string>();

        // Reused scratch set for despawn detection (avoids allocating each scan).
        private readonly HashSet<string>  m_liveScratch = new HashSet<string>();

        // Cached connection — also avoids re-creating the singleton during OnDestroy/shutdown.
        private ROSConnection m_ros;

        // Row layout metrics (shared by AddAgentRow and RelayoutRows).
        private const float k_RowHeight = 46f;
        private const float k_RowGap    = 6f;

        private GameObject m_window;
        private TMP_Text   m_titleText;
        private TMP_Text   m_subtitleText;
        private TMP_Text   m_toastText;
        private GameObject m_scrollContent;

        private float m_discoveryAccum = 0f;
        private float m_toastUntil     = 0f;
        private string m_toast         = "";
        private bool m_toastVisible    = false;
        private int m_lastAgentCount   = -1;

        private bool            m_cursorStateSaved;
        private CursorLockMode  m_savedLockState;
        private bool            m_savedCursorVisible;

        // Bar fill textures (1×1, reused)
        private Texture2D m_texGreen;
        private Texture2D m_texOrange;
        private Texture2D m_texRed;
        private Texture2D m_texBarBg;

        // Colors
        private static readonly Color k_ColorGreen  = new Color(0.25f, 0.85f, 0.45f);
        private static readonly Color k_ColorOrange = new Color(1.00f, 0.60f, 0.15f);
        private static readonly Color k_ColorRed    = new Color(0.90f, 0.20f, 0.20f);
        private static readonly Color k_ColorBarBg  = new Color(1f, 1f, 1f, 0.10f);
        private static readonly Color k_WinBg       = new Color(0.05f, 0.08f, 0.14f, 1f);

        #endregion
        // -----------------------------------------------------------------------

        #region Unity Lifecycle

        private void Start()
        {
            m_ros = ROSConnection.GetOrCreateInstance();

            // Release a battery subscription the instant the simulator removes its agent, instead of
            // waiting for the next discovery scan — keeps the delete clean and avoids a lingering sub.
            RosInterface.VesselRemoved += OnVesselRemoved;

            EnsureEventSystem();
            CreateBarTextures();
            BuildUI();
            ScanForAgents();
            m_window.SetActive(false);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyBindings.ToggleBatteryHUD))
                SetWindowOpen(!m_window.activeSelf);

            // Discovery + despawn cleanup runs regardless of panel visibility, so battery
            // subscriptions track agent lifecycle (a despawned agent must release its sub
            // even while the panel is hidden, otherwise the endpoint node lingers).
            m_discoveryAccum += Time.deltaTime;
            if (m_discoveryAccum >= discoveryIntervalSec)
            {
                m_discoveryAccum = 0f;
                ScanForAgents();
            }

            if (!m_window.activeSelf) return;

            // Keep cursor free for UI interaction
            if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
            if (!Cursor.visible) Cursor.visible = true;

            RefreshBars();

            bool shouldShowToast = Time.unscaledTime <= m_toastUntil;
            if (shouldShowToast && !m_toastVisible)
            {
                m_toastText.text = m_toast;
                m_toastVisible = true;
            }
            else if (!shouldShowToast && m_toastVisible)
            {
                m_toastText.text = "";
                m_toastVisible = false;
            }

            if (m_agents.Count != m_lastAgentCount)
            {
            m_subtitleText.text = $"<color=#A0A8B8>{m_agents.Count} agent(s) tracked</color>";
                m_lastAgentCount = m_agents.Count;
            }
        }

        private void OnDestroy()
        {
            RosInterface.VesselRemoved -= OnVesselRemoved;

            // Release every battery subscription so per-agent endpoint nodes don't linger.
            if (m_ros != null)
            {
                foreach (AgentEntry e in m_agents)
                {
                    if (e.subscribed)
                    {
                        m_ros.Unsubscribe(e.topic);
                        e.subscribed = false;
                    }
                }
            }

            if (m_texGreen  != null) Destroy(m_texGreen);
            if (m_texOrange != null) Destroy(m_texOrange);
            if (m_texRed    != null) Destroy(m_texRed);
            if (m_texBarBg  != null) Destroy(m_texBarBg);
        }

        #endregion
        // -----------------------------------------------------------------------

        #region Agent Discovery & ROS Subscription

        /// <summary>
        /// Scans the scene for <see cref="RendererPosesWaypointFollower"/> components.
        /// Every GameObject that carries one was spawned by LotusimConnector and is therefore
        /// a tracked agent — no name filtering required.
        /// </summary>
        private void ScanForAgents()
        {
            var followers = FindObjectsByType<RendererPosesWaypointFollower>(FindObjectsSortMode.None);

            m_liveScratch.Clear();
            foreach (RendererPosesWaypointFollower f in followers)
                m_liveScratch.Add(f.gameObject.name);

            // Newly spawned agents → subscribe.
            foreach (string n in m_liveScratch)
            {
                if (!m_knownNames.Contains(n))
                    RegisterAgent(n);
            }

            // Despawned agents → unsubscribe + drop row. Releases the ROS endpoint's
            // per-agent subscriber node; otherwise the battery topic lingers forever.
            for (int i = m_agents.Count - 1; i >= 0; i--)
            {
                if (!m_liveScratch.Contains(m_agents[i].agentName))
                    UnregisterAgent(i);
            }
        }

        private void RegisterAgent(string agentName)
        {
            m_knownNames.Add(agentName);

            // Fetch the namespace dynamically from the ROS Interface to prevent duplicates
            string worldNamespace = RosInterface.Instance.RosNamespace ?? "energy";
            string topic = string.Format(topicPattern, worldNamespace, agentName);
            AgentEntry entry = new AgentEntry
            {
                agentName = agentName,
                topic     = topic,
            };

            // Subscribe via ROSConnection
            m_ros.Subscribe<BatteryStateMsg>(topic, msg => OnBatteryReceived(entry, msg));
            entry.subscribed = true;

            AddAgentRow(entry);
            m_agents.Add(entry);

            Debug.Log($"[AgentBatteryHUD] Subscribed to {topic}");
        }

        /// <summary>
        /// Driven by <see cref="RosInterface.VesselRemoved"/>: drops a removed agent's battery
        /// subscription and row the moment the DELETE/EXPLODE command is processed. The agent's
        /// GameObject is destroyed in the same connector Update tick, so the next discovery scan
        /// won't re-add it.
        /// </summary>
        private void OnVesselRemoved(string agentName)
        {
            for (int i = m_agents.Count - 1; i >= 0; i--)
            {
                if (m_agents[i].agentName == agentName)
                {
                    UnregisterAgent(i);
                    break;
                }
            }
        }

        /// <summary>
        /// Tears down a despawned agent: releases its battery subscription, destroys its row,
        /// and reflows the remaining rows.
        /// </summary>
        private void UnregisterAgent(int index)
        {
            AgentEntry e = m_agents[index];

            if (e.subscribed && m_ros != null)
            {
                m_ros.Unsubscribe(e.topic);
                e.subscribed = false;
            }

            if (e.row != null) Destroy(e.row);

            m_agents.RemoveAt(index);
            m_knownNames.Remove(e.agentName);

            RelayoutRows();

            Debug.Log($"[AgentBatteryHUD] Unsubscribed from {e.topic}");
        }

        // Reposition surviving rows and resize scroll content after a removal.
        private void RelayoutRows()
        {
            for (int i = 0; i < m_agents.Count; i++)
            {
                if (m_agents[i].row == null) continue;
                RectTransform rt = m_agents[i].row.GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(0f, -i * (k_RowHeight + k_RowGap));
            }

            if (m_scrollContent != null)
            {
                RectTransform scRT = m_scrollContent.GetComponent<RectTransform>();
                scRT.sizeDelta = new Vector2(0f, m_agents.Count * (k_RowHeight + k_RowGap));
            }
        }

        private void OnBatteryReceived(AgentEntry entry, BatteryStateMsg msg)
        {
            entry.mutex.WaitOne();
            
            // To avoid ambiguities between 0-1 and 0-100 scales in the 'percentage' field 
            // (which causes a massive UI jump when a 0-100 scale drops below 1.0%), 
            // we calculate the percentage directly from charge and capacity if possible.
            if (msg.capacity > 0.001f)
            {
                entry.percentage = Mathf.Clamp01(msg.charge / msg.capacity) * 100f;
            }
            else
            {
                // Fallback
                float raw = msg.percentage;
                entry.percentage = raw <= 1.0f ? raw * 100f : raw;
            }

            entry.voltage    = msg.voltage;
            entry.current    = msg.current;
            entry.status     = msg.power_supply_status;
            entry.mutex.ReleaseMutex();
        }

        #endregion
        // -----------------------------------------------------------------------

        #region Bar Rendering

        private void RefreshBars()
        {
            foreach (AgentEntry e in m_agents)
            {
                e.mutex.WaitOne();
                float pct    = e.percentage;
                float volt   = e.voltage;
                float curr   = e.current;
                byte  status = e.status;
                e.mutex.ReleaseMutex();

                if (e.row == null) continue;

                // Only update UI if values have changed
                if (Mathf.Approximately(pct, e.lastPercentage) &&
                    Mathf.Approximately(volt, e.lastVoltage) &&
                    Mathf.Approximately(curr, e.lastCurrent) &&
                    status == e.lastStatus)
                {
                    continue;
                }

                e.lastPercentage = pct;
                e.lastVoltage = volt;
                e.lastCurrent = curr;
                e.lastStatus = status;

                if (pct < 0f)
                {
                    e.valueText.text = "-- %    --.- V    --.- A";
                    SetBarFillAnchor(e.barFill, 0f, m_texBarBg);
                    continue;
                }

                float t01 = Mathf.Clamp01(pct / 100f);

                Texture2D fillTex;
                if      (pct >= 40f) fillTex = m_texGreen;
                else if (pct >= 20f) fillTex = m_texOrange;
                else                 fillTex = m_texRed;

                SetBarFillAnchor(e.barFill, t01, fillTex);

                string statusTag = status == BatteryStateMsg.POWER_SUPPLY_STATUS_CHARGING
                    ? "<color=#9DD5FF>CHG</color>"
                    : status == BatteryStateMsg.POWER_SUPPLY_STATUS_FULL
                        ? "<color=#55FF88>FULL</color>"
                        : "<color=#FF9966>DIS</color>";

                e.valueText.text  = $"<b>{pct:F1}%</b>    {volt:F1} V    {Mathf.Abs(curr):F2} A    {statusTag}";
                e.labelText.color = pct < 20f ? new Color(1f, 0.45f, 0.45f) : Color.white;
            }
        }

        // Fill a bar by moving its anchorMax.x — works at any RectTransform size.
        private static void SetBarFillAnchor(RawImage fill, float t01, Texture2D tex)
        {
            if (fill == null) return;
            fill.texture = tex;
            RectTransform rt = fill.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(Mathf.Clamp01(t01), 1f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        #endregion
        // -----------------------------------------------------------------------

        #region UI Construction

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        private void CreateBarTextures()
        {
            m_texGreen  = MakeTex(k_ColorGreen);
            m_texOrange = MakeTex(k_ColorOrange);
            m_texRed    = MakeTex(k_ColorRed);
            m_texBarBg  = MakeTex(k_ColorBarBg);
        }

        private static Texture2D MakeTex(Color c)
        {
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        private void BuildUI()
        {
            // Canvas
            GameObject canvasGO = new GameObject("AgentBatteryHUD_Canvas");
            canvasGO.transform.SetParent(transform, false);
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 199;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            // Window — anchored to right-center
            m_window = new GameObject("AgentBatteryHUD_Window");
            m_window.transform.SetParent(canvasGO.transform, false);
            RectTransform winRT = m_window.AddComponent<RectTransform>();
            winRT.anchorMin        = winRT.anchorMax = new Vector2(1f, 0.5f);
            winRT.pivot            = new Vector2(1f, 0.5f);
            winRT.sizeDelta        = new Vector2(panelWidth, panelHeight);
            winRT.anchoredPosition = new Vector2(-16f, 0f);
            Image winBg = m_window.AddComponent<Image>();
            winBg.color = k_WinBg;

            // ---- Title bar ----
            GameObject titleBar = new GameObject("TitleBar");
            titleBar.transform.SetParent(m_window.transform, false);
            Image titleBarImg = titleBar.AddComponent<Image>();
            titleBarImg.color = new Color(0.08f, 0.14f, 0.26f, 1f);
            RectTransform tbRT = titleBar.GetComponent<RectTransform>();
            tbRT.anchorMin        = new Vector2(0f, 1f);
            tbRT.anchorMax        = new Vector2(1f, 1f);
            tbRT.pivot            = new Vector2(0.5f, 1f);
            tbRT.sizeDelta        = new Vector2(0f, 40f);
            tbRT.anchoredPosition = Vector2.zero;

            m_titleText = CreateTMP("Title", titleBar.transform);
            m_titleText.text      = "Agent Battery Monitor";
            m_titleText.fontSize  = 14f;
            m_titleText.fontStyle = FontStyles.Bold;
            m_titleText.color     = new Color(0.55f, 0.90f, 1f);
            m_titleText.alignment = TextAlignmentOptions.MidlineLeft;
            RectTransform tRT = m_titleText.rectTransform;
            tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one;
            tRT.offsetMin = new Vector2(12f, 0f);
            tRT.offsetMax = new Vector2(-12f, 0f);

            // ---- Subtitle ----
            m_subtitleText = CreateTMP("Subtitle", m_window.transform);
            m_subtitleText.fontSize  = 10f;
            m_subtitleText.color     = new Color(0.65f, 0.70f, 0.80f);
            m_subtitleText.alignment = TextAlignmentOptions.MidlineLeft;
            RectTransform subRT = m_subtitleText.rectTransform;
            subRT.anchorMin        = new Vector2(0f, 1f);
            subRT.anchorMax        = new Vector2(1f, 1f);
            subRT.pivot            = new Vector2(0f, 1f);
            subRT.sizeDelta        = new Vector2(-16f, 18f);
            subRT.anchoredPosition = new Vector2(12f, -44f);

            // ---- Separator line below subtitle ----
            GameObject sep = new GameObject("Sep");
            sep.transform.SetParent(m_window.transform, false);
            Image sepImg = sep.AddComponent<Image>();
            sepImg.color = new Color(1f, 1f, 1f, 0.07f);
            RectTransform sepRT = sep.GetComponent<RectTransform>();
            sepRT.anchorMin        = new Vector2(0f, 1f);
            sepRT.anchorMax        = new Vector2(1f, 1f);
            sepRT.pivot            = new Vector2(0.5f, 1f);
            sepRT.sizeDelta        = new Vector2(-16f, 1f);
            sepRT.anchoredPosition = new Vector2(0f, -64f);

            // ---- Scroll view container ----
            GameObject scrollGO = new GameObject("ScrollView");
            scrollGO.transform.SetParent(m_window.transform, false);
            RectTransform svRT = scrollGO.AddComponent<RectTransform>();
            svRT.anchorMin = new Vector2(0f, 0f);
            svRT.anchorMax = new Vector2(1f, 1f);
            svRT.offsetMin = new Vector2(12f, 28f);
            svRT.offsetMax = new Vector2(-12f, -68f);
            
            Image svImg = scrollGO.AddComponent<Image>();
            svImg.color = new Color(0f, 0f, 0f, 0.01f); // transparent but captures scroll events
            
            ScrollRect scrollRect = scrollGO.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 25f;

            // ---- Viewport ----
            GameObject viewportGO = new GameObject("Viewport");
            viewportGO.transform.SetParent(scrollGO.transform, false);
            RectTransform vpRT = viewportGO.AddComponent<RectTransform>();
            vpRT.anchorMin = Vector2.zero;
            vpRT.anchorMax = Vector2.one;
            vpRT.offsetMin = Vector2.zero;
            vpRT.offsetMax = Vector2.zero;
            viewportGO.AddComponent<RectMask2D>();
            
            // ---- Scroll content container ----
            GameObject contentGO = new GameObject("ScrollContent");
            contentGO.transform.SetParent(viewportGO.transform, false);
            m_scrollContent = contentGO;
            RectTransform scRT = contentGO.AddComponent<RectTransform>();
            scRT.anchorMin = new Vector2(0f, 1f);
            scRT.anchorMax = new Vector2(1f, 1f);
            scRT.pivot = new Vector2(0.5f, 1f);
            scRT.sizeDelta = new Vector2(0f, 0f);
            scRT.anchoredPosition = Vector2.zero;

            scrollRect.viewport = vpRT;
            scrollRect.content = scRT;

            // ---- Toast (bottom) ----
            m_toastText = CreateTMP("Toast", m_window.transform);
            RectTransform toRT = m_toastText.rectTransform;
            toRT.anchorMin        = new Vector2(0f, 0f);
            toRT.anchorMax        = new Vector2(1f, 0f);
            toRT.pivot            = new Vector2(0.5f, 0f);
            toRT.sizeDelta        = new Vector2(-24f, 26f);
            toRT.anchoredPosition = new Vector2(0f, 4f);
            m_toastText.fontSize  = 10f;
            m_toastText.alignment = TextAlignmentOptions.Center;
            m_toastText.color     = new Color(0.75f, 0.90f, 1f, 0.80f);
        }

        /// <summary>Creates and attaches a UI row for a newly discovered agent.</summary>
        private void AddAgentRow(AgentEntry entry)
        {
            int idx = m_agents.Count;

            GameObject row = new GameObject($"Row_{entry.agentName}");
            row.transform.SetParent(m_scrollContent.transform, false);
            RectTransform rowRT = row.AddComponent<RectTransform>();
            rowRT.anchorMin        = new Vector2(0f, 1f);
            rowRT.anchorMax        = new Vector2(1f, 1f);
            rowRT.pivot            = new Vector2(0f, 1f);
            rowRT.sizeDelta        = new Vector2(0f, k_RowHeight);
            rowRT.anchoredPosition = new Vector2(0f, -idx * (k_RowHeight + k_RowGap));
            entry.row = row;

            // --- Top line: agent name (left) + value text (right) ---
            entry.labelText = CreateTMP("Label", row.transform);
            entry.labelText.text      = entry.agentName;
            entry.labelText.fontSize  = 11.5f;
            entry.labelText.fontStyle = FontStyles.Bold;
            entry.labelText.color     = Color.white;
            entry.labelText.alignment = TextAlignmentOptions.MidlineLeft;
            entry.labelText.enableWordWrapping = false;
            RectTransform labRT = entry.labelText.rectTransform;
            labRT.anchorMin        = new Vector2(0f, 1f);
            labRT.anchorMax        = new Vector2(0.55f, 1f);
            labRT.pivot            = new Vector2(0f, 1f);
            labRT.sizeDelta        = new Vector2(0f, 18f);
            labRT.anchoredPosition = new Vector2(0f, -1f);

            // Small topic label for diagnostics (shown below agent name, inside bar area)
            TMP_Text topicLabel = CreateTMP("Topic", row.transform);
            topicLabel.text      = entry.topic;
            topicLabel.fontSize  = 7.5f;
            topicLabel.color     = new Color(0.45f, 0.55f, 0.65f);
            topicLabel.alignment = TextAlignmentOptions.BottomLeft;
            topicLabel.enableWordWrapping = false;
            RectTransform topicRT = topicLabel.rectTransform;
            topicRT.anchorMin        = new Vector2(0f, 0f);
            topicRT.anchorMax        = new Vector2(1f, 0f);
            topicRT.pivot            = new Vector2(0f, 0f);
            topicRT.sizeDelta        = new Vector2(0f, 13f);
            topicRT.anchoredPosition = new Vector2(0f, 2f);

            entry.valueText = CreateTMP("Value", row.transform);
            entry.valueText.fontSize  = 10f;
            entry.valueText.richText  = true;
            entry.valueText.color     = new Color(0.85f, 0.90f, 1f);
            entry.valueText.alignment = TextAlignmentOptions.MidlineRight;
            entry.valueText.enableWordWrapping = false;
            RectTransform valRT = entry.valueText.rectTransform;
            valRT.anchorMin        = new Vector2(0.45f, 1f);
            valRT.anchorMax        = new Vector2(1f, 1f);
            valRT.pivot            = new Vector2(1f, 1f);
            valRT.sizeDelta        = new Vector2(0f, 18f);
            valRT.anchoredPosition = new Vector2(0f, -1f);

            // --- Bar background (bottom part of row) ---
            GameObject barBgGO = new GameObject("BarBg");
            barBgGO.transform.SetParent(row.transform, false);
            entry.barBg = barBgGO.AddComponent<RawImage>();
            entry.barBg.texture = m_texBarBg;
            RectTransform bgRT = entry.barBg.rectTransform;
            bgRT.anchorMin = new Vector2(0f, 0f);
            bgRT.anchorMax = new Vector2(1f, 1f);
            bgRT.offsetMin = new Vector2(0f, 0f);
            bgRT.offsetMax = new Vector2(0f, -22f);    // leave top 22px for text line

            // --- Bar fill — anchored, no sizeDelta needed ---
            GameObject fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(barBgGO.transform, false);
            entry.barFill = fillGO.AddComponent<RawImage>();
            entry.barFill.texture = m_texBarBg;
            RectTransform fillRT = entry.barFill.rectTransform;
            fillRT.anchorMin = new Vector2(0f, 0f);
            fillRT.anchorMax = new Vector2(0f, 1f);    // anchorMax.x is the fill ratio
            fillRT.offsetMin = fillRT.offsetMax = Vector2.zero;

            // Grow content height
            float needed = (idx + 1) * (k_RowHeight + k_RowGap);
            RectTransform scRT = m_scrollContent.GetComponent<RectTransform>();
            scRT.sizeDelta = new Vector2(0f, needed);
        }

        private void CloseWindow() => SetWindowOpen(false);

        private void SetWindowOpen(bool open)
        {
            if (open == m_window.activeSelf) return;
            if (open)
            {
                m_savedLockState    = Cursor.lockState;
                m_savedCursorVisible = Cursor.visible;
                m_cursorStateSaved  = true;
                Cursor.lockState    = CursorLockMode.None;
                Cursor.visible      = true;
                m_window.SetActive(true);
                ScanForAgents();
            }
            else
            {
                m_window.SetActive(false);
                if (m_cursorStateSaved)
                {
                    Cursor.lockState = m_savedLockState;
                    Cursor.visible   = m_savedCursorVisible;
                    m_cursorStateSaved = false;
                }
            }
        }

        private void ShowToast(string msg, float seconds = 3f)
        {
            m_toast     = msg;
            m_toastUntil = Time.unscaledTime + seconds;
        }

        // ---- UI Helpers --------------------------------------------------------

        private TMP_Text CreateTMP(string n, Transform parent)
        {
            GameObject go = new GameObject(n);
            go.transform.SetParent(parent, false);
            TMP_Text t = go.AddComponent<TextMeshProUGUI>();
            t.raycastTarget = false;
            return t;
        }

        private Button CreateButton(string label, Transform parent,
            float x, float y, float w, float h, Color color,
            Action onClick, bool anchorTopRight = false)
        {
            GameObject go = new GameObject($"Btn_{label}");
            go.transform.SetParent(parent, false);
            Image img = go.AddComponent<Image>();
            img.color = color;
            Button btn = go.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor      = color;
            cb.highlightedColor = new Color(
                Mathf.Min(1f, color.r * 1.3f),
                Mathf.Min(1f, color.g * 1.3f),
                Mathf.Min(1f, color.b * 1.3f), 1f);
            cb.pressedColor  = new Color(color.r * 0.7f, color.g * 0.7f, color.b * 0.7f, 1f);
            btn.colors       = cb;
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick());

            RectTransform rt = go.GetComponent<RectTransform>();
            if (anchorTopRight)
            {
                rt.anchorMin = new Vector2(1f, 1f); rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot     = new Vector2(1f, 1f);
            }
            else
            {
                rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot     = new Vector2(0f, 1f);
            }
            rt.sizeDelta        = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, y);

            TMP_Text t = CreateTMP("Label", go.transform);
            RectTransform trt = t.rectTransform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            t.text        = label;
            t.fontSize    = 13f;
            t.fontStyle   = FontStyles.Bold;
            t.color       = Color.white;
            t.alignment   = TextAlignmentOptions.Center;
            return btn;
        }

        #endregion
    }
}
