using System.Collections.Generic;
using UnityEngine;
using RosMessageTypes.Lotusim;

namespace Lotusim
{
    [System.Serializable]
    public class ConeSegmentShapeRenderer : IWindRegionShapeRenderer
    {
        [Header("Cone Geometry")]
        [Tooltip("World-space Y (height) of the cone's centerline.")]
        [SerializeField] private float axisHeight = 85f;

        [Tooltip("Number of vertices around the circumference.")]
        [SerializeField] private int circleSegments = 24;

        [Tooltip("Number of subdivisions along the length of the cone.")]
        [SerializeField] private int lengthSegments = 16;

        [Header("Continuous Mesh")]
        [Tooltip("Material used to render the wind-region mesh.")]
        [SerializeField] private Material meshMaterial;

        [Tooltip("Show the continuous mesh.")]
        [SerializeField] private bool showMesh = true;

        [Header("Mesh Shape Variation")]
        [Tooltip("Adds gentle sideways variation to the tunnel.")]
        [SerializeField] private bool useShapeNoise = false;

        [Tooltip("Amount of sideways displacement caused by shape noise.")]
        [SerializeField] private float noiseStrength = 2f;

        [Tooltip("How frequently the shape changes along its length.")]
        [SerializeField] private float noiseFrequency = 0.5f;

        [Tooltip("Seed used for the procedural shape variation.")]
        [SerializeField] private int noiseSeed = 12345;

        [Header("Arrow Spine")]
        [SerializeField] private float arrowSpacing = 15f;
        [SerializeField] private int minArrowCount = 2;
        [SerializeField] private int maxArrowCount = 6;

        private class State
        {
            // Continuous mesh
            public GameObject meshRoot;
            public MeshFilter meshFilter;
            public MeshRenderer meshRenderer;
            public Mesh mesh;
            public Material runtimeMaterial;

            // Existing arrows
            public WindArrowFieldRenderer arrows;
        }

        public object Build(
            Transform root,
            WindZoneRenderSettings settings)
        {
            var s = new State();

            // =========================================================
            // CONTINUOUS MESH
            // =========================================================

            s.meshRoot = new GameObject("WindTunnelMesh");

            s.meshRoot.transform.SetParent(
                root,
                false
            );

            if (settings.LayerIndex > -1)
                s.meshRoot.layer = settings.LayerIndex;

            s.meshFilter =
                s.meshRoot.AddComponent<MeshFilter>();

            s.meshRenderer =
                s.meshRoot.AddComponent<MeshRenderer>();

            s.mesh =
                new Mesh();

            s.mesh.name =
                "WindTunnelMesh";

            // The mesh will be updated when the wind region changes.
            s.mesh.MarkDynamic();

            s.meshFilter.sharedMesh =
                s.mesh;

            // =========================================================
            // MATERIAL
            // =========================================================

            if (meshMaterial != null)
            {
                s.meshRenderer.sharedMaterial =
                    meshMaterial;
            }
            else
            {
                Shader shader =
                    Shader.Find("HDRP/Unlit") ??
                    Shader.Find("Unlit/Color");

                if (shader != null)
                {
                    s.runtimeMaterial =
                        new Material(shader);

                    s.runtimeMaterial.name =
                        "WindTunnel_RuntimeMaterial";

                    s.meshRenderer.sharedMaterial =
                        s.runtimeMaterial;
                }
            }

            s.meshRenderer.enabled =
                showMesh;

            // =========================================================
            // ARROWS
            // =========================================================

            s.arrows =
                new WindArrowFieldRenderer(
                    root,
                    "Arrows",
                    settings.ArrowLengthScale,
                    settings.MaxArrowLength,
                    settings.ArrowLineWidthMin,
                    settings.ArrowLineWidthMax,
                    settings.MaxWindSpeedForScale
                );

            return s;
        }

