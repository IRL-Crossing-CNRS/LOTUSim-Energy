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
//  AgentSpawner.cs
//
//  Description:
//  Spawns multiple instances of a given model prefab at defined positions, optionally using CLI arguments.
// --------------------------------------------------------------------------------------------------------------------

using UnityEngine;

public class AgentSpawner : MonoBehaviour
{
    #region Public Fields

    [Header("Prefab to spawn")]
    [Tooltip("The model prefab to spawn")]
    public GameObject model;

    [Header("Number of agents to create (default if no CLI arg)")]
    [Tooltip("Number of agents to spawn; can be overridden by --agent_num CLI argument")]
    public int numberOfAgents = 0;

    [Header("Spawn Parameters")]
    [Tooltip("Starting position for the first agent")]
    public Vector3 startPosition = Vector3.zero;

    [Tooltip("Spacing between spawned agents")]
    public Vector3 spawnSpacing = new Vector3(2f, 0f, 2f);

    #endregion

    #region MonoBehaviour Callbacks

    private void Start()
    {
        if (model == null)
        {
            Debug.LogError("AgentSpawner: No model prefab assigned.");
            return;
        }

        ParseCommandLineArguments();
        SpawnAgents();
    }

    #endregion

    #region Private Methods

    private void ParseCommandLineArguments()
    {
        string[] args = System.Environment.GetCommandLineArgs();

        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--agent_num")
            {
                if (int.TryParse(args[i + 1], out int parsedValue))
                {
                    numberOfAgents = parsedValue;
                    Debug.Log($"Command-line argument --agent_num found: {numberOfAgents}");
                }
                else
                {
                    Debug.LogWarning("Invalid value for --agent_num. Using default numberOfAgents.");
                }
                break;
            }
        }
    }

    private void SpawnAgents()
    {
        string baseName = model.name.ToLower();

        for (int i = 0; i < numberOfAgents; i++)
        {
            Vector3 spawnPos = startPosition + i * spawnSpacing;
            GameObject agent = Instantiate(model, spawnPos, Quaternion.identity);
            agent.name = $"{baseName}{i}";
        }
    }

    #endregion
}
