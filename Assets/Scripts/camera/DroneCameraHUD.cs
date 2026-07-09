using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Attach to each drone's Camera GameObject.
///
/// Creates a ScreenSpaceCamera Canvas on the drone camera itself.
/// InspectionDetector excludes the UI layer during snapCam.Render(), so the
/// overlay is never sent to the YOLO server.
///
/// Fleet is grouped by drone type name (x500 / wamv / bluerov).
/// [↑ ↓] cycle drones within the fleet.
/// </summary>
[RequireComponent(typeof(Camera))]
public class DroneCameraHUD : MonoBehaviour
{

    // ── Inspector ─────────────────────────────────────────────────────────────

    public enum FleetType { Auto, BlueROV, X500, WAMV, Drone }

    [Tooltip("Fleet this camera belongs to. Set on the prefab so the fleet is known regardless " +
             "of the vessel's ROS name. 'Auto' falls back to name-based detection.")]
    [SerializeField] private FleetType m_fleetType = FleetType.Auto;

    // ── State ─────────────────────────────────────────────────────────────────

    public string FleetName => m_fleetName;

    private Camera m_cam;
    private string m_droneName;
    private string m_fleetName;

    private TMP_Text m_titleText;
    private TMP_Text m_nameText;
    private RectTransform m_dotsRow;
    private readonly List<Image> m_dots = new List<Image>();

    // One switch allowed per fleet per frame
    private static readonly Dictionary<string, int> s_switchFrame = new Dictionary<string, int>();

    private const float k_PanelW = 272f;
    private const float k_PanelH = 108f;
    private const float k_DotsW = 252f;

    private static readonly Color k_DotActive = new Color(0.22f, 0.62f, 1.00f);
    private static readonly Color k_DotInactive = new Color(0.28f, 0.28f, 0.28f, 0.75f);
    private static readonly Color k_PanelBg = new Color(0.04f, 0.06f, 0.10f, 0.82f);

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        m_cam = GetComponent<Camera>();

        var follower = GetComponentInParent<RendererPosesWaypointFollower>();
        m_droneName = follower != null ? follower.gameObject.name : gameObject.name;
        m_fleetName = m_fleetType == FleetType.Auto ? FleetOf(m_droneName) : m_fleetType.ToString();

