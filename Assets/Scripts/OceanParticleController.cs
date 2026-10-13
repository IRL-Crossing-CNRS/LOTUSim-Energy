using UnityEngine;

namespace Lotusim
{
    public class OceanParticleController : MonoBehaviour
    {
        [Header("Particles")]
        public ParticleSystem currentParticles;

        [Header("Flow (placeholder until real current source is wired in)")]
        [Tooltip("World-space direction the current flows toward. Will later be replaced by a live current vector.")]
        public Vector3 currentDirection = Vector3.right;

        [Tooltip("Speed of the linear drift along currentDirection. Keep this low/slow for underwater currents.")]
        public float currentSpeed = 0.4f;

        [Header("Wavy Motion (per-particle, not a shared vortex)")]
        [Tooltip("How far each particle wanders off its straight path.")]
        public float waveStrength = 0.6f;

        [Tooltip("How tightly packed the waves/wobbles are. Lower = broader, slower-feeling waves.")]
        public float waveFrequency = 0.15f;

        [Tooltip("How quickly the wave pattern itself drifts/evolves over time.")]
        public float waveScrollSpeed = 0.05f;

        [Header("Spawn Volume (must stay below the waterline)")]
        [Tooltip("World Y position of the waterline. Particles will only spawn at or below this.")]
        public float waterLevel = 0f;

        [Tooltip("How far below the waterline the spawn volume extends.")]
        public float spawnDepth = 100f;

        [Tooltip("Width of the spawn volume along world X. Set this to cover your simulation area.")]
        public float spawnWidthX = 2000f;

        [Tooltip("Length of the spawn volume along world Z. Set this to cover your simulation area.")]
        public float spawnLengthZ = 2000f;

        private bool initialized;

        void Start()
        {
            if (currentParticles == null)
                currentParticles = GetComponent<ParticleSystem>();

            if (currentParticles == null)
            {
                Debug.LogError("[OceanParticleController] No ParticleSystem assigned or found on GameObject.");
                return;
            }

            ConfigureParticleSystem();
        }

        void Update()
        {
            if (currentParticles == null) return;

            ConfigureParticleSystem();
        }

        void OnValidate()
        {
            if (currentParticles != null)
                ConfigureParticleSystem();
        }

        private void ConfigureParticleSystem()
        {
            var main = currentParticles.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var trails = currentParticles.trails;
            trails.enabled = false;

            var vel = currentParticles.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;

            Vector3 dir = currentDirection.sqrMagnitude > 0.0001f ? currentDirection.normalized : Vector3.right;
            Vector3 velocity = dir * currentSpeed;
            vel.x = velocity.x;
            vel.y = velocity.y;
            vel.z = velocity.z;

            var noise = currentParticles.noise;
            noise.enabled = true;
            noise.separateAxes = false;
            noise.strength = waveStrength;
            noise.frequency = waveFrequency;
            noise.scrollSpeed = waveScrollSpeed;
            noise.damping = true;
            noise.octaveCount = 1;
            noise.quality = ParticleSystemNoiseQuality.Medium;

            var shape = currentParticles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.position = new Vector3(0f, waterLevel - (spawnDepth * 0.5f), 0f);
            shape.scale = new Vector3(spawnWidthX, spawnDepth, spawnLengthZ);

            initialized = true;
        }
    }
}