        public Vector3 UpdateGeometry(
            object state,
            WindRegionMsg region,
            WindZoneRenderSettings settings,
            out float windMagnitude)
        {
            var s = (State)state;

            WindRegionConeSegmentMsg cone =
                region.cone ??
                new WindRegionConeSegmentMsg();

            // =========================================================
            // WIND AXIS
            // =========================================================

            Vector2 axisXY =
                new Vector2(
                    (float)cone.axis.x,
                    (float)cone.axis.y
                );

            Vector3 axisWorld;

            if (axisXY.sqrMagnitude > 0.0001f)
            {
                axisWorld =
                    new Vector3(
                        axisXY.x,
                        0f,
                        axisXY.y
                    ).normalized;
            }
            else
            {
                axisWorld =
                    Vector3.forward;
            }

            // Vector pointing around the circumference.
            Vector3 right =
                Vector3.Cross(
                    Vector3.up,
                    axisWorld
                ).normalized;

            if (right.sqrMagnitude < 0.0001f)
                right = Vector3.right;

            // =========================================================
            // START / END
            // =========================================================

            Vector3 start =
                new Vector3(
                    (float)cone.origin.x,
                    axisHeight,
                    (float)cone.origin.y
                );

            float length =
                Mathf.Max(
                    0.01f,
                    (float)cone.length
                );

            Vector3 end =
                start +
                axisWorld *
                length;

            float rStart =
                Mathf.Max(
                    0f,
                    (float)cone.r_start
                );

            float rEnd =
                Mathf.Max(
                    0f,
                    (float)cone.r_end
                );

            // =========================================================
            // UPDATE MESH
            // =========================================================

            s.meshRenderer.enabled =
                showMesh;

            if (showMesh)
            {
                BuildTunnelMesh(
                    s.mesh,
                    s.meshRoot.transform,
                    start,
                    end,
                    right,
                    rStart,
                    rEnd
                );
            }

            // =========================================================
            // UPDATE ARROWS
            // =========================================================

            int arrowCount =
                Mathf.Clamp(
                    Mathf.RoundToInt(
                        length / arrowSpacing
                    ) + 1,
                    minArrowCount,
                    maxArrowCount
                );

            List<Vector3> origins =
                new List<Vector3>(
                    arrowCount
                );

            for (int i = 0;
                 i < arrowCount;
                 i++)
            {
                float t;

                if (arrowCount > 1)
                {
                    t =
                        i /
                        (float)(arrowCount - 1);
                }
                else
                {
                    t = 0.5f;
                }

                origins.Add(
                    Vector3.Lerp(
                        start,
                        end,
                        t
                    )
                );
            }

            if (!s.arrows.PointsMatch(origins))
            {
                s.arrows.BuildFromPoints(
                    origins
                );
            }

            windMagnitude =
                WindZoneGeometry.UpdateArrowsFromVelocity(
                    s.arrows,
                    region.linear_velocity,
                    settings
                );

            // =========================================================
            // RETURN POSITION
            // =========================================================

            return
                Vector3.Lerp(
                    start,
                    end,
                    0.5f
                )
                +
                Vector3.up *
                Mathf.Max(
                    rStart,
                    rEnd
                );
        }

        // =============================================================
        // BUILD CONTINUOUS TUNNEL MESH
        // =============================================================

