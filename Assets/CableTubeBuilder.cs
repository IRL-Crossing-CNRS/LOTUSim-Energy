using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class Polyline
{
    public readonly List<Vector3> pts = new List<Vector3>();
    public readonly List<float> cum = new List<float>();
    public float Length { get { return cum.Count > 0 ? cum[cum.Count - 1] : 0f; } }

    public Polyline(IList<Vector3> source)
    {
        foreach (var p in source)
        {
            if (pts.Count == 0) { pts.Add(p); cum.Add(0f); continue; }
            float d = Vector3.Distance(pts[pts.Count - 1], p);
            if (d < 0.001f) continue;
            pts.Add(p);
            cum.Add(cum[cum.Count - 1] + d);
        }
    }

    int IndexAt(float s)
    {
        int lo = 0, hi = cum.Count - 2;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (cum[mid] <= s) lo = mid; else hi = mid - 1;
        }
        return lo;
    }

    public Vector3 PointAt(float s)
    {
        if (pts.Count == 0) return Vector3.zero;
        if (s <= 0f) return pts[0];
        if (s >= Length) return pts[pts.Count - 1];
        int i = IndexAt(s);
        float span = cum[i + 1] - cum[i];
        float t = span > 1e-6f ? (s - cum[i]) / span : 0f;
        return Vector3.Lerp(pts[i], pts[i + 1], t);
    }

    public Vector3 TangentAt(float s)
    {
        const float h = 0.5f;
        Vector3 a = PointAt(Mathf.Max(0f, s - h));
        Vector3 b = PointAt(Mathf.Min(Length, s + h));
        Vector3 d = b - a;
        return d.sqrMagnitude > 1e-8f ? d.normalized : Vector3.forward;
    }

    public List<Vector3> SubPath(float s0, float s1, float maxStep)
    {
        var result = new List<Vector3>();
        s0 = Mathf.Clamp(s0, 0f, Length);
        s1 = Mathf.Clamp(s1, 0f, Length);
        if (s1 - s0 < 0.001f) return result;

        var marks = new List<float> { s0 };
        for (int i = 0; i < cum.Count; i++)
            if (cum[i] > s0 + 0.001f && cum[i] < s1 - 0.001f) marks.Add(cum[i]);
        marks.Add(s1);

        result.Add(PointAt(marks[0]));
        for (int i = 1; i < marks.Count; i++)
        {
            float a = marks[i - 1], b = marks[i];
            int sub = Mathf.Max(1, Mathf.CeilToInt((b - a) / maxStep));
            for (int k = 1; k <= sub; k++)
                result.Add(PointAt(Mathf.Lerp(a, b, k / (float)sub)));
        }
        return result;
    }
}

public class TubeMeshBuilder
{
    readonly Vector3 origin;
    readonly List<Vector3> verts = new List<Vector3>();
    readonly List<Vector3> norms = new List<Vector3>();
    readonly List<Vector2> uvs = new List<Vector2>();
    readonly List<Color> colors = new List<Color>();
    readonly List<int> tris = new List<int>();

    public bool HasGeometry { get { return verts.Count > 0; } }

    public TubeMeshBuilder(Vector3 origin) { this.origin = origin; }

    public void AddTube(IList<Vector3> source, float radius, int sides, Color color, float uvTile,
                        bool capStart, bool capEnd, float uvStartDist = 0f,
                        System.Func<float, float, float> radiusScale = null)
    {
        var pts = new List<Vector3>();
        foreach (var p in source)
            if (pts.Count == 0 || (p - pts[pts.Count - 1]).sqrMagnitude > 1e-6f) pts.Add(p);

        int n = pts.Count;
        if (n < 2) return;
        sides = Mathf.Max(3, sides);
        uvTile = Mathf.Max(0.01f, uvTile);

        var tan = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            Vector3 a = pts[Mathf.Max(i - 1, 0)];
            Vector3 b = pts[Mathf.Min(i + 1, n - 1)];
            tan[i] = (b - a).normalized;
        }

