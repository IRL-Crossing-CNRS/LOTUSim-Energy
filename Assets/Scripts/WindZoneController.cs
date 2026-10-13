using UnityEngine;

namespace Lotusim
{
    public class WindZoneController : MonoBehaviour
    {
        public WindSliderController windController;
        public WindZone windZone;
        public ParticleSystem windParticles;
        public float maxWindSpeed = 20f;
        public float minWindThreshold = 0.01f;

        [Header("Particle Scaling (multiplies your existing particle system values)")]
        [Tooltip("Multiplier on the particle system's base start speed at zero wind.")]
        public float minParticleSpeed = 1f;
        [Tooltip("Multiplier on the particle system's base start speed at max wind speed.")]
        public float maxParticleSpeed = 3f;

        [Tooltip("Multiplier on the particle system's base start size at zero wind.")]
        public float minParticleSize = 1f;
        [Tooltip("Multiplier on the particle system's base start size at max wind speed.")]
        public float maxParticleSize = 1.5f;

        [Tooltip("Multiplier on the particle system's base emission rate at zero wind.")]
        public float minParticleRate = 1f;
        [Tooltip("Multiplier on the particle system's base emission rate at max wind speed.")]
        public float maxParticleRate = 2.5f;

        private bool isEmitting = true;
        private float baseStartSpeed;
        private float baseStartSize;
        private float baseRateOverTime;
        private bool baseValuesCached;

        void Start()
        {
            if (windZone == null)
                windZone = GetComponent<WindZone>();

            if (windZone == null)
                Debug.LogError("[WindZoneController] No WindZone assigned or found on GameObject.");

            if (windParticles != null)
            {
                var main = windParticles.main;
                var emission = windParticles.emission;
                baseStartSpeed = main.startSpeed.constant;
                baseStartSize = main.startSize.constant;
                baseRateOverTime = emission.rateOverTime.constant;
                baseValuesCached = true;
            }
        }

        void Update()
        {
            if (windController == null || windZone == null) return;

            Vector3 windVec = windController.CurrentWindVector;
            Vector3 horizontal = new Vector3(windVec.x, 0f, windVec.y);
            float magnitude = horizontal.magnitude;

            if (magnitude > 0.01f)
            {
                Vector3 windDir = horizontal.normalized;
                Quaternion lookRot = Quaternion.LookRotation(windDir, Vector3.up);

                windZone.transform.rotation = lookRot;

                if (windParticles != null)
                    windParticles.transform.rotation = lookRot;
            }

            windZone.windMain = Mathf.Lerp(0f, maxWindSpeed, magnitude / maxWindSpeed);
            windZone.windTurbulence = Mathf.Lerp(0.1f, 1f, magnitude / maxWindSpeed);

            if (windParticles != null)
            {
                bool shouldEmit = magnitude > minWindThreshold;
                if (shouldEmit != isEmitting)
                {
                    isEmitting = shouldEmit;
                    var emission = windParticles.emission;
                    emission.enabled = shouldEmit;
                }

                UpdateParticleProperties(magnitude);
            }
        }

        private void UpdateParticleProperties(float magnitude)
        {
            if (!baseValuesCached) return;

            float windRatio = Mathf.Clamp01(magnitude / maxWindSpeed);

            float speedFactor = Mathf.Lerp(minParticleSpeed, maxParticleSpeed, windRatio);
            float sizeFactor = Mathf.Lerp(minParticleSize, maxParticleSize, windRatio);
            float rateFactor = Mathf.Lerp(minParticleRate, maxParticleRate, windRatio);

            var main = windParticles.main;
            var startSpeed = main.startSpeed;
            startSpeed.constant = baseStartSpeed * speedFactor;
            main.startSpeed = startSpeed;

            var startSize = main.startSize;
            startSize.constant = baseStartSize * sizeFactor;
            main.startSize = startSize;

            var emission = windParticles.emission;
            var rateOverTime = emission.rateOverTime;
            rateOverTime.constant = baseRateOverTime * rateFactor;
            emission.rateOverTime = rateOverTime;
        }
    }
}