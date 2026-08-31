using UnityEngine;
using System.Collections.Generic;
using TMPro;

public class ObjectLabelDisplay : MonoBehaviour
{
    [System.Serializable]
    public class LabeledObject
    {
        public Transform target;
        public string displayName = "Name";
        public float labelHeight = 2f;
        public float textSize = 12f;
    }

    public List<LabeledObject> objectsToLabel = new List<LabeledObject>();
    public KeyCode toggleKey = KeyCode.F1;
    public TMP_FontAsset textMeshProFont;

    private bool labelsVisible = false;
    private Dictionary<Transform, GameObject> labelInstances = new Dictionary<Transform, GameObject>();
    private Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>();

        foreach (var entry in objectsToLabel)
        {
            if (entry.target == null) continue;

            // Créer le conteneur du label
            GameObject label = new GameObject("Label_" + entry.displayName);
            label.transform.SetParent(entry.target);
            label.transform.localPosition = new Vector3(0, entry.labelHeight, 0);

            // Créer le texte TMP
            TextMeshPro tmp = label.AddComponent<TextMeshPro>();
            tmp.text = entry.displayName;
            tmp.fontSize = entry.textSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(1f, 0.95f, 0.8f, 0.50f);;
            tmp.enableWordWrapping = false;
            tmp.raycastTarget = false;
            tmp.rectTransform.pivot = new Vector2(0.5f, 0.5f);

            if (textMeshProFont != null)
            {
                tmp.font = textMeshProFont;
            }

            tmp.ForceMeshUpdate();

            // Fond blanc
            GameObject background = GameObject.CreatePrimitive(PrimitiveType.Quad);
            background.name = "LabelBackground";
            background.transform.SetParent(label.transform);
            background.transform.localRotation = Quaternion.identity;

            // Créer un nouveau matériau transparent unlit
            Material bgMat = new Material(Shader.Find("HDRP/Unlit"));
            bgMat.SetColor("_UnlitColor", new Color(0.2f, 0.25f, 0.3f, 0.5f));
            bgMat.SetFloat("_SurfaceType", 1); // 1 = Transparent
            bgMat.renderQueue = 3000; // Assure le bon ordre de rendu
            background.GetComponent<MeshRenderer>().material = bgMat;
            MeshRenderer bgRenderer = background.GetComponent<MeshRenderer>();
            bgRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            bgRenderer.receiveShadows = false;

            // Adapter la taille du fond au texte
            Vector2 textSize = tmp.GetRenderedValues(false);
            float paddingX = 1f;
            float paddingY = 0.8f;
            background.transform.localPosition = new Vector3(0, 0, 0.01f);
            background.transform.localScale = new Vector3(textSize.x + paddingX, textSize.y + paddingY, 1f);

            // Cacher au départ
            label.SetActive(false);
            labelInstances[entry.target] = label;
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            labelsVisible = !labelsVisible;
            foreach (var label in labelInstances.Values)
            {
                label.SetActive(labelsVisible);
            }
        }

        if (labelsVisible)
        {
            foreach (var kvp in labelInstances)
            {
                if (kvp.Value != null)
                {
                    kvp.Value.transform.rotation = Quaternion.LookRotation(kvp.Value.transform.position - cam.transform.position);
                }
            }
        }
    }
}