        private void BuildTunnelMesh(
            Mesh mesh,
            Transform meshTransform,
            Vector3 start,
            Vector3 end,
            Vector3 right,
            float rStart,
            float rEnd)
        {
            int segments =
                Mathf.Max(
                    3,
                    circleSegments
                );

            int rings =
                Mathf.Max(
                    2,
                    lengthSegments + 1
                );

            int vertexCount =
                rings *
                segments;

            Vector3[] vertices =
                new Vector3[vertexCount];

            Vector2[] uv =
                new Vector2[vertexCount];

            int[] triangles =
                new int[
                    lengthSegments *
                    segments *
                    6
                ];

            // =========================================================
            // VERTICES
            // =========================================================

            for (int ring = 0;
                 ring < rings;
                 ring++)
            {
                float t =
                    ring /
                    (float)(rings - 1);

                Vector3 center =
                    Vector3.Lerp(
                        start,
                        end,
                        t
                    );

                float radius =
                    Mathf.Lerp(
                        rStart,
                        rEnd,
                        t
                    );

                // -----------------------------------------------------
                // Optional shape noise
                // -----------------------------------------------------

                Vector3 noiseOffset =
                    Vector3.zero;

                if (useShapeNoise &&
                    noiseStrength > 0f)
                {
                    float noiseX =
                        Mathf.PerlinNoise(
                            noiseSeed,
                            t * noiseFrequency
                        );

                    float noiseY =
                        Mathf.PerlinNoise(
                            noiseSeed + 1000,
                            t * noiseFrequency
                        );

                    noiseX =
                        (noiseX - 0.5f) *
                        2f;

                    noiseY =
                        (noiseY - 0.5f) *
                        2f;

                    noiseOffset =
                        right *
                        noiseX *
                        noiseStrength;

                    noiseOffset +=
                        Vector3.up *
                        noiseY *
                        noiseStrength;
                }

                center +=
                    noiseOffset;

                // -----------------------------------------------------
                // Ring vertices
                // -----------------------------------------------------

                for (int segment = 0;
                     segment < segments;
                     segment++)
                {
                    float theta =
                        segment /
                        (float)segments *
                        Mathf.PI *
                        2f;

                    Vector3 offset =
                        radius *
                        (
                            Mathf.Cos(theta) *
                            right
                            +
                            Mathf.Sin(theta) *
                            Vector3.up
                        );

                    Vector3 worldPosition =
                        center +
                        offset;

                    int index =
                        ring *
                        segments +
                        segment;

                    // IMPORTANT:
                    //
                    // The cone coordinates are world-space.
                    // Mesh vertices are local-space.
                    //
                    // Converting here prevents the mesh from being
                    // offset from the original wind region.

                    vertices[index] =
                        meshTransform.InverseTransformPoint(
                            worldPosition
                        );

                    uv[index] =
                        new Vector2(
                            segment /
                            (float)segments,
                            t
                        );
                }
            }

            // =========================================================
            // TRIANGLES
            // =========================================================

            int triangleIndex = 0;

            for (int ring = 0;
                 ring < lengthSegments;
                 ring++)
            {
                for (int segment = 0;
                     segment < segments;
                     segment++)
                {
                    int nextSegment =
                        (segment + 1) %
                        segments;

                    int currentRing =
                        ring *
                        segments;

                    int nextRing =
                        (ring + 1) *
                        segments;

                    int v00 =
                        currentRing +
                        segment;

                    int v01 =
                        currentRing +
                        nextSegment;

                    int v10 =
                        nextRing +
                        segment;

                    int v11 =
                        nextRing +
                        nextSegment;

                    // First triangle
                    triangles[triangleIndex++] =
                        v00;

                    triangles[triangleIndex++] =
                        v01;

                    triangles[triangleIndex++] =
                        v10;

                    // Second triangle
                    triangles[triangleIndex++] =
                        v01;

                    triangles[triangleIndex++] =
                        v11;

                    triangles[triangleIndex++] =
                        v10;
                }
            }

            // =========================================================
            // APPLY
            // =========================================================

            mesh.Clear();

            mesh.vertices =
                vertices;

            mesh.uv =
                uv;

            mesh.triangles =
                triangles;

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        // =============================================================
        // CLEANUP
        // =============================================================

        public void Dispose(object state)
        {
            var s =
                (State)state;

            s?.arrows?.Dispose();

            if (s?.mesh != null)
            {
                Object.Destroy(
                    s.mesh
                );
            }

            if (s?.runtimeMaterial != null)
            {
                Object.Destroy(
                    s.runtimeMaterial
                );
            }

            if (s?.meshRoot != null)
            {
                Object.Destroy(
                    s.meshRoot
                );
            }
        }
    }
}
