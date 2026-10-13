using UnityEngine;

namespace Lotusim
{
    [RequireComponent(typeof(RectTransform))]
    public class CompassCameraSync : MonoBehaviour
    {
        [Header("Camera Source")]
        [Tooltip("Leave empty to auto-use Camera.main.")]
        [SerializeField] private Transform cameraTransform;

        [Header("Smoothing")]
        [SerializeField] private float rotationSmoothSpeed = 5f;

        private RectTransform rt;

        private void Awake()
        {
            rt = GetComponent<RectTransform>();
            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;
        }

        private void Update()
        {
            if (cameraTransform == null) return;

            float cameraYaw = cameraTransform.eulerAngles.y;
            float targetZ = cameraYaw; // if the compass spins the wrong way, change this to -cameraYaw

            rt.rotation = Quaternion.Slerp(rt.rotation, Quaternion.Euler(0f, 0f, targetZ), rotationSmoothSpeed * Time.deltaTime);
        }
    }
}