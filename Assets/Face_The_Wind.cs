using UnityEngine;

namespace Lotusim
{
    public class Face_The_Wind : MonoBehaviour
    {
        [Header("Data Source")]
        [SerializeField] private WindSliderController windController;

        [Header("Rotation")]
        [SerializeField] private float rotationSpeed = 180f;

        [Tooltip("Add/subtract degrees if the turbine model's zero direction doesn't match the wind.")]
        [SerializeField] private float angleOffset = 0f;

        [Tooltip("Reverse if the turbine faces away from the wind instead of into it.")]
        [SerializeField] private bool reverseWind = false;

        private void Update()
        {
            if (windController == null)
                return;

            Vector3 wind = windController.CurrentWindVector;

            // ROS coordinate system — X and Y are horizontal wind direction
            Vector2 wind2D = new Vector2(wind.x, wind.y);

            if (wind2D.sqrMagnitude < 0.0001f)
                return;

            if (reverseWind)
                wind2D = -wind2D;

            // Atan2 gives us the angle in the XY plane
            float targetAngle = Mathf.Atan2(wind2D.x, wind2D.y) * Mathf.Rad2Deg;
            targetAngle += angleOffset;

            float currentAngle = transform.localEulerAngles.y;

            float newAngle = Mathf.MoveTowardsAngle(
                currentAngle,
                targetAngle,
                rotationSpeed * Time.deltaTime
            );

            // Only rotate on Y — X and Z locked
            transform.localRotation = Quaternion.Euler(0f, newAngle, 0f);
        }
    }
}