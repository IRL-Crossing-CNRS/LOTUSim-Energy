using UnityEngine;

public class MoveForward : MonoBehaviour
{
    [SerializeField] public float speed = 5f;

    void Update()
    {
        transform.Translate(Vector3.forward * speed * Time.deltaTime);
    }
}