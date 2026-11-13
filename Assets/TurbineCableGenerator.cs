using System.Collections.Generic;
using UnityEngine;

public class TurbineCableGenerator : MonoBehaviour
{
    [Header("Endpoints")]
    public Transform collectionPoint;
    public Transform turbineParent;
    public List<Transform> turbines = new List<Transform>();

    [Header("Attachment")]
    public string attachChildName = "CableAttach";
    public Vector3 turbineAttachOffset = Vector3.zero;
    public Vector3 collectionAttachOffset = Vector3.zero;

    [Header("Cable Path")]
    public float seabedY = -50f;
    public float touchdownDistance = 25f;
    public float seabedClearance = 0.2f;
    public float laneSpacing = 0f;

    [Header("Lazy Wave (buoyancy hump)")]
    public bool useLazyWave = true;
    public float lazyWaveLength = 140f;
    [Range(0f, 0.8f)] public float sagHeight = 0.2f;
    [Range(0.1f, 0.95f)] public float humpHeight = 0.55f;
    public CableBuoyancySettings buoyancy = new CableBuoyancySettings();

    [Header("Float Failure (damaged buoyancy modules)")]
    public bool enableFloatFailure = true;
    public Vector2Int failedModules = new Vector2Int(1, 2);
    public int floatFailureSeed = 0;

    [Header("Touchdown Protection")]
    public CableProtectionSettings protection = new CableProtectionSettings();

    [Header("Line Renderer")]
    public Material cableMaterial;
    public float cableWidth = 0.6f;
    public Color cableColor = Color.black;
    [Range(4, 64)] public int curveSamples = 24;

    [Header("Tube Mesh")]
    public bool useTubeMesh = true;
    [Range(3, 24)] public int radialSegments = 8;
    public bool castShadows = false;

    [Header("Damage")]
    public CableDamageSettings damage = new CableDamageSettings();

    [Header("Options")]
    public bool generateOnStart = true;

    const string CableContainerName = "__Cables";

    float _crestS = -1f;
    float _touchdownS = -1f;
    bool _floatFailed;
    int _failedModuleIndex = -1;

    void Start()
    {
        if (generateOnStart) GenerateCables();
    }

    [ContextMenu("Generate Cables")]
    public void GenerateCables()
    {
        if (collectionPoint == null)
        {
            Debug.LogError("TurbineCableGenerator: Collection Point is not assigned.", this);
            return;
        }

        ClearCables();

        var container = new GameObject(CableContainerName).transform;
        container.SetParent(transform, false);

        var all = GatherTurbines();
        Dictionary<int, int> failures = PickFloatFailures(all);

        for (int i = 0; i < all.Count; i++)
        {
            Transform t = all[i];
            if (t == null) continue;

            int moduleIndex;
            _floatFailed = failures.TryGetValue(i, out moduleIndex);
            _failedModuleIndex = _floatFailed ? moduleIndex : -1;

            float lane = (i - (all.Count - 1) * 0.5f) * laneSpacing;
            List<Vector3> path = BuildPath(t, lane);
            CreateLine(container, "Cable_" + t.name, path);

            if (_floatFailed)
                Debug.Log("Float failure: cable of '" + t.name + "' (turbine " + (i + 1) + " of " + all.Count
                          + "), module " + (moduleIndex + 1) + " damaged.", t);
        }

        // Cables generated in the editor are never saved into the scene
        if (!Application.isPlaying)
            foreach (Transform node in container.GetComponentsInChildren<Transform>(true))
                node.gameObject.hideFlags = HideFlags.DontSaveInEditor;
    }

    Dictionary<int, int> PickFloatFailures(List<Transform> all)
    {
        var map = new Dictionary<int, int>();
        if (!useLazyWave || !enableFloatFailure || buoyancy == null || !buoyancy.enabled) return map;

        System.Random rng = floatFailureSeed != 0 ? new System.Random(floatFailureSeed) : new System.Random();

        var candidates = new List<int>();
        for (int i = 0; i < all.Count; i++)
            if (all[i] != null) candidates.Add(i);

        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            int tmp = candidates[i]; candidates[i] = candidates[j]; candidates[j] = tmp;
        }

        int count = rng.Next(failedModules.x, Mathf.Max(failedModules.x, failedModules.y) + 1);
        count = Mathf.Min(count, candidates.Count);

        for (int k = 0; k < count; k++)
            map[candidates[k]] = rng.Next(Mathf.Max(1, buoyancy.moduleCount));

