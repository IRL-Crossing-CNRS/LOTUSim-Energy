using UnityEngine;

public class SeabedObjects : MonoBehaviour
{
    [Header("References")]
    public InfinitePlane infinitePlane;

    [Header("Prefabs")]
    public GameObject[] prefabs;

    [Header("Placement Settings")]
    public float planeY = -300f;
    public float chunkSize = 1000f;
    public int gridSize = 3;
    public float objectYOffset = -1f;
    public int objectsPerChunk = 5;

    private GameObject[] pool;

    void Start()
    {
        if (infinitePlane == null)
        {
            Debug.LogError("SeabedObjects: infinitePlane is not assigned.");
            enabled = false;
            return;
        }

        if (prefabs == null || prefabs.Length == 0)
        {
            Debug.LogError("SeabedObjects: No prefabs assigned.");
            enabled = false;
            return;
        }

        int total = gridSize * gridSize * objectsPerChunk;
        pool = new GameObject[total];

        for (int i = 0; i < total; i++)
        {
            GameObject prefab = prefabs[Random.Range(0, prefabs.Length)];
            pool[i] = Instantiate(prefab, Vector3.zero, Quaternion.identity, transform);
            pool[i].name = $"SeabedObject_{i}";
            pool[i].SetActive(false);
        }

        infinitePlane.onChunkRepositioned += PlaceObject;
    }

    void PlaceObject(Vector3 chunkOrigin, int chunkIndex)
    {
        if (pool == null) return;

        for (int i = 0; i < objectsPerChunk; i++)
        {
            int poolIndex = chunkIndex * objectsPerChunk + i;
            if (poolIndex >= pool.Length || pool[poolIndex] == null) continue;

            float halfChunk = chunkSize * 0.5f;
            float randX = chunkOrigin.x + Random.Range(-halfChunk, halfChunk);
            float randZ = chunkOrigin.z + Random.Range(-halfChunk, halfChunk);

            pool[poolIndex].transform.position = new Vector3(randX, planeY + objectYOffset, randZ);
            pool[poolIndex].transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            pool[poolIndex].SetActive(true);
        }
    }

    void OnDestroy()
    {
        if (infinitePlane != null)
            infinitePlane.onChunkRepositioned -= PlaceObject;
    }
}
