using UnityEngine;

namespace Lotusim
{
    public class WindZoneController : MonoBehaviour
    {
        public WindSliderController windController;
        public WindZone windZone;
        public ParticleSystem windParticles; // assign your windSpawner particle system here
        public float maxWindSpeed = 20f;
        public float minWindThreshold = 0.01f; // below this, no wind = no particles

        private bool isEmitting = true;

        void Start()
        {
            if (windZone == null)
                windZone = GetComponent<WindZone>();

            if (windZone == null)
                Debug.LogError("[WindZoneController] No WindZone assigned or found on GameObject.");
        }

        void Update()
        {
            if (windController == null || windZone == null) return;

            Vector3 windVec = windController.CurrentWindVector;
            float magnitude = new Vector3(windVec.x, 0f, windVec.z).magnitude;

            // Rotate the WindZone to face the wind direction
            if (magnitude > 0.01f)
            {
                Vector3 windDir = new Vector3(windVec.x, 0f, windVec.z).normalized;
                windZone.transform.rotation = Quaternion.LookRotation(windDir, Vector3.up);
            }

            // Scale wind speed to WindZone's main value
            windZone.windMain = Mathf.Lerp(0f, maxWindSpeed, magnitude / maxWindSpeed);
            windZone.windTurbulence = Mathf.Lerp(0.1f, 1f, magnitude / maxWindSpeed);

            // Toggle particle emission based on whether there's actual wind
            if (windParticles != null)
            {
                bool shouldEmit = magnitude > minWindThreshold;
                if (shouldEmit != isEmitting)
                {
                    isEmitting = shouldEmit;
                    var emission = windParticles.emission;
                    emission.enabled = shouldEmit;

                }
            }
        }
    }
}