        var ringN = new Vector3[n];
        var ringB = new Vector3[n];
        Vector3 normal = Vector3.Cross(tan[0], Mathf.Abs(Vector3.Dot(tan[0], Vector3.up)) > 0.99f
                                                   ? Vector3.right : Vector3.up).normalized;
        for (int i = 0; i < n; i++)
        {
            if (i > 0)
            {
                normal = normal - tan[i] * Vector3.Dot(normal, tan[i]);
                if (normal.sqrMagnitude < 1e-6f) normal = Vector3.Cross(tan[i], Vector3.up).normalized;
                else normal.Normalize();
            }
            ringN[i] = normal;
            ringB[i] = Vector3.Cross(tan[i], normal);
        }

        var cum = new float[n];
        for (int i = 1; i < n; i++) cum[i] = cum[i - 1] + Vector3.Distance(pts[i - 1], pts[i]);
        float total = cum[n - 1];

        int baseIndex = verts.Count;
        for (int i = 0; i < n; i++)
        {
            float t = total > 1e-6f ? cum[i] / total : 0f;
            for (int s = 0; s <= sides; s++)
            {
                float frac = s / (float)sides;
                float ang = frac * Mathf.PI * 2f;
                Vector3 dir = Mathf.Cos(ang) * ringN[i] + Mathf.Sin(ang) * ringB[i];
                float sc = radiusScale != null ? radiusScale(t, frac) : 1f;
                verts.Add(pts[i] - origin + dir * radius * sc);
                norms.Add(dir);
                uvs.Add(new Vector2(frac, (uvStartDist + cum[i]) / uvTile));
                colors.Add(color);
            }
        }

        for (int i = 0; i < n - 1; i++)
        {
            for (int s = 0; s < sides; s++)
            {
                int a = baseIndex + i * (sides + 1) + s;
                int b = a + sides + 1;
                tris.Add(a); tris.Add(a + 1); tris.Add(b);
                tris.Add(a + 1); tris.Add(b + 1); tris.Add(b);
            }
        }

        if (capStart) AddCap(pts[0], -tan[0], ringN[0], ringB[0], radius, sides, color, radiusScale, 0f, false);
        if (capEnd) AddCap(pts[n - 1], tan[n - 1], ringN[n - 1], ringB[n - 1], radius, sides, color, radiusScale, 1f, true);
    }

    public void AddStrand(Vector3 p0, Vector3 p1, Vector3 p2, float radius, Color color, int samples = 6)
    {
        var pts = new List<Vector3>();
        for (int i = 0; i <= samples; i++)
        {
            float t = i / (float)samples;
            float u = 1f - t;
            pts.Add(u * u * p0 + 2f * u * t * p1 + t * t * p2);
        }
        AddTube(pts, radius, 4, color, 1f, false, true, 0f, (t, a) => Mathf.Lerp(1f, 0.55f, t));
    }

    void AddCap(Vector3 center, Vector3 outward, Vector3 n, Vector3 b, float radius, int sides,
                Color color, System.Func<float, float, float> radiusScale, float tEnd, bool isEnd)
    {
        int c = verts.Count;
        verts.Add(center - origin);
        norms.Add(outward);
        uvs.Add(new Vector2(0.5f, 0.5f));
        colors.Add(color);

        for (int s = 0; s < sides; s++)
        {
            float frac = s / (float)sides;
            float ang = frac * Mathf.PI * 2f;
            Vector3 dir = Mathf.Cos(ang) * n + Mathf.Sin(ang) * b;
            float sc = radiusScale != null ? radiusScale(tEnd, frac) : 1f;
            verts.Add(center - origin + dir * radius * sc);
            norms.Add(outward);
            uvs.Add(new Vector2(0.5f + 0.5f * Mathf.Cos(ang), 0.5f + 0.5f * Mathf.Sin(ang)));
            colors.Add(color);
        }

        for (int s = 0; s < sides; s++)
        {
            int r0 = c + 1 + s;
            int r1 = c + 1 + (s + 1) % sides;
            if (isEnd) { tris.Add(c); tris.Add(r0); tris.Add(r1); }
            else       { tris.Add(c); tris.Add(r1); tris.Add(r0); }
        }
    }

    public Mesh ToMesh(string name)
    {
        var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetColors(colors);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }
}

public static class CableTubeBuilder
{

