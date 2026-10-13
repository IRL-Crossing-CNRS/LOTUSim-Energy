using UnityEngine;

namespace Lotusim
{
    public class TurbineWake : MonoBehaviour
    {
        [Header("References")]
        public WindSliderController windController;

        [Header("Wake Shape")]
        public float rotorDiameter  = 60f;
        public float wakeLength     = 720f;
        public float expansionRate  = 0.04f;
        public int   radialSegments = 32;
        public int   lengthSegments = 40;

        [Header("Appearance")]
        public Color colorNear = new Color(0.05f, 0.15f, 0.75f, 0.6f);
        public Color colorMid  = new Color(0.25f, 0.55f, 0.90f, 0.35f);
        public Color colorFar  = new Color(0.70f, 0.88f, 1.00f, 0.0f);

        [Header("Wind Threshold")]
        public float minWindSpeed = 0.5f;

        private GameObject  wakeObj;
        private MeshFilter  meshFilter;
        private MeshRenderer meshRenderer;
        private Material    wakeMat;

        void Start()
        {
            BuildWakeMesh();
        }

        void BuildWakeMesh()
        {
            wakeObj = new GameObject("WakeMesh");
            wakeObj.transform.SetParent(transform);
            wakeObj.transform.localPosition = Vector3.zero;
            wakeObj.transform.localRotation = Quaternion.identity;

            meshFilter   = wakeObj.AddComponent<MeshFilter>();
            meshRenderer = wakeObj.AddComponent<MeshRenderer>();

            meshFilter.mesh = GenerateConeMesh();

            wakeMat = new Material(Shader.Find("Unlit/Transparent"));
            wakeMat.color = new Color(0.1f, 0.3f, 0.85f, 0.25f);
            meshRenderer.material = wakeMat;

            meshRenderer.material = wakeMat;
            meshRenderer.shadowCastingMode  = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows     = false;

            wakeObj.SetActive(false);
        }

        Mesh GenerateConeMesh()
        {
            Mesh mesh = new Mesh();
            mesh.name = "LarsenWake";

            int vertCount = (lengthSegments + 1) * (radialSegments + 1);
            Vector3[] verts  = new Vector3[vertCount];
            Color[]   colors = new Color[vertCount];
            Vector2[] uvs    = new Vector2[vertCount];

            float rotorRadius = rotorDiameter * 0.5f;

            for (int l = 0; l <= lengthSegments; l++)
            {
                float tLen = (float)l / lengthSegments;

                // Distance along wake
                float z = -tLen * wakeLength;

                // Radius expands linearly — Larsen approximation
                float radius = rotorRadius + z * expansionRate * rotorRadius;

                // Colour lerps near → mid → far
                Color col;
                if (tLen < 0.4f)
                    col = Color.Lerp(colorNear, colorMid, tLen / 0.4f);
                else
                    col = Color.Lerp(colorMid, colorFar, (tLen - 0.4f) / 0.6f);

                for (int r = 0; r <= radialSegments; r++)
                {
                    float angle = (float)r / radialSegments * Mathf.PI * 2f;
                    float x     = Mathf.Cos(angle) * radius;
                    float y     = Mathf.Sin(angle) * radius;

                    int idx = l * (radialSegments + 1) + r;
                    verts[idx]  = new Vector3(x, y, z);
                    colors[idx] = col;
                    uvs[idx]    = new Vector2(tLen, (float)r / radialSegments);
                }
            }

            // Triangles
            int triCount = lengthSegments * radialSegments * 6;
            int[] tris = new int[triCount];
            int   ti   = 0;

            for (int l = 0; l < lengthSegments; l++)
            {
                for (int r = 0; r < radialSegments; r++)
                {
                    int a = l       * (radialSegments + 1) + r;
                    int b = (l + 1) * (radialSegments + 1) + r;
                    int c = (l + 1) * (radialSegments + 1) + r + 1;
                    int d = l       * (radialSegments + 1) + r + 1;

                    // Front face
                    tris[ti++] = a; tris[ti++] = b; tris[ti++] = c;
                    tris[ti++] = a; tris[ti++] = c; tris[ti++] = d;
                }
            }

            mesh.vertices  = verts;
            mesh.colors    = colors;
            mesh.uv        = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();

            return mesh;
        }

        void Update()
        {
            if (windController == null || wakeObj == null) return;

            Vector3 wind      = windController.CurrentWindVector;
            float   magnitude = new Vector2(wind.x, wind.y).magnitude;
            bool    hasWind   = magnitude >= minWindSpeed;

            wakeObj.SetActive(hasWind && WindRegionVisualizer.WakeVisible);
        }
    }
}