using UnityEngine;

public class InfinitePlane : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The camera to follow. Defaults to Camera.main if left empty.")]
    public Transform cam;

    [Tooltip("A plane prefab with a seamlessly tiling material.")]
    public GameObject chunkPrefab;

    [Header("Grid Settings")]
    [Tooltip("Number of chunks along each axis. Must be odd (e.g. 3, 5, 7).")]
    public int gridSize = 3;

    [Tooltip("World-unit size of each chunk. Formula: planeScale x 10. E.g. scale 100 = chunkSize 1000.")]
    public float chunkSize = 1000f;

    [Tooltip("Y position the plane spawns at.")]
    public float planeY = -300f;

    public System.Action<Vector3, int> onChunkRepositioned;

    private GameObject[,] chunks;

    private Vector2 lastCamChunk;


    void Start()
    {

        if (cam == null)
        {
            if (Camera.main != null)
                cam = Camera.main.transform;
            else
            {
                Debug.LogError("InfinitePlane: No camera assigned and Camera.main is null.");
                enabled = false;
                return;
            }
        }


        if (gridSize % 2 == 0)
        {
            Debug.LogWarning("InfinitePlane: gridSize should be odd. Incrementing by 1.");
            gridSize++;
        }

        if (chunkPrefab == null)
        {
            Debug.LogError("InfinitePlane: chunkPrefab is not assigned.");
            enabled = false;
            return;
        }

        InitialiseChunks();
        lastCamChunk = GetCamChunk();
    }


    void Update()
    {
        Vector2 currentChunk = GetCamChunk();

        if (currentChunk != lastCamChunk)
        {
            lastCamChunk = currentChunk;
            RepositionChunks();
        }
    }


    void InitialiseChunks()
    {
        chunks = new GameObject[gridSize, gridSize];
        int half = gridSize / 2;
        int index = 0;

        Vector2 startChunk = GetCamChunk();

        for (int x = 0; x < gridSize; x++)
        {
            for (int z = 0; z < gridSize; z++)
            {
                Vector3 pos = new Vector3(
                    (startChunk.x + x - half) * chunkSize,
                    planeY,
                    (startChunk.y + z - half) * chunkSize
                );

                chunks[x, z] = Instantiate(chunkPrefab, pos, Quaternion.identity, transform);
                chunks[x, z].name = $"Chunk_{x}_{z}";

                onChunkRepositioned?.Invoke(pos, index);
                index++;
            }
        }
    }

    void RepositionChunks()
    {
        int half = gridSize / 2;
        int index = 0;

        for (int x = 0; x < gridSize; x++)
        {
            for (int z = 0; z < gridSize; z++)
            {
                Vector3 newPos = new Vector3(
                    (lastCamChunk.x + x - half) * chunkSize,
                    planeY,
                    (lastCamChunk.y + z - half) * chunkSize
                );

                chunks[x, z].transform.position = newPos;

                onChunkRepositioned?.Invoke(newPos, index);
                index++;
            }
        }
    }

    Vector2 GetCamChunk()
    {
        return new Vector2(
            Mathf.Floor(cam.position.x / chunkSize),
            Mathf.Floor(cam.position.z / chunkSize)
        );
    }

    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || chunks == null) return;

        Gizmos.color = Color.cyan;
        float total = gridSize * chunkSize;
        Vector3 centre = new Vector3(
            lastCamChunk.x * chunkSize + chunkSize * 0.5f,
            planeY,
            lastCamChunk.y * chunkSize + chunkSize * 0.5f
        );
        Gizmos.DrawWireCube(centre, new Vector3(total, 0.1f, total));
    }
}
