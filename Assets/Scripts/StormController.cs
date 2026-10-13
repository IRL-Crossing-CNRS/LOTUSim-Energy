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
        public WindSliderController windController;

        [Header("Storm Trigger")]
        [Range(0f, 1f)]
        public float stormIntensity = 0f;
        public float transitionSpeed = 0.5f;
        public KeyCode stormKey = KeyCode.F1;

        [Tooltip("Horizontal wind speed (m/s) pushed onto the wind sliders while the storm is active.")]
        public float stormWindSpeed = 28f;

        [Header("Fog Settings")]
        public float calmFogAttenuation = 400f;
        public float stormFogAttenuation = 60f;
        public float calmFogDistance = 5000f;
        public float stormFogDistance = 600f;

        [Header("Cloud Settings")]
        public float calmCloudOpacity = 0.61f;
        public float stormCloudOpacity = 1f;
        public Color calmCloudTint = Color.white;
        public Color stormCloudTint = new Color(0.25f, 0.25f, 0.28f);
        public float calmCloudOpacityR = 1f;
        public float stormCloudOpacityR = 1f;

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

        [Header("Ocean Base Settings")]
        public float calmLargeWindSpeed = 0f;
        public float stormLargeWindSpeed = 60f;
        public float calmRipplesWindSpeed = 9.28f;
        public float stormRipplesWindSpeed = 40f;
        public float calmLargeChaos = 0f;
        public float stormLargeChaos = 1f;
        public float calmRipplesChaos = 0.8f;
        public float stormRipplesChaos = 1f;
        public float calmFoamAmount = 0.1f;
        public float stormFoamAmount = 0.9f;

        [Header("Beaufort Storm Driving")]
        [Tooltip("If true, storm intensity automatically rises with Beaufort scale — force 12 = full storm.")]
        public bool beaufortDrivesStorm = true;

        [Header("Beaufort Wave Scaling")]
        public float baseWaveWindSpeed = 10f;
        public float waveWindScaleFactor = 30f;
        public float baseRippleWindSpeed = 9.28f;
        public float rippleWindScaleFactor = 15f;
        public float foamScaleFactor = 0.2f;

        [Header("Force 12 Warning")]
        [Tooltip("Shown automatically when wind reaches Beaufort force 12. Auto-found if left empty.")]
        public EnvironmentalWarningHUD warningHUD;
        [Tooltip("Hide the warning again when wind drops back below force 12.")]
        public bool hideWarningBelowForce12 = true;

        private bool wasForce12 = false;

        private bool stormActive = false;
        private Vector3 calmWind;
        public bool StormActive => stormActive;
        private float targetIntensity = 0f;
        private float lastIntensity = -1f;
        private float lastWindMagnitude = -1f;

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

            if (warningHUD == null)
            warningHUD = FindFirstObjectByType<EnvironmentalWarningHUD>();

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

                if (stormActive)
                {
                    calmWind = windController != null ? windController.CurrentWindVector : Vector3.zero;
                    if (windController != null)
                        windController.SetWind(float.MaxValue, float.MaxValue, float.MaxValue);
                    targetIntensity = 1f;
                }
                else
                {
                    if (windController != null)
                        windController.SetWind(calmWind.x, calmWind.y, calmWind.z);
                    targetIntensity = 0f;
                }

                Debug.Log($"[StormController] Storm {(stormActive ? "activated" : "deactivated")}");
            }

            stormIntensity = Mathf.MoveTowards(stormIntensity, targetIntensity,
                transitionSpeed * Time.deltaTime);

            if (rainObject != null)
            {
                Transform activeCam = GetActiveCamera();
                if (activeCam != null)
                {
                    rainObject.transform.position = new Vector3(
                        activeCam.position.x,
                        activeCam.position.y + rainHeightAboveCamera,
                        activeCam.position.z
                    );
                }
            }

            if (!Mathf.Approximately(stormIntensity, lastIntensity))
            {
                ApplyFog();
                ApplyClouds();
                ApplySky();
                lastIntensity = stormIntensity;
            }

            ApplyOcean();
            ApplyRain();

            CheckForce12Warning();
        }

        void ApplyStormWind()
        {
            if (windController == null) return;

            if (stormActive)
            {
                calmWind = windController.CurrentWindVector;

                Vector2 direction = new Vector2(calmWind.x, calmWind.y);
                if (direction.sqrMagnitude < 0.01f) direction = Vector2.right;
                direction.Normalize();

                windController.SetWind(direction.x * stormWindSpeed,
                                       direction.y * stormWindSpeed,
                                       calmWind.z);
            }
            else
            {
                windController.SetWind(calmWind.x, calmWind.y, calmWind.z);
            }
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

        void CheckForce12Warning()
        {
            if (windController == null || warningHUD == null) return;

            var (force, _, _, _) = BeaufortScale.GetForce(windController.CurrentWindVector.magnitude);
            bool atForce12 = force >= 12;

            if (atForce12 && !wasForce12)
                warningHUD.Show();
            else if (!atForce12 && wasForce12 && hideWarningBelowForce12)
                warningHUD.Hide();

            wasForce12 = atForce12;
        }

        void ApplyClouds()
        {
            if (cloudLayer == null) return;

            if (stormIntensity <= 0f)
            {
                cloudLayer.opacity.overrideState = false;
                cloudLayer.layerA.tint.overrideState = false;
                cloudLayer.layerA.opacityR.overrideState = false;
                return;
            }

            cloudLayer.opacity.Override(
                Mathf.Lerp(calmCloudOpacity, stormCloudOpacity, stormIntensity));

            cloudLayer.layerA.tint.Override(
                Color.Lerp(calmCloudTint, stormCloudTint, stormIntensity));

            cloudLayer.layerA.opacityR.Override(
                Mathf.Lerp(calmCloudOpacityR, stormCloudOpacityR, stormIntensity));

            cloudLayer.layerA.opacityG.Override(
                Mathf.Lerp(0f, 1f, stormIntensity));

            cloudLayer.layerA.opacityA.Override(
                Mathf.Lerp(0f, 1f, stormIntensity));
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

        Transform GetActiveCamera()
        {
            if (DisplaySwitcher.Instance == null) return playerCamera;

            int idx = DisplaySwitcher.Instance.ActiveSlotIndex;
            Camera[] cams = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (Camera c in cams)
                if (c.targetDisplay == 0)
                    return c.transform;

            return playerCamera;
        }

        void ApplyOcean()
        {
            if (waterSurface == null) return;

            float windMagnitude = 0f;
            float beaufortMultiplier = 1f;
            float beaufortIntensity = 0f;

            if (windController != null)
            {
                Vector3 windVec = windController.CurrentWindVector;
                windMagnitude = new Vector3(windVec.x, windVec.y, windVec.z).magnitude;
                var (force, _, _, multiplier) = BeaufortScale.GetForce(windMagnitude);
                beaufortMultiplier = multiplier;

                beaufortIntensity = Mathf.Clamp01(force / 12f);
                beaufortIntensity = Mathf.Clamp01((beaufortIntensity - 0.70f) / 0.30f);

                if (beaufortDrivesStorm && !stormActive)
                {
                    targetIntensity = beaufortIntensity;
                }
            }

            float largeWind;
            float ripplesWind;
            float chaos;
            float ripplesChaos;
            float foam;

            if (stormIntensity > 0f)
            {
                largeWind    = Mathf.Lerp(calmLargeWindSpeed,   stormLargeWindSpeed,   stormIntensity);
                ripplesWind  = Mathf.Lerp(calmRipplesWindSpeed, stormRipplesWindSpeed, stormIntensity);
                chaos        = Mathf.Lerp(calmLargeChaos,       stormLargeChaos,       stormIntensity);
                ripplesChaos = Mathf.Lerp(calmRipplesChaos,     stormRipplesChaos,     stormIntensity);
                foam         = Mathf.Lerp(calmFoamAmount,       stormFoamAmount,       stormIntensity);

                largeWind   *= beaufortMultiplier;
                ripplesWind *= beaufortMultiplier;
                foam         = Mathf.Clamp01(foam * beaufortMultiplier);
            }
            else
            {
                largeWind    = baseWaveWindSpeed   * beaufortMultiplier * waveWindScaleFactor   / 30f;
                ripplesWind  = baseRippleWindSpeed * beaufortMultiplier * rippleWindScaleFactor / 15f;
                chaos        = Mathf.Lerp(0f, 0.5f, (beaufortMultiplier - 1f) / 2f);
                ripplesChaos = Mathf.Lerp(calmRipplesChaos, 1f, (beaufortMultiplier - 1f) / 2f);
                foam         = Mathf.Clamp01(calmFoamAmount + (beaufortMultiplier - 1f) * foamScaleFactor);
            }

            waterSurface.largeWindSpeed       = largeWind;
            waterSurface.ripplesWindSpeed     = ripplesWind;
            waterSurface.largeChaos           = chaos;
            waterSurface.ripplesChaos         = ripplesChaos;
            waterSurface.simulationFoamAmount = foam;
        }

        void OnDestroy()
        {
            if (rainObject != null)
                Destroy(rainObject);
        }
    }
}