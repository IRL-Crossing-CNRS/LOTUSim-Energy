// --------------------------------------------------------------------------------------------------------------------
// InspectionDetector.cs
//
// Description:
//   Captures frames from a Unity Camera, publishes them over ROS as sensor_msgs/CompressedImage,
//   and draws bounding boxes over detected corrosion and cracks in the Game View.
//   Detections arrive asynchronously on a separate ROS topic (std_msgs/String, JSON).
//   - Orange boxes = corrosion (color detection)
//   - Red/green boxes = cracks (YOLO detection)
//
//   All ROS I/O is routed through RosInterface:
//     publish    {ns}/{agent}/inspection/image       (sensor_msgs/CompressedImage)
//     subscribe  {ns}/{agent}/inspection/detections  (std_msgs/String, JSON)
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Lotusim
{
    [RequireComponent(typeof(Camera))]
    public class InspectionDetector : MonoBehaviour
    {
        #region Inspector Fields

        // NOTE: This component does NOT run any detection. Corrosion (HSV) and crack (YOLO)
        // detection run entirely on the remote/host ROS node. The fields below only control
        // what this client captures/publishes and how it *draws* the boxes it receives back.

        [Header("Camera Capture → published over ROS")]
        [Tooltip("Camera used for frame capture. Auto-detected if left null.")]
        public Camera snapCam;

        [Tooltip("Width of the JPEG frame published to the remote detector (pixels).")]
        public int imageWidth = 640;

        [Tooltip("Height of the JPEG frame published to the remote detector (pixels).")]
        public int imageHeight = 480;

        [Tooltip("Interval (seconds) between published frames. The remote detects on each frame it receives.")]
        [SerializeField] private float inferenceRate = 0.5f;

        [Header("ROS Topic")]
        [Tooltip("Optional: override the agent name used in the ROS topics. Leave empty to derive from the root GameObject name.")]
        public string agentNameOverride = "";

        [Header("Overlay Display (local only — does NOT affect detection)")]
        [Tooltip("Box colour for detections tagged source=\"color\" (corrosion) by the remote.")]
        [FormerlySerializedAs("corrosionColor")]
        public Color corrosionBoxColor = new Color(1f, 0.5f, 0f); // orange

        [Tooltip("Box colour for detections tagged source=\"yolo\" (cracks) by the remote.")]
        [FormerlySerializedAs("crackColor")]
        public Color crackBoxColor = Color.red;

        [Tooltip("Show the label above each drawn box.")]
        public bool showLabels = true;

        [Tooltip("Hide received crack boxes below this confidence. Display filter only — the remote already runs YOLO and decides what counts as a detection.")]
        [Range(0f, 1f)]
        [FormerlySerializedAs("yoloConfidenceThreshold")]
        public float crackDisplayThreshold = 0.05f;

        #endregion

        #region Private Fields

        private float timeElapsed = 0f;
        private bool isProcessing = false;
        private string resolvedAgentName;
        private List<Detection> currentDetections = new List<Detection>();
        private List<Detection> uiSnapshot = new List<Detection>();
        private readonly object detectionLock = new object();
        
        // Reusable Textures (to prevent Garbage Collection spikes)
        private RenderTexture renderTex;
        private Texture2D captureTex;

        // UI Fields
        private Canvas uiCanvas;
        private RectTransform uiContainer;
        private UnityEngine.UI.Text statusText;
        private List<GameObject> boxPool = new List<GameObject>();
        private Font defaultFont;
        
        private int lastCorrosionCount = -1;
        private int lastCrackCount = -1;
        private bool lastIsProcessing = false;

        #endregion

        #region Data Structures

        [Serializable]
        public class Detection
        {
            public string label;
            public float confidence;
            public int x, y, w, h, cx, cy;
            public string source; // "color" = corrosion, "yolo" = crack
        }

        [Serializable]
        private class DetectionResponse
        {
            public Detection[] detections;
            public int count;
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (snapCam == null) snapCam = GetComponent<Camera>();
            if (snapCam == null) snapCam = GetComponentInChildren<Camera>();
            if (snapCam == null)
            {
                Debug.LogError("[InspectionDetector] No camera found!");
                enabled = false;
                return;
            }

            // Pre-allocate textures to avoid Memory Allocation lag spikes.
            // Explicit color format + Create() so the GPU resource exists before the
            // first Render()/ReadPixels — an uninitialised RenderTexture is one more
            // way to fault the Vulkan backend on the render thread.
            renderTex = new RenderTexture(imageWidth, imageHeight, 24, RenderTextureFormat.ARGB32);
            renderTex.Create();
            captureTex = new Texture2D(imageWidth, imageHeight, TextureFormat.RGB24, false);

            InitializeUI();
        }

        private void OnDestroy()
        {
            if (resolvedAgentName != null && RosInterface.Instance.IsConnected)
                RosInterface.Instance.CleanupInspection(resolvedAgentName);

            if (renderTex != null)
            {
                renderTex.Release();
                Destroy(renderTex);
            }
            if (captureTex != null)
                Destroy(captureTex);
        }

        private void InitializeUI()
        {
            defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (defaultFont == null) defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

            // Create Canvas
            GameObject canvasObj = new GameObject("InspectionCanvas");
            canvasObj.transform.SetParent(snapCam.transform, false);
            uiCanvas = canvasObj.AddComponent<Canvas>();
            uiCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            uiCanvas.worldCamera = snapCam;
            uiCanvas.planeDistance = snapCam.nearClipPlane + 0.1f;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            // Container to hold elements with Top-Left origin
            GameObject containerObj = new GameObject("Container");
            containerObj.transform.SetParent(canvasObj.transform, false);
            uiContainer = containerObj.AddComponent<RectTransform>();
            // Stretch container to fill the screen
            uiContainer.anchorMin = new Vector2(0, 0);
            uiContainer.anchorMax = new Vector2(1, 1);
            uiContainer.pivot = new Vector2(0.5f, 0.5f);
            uiContainer.offsetMin = Vector2.zero; // Bottom-Left offset
            uiContainer.offsetMax = Vector2.zero; // Top-Right offset

            // Status Text
            GameObject statusObj = new GameObject("StatusText");
            statusObj.transform.SetParent(uiContainer, false);
            RectTransform statusRect = statusObj.AddComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0, 1);
            statusRect.anchorMax = new Vector2(0, 1);
            statusRect.pivot = new Vector2(0, 1);
            statusRect.anchoredPosition = new Vector2(10, -10);
            statusRect.sizeDelta = new Vector2(350, 30);

            statusText = statusObj.AddComponent<UnityEngine.UI.Text>();
            statusText.font = defaultFont;
            statusText.fontSize = 25;
            statusText.fontStyle = FontStyle.Bold;
            statusText.color = Color.white;
            statusText.horizontalOverflow = HorizontalWrapMode.Overflow;
            statusText.verticalOverflow = VerticalWrapMode.Overflow;

        }

        private void Start()
        {
            resolvedAgentName = string.IsNullOrWhiteSpace(agentNameOverride)
                ? DeriveAgentName(transform.root.name)
                : agentNameOverride.Trim();

            if (RosInterface.Instance.IsConnected)
            {
                RosInterface.Instance.RegisterInspectionDetectionsSubscription(resolvedAgentName);
                Debug.Log($"[InspectionDetector] Starting — agent='{resolvedAgentName}' — publishing frames over ROS.");
            }
            else
            {
                Debug.LogWarning("[InspectionDetector] ROS not connected — no frames will be published.");
            }
        }

        // Strips characters invalid in ROS names (spaces, dots, etc.) while preserving underscores,
        // which are valid in both ROS node names and topic paths (e.g. "mybluerov0_0" stays "mybluerov0_0").
        private static string DeriveAgentName(string raw)
        {
            return System.Text.RegularExpressions.Regex.Replace(raw ?? "", @"[^A-Za-z0-9_]", "").ToLowerInvariant();
        }

        private void Update()
        {
            // Consume any detections received from the remote since last frame
            string detectionsJson = RosInterface.Instance.IsConnected
                ? RosInterface.Instance.ConsumeInspectionDetections(resolvedAgentName)
                : null;
            if (detectionsJson != null)
                ParseDetections(detectionsJson);

            timeElapsed += Time.deltaTime;
            if (timeElapsed >= inferenceRate && !isProcessing)
            {
                timeElapsed = 0f;
                StartCoroutine(CaptureAndPublish());
            }

            UpdateUI();
        }

        private void UpdateUI()
        {
            lock (detectionLock)
            {
                uiSnapshot.Clear();
                uiSnapshot.AddRange(currentDetections);
            }

            int corrosionCount = 0;
            int crackCount = 0;
            int activeBoxes = 0;

            float scaleX = (float)snapCam.pixelWidth / imageWidth;
            float scaleY = (float)snapCam.pixelHeight / imageHeight;

            foreach (var det in uiSnapshot)
            {
                bool isCorrosion = det.source == "color";
                if (!isCorrosion && det.confidence < crackDisplayThreshold) continue;

                if (activeBoxes >= boxPool.Count)
                {
                    boxPool.Add(CreateBoxPrefab());
                }

                GameObject boxObj = boxPool[activeBoxes];
                boxObj.SetActive(true);

                RectTransform boxRect = boxObj.GetComponent<RectTransform>();
                
                float screenX = det.x * scaleX;
                float screenY = det.y * scaleY;
                float screenW = det.w * scaleX;
                float screenH = det.h * scaleY;

                boxRect.anchoredPosition = new Vector2(screenX, -screenY);
                boxRect.sizeDelta = new Vector2(screenW, screenH);

                Color boxCol = isCorrosion ? corrosionBoxColor : crackBoxColor;
                
                foreach(Image img in boxObj.GetComponentsInChildren<Image>())
                {
                    img.color = boxCol;
                }

                UnityEngine.UI.Text labelTxt = boxObj.GetComponentInChildren<UnityEngine.UI.Text>();
                if (showLabels)
                {
                    labelTxt.enabled = true;
                    string tag = isCorrosion ? "corrosion" : det.label;
                    labelTxt.text = tag;
                }
                else
                {
                    labelTxt.enabled = false;
                }

                if (isCorrosion) corrosionCount++;
                else crackCount++;

                activeBoxes++;
            }

            for (int i = activeBoxes; i < boxPool.Count; i++)
            {
                boxPool[i].SetActive(false);
            }

            if (isProcessing != lastIsProcessing || corrosionCount != lastCorrosionCount || crackCount != lastCrackCount)
            {
                statusText.color = isProcessing ? Color.yellow : Color.green;
                statusText.text = isProcessing ? "Detecting..." : $"Corrosion: {corrosionCount}  |  Cracks: {crackCount}";
                lastIsProcessing = isProcessing;
                lastCorrosionCount = corrosionCount;
                lastCrackCount = crackCount;
            }
        }

        private GameObject CreateBoxPrefab()
        {
            GameObject boxObj = new GameObject("Box");
            boxObj.transform.SetParent(uiContainer, false);
            RectTransform boxRect = boxObj.AddComponent<RectTransform>();
            boxRect.anchorMin = new Vector2(0, 1);
            boxRect.anchorMax = new Vector2(0, 1);
            boxRect.pivot = new Vector2(0, 1);

            float thickness = 3f;

            CreateEdge(boxObj.transform, "Top", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(0, thickness), new Vector2(0, 0));
            CreateEdge(boxObj.transform, "Bottom", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, thickness), new Vector2(0, 0));
            CreateEdge(boxObj.transform, "Left", new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 1), new Vector2(thickness, 0), new Vector2(0, 0));
            CreateEdge(boxObj.transform, "Right", new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 1), new Vector2(thickness, 0), new Vector2(0, 0));

            GameObject textObj = new GameObject("Label");
            textObj.transform.SetParent(boxObj.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0, 1);
            textRect.anchorMax = new Vector2(0, 1);
            textRect.pivot = new Vector2(0, 1);
            textRect.anchoredPosition = new Vector2(100, -2);
            textRect.sizeDelta = new Vector2(300, 30);
            
            UnityEngine.UI.Text txt = textObj.AddComponent<UnityEngine.UI.Text>();
            txt.font = defaultFont;
            txt.fontSize = 25;
            txt.fontStyle = FontStyle.Bold;
            txt.color = Color.white;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;

            return boxObj;
        }

        private void CreateEdge(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 sizeDelta, Vector2 anchoredPos)
        {
            GameObject edgeObj = new GameObject(name);
            edgeObj.transform.SetParent(parent, false);
            RectTransform rect = edgeObj.AddComponent<RectTransform>();
            rect.anchorMin = aMin;
            rect.anchorMax = aMax;
            rect.pivot = pivot;
            rect.sizeDelta = sizeDelta;
            rect.anchoredPosition = anchoredPos;
            
            Image img = edgeObj.AddComponent<Image>();
            img.color = Color.white;
        }

        #endregion

        #region Capture & Inference

        private IEnumerator CaptureAndPublish()
        {
            isProcessing = true;

            // ALL GPU work must happen at the END of the frame.
            //
            // Root cause of the historical SIGSEGV in Unity's "UnityGfxDeviceW" render
            // thread (seen on both NVIDIA/Optimus and Mesa/Intel Vulkan): this capture
            // ran inside Update(), mid-frame. Rendering the camera and reading its pixels
            // back before the frame's own rendering had finished raced the render thread —
            // ReadPixels touched a RenderTexture the GPU was still writing. It faults
            // intermittently ("after a while"), and most reliably once DroneCameraHUD
            // switches this camera to the active display, so Unity's auto-render and our
            // manual Render() collide in the same frame.
            //
            // WaitForEndOfFrame moves the whole capture past the frame's normal rendering
            // (including this camera's auto-render when it is the active HUD view), turning
            // it into a clean, well-ordered second pass instead of a mid-frame injection.
            // Switching async→sync ReadPixels alone (the previous attempted fix) did NOT
            // help, because the bug was the *timing*, not the async path.
            yield return new WaitForEndOfFrame();

            // 1. Render to our reusable RenderTexture.
            // Disable every canvas that lives on this camera so nothing from the HUD
            // (InspectionDetector overlay or DroneCameraHUD switcher) is baked into
            // the frame published over ROS.  Unity's normal Game View rendering
            // is unaffected — the canvases are re-enabled immediately after.
            Canvas[] childCanvases = snapCam.GetComponentsInChildren<Canvas>(true);
            foreach (var c in childCanvases) c.enabled = false;
            RenderTexture prevTarget = snapCam.targetTexture;
            snapCam.targetTexture = renderTex;
            snapCam.Render();
            snapCam.targetTexture = prevTarget;
            foreach (var c in childCanvases) c.enabled = true;

            // 2. Read the pixels back SYNCHRONOUSLY. We are now at end-of-frame, so the
            //    render above has completed and the RenderTexture is safe to read.
            RenderTexture prevActive = RenderTexture.active;
            RenderTexture.active = renderTex;
            captureTex.ReadPixels(new Rect(0, 0, imageWidth, imageHeight), 0, 0);
            captureTex.Apply(false);
            RenderTexture.active = prevActive;

            // 3. Encode to JPG on the main thread (requires Unity API)
            byte[] jpgBytes = captureTex.EncodeToJPG(75);

            // 4. Publish over ROS as sensor_msgs/CompressedImage. Detections come back
            //    asynchronously on {ns}/{agent}/inspection/detections and are consumed in Update().
            if (RosInterface.Instance.IsConnected)
                RosInterface.Instance.PublishInspectionImage(resolvedAgentName, jpgBytes);

            isProcessing = false;
        }

        private void ParseDetections(string json)
        {
            try
            {
                DetectionResponse response = JsonUtility.FromJson<DetectionResponse>(json);
                lock (detectionLock)
                {
                    currentDetections.Clear();
                    if (response?.detections != null)
                        currentDetections.AddRange(response.detections);
                }
                if (response != null)
                    Debug.Log($"[InspectionDetector] {response.count} detection(s) received.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[InspectionDetector] JSON parse error: {e.Message}");
            }
        }

        #endregion

        #region Public API

        public List<Detection> GetLatestDetections()
        {
            lock (detectionLock)
                return new List<Detection>(currentDetections);
        }

        #endregion
    }
}
