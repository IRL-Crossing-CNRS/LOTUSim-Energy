using UnityEngine;

/// <summary>
/// Simple third-person camera. Attach this to the Camera, and assign "Target" to
/// the drone (the same object DroneManualController is on).
/// Sits behind and slightly above the target, smoothly following its position and rotation.
///
/// Press the toggle key to switch this camera on/off. If you assign "Other Camera"
/// (e.g. the drone's onboard/first-person camera), toggling this one off automatically
/// switches that one on, and vice versa, so only one camera is ever rendering.
/// </summary>
public class ThirdPersonCameraFollow : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The drone this camera follows.")]
    public Transform target;

    [Tooltip("Offset from the target, in the target's own local space (x = left/right, y = up, z = forward/back — negative z is behind).")]
    public Vector3 offset = new Vector3(0f, 3f, -8f);

    [Tooltip("How quickly the camera catches up to the target's position. Lower = snappier, higher = floatier.")]
    public float positionSmoothTime = 0.15f;

    [Tooltip("How quickly the camera rotates to keep looking at the target.")]
    public float rotationSmoothSpeed = 8f;

    [Header("Toggle")]
    [Tooltip("Key that switches this third-person view on/off.")]
    public KeyCode toggleKey = KeyCode.T;

    [Tooltip("Optional: the drone's other camera (e.g. first-person/onboard view). When set, toggling this camera off automatically enables that one, and vice versa.")]
    public Camera otherCamera;

    [Tooltip("Whether this camera is the active one when the scene starts.")]
    public bool startActive = true;

    private Camera cam;
    private AudioListener listener;
    private bool isActive;
    private Vector3 velocity;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        listener = GetComponent<AudioListener>();
        SetActive(startActive);
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            SetActive(!isActive);
    }

    private void LateUpdate()
    {
        if (!isActive || target == null) return;

        Vector3 desiredPosition = target.TransformPoint(offset);
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, positionSmoothTime);

        Quaternion desiredRotation = Quaternion.LookRotation(target.position - transform.position, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationSmoothSpeed * Time.deltaTime);
    }

    private void SetActive(bool active)
    {
        isActive = active;
        if (cam != null) cam.enabled = active;
        if (listener != null) listener.enabled = active;

        if (otherCamera != null)
        {
            otherCamera.enabled = !active;
            AudioListener otherListener = otherCamera.GetComponent<AudioListener>();
            if (otherListener != null) otherListener.enabled = !active;
        }
    }
}