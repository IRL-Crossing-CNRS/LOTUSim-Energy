using System.Collections.Generic;
using UnityEngine;

public enum CableDamageType
{
    Break,
    Tear,
    Corrosion,
    BuoyancyModule
}

[System.Serializable]
public class CableDamageSettings
{
    public bool enabled = true;

    [Tooltip("0 = different damage every run. Any other number repeats the same damage when the cable layout is the same.")]
    public int seed = 0;

    [Header("How much damage")]
    [Range(0f, 1f)] public float damagedCableChance = 0.4f;
    [Tooltip("Min and max damage spots on a cable that has damage.")]
    public Vector2Int damagesPerCable = new Vector2Int(1, 2);
    [Tooltip("Relative likelihood of each type.")]
    public float breakWeight = 0.25f;
    public float tearWeight = 0.35f;
    public float corrosionWeight = 0.4f;

    [Header("Placement")]
    [Tooltip("Keep damage this far (m) from both ends of the cable.")]
    public float edgeMargin = 50f;
    [Tooltip("Minimum distance (m) between damage spots on one cable.")]
    public float minSpacing = 40f;

    [Header("Size")]
    [Tooltip("Length (m) of the missing section for a Break.")]
    public Vector2 breakGap = new Vector2(1.5f, 4f);
    [Tooltip("Length (m) of a Tear or Corrosion section.")]
    public Vector2 patchLength = new Vector2(2f, 6f);

    [Header("Wires")]
    public int strandsPerBreak = 24;
    [Tooltip("Wire thickness as a fraction of the cable radius. Raise it to make damage easier to see from far away.")]
    public float strandRadiusFraction = 0.07f;
    public float strandLengthScale = 1f;
    public Color[] strandColors =
    {
        new Color(1.0f, 0.5f, 0.1f), new Color(0.1f, 0.3f, 1.0f), new Color(0.9f, 0.9f, 0.9f),
        new Color(0.1f, 0.8f, 0.3f), new Color(0.9f, 0.1f, 0.1f), new Color(1.0f, 0.85f, 0.1f),
        new Color(0.4f, 0.25f, 0.1f)
    };

    [Header("Materials (optional)")]
    [Tooltip("Lit material for Tear/Corrosion sections. Leave empty for a generated rust/green/copper one.")]
    public Material patchMaterial;
    [Tooltip("Material for wires. Must read vertex colours. Leave empty for the default.")]
    public Material strandMaterial;

    [Header("Detection")]
    [Tooltip("Layer for the damage trigger colliders. Leave empty to keep the default layer.")]
    public string damageLayerName = "";
}

public struct DamageEvent
{
    public CableDamageType type;
    public float s;
    public float length;
    public float severity;
    public int rngSeed;
}

public class CableDamageMarker : MonoBehaviour
{
    public static readonly List<CableDamageMarker> All = new List<CableDamageMarker>();

    public CableDamageType type;
    [Range(0f, 1f)] public float severity;
    public string cableName;
    public float distanceAlongCable;
    public float length;
    public Vector3 localCenter;

    public Vector3 WorldPosition { get { return transform.TransformPoint(localCenter); } }

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }
}

public static class CableDamage
{

    static readonly System.Random Shared = new System.Random();

