using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class SeabedCableRoute : MonoBehaviour
{
    [Header("Route")]
    public Transform startPoint;
    [Tooltip("Cable start, in the start point's local space.")]
    public Vector3 startOffset = Vector3.zero;
    [Tooltip("Optional bend points on the flat seabed run, before the ascent point. Element 0 is nearest the collection point.")]
    public List<Transform> preAscentWaypoints = new List<Transform>();
    [Tooltip("Where the cable stops running flat and starts to climb.")]
    public Transform ascentPoint;
    [Tooltip("Points the cable passes through while climbing, in order from the ascent point toward the shore.")]
    [FormerlySerializedAs("seabedWaypoints")]
    public List<Transform> ascentWaypoints = new List<Transform>();
    [Tooltip("Where the cable reaches the shoreline.")]
    public Transform shorePoint;

    [Header("Heights (world Y)")]
    public float seabedY = -300f;
    public float shoreY = 0f;
    [Tooltip("If on, the climb passes through each Ascent Waypoint's world Y. If off, only their X/Z is used and the climb is one smooth ramp.")]
    public bool useAscentWaypointHeights = false;

    [Header("Shape")]
    [Tooltip("Horizontal distance over which the cable curves from the start down to the seabed.")]
    public float touchdownDistance = 40f;
    [Tooltip("Corner rounding radius at waypoints and the ascent point. Automatically limited on short segments. 0 = sharp corners.")]
    public float cornerRadius = 100f;
    [Tooltip("Points per curved section (descent, each rounded corner, and the climb).")]
    [Range(4, 96)] public int curveSamples = 32;
    [Tooltip("Maximum spacing of points along the flat seabed run.")]
    public float flatSpacing = 50f;

    [Header("Line Renderer")]
    public Material cableMaterial;
    public float cableWidth = 0.6f;
    public Color cableColor = Color.black;

    [Header("Options")]
    public bool generateOnStart = true;

    const string CableName = "__SeabedCable";

    void Start()
    {
        if (generateOnStart) Generate();
    }

    [ContextMenu("Generate")]
    public void Generate()
    {
        List<Vector3> path = BuildPath(true);
        if (path == null) return;

        ClearCable();
        CreateLine(path);
    }

    [ContextMenu("Clear Cable")]
    public void ClearCable()
    {
        Transform existing = transform.Find(CableName);
        if (existing == null) return;

        if (Application.isPlaying) Destroy(existing.gameObject);
        else DestroyImmediate(existing.gameObject);
    }

    List<Vector3> BuildPath(bool logErrors)
    {
        if (startPoint == null || ascentPoint == null || shorePoint == null)
        {
            if (logErrors)
                Debug.LogError("SeabedCableRoute: Start Point, Ascent Point and Shore Point must all be assigned.", this);
            return null;
        }

        Vector3 start = startPoint.TransformPoint(startOffset);
        Vector3 ascentPlan = Flat(ascentPoint.position, 0f);
        Vector3 shorePlan = Flat(shorePoint.position, 0f);

        var nodes = new List<Vector3>();
        foreach (var w in preAscentWaypoints)
            if (w != null) nodes.Add(Flat(w.position, 0f));
        nodes.Add(ascentPlan);

        var validAscentWps = new List<Transform>();
        foreach (var w in ascentWaypoints)
        {
            if (w == null) continue;
            validAscentWps.Add(w);
            nodes.Add(Flat(w.position, 0f));
        }

        Vector3 startPlan = Flat(start, 0f);
        Vector3 toFirst = HorizontalDir(startPlan, nodes[0]);
        float distToFirst = HorizontalDist(startPlan, nodes[0]);
        float td = Mathf.Min(touchdownDistance, distToFirst * 0.5f);
        Vector3 touchdownPlan = startPlan + toFirst * td;

        var plan = new List<Vector3> { touchdownPlan };
        plan.AddRange(nodes);
        plan.Add(shorePlan);
        plan = RemoveDuplicates(plan);

        if (plan.Count < 2)
        {
            if (logErrors) Debug.LogError("SeabedCableRoute: route points are all at the same position.", this);
            return null;
        }

        plan = Fillet(plan, cornerRadius, Mathf.Max(4, curveSamples / 4));

        float[] cum = new float[plan.Count];
        for (int i = 1; i < plan.Count; i++)
            cum[i] = cum[i - 1] + Vector3.Distance(plan[i - 1], plan[i]);
        float total = cum[cum.Length - 1];

        float sAscent = ArcLengthNearest(plan, cum, ascentPlan);

        var ctrlS = new List<float> { sAscent };
        var ctrlY = new List<float> { seabedY };

        if (useAscentWaypointHeights)
        {
            float lastS = sAscent;
            foreach (var w in validAscentWps)
            {
                float s = ArcLengthNearest(plan, cum, Flat(w.position, 0f));
                s = Mathf.Max(s, lastS + 0.5f);
                if (s >= total - 0.5f) continue;
                ctrlS.Add(s);
                ctrlY.Add(w.position.y);
                lastS = s;
            }
        }
        ctrlS.Add(total);
        ctrlY.Add(shoreY);
        float[] tangents = ComputeTangents(ctrlS, ctrlY);

        var samples = new List<float>();
        float spacing = Mathf.Max(1f, flatSpacing);
        for (float d = 0f; d < sAscent; d += spacing) samples.Add(d);

        int riseSamples = curveSamples * (1 + validAscentWps.Count);
        for (int i = 0; i <= riseSamples; i++)
            samples.Add(Mathf.Lerp(sAscent, total, i / (float)riseSamples));

        for (int i = 0; i < cum.Length; i++) samples.Add(cum[i]);
        samples.Sort();

        var path = new List<Vector3>();

        Vector3 touchdown = new Vector3(touchdownPlan.x, seabedY, touchdownPlan.z);
        float hv = Mathf.Max(start.y - seabedY, 0f) * 0.5f;
        float hh = td * 0.5f;
        for (int i = 0; i <= curveSamples; i++)
        {
            float t = i / (float)curveSamples;
            path.Add(Bezier(start,
                            start + Vector3.down * hv,
                            touchdown - toFirst * hh,
                            touchdown, t));
        }

        float lastSample = 0f;
        foreach (float s in samples)
        {
            if (s - lastSample < 0.01f) continue;
            lastSample = s;

            Vector3 p = PointAtLength(plan, cum, s);
            float y = s <= sAscent ? seabedY : EvalHermite(ctrlS, ctrlY, tangents, s);
            path.Add(new Vector3(p.x, y, p.z));
        }

        path[path.Count - 1] = new Vector3(shorePlan.x, shoreY, shorePlan.z);

        return path;
    }

    static float[] ComputeTangents(List<float> xs, List<float> ys)
    {
        int n = xs.Count;
        var m = new float[n];
        if (n < 3) return m;

        var h = new float[n - 1];
        var d = new float[n - 1];
        for (int k = 0; k < n - 1; k++)
        {
            h[k] = xs[k + 1] - xs[k];
            d[k] = (ys[k + 1] - ys[k]) / h[k];
        }

        for (int k = 1; k < n - 1; k++)
        {
            if (d[k - 1] * d[k] <= 0f) { m[k] = 0f; continue; }
            float w1 = 2f * h[k] + h[k - 1];
            float w2 = h[k] + 2f * h[k - 1];
            m[k] = (w1 + w2) / (w1 / d[k - 1] + w2 / d[k]);
        }
        return m;
    }

    static float EvalHermite(List<float> xs, List<float> ys, float[] m, float s)
    {
        int n = xs.Count;
        if (s <= xs[0]) return ys[0];
        if (s >= xs[n - 1]) return ys[n - 1];

        int k = 0;
        while (k < n - 2 && s > xs[k + 1]) k++;

        float h = xs[k + 1] - xs[k];
        float t = (s - xs[k]) / h;
        float t2 = t * t, t3 = t2 * t;

        return (2f * t3 - 3f * t2 + 1f) * ys[k]
             + (t3 - 2f * t2 + t) * h * m[k]
             + (-2f * t3 + 3f * t2) * ys[k + 1]
             + (t3 - t2) * h * m[k + 1];
    }

    static List<Vector3> Fillet(List<Vector3> pts, float radius, int samples)
    {
        if (radius <= 0.01f || pts.Count < 3) return pts;

        var result = new List<Vector3> { pts[0] };
        for (int i = 1; i < pts.Count - 1; i++)
        {
            Vector3 prev = pts[i - 1], cur = pts[i], next = pts[i + 1];
            float lenIn = Vector3.Distance(prev, cur);
            float lenOut = Vector3.Distance(cur, next);
            float r = Mathf.Min(radius, lenIn * 0.45f, lenOut * 0.45f);

            Vector3 a = cur + (prev - cur).normalized * r;
            Vector3 b = cur + (next - cur).normalized * r;

            for (int k = 0; k <= samples; k++)
            {
                float t = k / (float)samples;
                float u = 1f - t;
                result.Add(u * u * a + 2f * u * t * cur + t * t * b);
            }
        }
        result.Add(pts[pts.Count - 1]);
        return RemoveDuplicates(result);
    }

    static List<Vector3> RemoveDuplicates(List<Vector3> pts)
    {
        var result = new List<Vector3>();
        foreach (var p in pts)
            if (result.Count == 0 || (p - result[result.Count - 1]).sqrMagnitude > 0.0001f)
                result.Add(p);
        return result;
    }

    static float ArcLengthNearest(List<Vector3> pts, float[] cum, Vector3 target)
    {
        float bestDist = float.MaxValue;
        float bestS = 0f;
        for (int i = 0; i < pts.Count - 1; i++)
        {
            Vector3 a = pts[i], b = pts[i + 1];
            Vector3 ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 < 1e-8f) continue;

            float t = Mathf.Clamp01(Vector3.Dot(target - a, ab) / len2);
            float d = (a + ab * t - target).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                bestS = cum[i] + t * Mathf.Sqrt(len2);
            }
        }
        return bestS;
    }

    static Vector3 PointAtLength(List<Vector3> pts, float[] cum, float s)
    {
        if (s <= 0f) return pts[0];
        for (int i = 1; i < pts.Count; i++)
        {
            if (s <= cum[i])
            {
                float seg = cum[i] - cum[i - 1];
                float t = seg > 1e-6f ? (s - cum[i - 1]) / seg : 0f;
                return Vector3.Lerp(pts[i - 1], pts[i], t);
            }
        }
        return pts[pts.Count - 1];
    }

    static Vector3 Bezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float u = 1f - t;
        return u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
    }

    static Vector3 Flat(Vector3 p, float y) => new Vector3(p.x, y, p.z);

    static float HorizontalDist(Vector3 a, Vector3 b) =>
        new Vector3(b.x - a.x, 0f, b.z - a.z).magnitude;

    static Vector3 HorizontalDir(Vector3 a, Vector3 b)
    {
        Vector3 d = new Vector3(b.x - a.x, 0f, b.z - a.z);
        return d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.forward;
    }

    void CreateLine(List<Vector3> points)
    {
        var go = new GameObject(CableName);
        go.transform.SetParent(transform, false);

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

    void OnDrawGizmosSelected()
    {
        List<Vector3> path = BuildPath(false);
        if (path == null) return;

        Gizmos.color = Color.cyan;
        for (int i = 1; i < path.Count; i++)
            Gizmos.DrawLine(path[i - 1], path[i]);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(path[path.Count - 1], 3f);
    }
}