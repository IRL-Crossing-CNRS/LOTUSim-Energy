using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;

/// <summary>
/// Turns the desktop camera rig into a VR rig while a headset is active.
///
/// Put this on the rig GameObject (e.g. CameraTargetFollower) and the headset
/// camera on a child (e.g. "XR Head"). When XR is running:
///   • the child camera follows the headset pose (look around freely);
///   • the controllers fly the rig around:
///       - left stick         : move forward/back/sideways (where you look)
///       - right stick X      : turn (snap or smooth)
///       - right stick Y      : move up/down
///       - either trigger     : move faster
///   • the desktop camera scripts on the rig are disabled, since they would
///     fight the controllers and tilt the horizon;
///   • every other camera rendering to the screen is disabled: HDRP renders
///     all of them into the headset, ignoring targetDisplay.
/// When XR stops, all of the above is restored and the desktop setup works.
///
/// StartVR / StopVR switch the headset on and off at runtime (DisplaySwitcher
/// calls them when changing view), so one executable can go back and forth
/// between VR and the desktop cameras during a demo.
/// </summary>
public class VRCameraRig : MonoBehaviour
{
    public static VRCameraRig Instance { get; private set; }

    /// <summary>The headset camera, also the desktop main camera when not in VR.</summary>
    public Camera HeadCamera => headCamera;

    /// <summary>True while an XR loader is running, i.e. a headset is in use.</summary>
    public static bool IsXRRunning
    {
        get
        {
            XRManagerSettings manager = GetXRManager();
            return manager != null && manager.activeLoader != null;
        }
    }

    #region Inspector

    [Header("References")]
    [Tooltip("The headset camera, child of this rig. Auto-found if left empty.")]
    [SerializeField] private Camera headCamera;

    [Header("Movement")]
    [Tooltip("Flying speed in m/s.")]
    [SerializeField] private float moveSpeed = 10f;

    [Tooltip("Flying speed in m/s while a trigger is held.")]
    [SerializeField] private float boostedSpeed = 100f;

    [Tooltip("Stick values below this are ignored.")]
    [SerializeField] private float deadzone = 0.2f;

    [Header("Turning")]
    [Tooltip("Snap turn (more comfortable) instead of smooth turn.")]
    [SerializeField] private bool snapTurn = true;

    [Tooltip("Angle of one snap turn, in degrees.")]
    [SerializeField] private float snapTurnAngle = 30f;

    [Tooltip("Smooth turn speed, in degrees per second.")]
    [SerializeField] private float smoothTurnSpeed = 60f;

    [Header("Desktop Setup")]
    [Tooltip("Desktop camera scripts on this rig to disable while in VR.")]
    [SerializeField] private Behaviour[] desktopControllers;

    [Tooltip("Disable every other camera rendering to the screen while in VR.")]
    [SerializeField] private bool disableOtherCameras = true;

    #endregion
    // -------------------------------------------------------------------------------------

    #region Private Fields

    private bool vrActive;
    private bool snapTurnReady = true;
    private Quaternion desktopRigRotation;
    private readonly List<Behaviour> disabledControllers = new List<Behaviour>();
    private readonly HashSet<Camera> disabledCameras = new HashSet<Camera>();
    private readonly List<XRInputSubsystem> inputSubsystems = new List<XRInputSubsystem>();
    private readonly List<InputDevice> leftHandDevices = new List<InputDevice>();
    private readonly List<InputDevice> rightHandDevices = new List<InputDevice>();

    #endregion
    // -------------------------------------------------------------------------------------

    #region Unity Callbacks

    private void Awake()
    {
        Instance = this;

        if (headCamera == null)
            headCamera = GetComponentInChildren<Camera>(true);

        if (desktopControllers == null || desktopControllers.Length == 0)
        {
            desktopControllers = new Behaviour[]
            {
                GetComponent<CameraModeSwitcher>(),
                GetComponent<CameraDynamicTargetsNavigator>(),
                GetComponent<FreeFlyCamera>(),
                GetComponent<CameraOrbitFollow>(),
            };
        }
    }

    private void OnEnable()
    {
        Application.onBeforeRender += UpdateHeadPose;
    }

    private void OnDisable()
    {
        Application.onBeforeRender -= UpdateHeadPose;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (!XRSettings.isDeviceActive)
        {
            if (vrActive)
                ExitVR();
            return;
        }

        if (!vrActive)
            EnterVR();

        UpdateHeadPose();
        UpdateLocomotion();
    }

