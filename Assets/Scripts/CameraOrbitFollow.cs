using UnityEngine;
using Lotusim;

/// <summary>
/// Simple camera follow/orbit script.
/// Attach this to the Camera. Leave "Target" empty in the Inspector — the drones spawned
/// from your JSON (bluerov/wamv/x500, underwater and above-water alike) don't exist until
/// play mode starts, so this script finds one at runtime instead of using a pre-assigned
/// reference. It does this by searching for RendererPosesWaypointFollower, the component
/// every agent LotusimConnector spawns carries (same detection trick AgentBatteryHUD uses).
///
/// Press F4 to start orbiting the first spawned agent it finds. Press F4 again to jump to
/// the next agent, again for the next, and so on, cycling back to the first after the last.
/// </summary>
public class CameraOrbitFollow : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Leave empty to auto-track a spawned agent. You can still drag something in manually if you want to lock onto a specific pre-existing object instead.")]
    public Transform target;

    [Tooltip("When Target is empty (or the tracked agent despawns), automatically find a spawned agent in the scene.")]
    public bool autoFindTarget = true;

    [Header("Orbit Settings")]
    [Tooltip("Horizontal distance from the target.")]
    public float distance = 15f;

    [Tooltip("Height above the target.")]
    public float height = 5f;

    [Tooltip("Degrees per second the camera orbits around the target.")]
    public float orbitSpeed = 20f;

    [Header("Toggle")]
    [Tooltip("Key that starts orbiting, then cycles to the next agent on each press.")]
    public KeyCode toggleKey = KeyCode.F4;

    [Tooltip("Key that stops orbiting entirely.")]
    public KeyCode stopKey = KeyCode.Escape;

    private bool isOrbiting = false;
    private float currentAngle = 0f;
    private int lastPickedIndex = -1;

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            if (!isOrbiting)
            {
                isOrbiting = true;
                if (autoFindTarget) AcquireTarget();
            }
            else if (autoFindTarget)
            {
                // Already orbiting — jump to the next agent instead of stopping.
                AcquireTarget(cycle: true);
            }
        }

        if (isOrbiting && Input.GetKeyDown(stopKey))
        {
            isOrbiting = false;
        }

        if (!isOrbiting) return;

        // Re-acquire if we don't have a target yet, or the one we had was destroyed/despawned.
        if (target == null && autoFindTarget)
            AcquireTarget();

        if (target == null) return;

        currentAngle += orbitSpeed * Time.deltaTime;

        Vector3 offset = new Vector3(
            Mathf.Sin(currentAngle * Mathf.Deg2Rad) * distance,
            height,
            Mathf.Cos(currentAngle * Mathf.Deg2Rad) * distance
        );

        transform.position = target.position + offset;
        transform.LookAt(target.position);
    }

    /// <summary>
    /// Finds spawned agents via RendererPosesWaypointFollower (every drone from the JSON —
    /// bluerov, wamv, x500 — carries this component once LotusimConnector spawns it).
    /// With cycle=true, advances to the next one in the list instead of always picking the first.
    /// </summary>
    private void AcquireTarget(bool cycle = false)
    {
        var agents = FindObjectsByType<RendererPosesWaypointFollower>(FindObjectsSortMode.None);
        if (agents.Length == 0)
        {
            target = null;
            return;
        }

        int idx = cycle ? (lastPickedIndex + 1) % agents.Length : 0;
        lastPickedIndex = idx;
        target = agents[idx].transform;
    }
}