    public static List<DamageEvent> Plan(CableDamageSettings st, float cableLength, string cableName, Vector3 startPoint)
    {
        var list = new List<DamageEvent>();

        System.Random rng;
        if (st.seed != 0)
        {

            int h = StableHash(cableName);
            unchecked
            {
                h = h * 31 + Mathf.RoundToInt(startPoint.x * 10f);
                h = h * 31 + Mathf.RoundToInt(startPoint.y * 10f);
                h = h * 31 + Mathf.RoundToInt(startPoint.z * 10f);
                h = h * 31 + Mathf.RoundToInt(cableLength * 10f);
            }
            rng = new System.Random(Mix(st.seed ^ Mix(h)));
        }
        else
        {

            rng = new System.Random(Shared.Next());
        }

        for (int i = 0; i < 4; i++) rng.Next();

        if (rng.NextDouble() > st.damagedCableChance) return list;

        int count = rng.Next(st.damagesPerCable.x, Mathf.Max(st.damagesPerCable.x, st.damagesPerCable.y) + 1);
        float lo = st.edgeMargin;
        float hi = cableLength - st.edgeMargin;
        if (hi - lo < 1f || count <= 0) return list;

        float wB = Mathf.Max(0f, st.breakWeight);
        float wT = Mathf.Max(0f, st.tearWeight);
        float wC = Mathf.Max(0f, st.corrosionWeight);
        float sum = wB + wT + wC;
        if (sum <= 0f) { wC = 1f; sum = 1f; }

        int attempts = 0;
        while (list.Count < count && attempts++ < 50)
        {
            float s = Mathf.Lerp(lo, hi, (float)rng.NextDouble());

            bool ok = true;
            foreach (var e in list)
                if (Mathf.Abs(e.s - s) < st.minSpacing) { ok = false; break; }
            if (!ok) continue;

            float pick = (float)rng.NextDouble() * sum;
            CableDamageType type = pick < wB ? CableDamageType.Break
                                 : pick < wB + wT ? CableDamageType.Tear
                                 : CableDamageType.Corrosion;

            float length = type == CableDamageType.Break
                ? Rand(rng, st.breakGap.x, st.breakGap.y)
                : Rand(rng, st.patchLength.x, st.patchLength.y);

            list.Add(new DamageEvent
            {
                type = type,
                s = s,
                length = length,
                severity = (float)rng.NextDouble(),
                rngSeed = rng.Next()
            });
        }

        list.Sort((a, b) => a.s.CompareTo(b.s));
        return list;
    }

    public static void Build(Transform root, Vector3 origin, Polyline line, List<DamageEvent> events,
                             float radius, CableDamageSettings st, string cableName, bool castShadows)
    {
        Color[] palette = (st.strandColors != null && st.strandColors.Length > 0) ? st.strandColors : DefaultPalette();
        Material patchMat = st.patchMaterial != null ? st.patchMaterial : GetPatchMaterial();
        Material strandMat = st.strandMaterial != null ? st.strandMaterial : GetStrandMaterial();
        int layer = string.IsNullOrEmpty(st.damageLayerName) ? -1 : LayerMask.NameToLayer(st.damageLayerName);
        float wireRadius = Mathf.Max(0.004f, radius * st.strandRadiusFraction);

        for (int i = 0; i < events.Count; i++)
        {
            DamageEvent e = events[i];
            var rng = new System.Random(e.rngSeed);

            var go = new GameObject("Damage_" + e.type + "_" + (i + 1));
            go.transform.SetParent(root, false);
            if (layer >= 0) go.layer = layer;

            var strands = new TubeMeshBuilder(origin);
            Vector3 center = line.PointAt(e.s);

            if (e.type == CableDamageType.Break)
            {
                float gapStart = e.s - e.length * 0.5f;
                float gapEnd = e.s + e.length * 0.5f;
                AddFrayedEnd(strands, line.PointAt(gapStart), line.TangentAt(gapStart), radius, wireRadius, st, palette, rng, e.severity);
                AddFrayedEnd(strands, line.PointAt(gapEnd), -line.TangentAt(gapEnd), radius, wireRadius, st, palette, rng, e.severity);
            }
            else
            {
                BuildPatch(go.transform, origin, line, e, radius, patchMat, castShadows, rng);
                if (e.type == CableDamageType.Tear)
                    AddTearStrands(strands, line, e, radius, wireRadius, st, palette, rng);
            }

            if (strands.HasGeometry)
                CableTubeBuilder.AddMeshChild(go.transform, "Wires", strands.ToMesh(go.name + "_Wires"), strandMat, false);

            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.center = center - origin;
            col.radius = Mathf.Max(e.length * 0.5f, radius * 3f);

            var marker = go.AddComponent<CableDamageMarker>();
            marker.type = e.type;
            marker.severity = e.severity;
            marker.cableName = cableName;
            marker.distanceAlongCable = e.s;
            marker.length = e.length;
            marker.localCenter = center - origin;
        }
    }

