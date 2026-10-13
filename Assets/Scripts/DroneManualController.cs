using UnityEngine;

public class DroneManualController : MonoBehaviour
{
    public float moveSpeed = 20f;
    public float ascendSpeed = 10f;
    public float turnSpeed = 90f;
    public float boostMultiplier = 2f;

    public float maxRollAngle = 25f;
    public float maxPitchAngle = 15f;
    public float tiltSmoothSpeed = 5f;

    public KeyCode wobbleKey = KeyCode.P;
    public float wobbleAmplitude = 0.3f;
    public float wobbleTiltAmplitude = 5f;
    public float wobbleSpeed = 2f;

    private float yaw;
    private float currentRoll;
    private float currentPitch;
    private bool isWobbling;
    private Vector3 basePosition;
    private float noiseSeedX, noiseSeedY, noiseSeedZ;

    private void Start()
    {
        yaw = transform.eulerAngles.y;
        basePosition = transform.position;
        noiseSeedX = Random.Range(0f, 1000f);
        noiseSeedY = Random.Range(0f, 1000f);
        noiseSeedZ = Random.Range(0f, 1000f);
    }

    private void Update()
    {
        if (Input.GetKeyDown(wobbleKey))
            isWobbling = !isWobbling;

        float forward = Input.GetAxis("Vertical");
        float strafe  = Input.GetAxis("Horizontal");

        float turn = 0f;
        if (Input.GetKey(KeyCode.Q)) turn -= 1f;
        if (Input.GetKey(KeyCode.E)) turn += 1f;

        float vertical = 0f;
        if (Input.GetKey(KeyCode.Space))       vertical += 1f;
        if (Input.GetKey(KeyCode.LeftControl)) vertical -= 1f;

        float speedMultiplier = Input.GetKey(KeyCode.LeftShift) ? boostMultiplier : 1f;

        yaw += turn * turnSpeed * Time.deltaTime;

        float targetRoll = -strafe * maxRollAngle;
        float targetPitch = forward * maxPitchAngle;
        currentRoll = Mathf.Lerp(currentRoll, targetRoll, tiltSmoothSpeed * Time.deltaTime);
        currentPitch = Mathf.Lerp(currentPitch, targetPitch, tiltSmoothSpeed * Time.deltaTime);

        Quaternion headingOnly = Quaternion.Euler(0f, yaw, 0f);
        Vector3 move = (headingOnly * Vector3.forward * forward + headingOnly * Vector3.right * strafe) * moveSpeed * speedMultiplier
                     + Vector3.up * vertical * ascendSpeed * speedMultiplier;

        basePosition += move * Time.deltaTime;

        Vector3 wobbleOffset = Vector3.zero;
        float wobbleRoll = 0f;
        float wobblePitch = 0f;

        if (isWobbling)
        {
            float t = Time.time * wobbleSpeed;
            wobbleOffset = new Vector3(
                (Mathf.PerlinNoise(t, noiseSeedX) - 0.5f) * 2f,
                (Mathf.PerlinNoise(t, noiseSeedY) - 0.5f) * 2f,
                (Mathf.PerlinNoise(t, noiseSeedZ) - 0.5f) * 2f
            ) * wobbleAmplitude;

            wobbleRoll = (Mathf.PerlinNoise(t, noiseSeedX + 50f) - 0.5f) * 2f * wobbleTiltAmplitude;
            wobblePitch = (Mathf.PerlinNoise(t, noiseSeedY + 50f) - 0.5f) * 2f * wobbleTiltAmplitude;
        }

        transform.position = basePosition + wobbleOffset;
        transform.rotation = Quaternion.Euler(currentPitch + wobblePitch, yaw, currentRoll + wobbleRoll);
    }
}