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
//  DynamicObjectNameDisplay.cs
//  
//  Description:
//  Dynamically displays the names of objects above them in the scene.
//  Allows the user to toggle the visibility of these labels using a designated key.
//  
// --------------------------------------------------------------------------------------------------------------------


using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

namespace Lotusim
{
    /// <summary>
    /// Displays dynamic labels for scene objects.
    /// Labels can be toggled with a key press (default F1).
    /// </summary>
    public class ObjectDynamicLabelDisplay : MonoBehaviour
    {
        #region Configurable Fields

        [Header("Controls")]
        public KeyCode toggleKey = KeyCode.F1;

        [Header("Text Settings")]
        public TMP_FontAsset textMeshProFont;
        public float defaultLabelHeight = 2f;
        public float defaultTextSize = 12f;

        [Header("Scene Filters")]
        public string[] ignoredRootNames = { "Environment", "World Script", "Canvas", "ROSConnectionPrefab(Clone)" };

        #endregion

        #region Private Variables

        private bool labelsVisible = false;
        private Dictionary<Transform, GameObject> labelInstances = new Dictionary<Transform, GameObject>();
        private Camera cam;

        #endregion

        #region Unity Callbacks

        private void Start()
        {
            cam = GetComponent<Camera>();
            if (cam == null)
            {
                Debug.LogError("[ObjectDynamicLabelDisplay] Camera component not found on this GameObject.");
                enabled = false;
            }
        }

        private void Update()
        {
            HandleToggleInput();

            if (labelsVisible)
            {
                UpdateLabelOrientation();
            }
        }

        #endregion

        #region Label Handling

        private void HandleToggleInput()
        {
            if (!Input.GetKeyDown(toggleKey))
                return;

            labelsVisible = !labelsVisible;

            if (labelsVisible && labelInstances.Count == 0)
            {
                CreateLabelsForSceneObjects();
            }

            foreach (var label in labelInstances.Values)
            {
                if (label != null)
                    label.SetActive(labelsVisible);
            }

            Debug.Log($"[ObjectDynamicLabelDisplay] Labels visibility toggled: {labelsVisible}");
        }

        private void UpdateLabelOrientation()
        {
            foreach (var kvp in labelInstances)
            {
                if (kvp.Value != null)
                {
                    Transform labelTransform = kvp.Value.transform;
                    labelTransform.rotation = Quaternion.LookRotation(labelTransform.position - cam.transform.position);
                }
            }
        }

        private void CreateLabelsForSceneObjects()
        {
            GameObject[] rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();

            foreach (var obj in rootObjects)
            {
                if (ignoredRootNames.Contains(obj.name) || !obj.activeInHierarchy)
                    continue;

                Transform target = obj.transform;
                if (labelInstances.ContainsKey(target))
                    continue;

                GameObject label = CreateLabelObject(obj);
                labelInstances[target] = label;
            }
        }

        private GameObject CreateLabelObject(GameObject targetObject)
        {
            GameObject label = new GameObject("Label_" + targetObject.name);
            label.transform.SetParent(targetObject.transform);
            label.transform.localPosition = new Vector3(0, defaultLabelHeight, 0);

            // TextMeshPro component
            TMP_Text tmp = label.AddComponent<TextMeshPro>();
            tmp.text = targetObject.name;
            tmp.fontSize = defaultTextSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(1f, 0.95f, 0.8f, 0.5f);
            tmp.enableWordWrapping = false;
            tmp.raycastTarget = false;
            if (textMeshProFont != null)
                tmp.font = textMeshProFont;
            tmp.ForceMeshUpdate();

            // Background quad
            GameObject background = GameObject.CreatePrimitive(PrimitiveType.Quad);
            background.name = "LabelBackground";
            background.transform.SetParent(label.transform);
            background.transform.localRotation = Quaternion.identity;

            Shader shader = Shader.Find("HDRP/Unlit") ?? Shader.Find("Unlit/Color");
            if (shader == null)
            {
                Debug.LogError("[ObjectDynamicLabelDisplay] No suitable unlit shader found!");
            }
            else
            {
                Material bgMat = new Material(shader) { color = new Color(0.2f, 0.25f, 0.3f, 0.5f), renderQueue = 3000 };
                MeshRenderer bgRenderer = background.GetComponent<MeshRenderer>();
                bgRenderer.material = bgMat;
                bgRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                bgRenderer.receiveShadows = false;

                Vector2 textSize = tmp.GetRenderedValues(false);
                background.transform.localPosition = new Vector3(0, 0, 0.01f);
                background.transform.localScale = new Vector3(textSize.x + 1f, textSize.y + 0.8f, 1f);
            }

            label.SetActive(false);
            return label;
        }

        #endregion
    }
}
