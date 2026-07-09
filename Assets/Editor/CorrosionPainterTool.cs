using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering.HighDefinition;

public class CorrosionPainterTool : EditorWindow
{
    public Material corrosionMaterial;
    private bool isPainting = false;
    private float corrosionSize = 0.5f;

    [MenuItem("LOTUSim/Corrosion Placement Tool")]
    public static void ShowWindow()
    {
        GetWindow<CorrosionPainterTool>("Corrosion Painter");
    }

    void OnGUI()
    {
        GUILayout.Space(10);
        GUILayout.Label("Corrosion Painter (Decals)", EditorStyles.boldLabel);
        GUILayout.Space(10);

        corrosionMaterial = (Material)EditorGUILayout.ObjectField("Corrosion Material", corrosionMaterial, typeof(Material), false);
        corrosionSize = EditorGUILayout.Slider("Corrosion Size", corrosionSize, 0.1f, 20f);

        GUILayout.Space(15);

        if (corrosionMaterial == null)
        {
            EditorGUILayout.HelpBox("Please select a Corrosion Material to start.", MessageType.Warning);
            return;
        }

        GUI.backgroundColor = isPainting ? Color.red : Color.green;
        if (GUILayout.Button(isPainting ? "STOP - Disable Paint Mode" : "START - Enable Paint Mode", GUILayout.Height(40)))
        {
            isPainting = !isPainting;
            if (isPainting) SceneView.duringSceneGui += OnSceneGUI;
            else SceneView.duringSceneGui -= OnSceneGUI;
        }
        GUI.backgroundColor = Color.white;

        if (isPainting)
        {
            GUILayout.Space(10);
            EditorGUILayout.HelpBox("PAINT MODE ACTIVE\n1. Go to the 3D Scene View.\n2. Click on the target object.\nThe tool will place a decal on the surface.", MessageType.Info);
        }
    }

    void OnSceneGUI(SceneView sceneView)
    {
        if (!isPainting || corrosionMaterial == null) return;

        Event e = Event.current;

        if (e.type == EventType.MouseDown && e.button == 0 && !e.alt && !e.control)
        {
            GameObject clickedObject = HandleUtility.PickGameObject(e.mousePosition, false);
            if (clickedObject != null)
            {
                e.Use();
                ApplyDecalSmart(clickedObject, e.mousePosition);
            }
        }
    }

    void ApplyDecalSmart(GameObject targetObj, Vector2 mousePos)
    {
        MeshCollider tempCollider = targetObj.GetComponent<MeshCollider>();
        bool addedCollider = false;

        if (tempCollider == null)
        {
            tempCollider = targetObj.AddComponent<MeshCollider>();
            addedCollider = true;
        }

        Ray ray = HandleUtility.GUIPointToWorldRay(mousePos);
        if (tempCollider.Raycast(ray, out RaycastHit hit, 10000f))
        {
            CreateDecal(hit.point, hit.normal, targetObj.transform);
        }

        if (addedCollider)
        {
            DestroyImmediate(tempCollider);
        }
    }

    void CreateDecal(Vector3 position, Vector3 normal, Transform parentObj)
    {
        GameObject decalObj = new GameObject("Corrosion_Decal");
        decalObj.transform.SetParent(parentObj);

        decalObj.transform.position = position;
        decalObj.transform.forward = -normal;

        decalObj.transform.Rotate(0, 0, Random.Range(0f, 360f), Space.Self);

        DecalProjector projector = decalObj.AddComponent<DecalProjector>();
        projector.material = corrosionMaterial;
        projector.size = new Vector3(corrosionSize, corrosionSize, 0.5f);

        Undo.RegisterCreatedObjectUndo(decalObj, "Paint Decal");
        Selection.activeGameObject = decalObj;
        isPainting = false;
    }

    void OnDestroy()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }
}
