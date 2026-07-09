using System.Collections.Generic;
using System.Collections;
using System;
using System.Text;
using System.IO;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEditor;


[Serializable]
public class Positions
{
    public double time;
    public List<Vector3> position = new List<Vector3>();
}

public class GazeboWaveInterface : MonoBehaviour
{
     // Public parameters
    public int x_points = 2;
    public int y_points = 2;
    public float resolution = 1;
    public WaterSurface waterSurface = null;

    string path = "Assets/Logs/result.yaml";
    // List of internal cubes
    // List<GameObject> cubes = new List<GameObject>();

    // Input job parameters
    NativeArray<float3> targetPositionBuffer;

    // Output job parameters
    NativeArray<float> heightBuffer;
    NativeArray<float> errorBuffer;
    NativeArray<float3> candidatePositionBuffer;
    NativeArray<int> stepCountBuffer;
    int runtime;
    float max_height=0.0f;
    // Start is called before the first frame update
    void Start()
    {
        // Allocate the buffers
        targetPositionBuffer = new NativeArray<float3>(x_points * y_points, Allocator.Persistent);
        heightBuffer = new NativeArray<float>(x_points * y_points, Allocator.Persistent);
        errorBuffer = new NativeArray<float>(x_points * y_points, Allocator.Persistent);
        candidatePositionBuffer = new NativeArray<float3>(x_points * y_points, Allocator.Persistent);
        stepCountBuffer = new NativeArray<int>(x_points * y_points, Allocator.Persistent);

        int i=0;
        for (int y = 0; y < y_points; ++y)
        {
            for (int x = 0; x < x_points; ++x)
            {
                // GameObject newCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                // newCube.transform.parent = this.transform;
                // newCube.transform.localPosition = new Vector3(x * 30, 0.0f, y * 30);
                // cubes.Add(newCube);
                targetPositionBuffer[i]  = new float3(x * resolution, 0.0f, y * resolution);
                i++;
            }
        }

        // int numElements = resolution * resolution;
        // for (int i = 0; i < numElements; ++i)
        //     targetPositionBuffer[i] = cubes[i].transform.position;
        runtime = Convert.ToInt32(Time.time);
        File.WriteAllText(path, string.Empty);
    }

    // Update is called once per frame
    void Update()
    {
        if (waterSurface == null)
            return;
        // if (runtime+1 >Time.time)
        //     return;
        runtime = Convert.ToInt32(Time.time);
        // Try to get the simulation data if available
        WaterSimSearchData simData = new WaterSimSearchData();
        if (!waterSurface.FillWaterSearchData(ref simData))
        {
            Debug.Log("Search failed");
            return;
        }            
        // Fill the input positions
        int numElements = x_points * y_points;

        // Prepare the first band
        WaterSimulationSearchJob searchJob = new WaterSimulationSearchJob();

        // Assign the simulation data
        searchJob.simSearchData = simData;

        // Fill the input data
        searchJob.targetPositionBuffer = targetPositionBuffer;
        searchJob.startPositionBuffer = targetPositionBuffer;
        searchJob.maxIterations = 8;
        searchJob.error = 0.01f;

        searchJob.heightBuffer = heightBuffer;
        searchJob.errorBuffer = errorBuffer;
        searchJob.candidateLocationBuffer = candidatePositionBuffer;
        searchJob.stepCountBuffer = stepCountBuffer;

        // Schedule the job with one Execute per index in the results array and only 1 item per processing batch
        JobHandle handle = searchJob.Schedule(numElements, 1);
        handle.Complete();
    
        Positions data = new Positions();
        data.time = Time.time;
        // Debug.Log(heightBuffer[0]);
        for (int i = 0; i < numElements; ++i)
        {
            // Debug.Log(i +" "+ cubes[i].transform.position.z);
            data.position.Add(new Vector3(targetPositionBuffer[i].x, targetPositionBuffer[i].z, heightBuffer[i]));
            // data.position.Add(new Vector3(cubes[i].transform.position.x, cubes[i].transform.position.z, heightBuffer[i]));
            max_height=Math.Max(max_height, Math.Abs(heightBuffer[i]));
        }
        // Debug.Log(max_height);
        
        Debug.Log(waterSurface.largeWindSpeed);
        
        // Debug.Log(JsonUtility.ToJson(data));
        WriteString(JsonUtility.ToJson(data));
    }

    private void OnDestroy()
    {
        targetPositionBuffer.Dispose();
        heightBuffer.Dispose();
        errorBuffer.Dispose();
        candidatePositionBuffer.Dispose();
        stepCountBuffer.Dispose();
        // cubes.ForEach(delegate(GameObject obj)
        //     {
        //         Destroy(obj);
        //     });
    }

    void WriteString(string str)
    {
        //Write some text to the test.txt file
        StreamWriter writer = new StreamWriter(path, true);
        writer.WriteLine(str);
        writer.Close();
    }
}


