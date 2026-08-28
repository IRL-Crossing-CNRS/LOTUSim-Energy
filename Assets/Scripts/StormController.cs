using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Lotusim
{
    public class StormController : MonoBehaviour
    {
        [Header("References")]
        public Volume skyAndFogVolume;
        public GameObject oceanObject;
        public Transform playerCamera;

        [Header("Storm Trigger")]
        [Range(0f, 1f)]
        public float stormIntensity = 0f;
        public float transitionSpeed = 0.5f;
        public KeyCode stormKey = KeyCode.F1;

        [Header("Fog Settings")]
        public float calmFogAttenuation = 400f;
        public float stormFogAttenuation = 60f;
        public float calmFogDistance = 5000f;
        public float stormFogDistance = 600f;

        [Header("Cloud Settings")]
        public float calmCloudOpacity = 0.61f;
        public float stormCloudOpacity = 1f;
        public Color calmCloudTint = Color.white;
        public Color stormCloudTint = new Color(0.3f, 0.3f, 0.35f);

        [Header("Sky Darkening")]
        public float calmExposureCompensation = 0f;
        public float stormExposureCompensation = -2.5f;
        public float calmAerosolDensity = 0.101f;
        public float stormAerosolDensity = 1f;

        [Header("Rain Settings")]
        public int stormRainEmission = 3000;
        public float stormRainSpeed = 25f;
        public float rainSpawnRadius = 80f;
        public float rainHeightAboveCamera = 30f;
        public float waterSurfaceY = 1f;

        [Header("Ocean Settings")]
        public float calmLargeWindSpeed = 0f;
        public float stormLargeWindSpeed = 60f;
        public float calmRipplesWindSpeed = 9.28f;
        public float stormRipplesWindSpeed = 40f;
        public float calmLargeChaos = 0f;
        public float stormLargeChaos = 1f;
        public float calmRipplesChaos = 0.8f;
        public float stormRipplesChaos = 1f;
        public float calmFoamAmount = 0.2f;
        public float stormFoamAmount = 0.9f;

        private bool stormActive = false;
        private float targetIntensity = 0f;
        private float lastIntensity = -1f;

        private Fog fog;
        private CloudLayer cloudLayer;
        private PhysicallyBasedSky pbrSky;
        private Exposure exposure;
        private WaterSurface waterSurface;

        private GameObject rainObject;
        private ParticleSystem rainPS;
        private ParticleSystem.EmissionModule rainEmission;
        private ParticleSystem.MainModule rainMain;

        void Start()
        {
            if (playerCamera == null && Camera.main != null)
                playerCamera = Camera.main.transform;

            if (skyAndFogVolume != null)
            {
                var profile = skyAndFogVolume.profile;
                profile.TryGet(out fog);
                profile.TryGet(out cloudLayer);
                profile.TryGet(out pbrSky);
                profile.TryGet(out exposure);
            }

            if (oceanObject != null)
                waterSurface = oceanObject.GetComponent<WaterSurface>();

            BuildRain();
        }

        void BuildRain()
        {
            rainObject = new GameObject("StormRain");
            rainObject.transform.SetParent(transform);

            rainPS = rainObject.AddComponent<ParticleSystem>();
            rainPS.Stop();

            rainMain = rainPS.main;
            rainMain.loop = true;
            rainMain.prewarm = true;
            rainMain.startLifetime = 3f;
            rainMain.startSpeed = 0f;
            rainMain.startSize3D = true;
            rainMain.startSizeX = 0.03f;
            rainMain.startSizeY = 1.2f;
            rainMain.startSizeZ = 0.03f;
            rainMain.startColor = new Color(0.6f, 0.75f, 1f, 0.4f);
            rainMain.maxParticles = 15000;
            rainMain.gravityModifier = 0f;
            rainMain.simulationSpace = ParticleSystemSimulationSpace.World;

            rainEmission = rainPS.emission;
            rainEmission.rateOverTime = 0;

            var shape = rainPS.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(rainSpawnRadius * 2f, 1f, rainSpawnRadius * 2f);

            var velocityOverLifetime = rainPS.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
            velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(0f);
            velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(-stormRainSpeed);
            velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(0f);

            var renderer = rainPS.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0f;
            renderer.lengthScale = 4f;
            renderer.material = new Material(Shader.Find("HDRP/Unlit"));
            renderer.material.SetColor("_UnlitColor", new Color(0.6f, 0.75f, 1f, 0.4f));

            rainObject.SetActive(false);
            Debug.Log("[StormController] Rain system built successfully.");
        }

        void Update()
        {
            if (Input.GetKeyDown(stormKey))
            {
                stormActive = !stormActive;
                targetIntensity = stormActive ? 1f : 0f;
                Debug.Log($"[StormController] Storm {(stormActive ? "activated" : "deactivated")}");
            }

            stormIntensity = Mathf.MoveTowards(stormIntensity, targetIntensity,
                transitionSpeed * Time.deltaTime);

            if (playerCamera != null && rainObject != null)
            {
                rainObject.transform.position = new Vector3(
                    playerCamera.position.x,
                    playerCamera.position.y + rainHeightAboveCamera,
                    playerCamera.position.z
                );
            }

            if (!Mathf.Approximately(stormIntensity, lastIntensity))
            {
                ApplyFog();
                ApplyClouds();
                ApplySky();
                ApplyOcean();
                lastIntensity = stormIntensity;
            }

            ApplyRain();
        }

        void ApplyFog()
        {
            if (fog == null) return;

            if (stormIntensity <= 0f)
            {
                fog.meanFreePath.overrideState = false;
                fog.depthExtent.overrideState = false;
                return;
            }

            fog.meanFreePath.Override(Mathf.Lerp(calmFogAttenuation, stormFogAttenuation, stormIntensity));
            fog.depthExtent.Override(Mathf.Lerp(calmFogDistance, stormFogDistance, stormIntensity));
        }

        void ApplyClouds()
        {
            if (cloudLayer == null) return;

            if (stormIntensity <= 0f)
            {
                cloudLayer.opacity.overrideState = false;
                cloudLayer.layerA.tint.overrideState = false;
                return;
            }

            cloudLayer.opacity.Override(Mathf.Lerp(calmCloudOpacity, stormCloudOpacity, stormIntensity));
            cloudLayer.layerA.tint.Override(Color.Lerp(calmCloudTint, stormCloudTint, stormIntensity));
        }

        void ApplySky()
        {
            if (stormIntensity <= 0f)
            {
                if (exposure != null) exposure.compensation.overrideState = false;
                if (pbrSky != null)
                {
                    pbrSky.aerosolDensity.overrideState = false;
                    pbrSky.colorSaturation.overrideState = false;
                }
                return;
            }

            if (exposure != null)
                exposure.compensation.Override(
                    Mathf.Lerp(calmExposureCompensation, stormExposureCompensation, stormIntensity));

            if (pbrSky != null)
            {
                pbrSky.aerosolDensity.Override(
                    Mathf.Lerp(calmAerosolDensity, stormAerosolDensity, stormIntensity));
                pbrSky.colorSaturation.Override(
                    Mathf.Lerp(1f, 0.1f, stormIntensity));
            }
        }

        void ApplyRain()
        {
            if (rainPS == null || rainObject == null) return;

            bool cameraUnderwater = playerCamera != null && playerCamera.position.y < waterSurfaceY;
            bool shouldShow = stormIntensity > 0.05f && !cameraUnderwater;

            rainObject.SetActive(shouldShow);

            if (!shouldShow) return;

            if (!rainPS.isPlaying) rainPS.Play();
            rainEmission.rateOverTime = Mathf.Lerp(0, stormRainEmission, stormIntensity);
        }

        void ApplyOcean()
        {
            if (waterSurface == null) return;
            waterSurface.largeWindSpeed      = Mathf.Lerp(calmLargeWindSpeed,   stormLargeWindSpeed,   stormIntensity);
            waterSurface.ripplesWindSpeed    = Mathf.Lerp(calmRipplesWindSpeed, stormRipplesWindSpeed, stormIntensity);
            waterSurface.largeChaos          = Mathf.Lerp(calmLargeChaos,       stormLargeChaos,       stormIntensity);
            waterSurface.ripplesChaos        = Mathf.Lerp(calmRipplesChaos,     stormRipplesChaos,     stormIntensity);
            waterSurface.simulationFoamAmount = Mathf.Lerp(calmFoamAmount,      stormFoamAmount,       stormIntensity);
        }

        void OnDestroy()
        {
            if (rainObject != null)
                Destroy(rainObject);
        }
    }
}
