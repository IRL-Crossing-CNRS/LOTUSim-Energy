/*
 * Copyright (c) 2025 Naval Group
 *
 * This program and the accompanying materials are made available under the
 * terms of the Eclipse Public License 2.0 which is available at
 * https://www.eclipse.org/legal/epl-2.0.
 *
 * SPDX-License-Identifier: EPL-2.0
 */

using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class FpsTracker : MonoBehaviour
{
    private Queue<float> lastFpsSamples = new Queue<float>(1000);
    private string outputPath;

    private void Awake()
    {
        outputPath = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop), "fps_output.csv");
        Debug.Log("FPS will be saved to: " + outputPath);
    }

    private void Update()
    {
        float fps = 1f / Time.unscaledDeltaTime;

        if (lastFpsSamples.Count >= 1000)
            lastFpsSamples.Dequeue();

        lastFpsSamples.Enqueue(fps);
    }

    private void OnApplicationQuit()
    {
        SaveFpsData();
    }

    private void SaveFpsData()
    {
        float averageFps = lastFpsSamples.Average();

        using (StreamWriter writer = new StreamWriter(outputPath, false))
        {
            writer.WriteLine("FPS Samples");
            foreach (float fps in lastFpsSamples)
                writer.WriteLine(fps);
            writer.WriteLine($"Average FPS,{averageFps}");
        }

        Debug.Log($"Last 1000 FPS samples saved to {outputPath}. Mean FPS: {averageFps}");
    }
}