    static void AddFrayedEnd(TubeMeshBuilder sb, Vector3 center, Vector3 outward, float radius, float wireRadius,
                             CableDamageSettings st, Color[] palette, System.Random rng, float severity)
    {
        Vector3 n, b;
        Frame(outward, out n, out b);

        int count = Mathf.Max(3, Mathf.RoundToInt(Mathf.Lerp(st.strandsPerBreak * 0.5f, st.strandsPerBreak, severity)));
        for (int i = 0; i < count; i++)
        {
            float rr = radius * 0.7f * Mathf.Sqrt((float)rng.NextDouble());
            float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
            Vector3 radial = Mathf.Cos(ang) * n + Mathf.Sin(ang) * b;

            Vector3 p0 = center - outward * radius * 0.25f + radial * rr;
            float spread = 0.2f + 0.9f * (rr / (radius * 0.7f)) * (float)rng.NextDouble();
            Vector3 dir = (outward + radial * spread).normalized;
            float len = radius * Rand(rng, 3f, 7f) * st.strandLengthScale;

            AddCurvedStrand(sb, p0, dir, len, wireRadius, palette[rng.Next(palette.Length)], rng);
        }
    }

    static void AddTearStrands(TubeMeshBuilder sb, Polyline line, DamageEvent e, float radius, float wireRadius,
                               CableDamageSettings st, Color[] palette, System.Random rng)
    {
        float s0 = e.s - e.length * 0.5f;
        float tearAngle = (float)rng.NextDouble() * Mathf.PI * 2f;
        int count = Mathf.Max(3, Mathf.RoundToInt(Mathf.Lerp(st.strandsPerBreak * 0.3f, st.strandsPerBreak * 0.8f, e.severity)));

        for (int i = 0; i < count; i++)
        {
            float s = s0 + e.length * (0.25f + 0.5f * (float)rng.NextDouble());
            Vector3 pos = line.PointAt(s);
            Vector3 tg = line.TangentAt(s);
            Vector3 n, b;
            Frame(tg, out n, out b);

            float ang = tearAngle + ((float)rng.NextDouble() - 0.5f) * 0.9f;
            Vector3 radial = Mathf.Cos(ang) * n + Mathf.Sin(ang) * b;

            Vector3 p0 = pos + radial * radius * 0.8f;
            Vector3 dir = (radial + tg * (((float)rng.NextDouble() - 0.5f) * 1.2f)).normalized;
            float len = radius * Rand(rng, 2.5f, 6f) * st.strandLengthScale;

            AddCurvedStrand(sb, p0, dir, len, wireRadius, palette[rng.Next(palette.Length)], rng);
        }
    }

    static void AddCurvedStrand(TubeMeshBuilder sb, Vector3 p0, Vector3 dir, float len, float wireRadius,
                                Color color, System.Random rng)
    {
        Vector3 perp = Vector3.Cross(dir, RandomUnit(rng));
        perp = perp.sqrMagnitude > 1e-6f ? perp.normalized : Vector3.up;

        Vector3 p1 = p0 + dir * len * 0.5f + perp * len * Rand(rng, -0.15f, 0.15f);
        Vector3 p2 = p0 + dir * len + perp * len * Rand(rng, -0.4f, 0.4f);
        sb.AddStrand(p0, p1, p2, wireRadius, color);
    }

    static void BuildPatch(Transform parent, Vector3 origin, Polyline line, DamageEvent e, float radius,
                           Material mat, bool castShadows, System.Random rng)
    {
        float s0 = e.s - e.length * 0.5f;
        float s1 = e.s + e.length * 0.5f;
        List<Vector3> sub = line.SubPath(s0, s1, Mathf.Max(0.05f, radius * 0.5f));
        if (sub.Count < 3) return;

        float seedX = (float)rng.NextDouble() * 100f;
        float seedY = (float)rng.NextDouble() * 100f;
        float bulge = Mathf.Lerp(0.08f, 0.35f, e.severity);

        var pb = new TubeMeshBuilder(origin);
        pb.AddTube(sub, radius, 16, Color.white, e.length, false, false, 0f,
            (along, around) =>
            {
                float ang = around * Mathf.PI * 2f;
                float noise = Mathf.PerlinNoise(seedX + along * 4f + Mathf.Cos(ang) * 1.2f,
                                                seedY + Mathf.Sin(ang) * 1.2f);
                float edge = Mathf.Sin(along * Mathf.PI);
                return 1f + edge * (0.04f + bulge * noise);
            });

        CableTubeBuilder.AddMeshChild(parent, "Patch", pb.ToMesh(parent.name + "_Patch"), mat, castShadows);
    }