    private void LateUpdate()
    {
        // Other scripts (e.g. DisplaySwitcher) may bring cameras back at any time.
        if (vrActive && disableOtherCameras)
            DisableOtherCameras();
    }

    #endregion
    // -------------------------------------------------------------------------------------

    #region Switching

    /// <summary>
    /// Starts the headset. Returns false if no headset could be started.
    /// </summary>
    public bool StartVR()
    {
        XRManagerSettings manager = GetXRManager();
        if (manager == null)
            return false;

        if (manager.activeLoader != null)
            return true;

        manager.InitializeLoaderSync();
        if (manager.activeLoader == null)
        {
            Debug.LogWarning("[VRCameraRig] Could not start VR: no headset found.");
            return false;
        }

        manager.StartSubsystems();
        return true;
    }

    /// <summary>
    /// Stops the headset and gives the cameras back to the desktop setup.
    /// </summary>
    public void StopVR()
    {
        if (vrActive)
            ExitVR();

        XRManagerSettings manager = GetXRManager();
        if (manager == null || manager.activeLoader == null)
            return;

        manager.StopSubsystems();
        manager.DeinitializeLoader();
    }

    private static XRManagerSettings GetXRManager()
    {
        return XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
    }

    #endregion
    // -------------------------------------------------------------------------------------

    #region VR Setup

    /// <summary>
    /// Switches from the desktop setup to the VR setup.
    /// </summary>
    private void EnterVR()
    {
        vrActive = true;

        if (headCamera == null)
        {
            Debug.LogError("[VRCameraRig] No head camera found under " + name + ".");
            enabled = false;
            return;
        }

        disabledControllers.Clear();
        foreach (var controller in desktopControllers)
        {
            if (controller != null && controller.enabled)
            {
                controller.enabled = false;
                disabledControllers.Add(controller);
                Debug.Log("[VRCameraRig] Disabled desktop controller " + controller.GetType().Name + ".");
            }
        }

        // Keep only the heading: a pitched or rolled rig tilts the horizon in the headset.
        desktopRigRotation = transform.rotation;
        transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        // The rig is at eye level, so track the head relative to the device, not the floor.
        SubsystemManager.GetSubsystems(inputSubsystems);
        foreach (var subsystem in inputSubsystems)
            subsystem.TrySetTrackingOriginMode(TrackingOriginModeFlags.Device);

        headCamera.enabled = true;
        headCamera.targetDisplay = 0;

        InputDevice head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        Debug.Log("[VRCameraRig] VR active on " + XRSettings.loadedDeviceName +
                  ", head device: " + (head.isValid ? head.name : "NOT FOUND") + ".");

        Invoke(nameof(LogDiagnostics), 3f);
    }

    /// <summary>
    /// Switches back from the VR setup to the desktop setup, undoing EnterVR.
    /// </summary>
    private void ExitVR()
    {
        vrActive = false;
        CancelInvoke(nameof(LogDiagnostics));

        foreach (var controller in disabledControllers)
            if (controller != null)
                controller.enabled = true;
        disabledControllers.Clear();

        foreach (var cam in disabledCameras)
            if (cam != null)
                cam.enabled = true;
        disabledCameras.Clear();

        transform.rotation = desktopRigRotation;
        headCamera.transform.localPosition = Vector3.zero;
        headCamera.transform.localRotation = Quaternion.identity;

        Debug.Log("[VRCameraRig] VR stopped, desktop cameras restored.");
    }

