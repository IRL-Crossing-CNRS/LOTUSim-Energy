using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BlueROVWaypointFollower : MonoBehaviour
{
    [Header("Route")]
    public List<Transform> waypoints = new List<Transform>();
    public bool loop = true;
    public float waypointRadius = 3f;

    [Header("Model Alignment")]
    [Tooltip("Yaw offset to align the model's forward with Unity forward. Try -90 or +90 if it drives sideways.")]
    public float headingOffsetDegrees = -90f;

    [Header("Speed")]
    public float maxSpeed = 6f;
    public float thrust = 25f;
    public float arrivalSlowdownDist = 12f;

    [Header("Turning")]
    public float turnTorque = 6f;
    public float turnSensitivityAngle = 90f;
    public float minTurnSpeed = 1.5f;
    public float yawDamping = 2.0f;

    [Header("Water feel")]
    public float lateralDamping = 2.0f;
    public float cruiseDrag = 0.2f;
    public float brakingDrag = 1.5f;

    [Header("Thrust gating")]
    [Range(0f, 1f)]
    public float minFacingThrust = 0.35f;
    public bool stopAtFinalWaypoint = false;

    [Header("Debug")]
    public bool drawGizmos = true;

    private Rigidbody rb;
    private int index = 0;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = false;

        if (rb.angularDrag < 1f) rb.angularDrag = 3f;
        if (rb.drag < 0.05f) rb.drag = cruiseDrag;
    }

    // Returns the boat's "true forward" direction, accounting for model alignment.
    Vector3 BoatForward()
    {
        return (Quaternion.Euler(0f, headingOffsetDegrees, 0f) * transform.forward).normalized;
    }

    // Returns the boat's "true right" direction, matching BoatForward()
    Vector3 BoatRight()
    {
        return (Quaternion.Euler(0f, headingOffsetDegrees, 0f) * transform.right).normalized;
    }

    void FixedUpdate()
    {
        if (waypoints == null || waypoints.Count == 0) return;
        if (waypoints[index] == null) { AdvanceWaypoint(); return; }

        Transform target = waypoints[index];

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;

        float dist = toTarget.magnitude;

        if (dist < Mathf.Max(0.01f, waypointRadius))
        {
            bool atLast = (!loop && index >= waypoints.Count - 1);
            if (atLast && stopAtFinalWaypoint)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                return;
            }

            AdvanceWaypoint();
            return;
        }

        Vector3 desiredDir = toTarget.normalized;

        Vector3 fwd = BoatForward();
        Vector3 right = BoatRight();

        float angle = Vector3.SignedAngle(fwd, desiredDir, Vector3.up);
        float forwardSpeed = Vector3.Dot(rb.velocity, fwd);

        // TURN
        float turn = Mathf.Clamp(angle / Mathf.Max(1f, turnSensitivityAngle), -1f, 1f);
        float speedFactor = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / Mathf.Max(0.01f, minTurnSpeed));
        rb.AddTorque(Vector3.up * turn * turnTorque * speedFactor, ForceMode.Acceleration);

        // Yaw damping
        float yawRate = rb.angularVelocity.y;
        rb.AddTorque(Vector3.up * (-yawRate) * yawDamping, ForceMode.Acceleration);

        // Lateral damping (hull resistance)
        Vector3 lateralVel = Vector3.Project(rb.velocity, right);
        rb.AddForce(-lateralVel * lateralDamping, ForceMode.Acceleration);

        // SPEED / DRAG
        float desiredSpeed = maxSpeed;
        if (dist < arrivalSlowdownDist)
        {
            desiredSpeed = Mathf.Lerp(0.8f, maxSpeed, dist / Mathf.Max(0.01f, arrivalSlowdownDist));
            rb.drag = brakingDrag;
        }
        else
        {
            rb.drag = cruiseDrag;
        }

        // THRUST
        float facing = Mathf.Clamp01(1f - Mathf.Abs(angle) / 90f);
        float thrustFactor = Mathf.Max(minFacingThrust, facing);

        if (forwardSpeed < desiredSpeed)
        {
            rb.AddForce(fwd * thrust * thrustFactor, ForceMode.Acceleration);
        }

        // Clamp max horizontal speed
        Vector3 v = rb.velocity;
        Vector3 flat = new Vector3(v.x, 0f, v.z);
        if (flat.magnitude > maxSpeed)
        {
            Vector3 flatClamped = flat.normalized * maxSpeed;
            rb.velocity = new Vector3(flatClamped.x, v.y, flatClamped.z);
        }
    }

    void AdvanceWaypoint()
    {
        index++;
        if (index >= waypoints.Count)
            index = loop ? 0 : waypoints.Count - 1;
    }

    void OnDrawGizmos()
    {
        if (!drawGizmos || waypoints == null || waypoints.Count == 0) return;

        Gizmos.color = Color.red;
        for (int i = 0; i < waypoints.Count; i++)
        {
            if (waypoints[i] == null) continue;
            Gizmos.DrawWireSphere(waypoints[i].position, waypointRadius);

            int next = i + 1;
            if (next < waypoints.Count && waypoints[next] != null)
                Gizmos.DrawLine(waypoints[i].position, waypoints[next].position);
            else if (loop && waypoints[0] != null)
                Gizmos.DrawLine(waypoints[i].position, waypoints[0].position);
        }

        // Draw the boat forward for quick debugging
        if (Application.isPlaying)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, transform.position + BoatForward() * 5f);
        }
    }
}