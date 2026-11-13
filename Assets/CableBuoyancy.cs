using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class CableBuoyancySettings
{
    public bool enabled = true;

    [Tooltip("Number of float modules along the hump.")]
    [Min(1)] public int moduleCount = 8;
    [Tooltip("Length (m) of each module.")]
    public float moduleLength = 2.5f;
    [Tooltip("Bare cable (m) left between modules.")]
    public float moduleGap = 1.2f;
    [Tooltip("Module radius as a multiple of the cable radius.")]
    public float radiusMultiplier = 2.6f;
    [Tooltip("Fraction of each module's length used for its tapered ends.")]
    [Range(0.05f, 0.5f)] public float taper = 0.2f;

    [Header("Look")]
    public Color color = new Color(0.95f, 0.8f, 0.1f);
    [Tooltip("Optional. Leave empty for a plain lit yellow material. Damaged modules use this same material.")]
    public Material material;

    [Header("Cracks on the damaged module")]
    [Tooltip("Your crack DECAL materials (URP Decal shader). One is picked at random per crack and projected onto the module. Needs URP with the Decal renderer feature enabled.")]
    public List<Material> crackDecalMaterials = new List<Material>();
    [Tooltip("Alternative: your own prefabs (e.g. a Decal Projector already set up, a crack quad, a mesh). One is picked at random per crack. +Z points into the module surface.")]
    public List<GameObject> cutPrefabs = new List<GameObject>();
    [Tooltip("Min and max number of cracks on each damaged module.")]
    public Vector2Int cutsPerModule = new Vector2Int(1, 3);
    [Tooltip("Random scale multiplier for each crack.")]
    public Vector2 cutScale = new Vector2(0.8f, 1.5f);
    [Tooltip("Size of a generated decal (width, height, projection depth) before scaling, in metres.")]
    public Vector3 decalSize = new Vector3(1.2f, 1.2f, 1.0f);
    [Tooltip("Spin each crack by a random angle so they don't all look the same.")]
    public bool randomRoll = true;
    [Tooltip("Extra rotation (Euler angles) for prefabs, e.g. to fix an imported model that faces the wrong way.")]
    public Vector3 cutRotationOffset = Vector3.zero;
    [Tooltip("Layer for the detection collider on a damaged module. Leave empty for the default layer.")]
    public string damagedLayerName = "";
}

public static class CableBuoyancy
{

    public static System.Action<GameObject> OnDamagedModuleSpawned;

    static readonly Dictionary<string, Material> _matCache = new Dictionary<string, Material>();
    static readonly System.Random Rng = new System.Random();

    static System.Type _decalType;
    static bool _decalTypeSearched;
    static bool _warnedNoDecal;

    public static void Build(Transform root, Vector3 origin, Polyline line, CableBuoyancySettings st,
                             float centerS, float cableRadius, bool castShadows,
                             int damagedIndex, string cableName)
    {
        int n = Mathf.Max(1, st.moduleCount);
        float len = Mathf.Max(0.2f, st.moduleLength);
        float gap = Mathf.Max(0f, st.moduleGap);
        float span = n * len + (n - 1) * gap;
        float s0 = centerS - span * 0.5f;

        float mult = Mathf.Max(1.1f, st.radiusMultiplier);
        float moduleRadius = cableRadius * mult;
        float endScale = 0.9f / mult;
        float taperFrac = Mathf.Clamp(st.taper, 0.05f, 0.5f);
        float uvTile = 2f * Mathf.PI * moduleRadius;

        Material mat = st.material != null ? st.material : GetMaterial(st.color);

        var intact = new TubeMeshBuilder(origin);
        for (int i = 0; i < n; i++)
        {
            float a = s0 + i * (len + gap);
            float b = a + len;
            if (a < 0.5f || b > line.Length - 0.5f) continue;

            List<Vector3> sub = line.SubPath(a, b, len / 16f);

            if (i == damagedIndex)
            {
                BuildDamaged(root, origin, line, st, sub, a, b, i, moduleRadius, uvTile,
                             endScale, taperFrac, mat, castShadows, cableName);
                continue;
            }

            intact.AddTube(sub, moduleRadius, 14, Color.white, uvTile, false, false, 0f,
                           (along, around) => TaperScale(along, taperFrac, endScale));
        }

        if (intact.HasGeometry)
            CableTubeBuilder.AddMeshChild(root, "BuoyancyModules", intact.ToMesh(root.name + "_Buoyancy"), mat, castShadows);
    }

    static void BuildDamaged(Transform root, Vector3 origin, Polyline line, CableBuoyancySettings st,
                             List<Vector3> sub, float a, float b, int index, float moduleRadius, float uvTile,
                             float endScale, float taperFrac, Material mat, bool castShadows, string cableName)
    {
        var db = new TubeMeshBuilder(origin);
        db.AddTube(sub, moduleRadius, 14, Color.white, uvTile, false, false, 0f,
                   (along, around) => TaperScale(along, taperFrac, endScale));

        GameObject go = CableTubeBuilder.AddMeshChild(root, "DamagedBuoyancyModule_" + (index + 1),
                                                      db.ToMesh(root.name + "_DamagedModule"), mat, castShadows);

        if (!string.IsNullOrEmpty(st.damagedLayerName))
        {
            int layer = LayerMask.NameToLayer(st.damagedLayerName);
            if (layer >= 0) go.layer = layer;
        }

        float mid = (a + b) * 0.5f;
        Vector3 center = line.PointAt(mid);

        var col = go.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.center = center - origin;
        col.radius = Mathf.Max((b - a) * 0.5f, moduleRadius * 1.5f);

        var marker = go.AddComponent<CableDamageMarker>();
        marker.type = CableDamageType.BuoyancyModule;
        marker.severity = 1f;
        marker.cableName = cableName;
        marker.distanceAlongCable = mid;
        marker.length = b - a;
        marker.localCenter = center - origin;

        SpawnCracks(go.transform, line, st, a, b, moduleRadius);

        if (OnDamagedModuleSpawned != null) OnDamagedModuleSpawned(go);
    }

