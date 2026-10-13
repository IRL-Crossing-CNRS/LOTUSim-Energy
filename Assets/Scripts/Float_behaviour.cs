using UnityEngine;

namespace Lotusim
{
    public class OceanDriftObject : MonoBehaviour
    {
        [Header("Flow (match OceanParticleController's currentDirection/currentSpeed)")]
        [Tooltip("World-space direction this object drifts toward. Match this to the particle system's currentDirection.")]
        public Vector3 currentDirection = Vector3.right;

        [Tooltip("Drift speed, in world units per second. Match this to the particle system's currentSpeed.")]
        public float currentSpeed = 0.4f;

        void Update()
        {
            Vector3 dir = currentDirection.sqrMagnitude > 0.0001f ? currentDirection.normalized : Vector3.right;
            transform.position += dir * currentSpeed * Time.deltaTime;
        }
    }
}