    static Material _patchMat;
    static Material _strandMat;
    static Texture2D _corrosionTex;

    static Material GetStrandMaterial()
    {
        if (_strandMat == null)
        {
            Shader sh = Shader.Find("Sprites/Default");
            _strandMat = new Material(sh);
        }
        return _strandMat;
    }

    static Material GetPatchMaterial()
    {
        if (_patchMat == null)
        {
            _patchMat = CableTubeBuilder.MakeFallbackMaterial(Color.white);
            Texture2D tex = GetCorrosionTexture();
            if (_patchMat.HasProperty("_BaseMap")) _patchMat.SetTexture("_BaseMap", tex);
            if (_patchMat.HasProperty("_MainTex")) _patchMat.SetTexture("_MainTex", tex);
            if (_patchMat.HasProperty("_Smoothness")) _patchMat.SetFloat("_Smoothness", 0.15f);
            if (_patchMat.HasProperty("_Glossiness")) _patchMat.SetFloat("_Glossiness", 0.15f);
        }
        return _patchMat;
    }

    static Texture2D GetCorrosionTexture()
    {
        if (_corrosionTex != null) return _corrosionTex;

        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        var px = new Color[size * size];

        Color dark = new Color(0.10f, 0.07f, 0.05f);
        Color rust = new Color(0.40f, 0.18f, 0.08f);
        Color green = new Color(0.15f, 0.50f, 0.42f);
        Color copper = new Color(0.78f, 0.46f, 0.22f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size;
                float v = y / (float)size;
                float a = u * Mathf.PI * 2f;
                float cx = Mathf.Cos(a) * 1.5f;
                float cy = Mathf.Sin(a) * 1.5f;

                float n1 = Mathf.PerlinNoise(cx + 10f, cy + v * 3f + 10f);
                float n2 = Mathf.PerlinNoise(cx * 2.3f + 40f, cy * 2.3f + v * 7f + 40f);
                float n3 = Mathf.PerlinNoise(cx * 3.1f + 80f, cy * 3.1f + v * 9f + 80f);

                Color c = Color.Lerp(dark, rust, n1);
                c = Color.Lerp(c, green, Smooth(0.55f, 0.80f, n2));
                c = Color.Lerp(c, copper, Smooth(0.72f, 0.88f, n3));
                px[y * size + x] = c;
            }
        }

        tex.SetPixels(px);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.Apply();
        _corrosionTex = tex;
        return tex;
    }

    static Color[] DefaultPalette()
    {
        return new[]
        {
            new Color(1.0f, 0.5f, 0.1f), new Color(0.1f, 0.3f, 1.0f), new Color(0.9f, 0.9f, 0.9f),
            new Color(0.1f, 0.8f, 0.3f), new Color(0.9f, 0.1f, 0.1f)
        };
    }

    static void Frame(Vector3 t, out Vector3 n, out Vector3 b)
    {
        n = Vector3.Cross(t, Mathf.Abs(Vector3.Dot(t, Vector3.up)) > 0.99f ? Vector3.right : Vector3.up).normalized;
        b = Vector3.Cross(t, n);
    }

    static float Smooth(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    static float Rand(System.Random rng, float a, float b)
    {
        return a + (float)rng.NextDouble() * (b - a);
    }

    static Vector3 RandomUnit(System.Random rng)
    {
        var v = new Vector3(Rand(rng, -1f, 1f), Rand(rng, -1f, 1f), Rand(rng, -1f, 1f));
        return v.sqrMagnitude < 1e-4f ? Vector3.up : v.normalized;
    }

    static int Mix(int x)
    {
        unchecked
        {
            uint h = (uint)x;
            h ^= h >> 16;
            h *= 0x7feb352du;
            h ^= h >> 15;
            h *= 0x846ca68bu;
            h ^= h >> 16;
            return (int)h;
        }
    }

    static int StableHash(string s)
    {
        unchecked
        {
            int h = 23;
            foreach (char c in s) h = h * 31 + c;
            return h;
        }
    }
}