using System.Collections.Generic;
using UnityEngine;

public class DroneManualSetter : MonoBehaviour
{
    public KeyCode snapKey = KeyCode.Y;
    public Camera droneCamera;

    private static readonly List<DroneManualSetter> allDrones = new List<DroneManualSetter>();
    private static int activeIndex = -1;

    private bool IsActive => allDrones.Count > 0 && activeIndex >= 0 && allDrones[activeIndex] == this;

    private void OnEnable()
    {
        allDrones.Add(this);
        if (activeIndex == -1) activeIndex = 0;
        SetCameraActive(IsActive);
    }

    private void OnDisable()
    {
        int idx = allDrones.IndexOf(this);
        allDrones.Remove(this);

        if (allDrones.Count == 0)
        {
            activeIndex = -1;
        }
        else if (activeIndex == idx)
        {
            activeIndex = activeIndex % allDrones.Count;
            SetCameraActive(false);
            allDrones[activeIndex].SetCameraActive(true);
        }
        else if (activeIndex > idx)
        {
            activeIndex--;
        }
    }

    private void Update()
    {
        if (allDrones.Count > 0 && allDrones[0] == this && Input.GetKeyDown(snapKey))
        {
            int previousIndex = activeIndex;
            activeIndex = (activeIndex + 1) % allDrones.Count;

            if (previousIndex != activeIndex)
            {
                if (previousIndex >= 0) allDrones[previousIndex].SetCameraActive(false);
                allDrones[activeIndex].SetCameraActive(true);
            }
        }
    }

    private void SetCameraActive(bool active)
    {
        if (droneCamera == null) return;
        droneCamera.enabled = active;

        AudioListener listener = droneCamera.GetComponent<AudioListener>();
        if (listener != null) listener.enabled = active;
    }
}