        BuildCanvas();
        StartCoroutine(LateRefresh());
    }

    // Wait one frame so all DroneCameraHUD.Start() calls have run before building the fleet.
    System.Collections.IEnumerator LateRefresh()
    {
        yield return null;
        RefreshHUD();
    }

    void Update()
    {
        if (Input.GetKeyDown(Lotusim.KeyBindings.DronePrev)) CycleDrone(-1);
        if (Input.GetKeyDown(Lotusim.KeyBindings.DroneNext)) CycleDrone(+1);
    }

    // ── Cycling ───────────────────────────────────────────────────────────────

    void CycleDrone(int dir)
    {
        if (s_switchFrame.TryGetValue(m_fleetName, out int f) && f == Time.frameCount) return;
        s_switchFrame[m_fleetName] = Time.frameCount;

        var fleet = GetFleet();
        if (fleet.Count <= 1) return;

        // Find the currently-active (enabled) camera — NOT IndexOf(this),
        // because whichever Update() wins the race would always start from
        // the same camera and cycle between just two entries.
        int idx = -1;
        for (int i = 0; i < fleet.Count; i++)
            if (fleet[i].m_cam.enabled) { idx = i; break; }

        if (idx < 0) idx = 0; // fallback: start from first

        int next = (idx + dir + fleet.Count) % fleet.Count;

        foreach (var h in fleet) h.m_cam.enabled = false;
        fleet[next].m_cam.enabled = true;

        // Tell DisplaySwitcher to re-route the active slot to the newly-enabled camera.
        Lotusim.DisplaySwitcher.Instance?.RefreshCurrentSlot();

        fleet[next].RefreshHUD();
    }



    // ── HUD ───────────────────────────────────────────────────────────────────

    public void RefreshHUD()
    {
        var fleet = GetFleet();
        int idx = fleet.IndexOf(this);
        int count = fleet.Count;

        m_titleText.text = $"{m_fleetName}   " +
                           $"<size=13><color=#A0B8CC>{idx + 1} / {count}</color></size>";
        m_nameText.text = m_droneName;
        RebuildDots(count, idx);
    }

    void RebuildDots(int count, int activeIdx)
    {
        foreach (var d in m_dots) if (d) Destroy(d.gameObject);
        m_dots.Clear();
        if (count == 0) return;

        float dot = Mathf.Min(20f, (k_DotsW - (count - 1) * 4f) / count);
        float gap = 4f;
        float startX = (k_DotsW - (count * (dot + gap) - gap)) * 0.5f;

        for (int i = 0; i < count; i++)
        {
            var go = new GameObject($"Dot{i}");
            go.transform.SetParent(m_dotsRow, false);
            var img = go.AddComponent<Image>();
            img.color = (i == activeIdx) ? k_DotActive : k_DotInactive;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(dot, dot);
            rt.anchoredPosition = new Vector2(startX + i * (dot + gap) + dot * 0.5f, 0f);
            m_dots.Add(img);
        }
    }

    // ── Fleet ─────────────────────────────────────────────────────────────────

    List<DroneCameraHUD> GetFleet()
    {
        var result = new List<DroneCameraHUD>();
        foreach (var h in FindObjectsByType<DroneCameraHUD>(FindObjectsSortMode.None))
            if (h.m_fleetName != null && h.m_fleetName == m_fleetName)
                result.Add(h);
        result.Sort((a, b) => string.CompareOrdinal(a.m_droneName, b.m_droneName));
        return result;
    }

    static string FleetOf(string name)
    {
        string n = name.ToLowerInvariant();
        if (n.Contains("x500")) return "X500";
        if (n.Contains("wamv")) return "WAMV";
        if (n.Contains("bluerov") || n.Contains("rov")) return "BlueROV";
        return "Drone";
    }

    // ── Canvas (on drone camera — excluded from YOLO via InspectionDetector's cullingMask) ──

    void BuildCanvas()
    {
        int hudLayer = LayerMask.NameToLayer("DroneHUD");
        if (hudLayer < 0)
        {
            Debug.LogError("[DroneCameraHUD] Layer 'DroneHUD' not found. " +
                           "Create it in Edit → Project Settings → Tags and Layers.");
            hudLayer = 0;
        }

        var cgo = new GameObject("DroneCamHUD_Canvas");
        cgo.transform.SetParent(transform, false);

        var canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = m_cam;
        canvas.planeDistance = 0.5f;
        canvas.sortingOrder = 10;
        cgo.AddComponent<CanvasScaler>();
        cgo.AddComponent<GraphicRaycaster>();

        // Panel — top-right corner
        var panel = new GameObject("Panel");
        panel.transform.SetParent(cgo.transform, false);
        panel.AddComponent<Image>().color = k_PanelBg;
        var pRT = panel.GetComponent<RectTransform>();
        pRT.anchorMin = new Vector2(1f, 1f);
        pRT.anchorMax = new Vector2(1f, 1f);
        pRT.pivot = new Vector2(1f, 1f);
        pRT.sizeDelta = new Vector2(k_PanelW, k_PanelH);
        pRT.anchoredPosition = new Vector2(-10f, -10f);

        m_titleText = MakeTMP("Title", panel.transform,
            16f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, Color.white,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(10, -36), new Vector2(-10, -8));

        m_nameText = MakeTMP("Name", panel.transform,
            11f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, new Color(0.65f, 0.80f, 1f),
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(10, -54), new Vector2(-10, -36));

        var dotsGO = new GameObject("DotsRow");
        dotsGO.transform.SetParent(panel.transform, false);
        m_dotsRow = dotsGO.AddComponent<RectTransform>();
        m_dotsRow.anchorMin = new Vector2(0, 1);
        m_dotsRow.anchorMax = new Vector2(1, 1);
        m_dotsRow.offsetMin = new Vector2(10, -80);
        m_dotsRow.offsetMax = new Vector2(-10, -54);

        var hint = MakeTMP("Hint", panel.transform,
            9f, FontStyles.Normal, TextAlignmentOptions.MidlineRight, new Color(0.40f, 0.40f, 0.40f),
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(10, 6), new Vector2(-10, 22));
        hint.text = $"[{Lotusim.KeyBindings.DronePrev}/{Lotusim.KeyBindings.DroneNext}] drone";

    }

    static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }

    static TMP_Text MakeTMP(string name, Transform parent,
        float size, FontStyles style, TextAlignmentOptions align, Color color,
        Vector2 ancMin, Vector2 ancMax, Vector2 offMin, Vector2 offMax)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = size; t.fontStyle = style; t.alignment = align;
        t.color = color; t.raycastTarget = false;
        var rt = t.rectTransform;
        rt.anchorMin = ancMin; rt.anchorMax = ancMax;
        rt.offsetMin = offMin; rt.offsetMax = offMax;
        return t;
    }
}
