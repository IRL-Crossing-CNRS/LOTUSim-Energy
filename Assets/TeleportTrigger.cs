using UnityEngine;

public class TeleportTrigger : MonoBehaviour
{
    public Transform teleportDestination;

    public bool resetVelocity = true;

    private void OnTriggerEnter(Collider other)
    {
        Teleport(other.gameObject);
    }

    private void Teleport(GameObject obj)
    {
        if (teleportDestination == null)
        {
            Debug.LogWarning("No teleport destination assigned on " + gameObject.name);
            return;
        }

        CharacterController cc = obj.GetComponent<CharacterController>();
        if (cc != null)
        {
            cc.enabled = false;
            obj.transform.position = teleportDestination.position;
            obj.transform.rotation = teleportDestination.rotation;
            cc.enabled = true;
        }
        else
        {
            obj.transform.position = teleportDestination.position;
            obj.transform.rotation = teleportDestination.rotation;
        }

        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb != null && resetVelocity)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
}