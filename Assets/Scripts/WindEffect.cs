using UnityEngine;

namespace Lotusim
{
    public class WindEffect : MonoBehaviour
    {
        public WindSliderController windController;

        public float spawnRadius = 30f;
        public float heightMin = 1f;
        public float heightMax = 20f;

        public float maxWindSpeed = 20f;
        public int particleCountMin = 10;
        public int particleCountMax = 80;
        public float particleSpeedMin = 5f;
        public float particleSpeedMax = 15f;
        public float noiseStrengthMin = 0.2f;
        public float noiseStrengthMax = 1.2f;
        public float noiseFrequencyMin = 0.2f;
        public float noiseFrequencyMax = 0.8f;

        public float particleLifetime = 8f;
        public float particleSize = 0.3f;
        public float trailWidth = 0.15f;
        public float trailLifetime = 0.3f;
        public Color windColor = Color.white;

        private ParticleSystem windParticles;
        private ParticleSystem.EmissionModule emission;
        private ParticleSystem.MainModule main;
        private ParticleSystem.NoiseModule noise;
        private ParticleSystem.VelocityOverLifetimeModule velocityOverLifetime;

        Texture2D CreateSwooshTexture()
        {
            int w = 128, h = 16;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    float xNorm = x / (float)w;
                    float yNorm = Mathf.Abs(y - h / 2f) / (h / 2f);
                    float alpha = (1f - xNorm) * (1f - yNorm);
                    alpha = Mathf.Pow(alpha, 0.8f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            return tex;
        }

        Material CreateTransparentMaterial(Texture2D tex)
        {
            Material mat = new Material(Shader.Find("HDRP/Unlit"));
            mat.SetFloat("_SurfaceType", 1);
            mat.SetFloat("_BlendMode", 0);
            mat.SetFloat("_AlphaCutoffEnable", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.mainTexture = tex;
            mat.color = Color.white;
            return mat;
        }

        void Start()
        {
            windParticles = gameObject.AddComponent<ParticleSystem>();
            windParticles.Stop();

            main = windParticles.main;
            main.loop = true;
            main.startLifetime = particleLifetime;
            main.startSpeed = particleSpeedMin;
            main.startSize = particleSize;
            main.startColor = windColor;
            main.maxParticles = particleCountMax * 2;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;

            emission = windParticles.emission;
            emission.rateOverTime = particleCountMin;

            var shape = windParticles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(spawnRadius * 2f, heightMax - heightMin, spawnRadius * 2f);
            shape.position = new Vector3(0f, (heightMin + heightMax) / 2f, 0f);

            velocityOverLifetime = windParticles.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
            velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(0f);
            velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(0f);
            velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(0f);

            noise = windParticles.noise;
            noise.enabled = true;
            noise.strength = noiseStrengthMin;
            noise.frequency = noiseFrequencyMin;
            noise.scrollSpeed = 0.15f;
            noise.damping = true;
            noise.octaveCount = 2;
            noise.octaveMultiplier = 0.5f;
            noise.octaveScale = 2f;

            var colOverLife = windParticles.colorOverLifetime;
            colOverLife.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0f,   0f),
                    new GradientAlphaKey(0.9f, 0.15f),
                    new GradientAlphaKey(0.9f, 0.4f),
                    new GradientAlphaKey(0f,   1f)
                }
            );
            colOverLife.color = gradient;

            var trails = windParticles.trails;
            trails.enabled = true;
            trails.mode = ParticleSystemTrailMode.PerParticle;
            trails.lifetime = new ParticleSystem.MinMaxCurve(trailLifetime);
            trails.minVertexDistance = 0.05f;
            trails.worldSpace = false;
            trails.dieWithParticles = false;
            trails.widthOverTrail = new ParticleSystem.MinMaxCurve(trailWidth);

            Gradient trailGradient = new Gradient();
            trailGradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0f,   0f),
                    new GradientAlphaKey(0.6f, 0.15f),
                    new GradientAlphaKey(0.6f, 0.4f),
                    new GradientAlphaKey(0f,   1f)
                }
            );
            trails.colorOverLifetime = trailGradient;

            Texture2D swooshTex = CreateSwooshTexture();

            var renderer = windParticles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.velocityScale = 0f;
            renderer.lengthScale = 1f;
            renderer.material = CreateTransparentMaterial(swooshTex);
            renderer.trailMaterial = CreateTransparentMaterial(swooshTex);

            windParticles.Play();
        }

        void Update()
        {
            if (windController == null) return;

            Vector3 windVec = windController.CurrentWindVector;
            float magnitude = new Vector3(windVec.x, 0f, windVec.z).magnitude;
            float t = Mathf.Clamp01(magnitude / maxWindSpeed);

            Vector3 windDir = magnitude > 0.01f
                ? new Vector3(windVec.x, 0f, windVec.z).normalized
                : Vector3.forward;

            float speed = Mathf.Lerp(particleSpeedMin, particleSpeedMax, t);

            velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(windDir.x * speed);
            velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(0f);
            velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(windDir.z * speed);

            emission.rateOverTime = Mathf.Lerp(particleCountMin, particleCountMax, t);
            noise.strength = Mathf.Lerp(noiseStrengthMin, noiseStrengthMax, t);
            noise.frequency = Mathf.Lerp(noiseFrequencyMin, noiseFrequencyMax, t);
            main.startSpeed = speed;
        }
    }
}