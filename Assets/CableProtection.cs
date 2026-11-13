using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class CableProtectionSettings
{
    public bool enabled = true;

    [Tooltip("Length (m) of sleeve on the curved part of the cable, before it reaches the seabed.")]
    public float lengthBefore = 15f;
    [Tooltip("Length (m) of sleeve past the touchdown point, lying on the seabed.")]
    public float lengthAfter = 18f;
    [Tooltip("Sleeve radius as a multiple of the cable radius.")]
    public float radiusMultiplier = 1.4f;
    [Tooltip("Length (m) over which each end narrows back down to the cable.")]
    public float endTaper = 1.5f;

    public Color color = new Color(0.9f, 0.35f, 0.1f);
    public Material material;
}

public static class CableProtection
{
    static readonly Dictionary<string, Material> _matCache = new Dictionary<string, Material>();

    public static void Build(Transform root, Vector3 origin, Polyline line, CableProtectionSettings st,
                             float touchdownS, float cableRadius, bool castShadows)
    {
        float s0 = Mathf.Max(0.5f, touchdownS - Mathf.Max(0f, st.lengthBefore));
        float s1 = Mathf.Min(line.Length - 0.5f, touchdownS + Mathf.Max(0f, st.lengthAfter));
        float length = s1 - s0;
        if (length < 1f) return;

        float mult = Mathf.Max(1.05f, st.radiusMultiplier);
        float radius = cableRadius * mult;
        float endScale = 0.9f / mult;
        float taperLength = Mathf.Clamp(st.endTaper, 0.1f, length * 0.5f);

        List<Vector3> sub = line.SubPath(s0, s1, 0.5f);

        var mb = new TubeMeshBuilder(origin);
        mb.AddTube(sub, radius, 14, Color.white, 2f * Mathf.PI * radius, false, false, 0f,
            (along, around) =>
            {
                float fromEnd = Mathf.Min(along, 1f - along) * length;
                float e = Mathf.Clamp01(fromEnd / taperLength);
                float smooth = e * e * (3f - 2f * e);
                return Mathf.Lerp(endScale, 1f, smooth);
            });

        if (!mb.HasGeometry) return;

        Material mat = st.material != null ? st.material : GetMaterial(st.color);
        CableTubeBuilder.AddMeshChild(root, "TouchdownProtection", mb.ToMesh(root.name + "_Protection"), mat, castShadows);
    }

    static Material GetMaterial(Color color)
    {
        string key = ColorUtility.ToHtmlStringRGBA(color);
        Material m;
        if (_matCache.TryGetValue(key, out m) && m != null) return m;

        m = CableTubeBuilder.MakeFallbackMaterial(color);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.3f);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.3f);
        _matCache[key] = m;
        return m;
    }
}