        return map;
    }

    [ContextMenu("Clear Cables")]
    public void ClearCables()
    {
        Transform existing = transform.Find(CableContainerName);
        if (existing == null) return;

        if (Application.isPlaying) Destroy(existing.gameObject);
        else DestroyImmediate(existing.gameObject);
    }

    List<Transform> GatherTurbines()
    {
        var result = new List<Transform>();
        if (turbineParent != null)
            foreach (Transform child in turbineParent) result.Add(child);
        foreach (var t in turbines)
            if (t != null && !result.Contains(t)) result.Add(t);
        return result;
    }

    Vector3 GetStartPoint(Transform turbine)
    {
        if (!string.IsNullOrEmpty(attachChildName))
        {
            Transform attach = FindDeep(turbine, attachChildName);
            if (attach != null) return attach.position;
        }
        return turbine.TransformPoint(turbineAttachOffset);
    }

    static Transform FindDeep(Transform root, string name)
    {
        foreach (Transform child in root)
        {
            if (child.name == name) return child;
            Transform found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }

    List<Vector3> BuildPath(Transform turbine, float lane)
    {
        _crestS = -1f;
        _touchdownS = -1f;

        Vector3 start = GetStartPoint(turbine);
        Vector3 end = collectionPoint.TransformPoint(collectionAttachOffset);
        float floorY = seabedY + seabedClearance;

        Vector3 flat = new Vector3(end.x - start.x, 0f, end.z - start.z);
        float flatDist = flat.magnitude;
        Vector3 dir = flatDist > 0.001f ? flat / flatDist : Vector3.right;
        Vector3 side = Vector3.Cross(Vector3.up, dir);

        float td = Mathf.Min(touchdownDistance, flatDist * 0.4f);
        Vector3 laneOffset = side * lane;

        Vector3 touchdown = new Vector3(start.x, floorY, start.z) + dir * td + laneOffset;
        Vector3 liftoff = new Vector3(end.x, floorY, end.z) - dir * td + laneOffset;

        float dropStart = Mathf.Max(start.y - floorY, 0f);
        float dropEnd = Mathf.Max(end.y - floorY, 0f);
        float hv1 = dropStart * 0.5f;
        float hv2 = dropEnd * 0.5f;
        float hh = td * 0.5f;

        var path = new List<Vector3>();

        bool wantLazy = useLazyWave && dropStart > 5f;
        bool hump = wantLazy && !_floatFailed;

        if (wantLazy)
        {
            float waveLength = Mathf.Min(lazyWaveLength, flatDist * 0.45f);
            var lazy = new List<Vector3>();
            int crestIndex = AddLazyWaveDescent(lazy, start, floorY, dir, side, lane, waveLength);

            float s = 0f;
            for (int i = 1; i <= crestIndex; i++) s += Vector3.Distance(lazy[i - 1], lazy[i]);
            _crestS = s;

            if (hump) path.AddRange(lazy);
        }

        if (!hump)
        {
            AddBezier(path, start,
                      start + Vector3.down * hv1,
                      touchdown - dir * hh,
                      touchdown, curveSamples, true);
        }

        float descentLength = 0f;
        for (int i = 1; i < path.Count; i++) descentLength += Vector3.Distance(path[i - 1], path[i]);
        _touchdownS = descentLength;

        AddBezier(path, liftoff,
                  liftoff + dir * hh,
                  end + Vector3.down * hv2,
                  end, curveSamples, false);

        return path;
    }

    int AddLazyWaveDescent(List<Vector3> path, Vector3 start, float floorY, Vector3 dir, Vector3 side,
                           float lane, float length)
    {
        float depth = start.y - floorY;
        float sagFrac = sagHeight;
        float humpFrac = Mathf.Min(Mathf.Max(humpHeight, sagFrac + 0.1f), 0.95f);
        float ySag = floorY + depth * sagFrac;
        float yHump = floorY + depth * humpFrac;

        float d1 = 0.28f * length;
        float d2 = 0.62f * length;

        System.Func<float, float, Vector3> P = (d, y) =>
        {
            float k = Mathf.Clamp01(d / length);
            float blend = k * k * (3f - 2f * k);
            return new Vector3(start.x, y, start.z) + dir * d + side * (lane * blend);
        };

        Vector3 a3 = P(d1, ySag);
        AddBezier(path, start,
                  start + Vector3.down * ((start.y - ySag) * 0.5f),
                  a3 - dir * (d1 * 0.5f),
                  a3, curveSamples, true);

        Vector3 b3 = P(d2, yHump);
        float hB = (d2 - d1) * 0.5f;
        AddBezier(path, a3, a3 + dir * hB, b3 - dir * hB, b3, curveSamples, true);
        int crestIndex = path.Count - 1;

        Vector3 c3 = P(length, floorY);
        float hC = (length - d2) * 0.5f;
        AddBezier(path, b3, b3 + dir * hC, c3 - dir * hC, c3, curveSamples, true);

        return crestIndex;
    }

    static void AddBezier(List<Vector3> list, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3,
                          int samples, bool includeFirst)
    {
        int startIndex = includeFirst ? 0 : 0;
        for (int i = startIndex; i <= samples; i++)
        {
            float t = i / (float)samples;
            float u = 1f - t;
            Vector3 p = u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
            list.Add(p);
        }
    }

    void CreateLine(Transform parent, string lineName, List<Vector3> points)
    {
        if (useTubeMesh)
        {
            CableTubeBuilder.Create(parent, lineName, points, cableWidth * 0.5f,
                                    radialSegments, cableMaterial, cableColor, castShadows, 5f, damage,
                                    useLazyWave ? buoyancy : null, _crestS, _failedModuleIndex,
                                    protection, _touchdownS);
            return;
        }

        var go = new GameObject(lineName);
        go.transform.SetParent(parent, false);

        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = points.Count;
        lr.SetPositions(points.ToArray());

        lr.widthMultiplier = cableWidth;
        lr.numCornerVertices = 4;
        lr.numCapVertices = 4;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;

        lr.sharedMaterial = cableMaterial != null
            ? cableMaterial
            : new Material(Shader.Find("Sprites/Default"));
        lr.startColor = cableColor;
        lr.endColor = cableColor;
    }
}