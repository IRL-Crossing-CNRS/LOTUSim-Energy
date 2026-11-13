using UnityEngine;

public class TurnOffOcean : MonoBehaviour
{
    public GameObject foggy;
    public GameObject water;
    public HDRPDistortCameraBelowY distortion;

    [SerializeField] private KeyCode toggleKey = KeyCode.F3;

    private bool isVisible = true;

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            isVisible = !isVisible;
            if (water != null) water.SetActive(isVisible);
            if (distortion != null) distortion.SetEffectEnabled(isVisible);
        }
    }
}