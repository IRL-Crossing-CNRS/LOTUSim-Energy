using UnityEngine;

namespace Lotusim
{
    [RequireComponent(typeof(RectTransform))]
    public class WindCompassController : MonoBehaviour
    {
        [Header("Data Source")]
        [SerializeField] private WindSliderController windController;

        [Header("Smoothing")]
        [Tooltip("How quickly the compass rotates to match the new wind direction. Higher = snappier.")]
        [SerializeField] private float rotationSmoothSpeed = 5f;

        private RectTransform rt;
        private float targetAngle;

        private void Awake()
        {
            rt = GetComponent<RectTransform>();
        }

        private void Update()
        {
            if (windController == null) return;

            Vector3 wind = windController.CurrentWindVector;
            Vector2 horizontal = new Vector2(wind.x, wind.y);

            if (horizontal.sqrMagnitude > 0.0001f)
            {
                float bearing = Mathf.Atan2(horizontal.x, horizontal.y) * Mathf.Rad2Deg;
                targetAngle = -bearing;
            }
            rt.localRotation = Quaternion.Slerp(rt.localRotation, Quaternion.Euler(0f, 0f, targetAngle), rotationSmoothSpeed * Time.deltaTime);
        }
    }
}