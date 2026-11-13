using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

public class HDRPDistortCameraBelowY : MonoBehaviour
{
    [SerializeField] private float yThreshold = 0f;
    [SerializeField] private CustomPassVolume volume;
    [SerializeField] private float fadeSpeed = 3f;
    [SerializeField] private ParticleSystem marineSnow;
    

    private Material runtimeMaterial;
    private float intensity;
    private bool effectEnabled = true;

    void Start()
    {
        var pass = volume.customPasses[0] as FullScreenCustomPass;
        runtimeMaterial = new Material(pass.fullscreenPassMaterial);
        pass.fullscreenPassMaterial = runtimeMaterial;
        volume.enabled = false;
    }

    void Update()
    {
        if (!effectEnabled) return;

        float target = transform.position.y < yThreshold ? 1f : 0f;
        intensity = Mathf.MoveTowards(intensity, target, fadeSpeed * Time.deltaTime);

        runtimeMaterial.SetFloat("_Intensity", intensity);
        volume.enabled = intensity > 0f;

        var emission = marineSnow.emission;
        emission.enabled = intensity > 0.01f;
    }

    public void SetEffectEnabled(bool enabled)
    {
        effectEnabled = enabled;

        if (!enabled)
        {
            intensity = 0f;
            runtimeMaterial.SetFloat("_Intensity", 0f);
            volume.enabled = false;
            var emission = marineSnow.emission;
            emission.enabled = false;
        }
    }
}