    public static GameObject Create(Transform parent, string name, IList<Vector3> points,
                                    float radius, int sides, Material material, Color color,
                                    bool castShadows = false, float uvTileLength = 5f,
                                    CableDamageSettings damage = null,
                                    CableBuoyancySettings buoyancy = null, float buoyancyCenter = -1f,
                                    int damagedModuleIndex = -1,
                                    CableProtectionSettings protection = null, float protectionCenter = -1f)
    {
        var line = new Polyline(points);
        if (line.pts.Count < 2) return null;

        sides = Mathf.Max(3, sides);
        Vector3 origin = line.pts[0];

        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.rotation = Quaternion.identity;
        root.transform.position = origin;
        Vector3 ls = parent != null ? parent.lossyScale : Vector3.one;
        root.transform.localScale = new Vector3(SafeInv(ls.x), SafeInv(ls.y), SafeInv(ls.z));

        Material cableMat = material != null ? material : MakeFallbackMaterial(color);

        List<DamageEvent> events = (damage != null && damage.enabled)
            ? CableDamage.Plan(damage, line.Length, name, origin)
            : new List<DamageEvent>();

        var jacket = new TubeMeshBuilder(origin);
        float cursor = 0f;
        foreach (var e in events)
        {
            if (e.type != CableDamageType.Break) continue;
            float gapStart = Mathf.Max(e.s - e.length * 0.5f, cursor);
            float gapEnd = e.s + e.length * 0.5f;
            if (gapStart - cursor > 0.05f)
                jacket.AddTube(line.SubPath(cursor, gapStart, float.MaxValue), radius, sides,
                               Color.white, uvTileLength, true, true, cursor);
            cursor = gapEnd;
        }
        if (line.Length - cursor > 0.05f)
            jacket.AddTube(line.SubPath(cursor, line.Length, float.MaxValue), radius, sides,
                           Color.white, uvTileLength, true, true, cursor);

        AddMeshChild(root.transform, "Jacket", jacket.ToMesh(name + "_Jacket"), cableMat, castShadows);

        if (events.Count > 0)
            CableDamage.Build(root.transform, origin, line, events, radius, damage, name, castShadows);

        if (buoyancy != null && buoyancy.enabled && buoyancyCenter >= 0f)
            CableBuoyancy.Build(root.transform, origin, line, buoyancy, buoyancyCenter, radius, castShadows,
                                damagedModuleIndex, name);

        if (protection != null && protection.enabled && protectionCenter >= 0f)
            CableProtection.Build(root.transform, origin, line, protection, protectionCenter, radius, castShadows);

        return root;
    }

    public static GameObject AddMeshChild(Transform parent, string name, Mesh mesh, Material mat, bool castShadows)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        mr.receiveShadows = true;
        return go;
    }

    static float SafeInv(float v) { return Mathf.Abs(v) > 1e-5f ? 1f / v : 1f; }

    static readonly Dictionary<string, Material> _stripedCache = new Dictionary<string, Material>();

    public static Material MakeStripedMaterial(Material source, Color baseColor, Color stripeColor,
                                               int twist, float stripeWidth)
    {
        stripeWidth = Mathf.Clamp(stripeWidth, 0.05f, 0.9f);
        twist = Mathf.Max(0, twist);

        string key = (source != null ? source.GetEntityId().ToString() : "none") + "|"
                   + ColorUtility.ToHtmlStringRGBA(baseColor) + "|"
                   + ColorUtility.ToHtmlStringRGBA(stripeColor) + "|"
                   + twist + "|" + stripeWidth.ToString("F3");

        Material cached;
        if (_stripedCache.TryGetValue(key, out cached) && cached != null) return cached;

        const int w = 128, h = 256;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
        var px = new Color[w * h];

        float half = stripeWidth * 0.5f;
        const float soft = 0.012f;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w;
                float v = (y + 0.5f) / h;

                float phase = Mathf.Repeat(u * twist + v, 1f);
                float d = Mathf.Abs(phase - 0.5f);

                float t = Mathf.Clamp01((d - (half - soft)) / (2f * soft));
                float k = 1f - t * t * (3f - 2f * t);

                px[y * w + x] = Color.Lerp(baseColor, stripeColor, k);
            }
        }

        tex.SetPixels(px);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Trilinear;
        tex.anisoLevel = 4;
        tex.Apply(true);

        Material mat = source != null ? new Material(source) : MakeFallbackMaterial(Color.white);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);

        _stripedCache[key] = mat;
        return mat;
    }

    public static Material MakeFallbackMaterial(Color color)
    {

        Shader shader = null;
        if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null)
            shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        var mat = new Material(shader);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.4f);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.4f);
        return mat;
    }
}