    /// <summary>
    /// Logs the tracking and rendering state once, to debug a head-locked view.
    /// </summary>
    private void LogDiagnostics()
    {
        var displays = new List<XRDisplaySubsystem>();
        SubsystemManager.GetSubsystems(displays);
        int renderPasses = displays.Count > 0 ? displays[0].GetRenderPassCount() : -1;

        var renderingCameras = new List<string>();
        foreach (var cam in Camera.allCameras)
            if (cam.targetTexture == null)
                renderingCameras.Add(cam.name);

        Debug.Log("[VRCameraRig] Diagnostics: head local rotation " +
                  headCamera.transform.localEulerAngles + ", XR render passes " + renderPasses +
                  " (1 = single pass), cameras rendering to the headset: " +
                  string.Join(", ", renderingCameras) + ".");

        var allDevices = new List<InputDevice>();
        InputDevices.GetDevices(allDevices);
        var deviceDescriptions = new List<string>();
        foreach (var device in allDevices)
        {
            bool hasStick = device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 _);
            deviceDescriptions.Add(device.name + " [" + device.characteristics + "]" +
                                   (hasStick ? " (thumbstick)" : ""));
        }
        Debug.Log("[VRCameraRig] XR devices: " +
                  (deviceDescriptions.Count > 0 ? string.Join("; ", deviceDescriptions) : "none") + ".");
    }

    /// <summary>
    /// Disables every enabled camera, other than the headset one, that renders
    /// to the screen (render-texture cameras are left alone).
    /// </summary>
    private void DisableOtherCameras()
    {
        foreach (var cam in Camera.allCameras)
        {
            if (cam == headCamera || cam.targetTexture != null)
                continue;

            cam.enabled = false;
            if (disabledCameras.Add(cam))
                Debug.Log("[VRCameraRig] Disabled camera " + cam.name + " so it does not render into the headset.");
        }
    }

    #endregion
    // -------------------------------------------------------------------------------------

    #region Head Tracking

    /// <summary>
    /// Copies the headset pose onto the head camera. Called in Update and again
    /// just before rendering, so the view uses the latest pose.
    /// </summary>
    private void UpdateHeadPose()
    {
        if (!vrActive || headCamera == null)
            return;

        InputDevice head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        if (!head.isValid)
            return;

        Transform headTransform = headCamera.transform;

        if (head.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position))
            headTransform.localPosition = position;

        if (head.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation))
            headTransform.localRotation = rotation;
    }

    #endregion
    // -------------------------------------------------------------------------------------

    #region Locomotion

    /// <summary>
    /// Flies the rig with the controller sticks.
    /// </summary>
    private void UpdateLocomotion()
    {
        Vector2 leftStick = ReadStick(XRNode.LeftHand);
        Vector2 rightStick = ReadStick(XRNode.RightHand);

        bool boost = IsTriggerPressed(XRNode.LeftHand) || IsTriggerPressed(XRNode.RightHand);
        float speed = boost ? boostedSpeed : moveSpeed;

        // Horizontal movement follows where the head looks, ignoring its pitch.
        Vector3 forward = Vector3.ProjectOnPlane(headCamera.transform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward);

        Vector3 move = forward * leftStick.y + right * leftStick.x + Vector3.up * rightStick.y;
        transform.position += move * speed * Time.deltaTime;

        UpdateTurn(rightStick.x);
    }

    /// <summary>
    /// Turns the rig around the head, so the view does not swing sideways.
    /// </summary>
    private void UpdateTurn(float input)
    {
        float angle = 0f;

        if (snapTurn)
        {
            if (Mathf.Abs(input) < 0.5f)
                snapTurnReady = true;
            else if (snapTurnReady)
            {
                angle = Mathf.Sign(input) * snapTurnAngle;
                snapTurnReady = false;
            }
        }
        else
        {
            angle = input * smoothTurnSpeed * Time.deltaTime;
        }

        if (angle != 0f)
            transform.RotateAround(headCamera.transform.position, Vector3.up, angle);
    }

    private Vector2 ReadStick(XRNode hand)
    {
        Vector2 stick = Vector2.zero;
        foreach (var device in GetHandDevices(hand))
        {
            if (device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 value) &&
                value.sqrMagnitude > stick.sqrMagnitude)
                stick = value;
        }

        stick.x = Mathf.Abs(stick.x) < deadzone ? 0f : stick.x;
        stick.y = Mathf.Abs(stick.y) < deadzone ? 0f : stick.y;
        return stick;
    }

    private bool IsTriggerPressed(XRNode hand)
    {
        foreach (var device in GetHandDevices(hand))
        {
            if (device.TryGetFeatureValue(CommonUsages.triggerButton, out bool pressed) && pressed)
                return true;
        }
        return false;
    }

    /// <summary>
    /// All devices held in one hand. Several can exist at once (e.g. a Touch
    /// controller plus a hand-tracking device), and only the controller has a
    /// thumbstick, so the sticks are read from all of them.
    /// </summary>
    private List<InputDevice> GetHandDevices(XRNode hand)
    {
        var side = hand == XRNode.LeftHand
            ? InputDeviceCharacteristics.Left
            : InputDeviceCharacteristics.Right;

        List<InputDevice> devices = hand == XRNode.LeftHand ? leftHandDevices : rightHandDevices;
        InputDevices.GetDevicesWithCharacteristics(side | InputDeviceCharacteristics.HeldInHand, devices);
        return devices;
    }

    #endregion
}