    static void SpawnCracks(Transform parent, Polyline line, CableBuoyancySettings st,
                            float a, float b, float moduleRadius)
    {
        var prefabs = new List<GameObject>();
        if (st.cutPrefabs != null)
            foreach (var p in st.cutPrefabs) if (p != null) prefabs.Add(p);

        var decals = new List<Material>();
        if (st.crackDecalMaterials != null)
            foreach (var m in st.crackDecalMaterials) if (m != null) decals.Add(m);

        if (prefabs.Count == 0 && decals.Count == 0) return;

        int cuts = Rng.Next(st.cutsPerModule.x, Mathf.Max(st.cutsPerModule.x, st.cutsPerModule.y) + 1);
        float len = b - a;

        for (int k = 0; k < cuts; k++)
        {

            float s = Mathf.Lerp(a + len * 0.25f, a + len * 0.75f, (float)Rng.NextDouble());
            Vector3 pos = line.PointAt(s);
            Vector3 tangent = line.TangentAt(s);

            Vector3 n, bin;
            Frame(tangent, out n, out bin);
            float ang = (float)Rng.NextDouble() * Mathf.PI * 2f;
            Vector3 outward = Mathf.Cos(ang) * n + Mathf.Sin(ang) * bin;
            Vector3 surface = pos + outward * moduleRadius;

            Quaternion rot = Quaternion.LookRotation(-outward, tangent);
            if (st.randomRoll) rot *= Quaternion.Euler(0f, 0f, (float)Rng.NextDouble() * 360f);
            float scale = Mathf.Lerp(st.cutScale.x, st.cutScale.y, (float)Rng.NextDouble());

            bool usePrefab = prefabs.Count > 0 && (decals.Count == 0 || Rng.NextDouble() < 0.5);
            if (usePrefab)
            {
                GameObject prefab = prefabs[Rng.Next(prefabs.Count)];
                GameObject inst = UnityEngine.Object.Instantiate(prefab, surface,
                                      rot * Quaternion.Euler(st.cutRotationOffset), parent);
                inst.transform.localScale = prefab.transform.localScale * scale;
            }
            else
            {
                Material decal = decals[Rng.Next(decals.Count)];
                CreateDecalProjector(parent, surface + outward * 0.15f, rot, decal, st.decalSize * scale);
            }
        }
    }

    static void CreateDecalProjector(Transform parent, Vector3 pos, Quaternion rot, Material material, Vector3 size)
    {
        System.Type type = FindDecalType();
        if (type == null)
        {
            if (!_warnedNoDecal)
            {
                _warnedNoDecal = true;
                Debug.LogWarning("CableBuoyancy: Crack Decal Materials need URP's Decal Projector, which wasn't found. "
                    + "Install/enable URP, or use Cut Prefabs instead.");
            }
            return;
        }

        var go = new GameObject("CrackDecal");
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.rotation = rot;

        Component projector = go.AddComponent(type);
        SetProperty(type, projector, "material", material);
        SetProperty(type, projector, "size", size);
        SetProperty(type, projector, "pivot", new Vector3(0f, 0f, size.z * 0.5f));
    }

    static System.Type FindDecalType()
    {
        if (_decalTypeSearched) return _decalType;
        _decalTypeSearched = true;

        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            System.Type t = asm.GetType("UnityEngine.Rendering.Universal.DecalProjector");
            if (t != null) { _decalType = t; break; }
        }
        return _decalType;
    }

    static void SetProperty(System.Type type, object target, string name, object value)
    {
        System.Reflection.PropertyInfo p = type.GetProperty(name);
        if (p != null && p.CanWrite) p.SetValue(target, value, null);
    }

    static float TaperScale(float along, float taperFrac, float endScale)
    {
        float e = Mathf.Clamp01(Mathf.Min(along, 1f - along) / taperFrac);
        float smooth = e * e * (3f - 2f * e);
        return Mathf.Lerp(endScale, 1f, smooth);
    }

    static void Frame(Vector3 t, out Vector3 n, out Vector3 b)
    {
        n = Vector3.Cross(t, Mathf.Abs(Vector3.Dot(t, Vector3.up)) > 0.99f ? Vector3.right : Vector3.up).normalized;
        b = Vector3.Cross(t, n);
    }

    static Material GetMaterial(Color color)
    {
        string key = ColorUtility.ToHtmlStringRGBA(color);
        Material m;
        if (_matCache.TryGetValue(key, out m) && m != null) return m;

        m = CableTubeBuilder.MakeFallbackMaterial(color);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.5f);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.5f);
        _matCache[key] = m;
        return m;
    }
}