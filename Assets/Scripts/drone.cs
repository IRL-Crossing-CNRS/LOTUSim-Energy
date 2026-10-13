using UnityEngine;

public class drone : MonoBehaviour
{
    [Header("Movement")]
    public float forwardSpeed = 1f;
    public float waveAmplitude = 3f;
    public float waveFrequency = 1f;
    public Vector3 moveDirection = Vector3.forward;

    [Header("Trail")]
    public float trailTime = 3f;
    public float trailStartWidth = 0.5f;
    public float trailEndWidth = 0f;
    public Color trailStartColor = new Color(1f, 0.5f, 0f, 1f);
    public Color trailEndColor = new Color(1f, 0.5f, 0f, 0f);

    private float timeElapsed = 0f;
    private Vector3 startPosition;
    private TrailRenderer trail;

    void Start()
    {
        startPosition = transform.position;

        // Build trail renderer in code
        trail = gameObject.AddComponent<TrailRenderer>();
        trail.time = trailTime;
        trail.startWidth = trailStartWidth;
        trail.endWidth = trailEndWidth;
        trail.startColor = trailStartColor;
        trail.endColor = trailEndColor;
        trail.minVertexDistance = 0.1f;

        // Trail material
        Material trailMat = new Material(Shader.Find("Sprites/Default"));
        trailMat.color = trailStartColor;
        trail.material = trailMat;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(trailStartColor, 0f),
                new GradientColorKey(trailEndColor,   1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        trail.colorGradient = gradient;
    }

    void Update()
    {
        timeElapsed += Time.deltaTime;

        // Forward movement
        Vector3 forward = moveDirection.normalized * forwardSpeed * timeElapsed;

        // Sine wave vertical oscillation
        float wave = Mathf.Sin(timeElapsed * waveFrequency * 2f * Mathf.PI) * waveAmplitude;

        transform.position = startPosition + forward + Vector3.up * wave;

        /*float waveDerivative = Mathf.Cos(timeElapsed * waveFrequency * 2f * Mathf.PI)
                               * waveAmplitude * waveFrequency * 2f * Mathf.PI;
        float pitchAngle = Mathf.Atan2(waveDerivative, forwardSpeed) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(-pitchAngle, 
                             Quaternion.LookRotation(moveDirection).eulerAngles.y, 0f);*/
    }
}