using UnityEngine;

namespace Lotusim
{
    /// <summary>
    /// F12 shows the frame rate in the top-left corner, averaged over half a second,
    /// and the lowest instantaneous frame rate (slowest frame) over that window.
    /// </summary>
    public class FpsOverlay : MonoBehaviour
    {
        bool m_visible;
        int m_frames;
        float m_elapsed, m_fps, m_worstMs, m_lowestFps;
        GUIStyle m_style;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var go = new GameObject("FpsOverlay");
            DontDestroyOnLoad(go);
            go.AddComponent<FpsOverlay>();
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyBindings.ShowFps)) m_visible = !m_visible;
            float dt = Time.unscaledDeltaTime;
            m_frames++;
            m_elapsed += dt;
            m_worstMs = Mathf.Max(m_worstMs, dt * 1000f);
            if (m_elapsed >= 0.5f)
            {
                m_fps = m_frames / m_elapsed;
                m_lowestFps = m_worstMs > 0f ? 1000f / m_worstMs : 0f;
                m_frames = 0; m_elapsed = 0f; m_worstMs = 0f;
            }
        }

        void OnGUI()
        {
            if (!m_visible) return;
            m_style ??= new GUIStyle(GUI.skin.box) { fontSize = 22, alignment = TextAnchor.MiddleLeft };
            GUI.Box(new Rect(10, 40, 260, 40), $" {m_fps:F0} fps   (lowest {m_lowestFps:F0} fps)", m_style);
        }
    }
}
