using UnityEngine;

public class Cam_TP : MonoBehaviour
{
    [SerializeField] private Transform _teleportDestination;
    [SerializeField] private string _actorTag = "Actor";

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(_actorTag) || _teleportDestination == null)
            return;

        transform.position = _teleportDestination.position;
        transform.rotation = _teleportDestination.rotation;
    }
}