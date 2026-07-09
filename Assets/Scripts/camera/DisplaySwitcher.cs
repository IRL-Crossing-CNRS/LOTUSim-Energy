using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lotusim
{
    /// <summary>
    /// Routes one camera at a time to the primary display (targetDisplay 0).
    /// All other cameras are parked to an off-screen display index so they do
    /// not overlap when running on a single monitor.
    ///
    /// Static cameras (Display 1, 4, 8) are wired in the Inspector.
    /// Agent cameras (Displays 5–7) are found at runtime by fleet name; if the
    /// agent has not been spawned yet the switch is silently skipped and a
    /// brief toast appears.
    ///
    /// HUDs that belong exclusively to Display 1 are listed in that slot's
    /// exclusiveHUDs array and toggled with SetActive so they never bleed into
    /// other camera views.
    ///
    /// Setup (once per scene):
    ///   1. Add this component to any persistent singleton GameObject.
    ///   2. Fill m_slots in the Inspector — one entry per camera view.
    ///      • Static cameras: assign the Camera component directly.
    ///      • Agent cameras:  leave staticCamera null, set agentFleet to
    ///        "BlueROV", "WAMV", or "X500" (matches DroneCameraHUD.FleetName).
    ///   3. For the Display-1 slot, add the root GameObjects of every HUD that
    ///      should only appear on the main view (WindSliderPanel, HelpMenuHUD
    ///      host, graph HUD hosts, battery HUD host) to exclusiveHUDs.
    ///   4. Make sure the main camera starts at targetDisplay 0 in the scene
    ///      (all other cameras should be on their designated display index, e.g.
    ///      3, 4, 5, 7 — they will be parked automatically on Start).
    /// </summary>
    public class DisplaySwitcher : MonoBehaviour
    {
        [Serializable]
        public class DisplaySlot
        {
            [Tooltip("Human-readable label shown in the toast when this slot is activated.")]
            public string label;

            [Tooltip("Numpad key that activates this slot.")]
            public KeyCode key;

            [Tooltip("Camera for static displays (Display 1, 4, 8). Leave null for agent camera slots.")]
            public Camera staticCamera;

            [Tooltip("Fleet name for agent camera slots: 'X500', 'WAMV', or 'BlueROV'. Leave empty when staticCamera is set.")]
            public string agentFleet;

            [Tooltip("HUD root GameObjects to show ONLY when this slot is active. All others are hidden.")]
            public GameObject[] exclusiveHUDs;
        }

        [SerializeField] private DisplaySlot[] m_slots;

        [Tooltip("Display index used to park inactive cameras (should not correspond to any physical monitor).")]
        [SerializeField] private int m_parkDisplay = 9;

        // ── Singleton ─────────────────────────────────────────────────────────

        public static DisplaySwitcher Instance { get; private set; }

        /// <summary>
        /// Re-resolves the camera for the currently active slot.
        /// Call this after externally changing which drone camera is enabled
        /// so DisplaySwitcher can route the right camera to the display.
        /// </summary>
        public void RefreshCurrentSlot()
        {
            if (m_activeSlotIndex >= 0)
                ActivateSlot(m_activeSlotIndex, force: true);
        }

        // ── State ─────────────────────────────────────────────────────────────

        public int ActiveSlotIndex => m_activeSlotIndex;

        private int m_activeSlotIndex = -1;
        private Camera m_activeCamera;

        // ── Toast ─────────────────────────────────────────────────────────────

        private TMP_Text m_toastText;
        private float m_toastTimer;
        private const float k_ToastDuration = 2f;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        void Awake()
        {
            Instance = this;
            BuildToast();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            // Park all static cameras; they will be brought to display 0 one at a time.
            foreach (var slot in m_slots)
                if (slot.staticCamera != null)
                    slot.staticCamera.targetDisplay = m_parkDisplay;

            ActivateSlot(0, force: true);
        }

        void Update()
        {
            for (int i = 0; i < m_slots.Length; i++)
            {
                if (Input.GetKeyDown(m_slots[i].key))
                {
                    ActivateSlot(i);
                    break;
                }
            }

            if (m_toastTimer > 0f)
            {
                m_toastTimer -= Time.deltaTime;
                if (m_toastTimer <= 0f)
                    m_toastText.gameObject.SetActive(false);
            }
        }

        // ── Switching ─────────────────────────────────────────────────────────

        void ActivateSlot(int index, bool force = false)
        {
            if (index == m_activeSlotIndex && !force) return;

            var slot = m_slots[index];
            Camera cam = ResolveCamera(slot);

            if (cam == null)
            {
                ShowToast($"{slot.label}: not spawned yet");
                return;
            }

            // Park the previously active camera.
            if (m_activeCamera != null)
                m_activeCamera.targetDisplay = m_parkDisplay;

            // Bring the new camera to the primary display.
            cam.targetDisplay = 0;
            m_activeCamera = cam;
            m_activeSlotIndex = index;

            // Show only the HUDs that belong to this slot.
            for (int i = 0; i < m_slots.Length; i++)
            {
                bool on = (i == index);
                foreach (var hud in m_slots[i].exclusiveHUDs)
                    if (hud != null) hud.SetActive(on);
            }

            ShowToast(slot.label);
        }

        Camera ResolveCamera(DisplaySlot slot)
        {
            if (slot.staticCamera != null) return slot.staticCamera;
            if (string.IsNullOrEmpty(slot.agentFleet)) return null;

            // Find the currently-enabled camera in the requested fleet first,
            // then fall back to any camera in that fleet.
            DroneCameraHUD preferred = null;
            DroneCameraHUD fallback = null;

            foreach (var hud in FindObjectsByType<DroneCameraHUD>(FindObjectsSortMode.None))
            {
                if (hud.FleetName != slot.agentFleet) continue;
                var c = hud.GetComponent<Camera>();
                if (c == null) continue;
                if (fallback == null) fallback = hud;
                if (c.enabled) { preferred = hud; break; }
            }

            var target = preferred ?? fallback;
            return target != null ? target.GetComponent<Camera>() : null;
        }

        // ── Toast ─────────────────────────────────────────────────────────────

        void ShowToast(string message)
        {
            m_toastText.text = message;
            m_toastText.gameObject.SetActive(true);
            m_toastTimer = k_ToastDuration;
        }

        void BuildToast()
        {
            var cgo = new GameObject("DisplaySwitcher_Canvas");
            cgo.transform.SetParent(transform, false);

            var canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            cgo.AddComponent<CanvasScaler>();

            var go = new GameObject("Toast");
            go.transform.SetParent(cgo.transform, false);
            m_toastText = go.AddComponent<TextMeshProUGUI>();
            m_toastText.fontSize = 20f;
            m_toastText.fontStyle = FontStyles.Bold;
            m_toastText.color = new Color(1f, 1f, 1f, 0.9f);
            m_toastText.alignment = TextAlignmentOptions.TopLeft;
            m_toastText.raycastTarget = false;

            var rt = m_toastText.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(400f, 36f);
            rt.anchoredPosition = new Vector2(20f, -20f);

            go.SetActive(false);
        }
    }
}
