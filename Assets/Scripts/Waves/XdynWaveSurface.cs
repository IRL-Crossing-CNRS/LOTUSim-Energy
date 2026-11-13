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
    /// water decal that deforms the surface. Inside that region the HDRP
    /// simulation's own swell is masked out, leaving only its small ripples for
    /// surface detail; beyond it, HDRP's swell carries the same Hs, Tp and
    /// direction to the horizon. The vessels are drawn at the same simulation
    /// time (RosInterface interpolates or extrapolates their poses to
    /// CurrentSimTime), with no added display delay.
    ///
    /// Frames: xdyn works in NED (x north, y east, z down), Unity in (X east,
    /// Y up, Z north), with the same origin, so x_ned = Z, y_ned = X and the
    /// rendered height is -zeta.
    /// </summary>
    [RequireComponent(typeof(WaterSurface))]
    public class XdynWaveSurface : MonoBehaviour
    {
        [Tooltip("Side of the square region drawn from the xdyn components (m). " +
                 "Beyond it HDRP's own swell carries the same Hs, Tp and direction to the horizon. HDRP " +
                 "deforms the water through one decal region texture of at most 2048 x 2048: " +
                 "1024 texels over 2048 m, a 2 m texel.")]
        public float regionSize = 2048f;

        [Tooltip("Height map resolution over the region (and HDRP decal region resolution).")]
        public int resolution = 1024;

        [Tooltip("Components shorter than this many texels are left out of the height map, " +
                 "which could not represent them (aliasing). HeightAt keeps every component.")]
        public float minTexelsPerWavelength = 4f;

        [Tooltip("Keep the HDRP ripples (small, short waves xdyn does not model) for surface detail.")]
        public bool keepRipples = true;

        [Tooltip("Wind speed driving the HDRP ripples when they are kept (km/h, HDRP's unit).")]
        public float ripplesWindSpeed = 2f;
        WindSliderController m_wind;

        [Tooltip("Width (m) of the band at the edge of the region over which the xdyn sea hands over to " +
                 "HDRP's own swell, which carries the same Hs, Tp and direction to the horizon.")]
        public float edgeFade = 300f;
        [Tooltip("Scale on the HDRP swell wind speed derived from Hs, to match the far sea's height.")]
        public float farSwellGain = 1f;
        Texture2D m_mask;
        float m_farWindKmh, m_farOrientation;
        Camera m_view;
        float m_viewCheckedAt = -1f;

        // The camera on screen, which the region follows. The scene tags none MainCamera, so
        // Camera.main was null and the region stayed at the ocean's origin.
        Camera ViewCamera()
        {
            if (m_view != null && m_view.isActiveAndEnabled && Time.realtimeSinceStartup - m_viewCheckedAt < 1f)
                return m_view;
            m_viewCheckedAt = Time.realtimeSinceStartup;
            m_view = null;
            foreach (var cam in Camera.allCameras)
                if (cam.targetTexture == null && (m_view == null || cam.depth > m_view.depth))
                    m_view = cam;
            return m_view;
        }

        public ComputeShader heightCompute;
        public Material decalMaterial;

        static readonly object s_lock = new object();
        static WaveSpectrumMsg s_pendingSpectrum;
        static double s_simTime;
        // Positions (east, north, up) of the vessels at the surface at the drawn time, for the probes.
        static readonly List<(string name, double east, double north, double up)> s_hulls =
            new List<(string, double, double, double)>();
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
        [Tooltip("Seconds between two probes of the water under each hull, one log line per hull (0: off).")]
        public float hullProbePeriod = 0f;
        float m_nextHullProbe;
        static bool s_hullProbeHeader;
        static bool s_ensured;
        int m_frames;
        float m_fpsWindowStart = -1f, m_worstFrame;

        /// <summary>Latest wave components, from the ROS thread.</summary>
        public static void ReportSpectrum(WaveSpectrumMsg msg)
        {
            lock (s_lock) s_pendingSpectrum = msg;
        }

        /// <summary>
        /// Where the surface vessels are drawn (Gazebo ENU, the same origin as Unity), so the probes
        /// can compare the drawn surface with the physics under each hull. Called on the main thread.
        /// </summary>
        public static void ReportHulls(IEnumerable<(string name, double east, double north, double up)> vessels)
        {
            lock (s_lock)
            {
                s_hulls.Clear();
                foreach (var v in vessels)
                    if (Math.Abs(v.up) < 3.0) // at the surface
                        s_hulls.Add((v.name, v.east, v.north, v.up));
            }
            // A run whose sea is not shared sends no components: the hull probe still needs the component.
            if (!s_ensured && HullProbeOnCommandLine())
            {
                s_ensured = true;
                EnsureInScene();
            }
        }

        static bool HullProbeOnCommandLine() => Array.IndexOf(Environment.GetCommandLineArgs(), "-xdynHullProbe") >= 0;

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

        // Clock the waves are drawn at. Pose messages reach Unity irregularly (the bridge
        // delivers them in bursts) and the simulation runs below real time, so jumping to
        // "last stamp + time since" made the sea step forward and back every message.
        // Instead the drawn time advances by the measured simulation rate and is pulled
        // gently toward the stamps, never backward.
        static double s_drawnTime = double.NaN;
        static int s_drawnFrame = -1;
        static double s_rate = 1.0;
        static double s_rateRefTime = double.NaN;
        static float s_rateRefAt;
        /// <summary>
        /// The frame is drawn at the current simulation time, estimated from the newest pose stamp and
        /// the simulation rate, so nothing is shown late; this bounds how far past the newest stamp it
        /// may go (s of simulation time) when poses stop coming. Player argument -xdynMaxExtrapolation.
        /// </summary>
        public static double maxExtrapolation = 0.5;

        /// <summary>Simulation time to draw this frame: smooth, monotonic, close to the pose stamps.</summary>
        public static double CurrentSimTime()
        {
            lock (s_lock)
            {
                float now = Time.realtimeSinceStartup;
                if (s_simTimeReceivedAt == -2f)
                {
                    s_simTimeReceivedAt = now;
                    // Simulation rate over at least 2 s of stamps, so bursts average out.
                    if (double.IsNaN(s_rateRefTime) || s_simTime < s_rateRefTime)
                    {
                        s_rateRefTime = s_simTime; s_rateRefAt = now;
                    }
                    else if (now - s_rateRefAt >= 2f)
                    {
                        double sample = (s_simTime - s_rateRefTime) / (now - s_rateRefAt);
                        s_rate = Math.Max(0.0, Math.Min(3.0, 0.7 * s_rate + 0.3 * sample));
                        s_rateRefTime = s_simTime; s_rateRefAt = now;
                    }
                }
                if (s_simTimeReceivedAt < 0f)
                    return 0.0;
                if (s_drawnFrame == Time.frameCount)
                    return s_drawnTime;

                double target = s_simTime + s_rate * Math.Min(now - s_simTimeReceivedAt, maxExtrapolation);
                if (double.IsNaN(s_drawnTime) || Math.Abs(target - s_drawnTime) > 1.0)
                {
                    s_drawnTime = target;
                }
                else
                {
                    double dt = Math.Min(Time.unscaledDeltaTime, 0.25);
                    double next = s_drawnTime + s_rate * dt + (target - s_drawnTime) * Math.Min(1.0, dt / 0.5);
                    if (next > s_drawnTime) s_drawnTime = next;
                }
                // The vessels are extrapolated to this time: keep it close to the newest poses.
                s_drawnTime = Math.Min(s_drawnTime, s_simTime + maxExtrapolation);
                s_drawnFrame = Time.frameCount;
                return s_drawnTime;
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
            // -xdynWaveRegion <m> -xdynWaveResolution <texels>; -xdynWaveProbe <s> turns the probe on.
            // The probe needs HDRP's CPU water simulation, which rebuilds its spectrum every frame
            // (~100 ms with the demo running): off unless asked.
            probePeriod = 0f;
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; ++i)
            {
                if (args[i] == "-xdynWaveRegion" && float.TryParse(args[i + 1],
                        System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float r))
                    regionSize = r;
                else if (args[i] == "-xdynWaveResolution" && int.TryParse(args[i + 1], out int n))
                    resolution = n;
                else if (args[i] == "-xdynWaveProbe" && float.TryParse(args[i + 1],
                        System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float p))
                    probePeriod = p;
                else if (args[i] == "-xdynHullProbe" && float.TryParse(args[i + 1],
                        System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float hp))
                    hullProbePeriod = hp;
                else if (args[i] == "-xdynMaxExtrapolation" && double.TryParse(args[i + 1],
                        System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double me))
                    maxExtrapolation = me;
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
            if (hullProbePeriod > 0f && Time.realtimeSinceStartup >= m_nextHullProbe)
                ProbeHulls();
            if (m_a == null)
                return;

            FollowWindWithRipples();

            Transform anchor = ViewCamera() != null ? m_view.transform : transform;
            Vector3 c = anchor.position;
            m_decal.transform.position = new Vector3(c.x, transform.position.y, c.z);
            m_water.waterMaskOffset = new Vector2(c.x - transform.position.x, c.z - transform.position.z);
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
            List<(string name, double east, double north, double up)> hulls;
            lock (s_lock) hulls = new List<(string, double, double, double)>(s_hulls);
            float half = regionSize / 2f;
            hulls.RemoveAll(h => Math.Abs(h.east - centreX) > half - edgeFade || Math.Abs(h.north - centreZ) > half - edgeFade);
            m_nextProbe = Time.realtimeSinceStartup + probePeriod;
            if (hulls.Count == 0)
                return;
            m_probePending = true;
            float amplitude = m_amplitude;
            int res = resolution;
            double[] physics = new double[hulls.Count];
            for (int h = 0; h < hulls.Count; ++h)
                physics[h] = ComponentsHeight(hulls[h].north, hulls[h].east, t);
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
                // What HDRP really displaces the ocean by there (decal included), against the map.
                double hse = 0.0, hsz = 0.0; int hn = 0;
                for (int h = 0; h < hulls.Count; ++h)
                {
                    var wsp = new WaterSearchParameters
                    {
                        targetPositionWS = new Vector3((float)hulls[h].east, transform.position.y, (float)hulls[h].north),
                        startPositionWS = new Vector3((float)hulls[h].east, transform.position.y, (float)hulls[h].north),
                        error = 0.01f, maxIterations = 8, includeDeformation = true,
                    };
                    if (!m_water.ProjectPointOnWaterSurface(wsp, out WaterSearchResult wsr)) continue;
                    double hdrp = wsr.projectedPositionWS.y - transform.position.y;
                    hse += (hdrp - physics[h]) * (hdrp - physics[h]); hsz += hdrp * hdrp; ++hn;
                }
                // The far sea, HDRP's swell, on a ring beyond the region, against the xdyn sea's Hs / 4.
                double fsz = 0.0; int fn = 0;
                for (int k = 0; k < 16; ++k)
                {
                    float ang = k * Mathf.PI / 8f, r = half + 800f;
                    var p = new Vector3(centreX + r * Mathf.Cos(ang), transform.position.y, centreZ + r * Mathf.Sin(ang));
                    var fsp = new WaterSearchParameters { targetPositionWS = p, startPositionWS = p, error = 0.01f, maxIterations = 8, includeDeformation = true };
                    if (!m_water.ProjectPointOnWaterSurface(fsp, out WaterSearchResult fsr)) continue;
                    double fh = fsr.projectedPositionWS.y - transform.position.y;
                    fsz += fh * fh; ++fn;
                }
                Debug.Log($"[XdynWaveFar] t {t:F1} s: HDRP swell RMS {(fn > 0 ? Math.Sqrt(fsz / fn) : double.NaN):F3} m over {fn} points " +
                          $"(wind {m_farWindKmh:F0} km/h), xdyn sea elevation RMS {Math.Sqrt(sz / n):F3} m");
                Debug.Log($"[XdynWaveProbe] t {t:F1} s, {n} hull(s): drawn - physics RMS {Math.Sqrt(se / n):F3} m, " +
                          $"max {worst:F3} m; elevation RMS {Math.Sqrt(sz / n):F3} m; HDRP surface ({hn} hulls) RMS " +
                          $"{(hn > 0 ? Math.Sqrt(hsz / hn) : double.NaN):F3} m, - physics RMS {(hn > 0 ? Math.Sqrt(hse / hn) : double.NaN):F3} m");
            });
        }

        // HDRP's ripples travel with the wind of the sliders (their speed follows the Beaufort
        // force in StormController).
        void FollowWindWithRipples()
        {
            if (!keepRipples)
                return;
            if (m_wind == null)
            {
                if (Time.frameCount % 60 != 0) return;
                m_wind = FindFirstObjectByType<WindSliderController>();
                if (m_wind == null) return;
            }
            Vector3 w = m_wind.CurrentWindVector; // ENU
            if (new Vector2(w.x, w.y).magnitude > 0.1f)
            {
                m_water.ripplesMotionMode = WaterPropertyOverrideMode.Custom;
                // HDRP orientation: degrees from +x (east) towards +z (north).
                m_water.ripplesOrientationValue = Mathf.Atan2(w.y, w.x) * Mathf.Rad2Deg;
            }
        }

        /// <summary>Rendered height (m, up) at a Unity world position, same sum as the GPU.</summary>
        public float HeightAt(Vector3 worldPosition)
        {
            if (m_a == null)
                return 0f;
            return (float)ComponentsHeight(worldPosition.z, worldPosition.x, CurrentSimTime());
        }

        /// <summary>Elevation (m, up) of every component at (x north, y east) and time t: -zeta of xdyn.</summary>
        double ComponentsHeight(double x, double y, double t)
        {
            double h = 0.0;
            for (int i = 0; i < m_a.Length; ++i)
                h += Gain(i, t) * m_a[i] * Math.Sin(m_k[i] * (x * m_cos[i] + y * m_sin[i]) - m_omega[i] * t + m_phase[i]);
            return h;
        }

        /// <summary>
        /// One line per surface vessel: the height of its pose as drawn, the height of the ocean HDRP
        /// draws under it (decal, masked swell and ripples included) and the elevation of the shared
        /// components (NaN when the sea is not shared), all at the drawn simulation time.
        /// </summary>
        void ProbeHulls()
        {
            m_nextHullProbe = Time.realtimeSinceStartup + hullProbePeriod;
            double t = CurrentSimTime();
            if (t <= 0.0)
                return;
            // HDRP answers height queries only with script interactions, ripples only if evaluated on the CPU.
            m_water.scriptInteractions = true;
            m_water.cpuEvaluateRipples = true;
            if (!s_hullProbeHeader)
            {
                s_hullProbeHeader = true;
                Debug.Log($"[WaterHullProbe] probing the drawn water under the hulls every {hullProbePeriod} s " +
                          "(t, name, east, north, hull_up, rendered_up, components_up)");
            }
            List<(string name, double east, double north, double up)> hulls;
            lock (s_lock) hulls = new List<(string, double, double, double)>(s_hulls);
            float y0 = transform.position.y;
            foreach (var h in hulls)
            {
                var p = new Vector3((float)h.east, y0, (float)h.north);
                var wsp = new WaterSearchParameters
                {
                    targetPositionWS = p, startPositionWS = p, error = 0.01f, maxIterations = 8, includeDeformation = true,
                };
                double rendered = m_water.ProjectPointOnWaterSurface(wsp, out WaterSearchResult r)
                    ? r.projectedPositionWS.y - y0 : double.NaN;
                double comps = m_a == null ? double.NaN : ComponentsHeight(h.north, h.east, t);
                Debug.Log(FormattableString.Invariant(
                    $"[WaterHullProbe] {t:F2},{h.name},{h.east:F2},{h.north:F2},{h.up:F4},{rendered:F4},{comps:F4}"));
            }
        }

        /// <summary>
        /// Gain of component i at time t while the sea state changes, the one xdyn
        /// applies (FlatDiscreteDirectionalWaveSpectrum::gain): sqrt(u_in (1 - u_out)).
        /// The square root keeps the variance of the surface moving linearly from the
        /// old sea to the new one; the hulls ride these amplitudes, so the image must too.
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
            return Math.Sqrt(gIn * gOut);
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
            heightCompute.SetFloat("_EdgeFade", edgeFade);
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
            m_water.scriptInteractions = probePeriod > 0f || hullProbePeriod > 0f; // for the probes' HDRP height query
            m_water.deformationRes = resolution >= 2048 ? WaterSurface.WaterDecalRegionResolution.Resolution2048
                                   : resolution >= 1024 ? WaterSurface.WaterDecalRegionResolution.Resolution1024
                                   : WaterSurface.WaterDecalRegionResolution.Resolution512;
            m_water.decalRegionSize = new Vector2(regionSize, regionSize);
            m_water.decalRegionAnchor = null; // follows the camera, like the decal
            // Beyond the region, HDRP's own swell, statistically the same sea: the mask keeps it out
            // of the region, where the xdyn sea is drawn, with a cross-fade over edgeFade.
            m_water.largeBand0Multiplier = 1f;
            m_water.largeBand1Multiplier = 1f;
            m_water.largeBand0FadeMode = WaterSurface.FadeMode.None;
            m_water.largeBand1FadeMode = WaterSurface.FadeMode.None;
            // HDRP fades its ripples out from 50 m: beyond the region the sea then turns into a
            // mirror. Kept to the horizon, they give the far sea its texture.
            m_water.ripplesFadeMode = WaterSurface.FadeMode.None;
            // HDRP's water mask (R, G: swell bands, B: ripples) over the region, moved with it in Update.
            // Its decal-mask path left the mask white in the player, so the swell stayed in the farm.
            m_water.waterMask = EdgeMask();
            m_water.waterMaskExtent = new Vector2(regionSize, regionSize);
            m_water.waterMaskRemap = new Vector2(0f, 1f);
            // Pierson-Moskowitz, Hs = 0.21 U^2 / g: the wind of a fully developed sea of that height.
            m_farWindKmh = farSwellGain * 3.6f * Mathf.Sqrt((float)msg.significant_height_m * 9.81f / 0.21f);
            // xdyn's direction is towards, clockwise from north; HDRP's angle is from east towards north.
            m_farOrientation = 90f - (float)msg.direction_deg;
            m_water.ripples = keepRipples;
            m_water.ripplesWindSpeed = ripplesWindSpeed;

            Debug.Log($"[XdynWaveSurface] {n} wave components (Hs {msg.significant_height_m} m, " +
                      $"Tp {msg.peak_period_s} s, towards {msg.direction_deg} deg) over {regionSize} m.");
        }

        // StormController writes the HDRP swell every Update; the far sea must follow the xdyn sea.
        void LateUpdate()
        {
            if (m_a == null || m_water == null)
                return;
            m_water.largeWindSpeed = m_farWindKmh;
            m_water.largeOrientationValue = m_farOrientation;
        }

        // 1 where the xdyn sea is drawn, 0 at the region's edge, smooth in between; the mask lets HDRP's
        // swell (both bands) through as (1 - w), and its ripples everywhere.
        float EdgeWeight(float u, float v)
        {
            float half = regionSize / 2f;
            float d = Mathf.Min(half - Mathf.Abs(u), half - Mathf.Abs(v));
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(d / edgeFade));
        }

        Texture2D EdgeMask()
        {
            if (m_mask != null) return m_mask;
            const int n = 256;
            m_mask = new Texture2D(n, n, TextureFormat.RGBAFloat, false, true) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[n * n];
            for (int j = 0; j < n; ++j)
                for (int i = 0; i < n; ++i)
                {
                    float far = 1f - EdgeWeight(((i + 0.5f) / n - 0.5f) * regionSize, ((j + 0.5f) / n - 0.5f) * regionSize);
                    // Neither swell band inside: their crests are not in the physics, so a hull would
                    // not ride them. Only the ripples (centimetres, B) are kept for surface detail.
                    px[j * n + i] = new Color(far, far, 1f, 1f);
                }
            m_mask.SetPixels(px);
            m_mask.Apply();
            return m_mask;
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
