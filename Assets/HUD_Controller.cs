using System;
using UnityEngine;

namespace Lotusim
{
    public class HUD_controller : MonoBehaviour
    {
        [Serializable]
        private class Entry
        {
            public KeyCode key;
            [Tooltip("Component on the HUD GameObject that implements IHudPanel.")]
            public MonoBehaviour hud;
            [NonSerialized] public IHudPanel panel;
        }

        [Header("Toggled panels (mutually exclusive)")]
        [SerializeField] private Entry[] entries =
        {
            new Entry { key = KeyCode.G },
            new Entry { key = KeyCode.B },
            new Entry { key = KeyCode.L },
        };

        [Header("Close-all")]
        [SerializeField] private KeyCode closeAllKey = KeyCode.Escape;

        [Tooltip("Panels with their own toggle key that behave like the toggled panels above (e.g. the help menu): " +
                 "they close when a toggled panel opens, and opening them closes the toggled panels. " +
                 "Also closed by the close-all key.")]
        [SerializeField] private MonoBehaviour[] closeWhenPanelOpens;

        [Tooltip("Panels with their own toggle key that are closed ONLY by the close-all key " +
                 "(e.g. the warning boxes).")]
        [SerializeField] private MonoBehaviour[] closeOnCloseAllOnly;

        private IHudPanel[] m_closeWhenOpens;
        private bool[] m_closeWhenOpensWasOpen;
        private IHudPanel[] m_closeAllOnly;

        private void Awake()
        {
            foreach (var e in entries)
            {
                if (e.hud == null) continue;
                e.panel = e.hud as IHudPanel;
                if (e.panel == null)
                    Debug.LogError($"[HUD_controller] {e.hud.name} ({e.hud.GetType().Name}) does not implement IHudPanel.", e.hud);
            }

            m_closeWhenOpens = Resolve(closeWhenPanelOpens);
            m_closeWhenOpensWasOpen = new bool[m_closeWhenOpens.Length];
            m_closeAllOnly = Resolve(closeOnCloseAllOnly);
        }

        private static IHudPanel[] Resolve(MonoBehaviour[] source)
        {
            var result = new IHudPanel[source != null ? source.Length : 0];
            for (int i = 0; i < result.Length; i++)
            {
                MonoBehaviour mb = source[i];
                if (mb == null) continue;
                result[i] = mb as IHudPanel;
                if (result[i] == null)
                    Debug.LogError($"[HUD_controller] {mb.name} ({mb.GetType().Name}) does not implement IHudPanel.", mb);
            }
            return result;
        }

        private void Update()
        {
            if (Input.GetKeyDown(closeAllKey))
            {
                CloseAll();
                return;
            }

            foreach (var e in entries)
            {
                if (e.panel == null || !Input.GetKeyDown(e.key)) continue;

                bool willOpen = !e.panel.IsOpen;
                CloseToggledPanels();

                if (willOpen)
                {
                    CloseList(m_closeWhenOpens); // e.g. help menu
                    e.panel.SetOpen(true);
                }
                break;
            }

            WatchTwoWayPanels();
        }

        private void WatchTwoWayPanels()
        {
            for (int i = 0; i < m_closeWhenOpens.Length; i++)
            {
                IHudPanel p = m_closeWhenOpens[i];
                if (p == null) continue;

                bool isOpen = p.IsOpen;
                if (isOpen && !m_closeWhenOpensWasOpen[i])
                    CloseToggledPanels();
                m_closeWhenOpensWasOpen[i] = isOpen;
            }
        }

        private void CloseToggledPanels()
        {
            foreach (var e in entries)
                if (e.panel != null && e.panel.IsOpen) e.panel.SetOpen(false);
        }

        private static void CloseList(IHudPanel[] panels)
        {
            if (panels == null) return;
            foreach (var p in panels)
                if (p != null && p.IsOpen) p.SetOpen(false);
        }

        [ContextMenu("Close All")]
        public void CloseAll()
        {
            CloseToggledPanels();
            CloseList(m_closeWhenOpens);
            CloseList(m_closeAllOnly);
        }
    }
}