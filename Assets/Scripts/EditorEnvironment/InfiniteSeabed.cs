/*
 * Copyright (c) 2025 Naval Group
 *
 * This program and the accompanying materials are made available under the
 * terms of the Eclipse Public License 2.0 which is available at
 * https://www.eclipse.org/legal/epl-2.0.
 *
 * SPDX-License-Identifier: EPL-2.0
 */

// --------------------------------------------------------------------------------------------------------------------
//  InfiniteSeabed.cs
//
//  Description:
//  Manages an infinite seabed using a 3x3 grid of tiles around the camera
// --------------------------------------------------------------------------------------------------------------------

using UnityEngine;

public class SeabedTiler : MonoBehaviour
{
    public Transform cameraTransform;
    public GameObject seabedPrefab;
    public float tileSize = 500f;

    private GameObject[,] tiles = new GameObject[3, 3];
    private Vector2Int currentTileIndex = Vector2Int.zero;
    private bool initialized = false;

    void Start()
    {
        if (cameraTransform == null)
            cameraTransform = Camera.main?.transform;

        if (cameraTransform == null || seabedPrefab == null)
        {
            Debug.LogWarning("SeabedTiler: Camera or Prefab not assigned.");
            return;
        }

        InitializeTiles();
        UpdateTiles(force: true);
        initialized = true;
    }

    void Update()
    {
        if (!initialized || cameraTransform == null) return;

        Vector2Int camIndex = new Vector2Int(
            Mathf.FloorToInt(cameraTransform.position.x / tileSize),
            Mathf.FloorToInt(cameraTransform.position.z / tileSize)
        );

        if (camIndex != currentTileIndex)
        {
            currentTileIndex = camIndex;
            UpdateTiles(force: false);
        }
    }

    /// <summary>
    /// Instantiate the 3x3 grid of seabed tiles as children of this transform
    /// </summary>
    void InitializeTiles()
    {
        for (int x = 0; x < 3; x++)
        {
            for (int z = 0; z < 3; z++)
            {
                GameObject tile = Instantiate(seabedPrefab, transform);
                tile.name = $"SeabedTile_{x}_{z}";
                tiles[x, z] = tile;
            }
        }
    }

    /// <summary>
    /// Reposition tiles based on the camera's current tile index
    /// </summary>
    void UpdateTiles(bool force)
    {
        int centerX = currentTileIndex.x;
        int centerZ = currentTileIndex.y;

        for (int x = -1; x <= 1; x++)
        {
            for (int z = -1; z <= 1; z++)
            {
                Vector3 position = new Vector3(
                    (centerX + x) * tileSize,
                    transform.position.y,
                    (centerZ + z) * tileSize
                );
                tiles[x + 1, z + 1].transform.position = position;
            }
        }
    }
}

