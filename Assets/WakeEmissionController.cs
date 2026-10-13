using UnityEngine;

namespace Lotusim
{
    public class WakeEmissionController : MonoBehaviour
    {
        [Header("References")]
        public WindTurbineController turbineController;

        [Header("Emission Settings")]
        [Tooltip("Minimum animSpeed before wake appears. Matches turbine cut-in.")]
        public float minSpeedThreshold = 0.1f;

        [Tooltip("Emission rates at full speed for each particle system (match your 3 systems).")]
        public float[] maxEmissionRates = { 15f, 10f, 10f };

        [Tooltip("Lifetimes at full speed for each particle system.")]
        public float[] maxLifetimes = { 15f, 8f, 12f };

        private ParticleSystem[] systems;
        private float[] lastT;

        void Start()
        {
            if (turbineController == null)
                turbineController = GetComponentInParent<WindTurbineController>();

            if (turbineController == null)
                Debug.LogWarning("[WakeEmissionController] No WindTurbineController found in parent.");

            systems = GetComponentsInChildren<ParticleSystem>();

            // Track last-applied t value per system so we can skip redundant updates
            lastT = new float[systems.Length];
            for (int i = 0; i < lastT.Length; i++) lastT[i] = -1f;

            // Force stop all systems immediately on startup
            foreach (ParticleSystem ps in systems)
            {
                ps.Stop();
                ps.Clear();
                var emission = ps.emission;
                emission.rateOverTime = 0f;
            }
        }

        void Update()
        {
            if (turbineController == null || systems == null) return;

            float animSpeed = turbineController.CurrentAnimSpeed;
            bool  spinning  = animSpeed >= minSpeedThreshold && WindRegionVisualizer.WakeVisible;
            float t         = Mathf.Clamp01(animSpeed);

            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem ps = systems[i];

                if (!spinning)
                {
                    // Stop and clear — no lingering particles when turbine is idle
                    if (ps.isPlaying)
                    {
                        var emission = ps.emission;
                        emission.rateOverTime = 0f;
                        ps.Stop();
                        ps.Clear();
                        lastT[i] = -1f;
                    }
                    continue;
                }

                // Skip update if speed hasn't changed meaningfully — avoids
                // touching particle system modules every single frame
                if (Mathf.Abs(t - lastT[i]) < 0.005f && ps.isPlaying) continue;

                var em   = ps.emission;
                var main = ps.main;

                float maxRate     = i < maxEmissionRates.Length ? maxEmissionRates[i] : 20f;
                float maxLifetime = i < maxLifetimes.Length     ? maxLifetimes[i]     : 5f;

                em.rateOverTime    = maxRate * t;
                main.startLifetime = maxLifetime * t;

                if (!ps.isPlaying) ps.Play();

                lastT[i] = t;
            }
        }
    }
}