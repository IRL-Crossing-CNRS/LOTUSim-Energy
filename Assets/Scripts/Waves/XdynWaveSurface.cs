using System;
using System.Collections.Generic;
using Unity.Collections;
using RosMessageTypes.Lotusim;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Lotusim
{
    /// <summary>
    /// Draws on the HDRP ocean the waves xdyn computes, so a vessel's motion and
    /// the water it sits in match crest for crest.
    ///
    /// The simulation publishes the wave components every xdyn vehicle model
    /// uses (lotusim_msgs/WaveSpectrum). Every frame this evaluates the same sum
    /// as xdyn's Airy model over a square region around the camera, at the
    /// simulation time of the poses being shown, and feeds the height map to a
    /// water decal that deforms the surface. The HDRP simulation's own swell is
    /// turned off while the components are known, leaving only its small
    /// ripples for surface detail.
    ///
    /// Frames: xdyn works in NED (x north, y east, z down), Unity in (X east,
    /// Y up, Z north), with the same origin, so x_ned = Z, y_ned = X and the
    /// rendered height is -zeta.
    /// </summary>
    [RequireComponent(typeof(WaterSurface))]
    public class XdynWaveSurface : MonoBehaviour
    {
        [Tooltip("Side of the square region drawn from the xdyn components (m). " +
                 "Beyond it the ocean shows only the HDRP ripples. 2048 m covers a wind farm " +
                 "around the camera; HDRP deforms the water through one decal region texture of " +
                 "at most 2048 x 2048, so this also sets the size of a deformation texel.")]
        public float regionSize = 2048f;

        [Tooltip("Height map resolution over the region (and HDRP decal region resolution).")]
        public int resolution = 2048;

        [Tooltip("Components shorter than this many texels are left out of the height map, " +
                 "which could not represent them (aliasing). HeightAt keeps every component.")]
        public float minTexelsPerWavelength = 4f;

        [Tooltip("Keep the HDRP ripples (small, short waves xdyn does not model) for surface detail.")]
        public bool keepRipples = true;

        [Tooltip("Wind speed driving the HDRP ripples when they are kept (m/s).")]
        public float ripplesWindSpeed = 2f;

        public ComputeShader heightCompute;
        public Material decalMaterial;

        static readonly object s_lock = new object();
        static WaveSpectrumMsg s_pendingSpectrum;
        static double s_simTime;
        // Horizontal positions (east, north) of the vessels at the surface, for the probe.
        static readonly List<(string name, double east, double north)> s_hulls = new List<(string, double, double)>();
        static float s_simTimeReceivedAt = -1f;

        WaterSurface m_water;
        WaterDecal m_decal;
        RenderTexture m_height;
        ComputeBuffer m_rays, m_phases;
        Vector2[] m_phaseGain;
        double[] m_a, m_k, m_cos, m_sin, m_omega, m_phase;
        // Fade of each component while the sea state changes (see WaveSpectrum.msg).
        double[] m_fadeInStart, m_fadeInDuration, m_fadeOutStart, m_fadeOutDuration;
        float m_amplitude;
        int m_kernel;
        [Tooltip("Seconds between two probes of the drawn height map against the physics under the hulls (0: off).")]
        public float probePeriod = 10f;
        float m_nextProbe;
        bool m_probePending;
        int m_frames;
        float m_fpsWindowStart = -1f, m_worstFrame;

        /// <summary>Latest wave components, from the ROS thread.</summary>
        public static void ReportSpectrum(WaveSpectrumMsg msg)
        {
            lock (s_lock) s_pendingSpectrum = msg;
        }

        /// <summary>
        /// Where the surface vessels are (Gazebo ENU, the same origin as Unity), so the probe can
        /// compare the drawn surface with the physics under each hull. Called on the ROS thread.
        /// </summary>
        public static void ReportHulls(IEnumerable<(string name, double east, double north, double up)> vessels)
        {
            lock (s_lock)
            {
                s_hulls.Clear();
                foreach (var v in vessels)
                    if (Math.Abs(v.up) < 3.0) // at the surface
                        s_hulls.Add((v.name, v.east, v.north));
            }
        }

        /// <summary>Simulation time stamped on the poses being rendered (s).</summary>
        public static void ReportSimTime(double t)
        {
            lock (s_lock)
            {
                s_simTime = t;
                s_simTimeReceivedAt = -2f; // stamped on the main thread in Update
            }
        }

        /// <summary>
        /// Adds this component to the scene's ocean the first time components
        /// arrive, so scenes need no manual setup.
        /// </summary>
        public static void EnsureInScene()
        {
            if (FindFirstObjectByType<XdynWaveSurface>() != null)
                return;
            var water = FindFirstObjectByType<WaterSurface>();
            if (water == null)
            {
                Debug.LogWarning("[XdynWaveSurface] wave components received but the scene has no WaterSurface.");
                return;
            }
            water.gameObject.AddComponent<XdynWaveSurface>();
        }

        /// <summary>Simulation time to draw this frame, extrapolated between pose messages.</summary>
        public static double CurrentSimTime()
        {
            lock (s_lock)
            {
                if (s_simTimeReceivedAt == -2f)
                    s_simTimeReceivedAt = Time.realtimeSinceStartup;
                if (s_simTimeReceivedAt < 0f)
                    return 0.0;
                // Poses arrive several times a second; never run far ahead of them.
                double ahead = Math.Min(Time.realtimeSinceStartup - s_simTimeReceivedAt, 0.5);
                return s_simTime + ahead;
            }
        }

        void Awake()
        {
            m_water = GetComponent<WaterSurface>();
            if (heightCompute == null)
                heightCompute = Resources.Load<ComputeShader>("XdynWaves");
            if (decalMaterial == null)
                decalMaterial = Resources.Load<Material>("XdynWaveDecal");
            if (heightCompute == null || decalMaterial == null)
            {
                Debug.LogError("[XdynWaveSurface] missing Resources/XdynWaves.compute or Resources/XdynWaveDecal.mat.");
                enabled = false;
                return;
            }
            m_kernel = heightCompute.FindKernel("CSMain");

            // Player overrides, to compare region sizes on the same build:
            // -xdynWaveRegion <m> -xdynWaveResolution <texels>
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; ++i)
            {
                if (args[i] == "-xdynWaveRegion" && float.TryParse(args[i + 1],
                        System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float r))
                    regionSize = r;
                else if (args[i] == "-xdynWaveResolution" && int.TryParse(args[i + 1], out int n))
                    resolution = n;
            }
        }

        /// <summary>Logs the frame rate every 10 s while the waves are drawn.</summary>
        void LogFrameRate()
        {
            float now = Time.realtimeSinceStartup;
            if (m_fpsWindowStart < 0f)
            {
                m_fpsWindowStart = now; m_frames = 0; m_worstFrame = 0f;
                return;
            }
            ++m_frames;
            m_worstFrame = Mathf.Max(m_worstFrame, Time.unscaledDeltaTime);
            if (now - m_fpsWindowStart < 10f)
                return;
            Debug.Log($"[XdynWaveSurface] {m_frames / (now - m_fpsWindowStart):F1} fps " +
                      $"(slowest frame {m_worstFrame * 1000f:F0} ms), {resolution}x{resolution} over {regionSize} m, " +
                      $"sim time {CurrentSimTime():F1} s");
            m_fpsWindowStart = now; m_frames = 0; m_worstFrame = 0f;
        }

        void Update()
        {
            WaveSpectrumMsg spectrum;
            lock (s_lock)
            {
                spectrum = s_pendingSpectrum;
                s_pendingSpectrum = null;
            }
            if (spectrum != null && !SameSpectrum(spectrum))
                Apply(spectrum);
            if (m_a == null)
                return;

            Transform anchor = Camera.main != null ? Camera.main.transform : transform;
            Vector3 c = anchor.position;
            m_decal.transform.position = new Vector3(c.x, transform.position.y, c.z);
            m_decal.transform.rotation = Quaternion.identity;

            // Fold the time and the region centre into one phase per component,
            // in double precision: arg_i = k_i (x_c cos + y_c sin) - omega_i t + phase_i,
            // next to the component's fade gain at that time.
            double t = CurrentSimTime();
            double xc = c.z, yc = c.x;
            for (int i = 0; i < m_a.Length; ++i)
            {
                double arg = m_k[i] * (xc * m_cos[i] + yc * m_sin[i]) - m_omega[i] * t + m_phase[i];
                m_phaseGain[i] = new Vector2((float)(arg % (2.0 * Math.PI)), (float)Gain(i, t));
            }
            m_phases.SetData(m_phaseGain);

            int groups = (resolution + 7) / 8;
            heightCompute.Dispatch(m_kernel, groups, groups, 1);
            m_decal.RequestUpdate();
            LogFrameRate();
            if (probePeriod > 0f && !m_probePending && Time.realtimeSinceStartup >= m_nextProbe)
                Probe(c.x, c.z, t);
        }

        /// <summary>
        /// Reads back the height map just drawn and compares it, under every surface vessel inside the
        /// region, with the elevation the hydrodynamics computes there (all components, same time).
        /// Logs the RMS and largest difference, next to the RMS elevation itself.
        /// </summary>
        void Probe(float centreX, float centreZ, double t)
        {
            List<(string name, double east, double north)> hulls;
            lock (s_lock) hulls = new List<(string, double, double)>(s_hulls);
            float half = regionSize / 2f;
            hulls.RemoveAll(h => Math.Abs(h.east - centreX) > half - 2f || Math.Abs(h.north - centreZ) > half - 2f);
            m_nextProbe = Time.realtimeSinceStartup + probePeriod;
            if (hulls.Count == 0)
                return;
            m_probePending = true;
            float amplitude = m_amplitude;
            int res = resolution;
            double[] physics = new double[hulls.Count];
            for (int h = 0; h < hulls.Count; ++h)
            {
                double x = hulls[h].north, y = hulls[h].east, sum = 0.0;
                for (int i = 0; i < m_a.Length; ++i)
                    sum += Gain(i, t) * m_a[i] * Math.Sin(m_k[i] * (x * m_cos[i] + y * m_sin[i]) - m_omega[i] * t + m_phase[i]);
                physics[h] = sum;
            }
            AsyncGPUReadback.Request(m_height, 0, request =>
            {
                m_probePending = false;
                if (request.hasError)
                    return;
                NativeArray<float> map = request.GetData<float>();
                double se = 0.0, sz = 0.0, worst = 0.0;
                for (int h = 0; h < hulls.Count; ++h)
                {
                    // Texel coordinates of the hull (texel centres at (i + 0.5) / res), bilinear.
                    double u = (hulls[h].east - centreX) / regionSize * res + res / 2.0 - 0.5;
                    double v = (hulls[h].north - centreZ) / regionSize * res + res / 2.0 - 0.5;
                    int i0 = Math.Max(0, Math.Min(res - 2, (int)Math.Floor(u)));
                    int j0 = Math.Max(0, Math.Min(res - 2, (int)Math.Floor(v)));
                    double fu = u - i0, fv = v - j0;
                    double drawn = amplitude * (
                        (1 - fu) * (1 - fv) * map[j0 * res + i0] + fu * (1 - fv) * map[j0 * res + i0 + 1] +
                        (1 - fu) * fv * map[(j0 + 1) * res + i0] + fu * fv * map[(j0 + 1) * res + i0 + 1]);
                    double e = drawn - physics[h];
                    se += e * e;
                    sz += physics[h] * physics[h];
                    worst = Math.Max(worst, Math.Abs(e));
                }
                int n = hulls.Count;
                Debug.Log($"[XdynWaveProbe] t {t:F1} s, {n} hull(s): drawn - physics RMS {Math.Sqrt(se / n):F3} m, " +
                          $"max {worst:F3} m; elevation RMS {Math.Sqrt(sz / n):F3} m");
            });
        }

        /// <summary>Rendered height (m, up) at a Unity world position, same sum as the GPU.</summary>
        public float HeightAt(Vector3 worldPosition)
        {
            if (m_a == null)
                return 0f;
            double t = CurrentSimTime();
            double x = worldPosition.z, y = worldPosition.x;
            double h = 0.0;
            for (int i = 0; i < m_a.Length; ++i)
                h += Gain(i, t) * m_a[i] * Math.Sin(m_k[i] * (x * m_cos[i] + y * m_sin[i]) - m_omega[i] * t + m_phase[i]);
            return (float)h;
        }

        /// <summary>
        /// Fade of component i at time t: the old sea fades out while the new one
        /// fades in, linearly, as the wave server does for xdyn.
        /// </summary>
        double Gain(int i, double t)
        {
            if (m_fadeInStart == null || i >= m_fadeInStart.Length)
                return 1.0;
            double gIn = t < m_fadeInStart[i] ? 0.0
                       : m_fadeInDuration[i] <= 0.0 ? 1.0
                       : Math.Min((t - m_fadeInStart[i]) / m_fadeInDuration[i], 1.0);
            double gOut = m_fadeOutDuration[i] <= 0.0 ? (t >= m_fadeOutStart[i] ? 0.0 : 1.0)
                        : 1.0 - Math.Min(Math.Max((t - m_fadeOutStart[i]) / m_fadeOutDuration[i], 0.0), 1.0);
            return gIn * gOut;
        }

        bool SameSpectrum(WaveSpectrumMsg msg)
        {
            if (m_a == null || msg.a.Length != m_a.Length)
                return false;
            for (int i = 0; i < m_a.Length; ++i)
                if (msg.a[i] != m_a[i] || msg.phase[i] != m_phase[i])
                    return false;
            // A change to a calm sea only starts the fade-out of the components.
            int nf = msg.fade_out_start != null ? msg.fade_out_start.Length : 0;
            if (nf != (m_fadeOutStart != null ? m_fadeOutStart.Length : 0))
                return false;
            for (int i = 0; i < nf; ++i)
                if (msg.fade_out_start[i] != m_fadeOutStart[i])
                    return false;
            return true;
        }

        void Apply(WaveSpectrumMsg msg)
        {
            int n = msg.a.Length;
            if (n == 0)
                return;
            ReleaseBuffers();

            m_a = msg.a; m_k = msg.k; m_omega = msg.omega; m_phase = msg.phase;
            bool hasFades = msg.fade_in_start != null && msg.fade_in_start.Length == n;
            m_fadeInStart = hasFades ? msg.fade_in_start : null;
            m_fadeInDuration = hasFades ? msg.fade_in_duration : null;
            m_fadeOutStart = hasFades ? msg.fade_out_start : null;
            m_fadeOutDuration = hasFades ? msg.fade_out_duration : null;
            m_cos = new double[n]; m_sin = new double[n];
            var rays = new Vector4[n];
            double sum = 0.0;
            for (int i = 0; i < n; ++i)
            {
                m_cos[i] = Math.Cos(msg.psi[i]);
                m_sin[i] = Math.Sin(msg.psi[i]);
                rays[i] = new Vector4((float)m_a[i], (float)m_k[i], (float)m_cos[i], (float)m_sin[i]);
                sum += Math.Abs(m_a[i]);
            }
            // The sum of the amplitudes bounds the elevation everywhere.
            m_amplitude = (float)sum;
            m_phaseGain = new Vector2[n];

            m_rays = new ComputeBuffer(n, sizeof(float) * 4);
            m_rays.SetData(rays);
            m_phases = new ComputeBuffer(n, sizeof(float) * 2);

            m_height = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.RFloat)
            {
                enableRandomWrite = true,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "XdynWaveHeight",
            };
            m_height.Create();

            heightCompute.SetTexture(m_kernel, "_Height", m_height);
            heightCompute.SetBuffer(m_kernel, "_Rays", m_rays);
            heightCompute.SetBuffer(m_kernel, "_Phases", m_phases);
            heightCompute.SetInt("_RayCount", n);
            heightCompute.SetInt("_Resolution", resolution);
            heightCompute.SetFloat("_RegionSize", regionSize);
            heightCompute.SetFloat("_InvAmplitude", 1f / m_amplitude);
            // Wave numbers above this cannot be drawn by the height map without aliasing.
            // HDRP renders the decal into its water decal atlas at the decal's
            // resolution: a decal larger than the atlas is dropped with
            // "No more space in the Water Decal Atlas" and the ocean stays flat.
            int decalRes = resolution;
            if (GraphicsSettings.currentRenderPipeline is HDRenderPipelineAsset hdrp)
            {
                int atlas = (int)hdrp.currentPlatformRenderPipelineSettings.waterDecalAtlasSize;
                if (decalRes > atlas)
                {
                    Debug.LogWarning($"[XdynWaveSurface] decal resolution {decalRes} exceeds the HDRP water decal " +
                                     $"atlas ({atlas}); drawing at {atlas}. Raise the atlas size in the HDRP asset.");
                    decalRes = atlas;
                }
            }
            float texel = regionSize / decalRes;
            heightCompute.SetFloat("_MaxK", 2f * Mathf.PI / (minTexelsPerWavelength * texel));

            if (m_decal == null)
            {
                var go = new GameObject("XdynWaveDecal");
                go.transform.SetParent(transform, false);
                m_decal = go.AddComponent<WaterDecal>();
                m_decal.material = new Material(decalMaterial);
            }
            m_decal.scaleMode = DecalScaleMode.ScaleInvariant;
            m_decal.regionSize = new Vector2(regionSize, regionSize);
            m_decal.resolution = new Vector2Int(decalRes, decalRes);
            m_decal.updateMode = CustomRenderTextureUpdateMode.Realtime;
            m_decal.amplitude = m_amplitude;
            m_decal.material.SetTexture("_XdynHeight", m_height);

            m_water.deformation = true;
            m_water.deformationRes = resolution >= 2048 ? WaterSurface.WaterDecalRegionResolution.Resolution2048
                                   : resolution >= 1024 ? WaterSurface.WaterDecalRegionResolution.Resolution1024
                                   : WaterSurface.WaterDecalRegionResolution.Resolution512;
            m_water.decalRegionSize = new Vector2(regionSize, regionSize);
            m_water.decalRegionAnchor = null; // follows the camera, like the decal
            m_water.largeBand0Multiplier = 0f;
            m_water.largeBand1Multiplier = 0f;
            m_water.ripples = keepRipples;
            m_water.ripplesWindSpeed = ripplesWindSpeed;

            Debug.Log($"[XdynWaveSurface] {n} wave components (Hs {msg.significant_height_m} m, " +
                      $"Tp {msg.peak_period_s} s, towards {msg.direction_deg} deg) over {regionSize} m.");
        }

        void ReleaseBuffers()
        {
            m_rays?.Release(); m_rays = null;
            m_phases?.Release(); m_phases = null;
            if (m_height != null) { m_height.Release(); m_height = null; }
        }

        void OnDestroy()
        {
            ReleaseBuffers();
        }
    }
}
