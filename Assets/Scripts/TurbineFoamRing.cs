using UnityEngine;

public class TurbineFoamRing : MonoBehaviour
{
    public float waterY = 1f;
    public float ringRadius = 2f;
    public int particleCount = 50;
    public float foamSize = 0.8f;
    public float foamLifetime = 2f;
    public float spreadSpeed = 0.3f;

    private ParticleSystem foamParticles;

    Texture2D CreateCircleTexture(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 centre = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f;

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), centre);
                float alpha = Mathf.Clamp01(1f - (dist / radius));
                alpha = Mathf.Pow(alpha, 1.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();
        return tex;
    }

    void Start()
    {
        GameObject foamObject = new GameObject("FoamRing");
        foamObject.transform.SetParent(transform);
        foamObject.transform.position = new Vector3(transform.position.x, waterY, transform.position.z);

        foamParticles = foamObject.AddComponent<ParticleSystem>();
        foamParticles.Stop();

        var main = foamParticles.main;
        main.loop = true;
        main.startLifetime = foamLifetime;
        main.startSpeed = spreadSpeed;
        main.startSize = foamSize;
        main.startColor = new Color(1f, 1f, 1f, 0.85f);
        main.maxParticles = particleCount * 2;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = foamParticles.emission;
        emission.rateOverTime = particleCount;

        var shape = foamParticles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = ringRadius;
        shape.radiusThickness = 0.1f;
        shape.rotation = new Vector3(90f, 0f, 0f);

        var colOverLife = foamParticles.colorOverLifetime;
        colOverLife.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.85f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colOverLife.color = gradient;

        var sizeOverLife = foamParticles.sizeOverLifetime;
        sizeOverLife.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0.5f);
        sizeCurve.AddKey(0.3f, 1f);
        sizeCurve.AddKey(1f, 0.2f);
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        Texture2D circleTexture = CreateCircleTexture(64);

        var renderer = foamParticles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        Material foamMat = new Material(Shader.Find("Unlit/Transparent"));
        foamMat.mainTexture = circleTexture;
        foamMat.color = Color.white;
        renderer.material = foamMat;

        foamParticles.Play();
    }
}
