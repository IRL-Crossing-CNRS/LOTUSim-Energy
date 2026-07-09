using UnityEngine;

namespace Lotusim
{
    [RequireComponent(typeof(LineRenderer))]
    public class TrajectoryDrawer : MonoBehaviour
    {
        [Header("Appearance")]
        [Tooltip("The color of the trajectory. Use an HDR color (intensity > 1) to make it glow underwater!")]
        [ColorUsage(true, true)] // Enables HDR mode to make the line glow
        public Color trajectoryColor = Color.cyan;
        
        [Tooltip("The thickness of the drawn line.")]
        public float lineWidth = 0.2f;

        [Tooltip("Optional: A custom Material (e.g., Unlit or Particles/Additive). If empty, uses the default shader.")]
        public Material customMaterial;

        [Header("Drawing Parameters")]
        [Tooltip("Maximum trail length in meters behind the drone. Only the most recent portion of the trajectory is shown.")]
        public float trailLength = 100f;

        [Tooltip("Minimum distance (in meters) the drone must travel before adding a fixed point. (Prevents creating 1000 points if the drone is hovering in place)")]
        public float minDistanceBetweenPoints = 0.5f;
        
        [Tooltip("Maximum number of stored points (hard safety cap).")]
        public int maxPoints = 5000;

        private LineRenderer lineRenderer;
        private Vector3 lastSavedPoint;

        private void Awake()
        {
            lineRenderer = GetComponent<LineRenderer>();
            
            // Basic LineRenderer configuration for proper display
            if (customMaterial != null)
            {
                lineRenderer.material = customMaterial;
            }
            else if (lineRenderer.sharedMaterial == null)
            {
                // The "Sprites/Default" shader works on almost all pipelines
                lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            }
            
            lineRenderer.useWorldSpace = true; // IMPORTANT: The line attaches to the world, not the drone
            
            // Configuration for nice rendering (rounded)
            lineRenderer.numCapVertices = 5;
            lineRenderer.numCornerVertices = 5;
        }

        private void Start()
        {
            lineRenderer.startWidth = lineWidth;
            lineRenderer.endWidth = lineWidth;
            
            lineRenderer.startColor = trajectoryColor;
            lineRenderer.endColor = trajectoryColor;
            
            lineRenderer.positionCount = 1;
            lineRenderer.SetPosition(0, transform.position);
            lastSavedPoint = transform.position;
        }

        private void Update()
        {
            // Allows viewing color/size changes in real-time in the inspector
            lineRenderer.startColor = trajectoryColor;
            lineRenderer.endColor = trajectoryColor;
            lineRenderer.startWidth = lineWidth;
            lineRenderer.endWidth = lineWidth;

            // If the drone has traveled enough distance, we validate the point and create a new one
            if (Vector3.Distance(transform.position, lastSavedPoint) >= minDistanceBetweenPoints)
            {
                lastSavedPoint = transform.position;
                lineRenderer.positionCount++;
                lineRenderer.SetPosition(lineRenderer.positionCount - 1, lastSavedPoint);

                // Trim the oldest points so the visible trail never exceeds trailLength meters.
                TrimTrailToLength();
            }
            else if (lineRenderer.positionCount > 0)
            {
                // The end of the line (the last point) is always attached to the drone's center in real-time
                lineRenderer.SetPosition(lineRenderer.positionCount - 1, transform.position);
            }
        }

        /// <summary>
        /// Removes the oldest points from the trajectory until the total polyline
        /// length is at most <see cref="trailLength"/> meters. Also enforces the
        /// hard <see cref="maxPoints"/> cap as a safety fallback.
        /// </summary>
        private void TrimTrailToLength()
        {
            int count = lineRenderer.positionCount;
            if (count < 2) return;

            // Read all current positions.
            Vector3[] pts = new Vector3[count];
            lineRenderer.GetPositions(pts);

            // Walk backwards from the newest point (end) and accumulate distance.
            float accumulated = 0f;
            int keepFrom = 0; // index of the first point we keep

            for (int i = count - 1; i > 0; i--)
            {
                float segLen = Vector3.Distance(pts[i], pts[i - 1]);
                if (accumulated + segLen > trailLength)
                {
                    // Keep from this index onward.
                    keepFrom = i;
                    break;
                }
                accumulated += segLen;
            }

            // Also enforce the hard point cap.
            int pointCap = Mathf.Max(0, count - maxPoints);
            keepFrom = Mathf.Max(keepFrom, pointCap);

            if (keepFrom > 0)
            {
                int newCount = count - keepFrom;
                Vector3[] trimmed = new Vector3[newCount];
                System.Array.Copy(pts, keepFrom, trimmed, 0, newCount);
                lineRenderer.positionCount = newCount;
                lineRenderer.SetPositions(trimmed);
            }
        }
    }
}
