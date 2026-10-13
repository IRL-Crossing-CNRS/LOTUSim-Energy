using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Sensor;

public class CameraPublisher : MonoBehaviour
{
    public string topicImage = "sensors/image/raw";
    public string topicCompressed = "sensors/image/compressed";
    public string topicCameraInfo = "sensors/image/camera_info";

    public bool publishRawImage = true;
    public bool publishCompressedImage = false;

    [Range(1, 100)] public int jpegQuality = 75;
    public float publishRate = 60f;
    public Camera cam;

    private ROSConnection ros;
    private RenderTexture renderTexture;

    private float nextPublishTime;
    private bool readbackInProgress;

    // L'encodage JPEG part sur un thread de travail : rien de cette chaine ne
    // doit faire attendre LOTUSim, sinon les FPS tombent et la simulation
    // n'est plus temps reel. Le resultat revient par cette file, vidée depuis
    // la boucle de publication puisque l'envoi ROS reste au main thread.
    private readonly ConcurrentQueue<byte[]> pendingJpegs = new ConcurrentQueue<byte[]>();
    private bool initialised;

    private int camW;
    private int camH;

    private double fx, fy, cx, cy;

    // Détermine une seule fois si un flip vertical est nécessaire.
    // Sous OpenGL/OpenGLES, l'origine V=0 est en bas -> AsyncGPUReadback
    // renvoie les lignes du bas vers le haut, il faut donc les inverser.
    // Sous Direct3D/Metal/Vulkan (selon la plateforme), l'origine V=0 est
    // déjà en haut -> aucune correction supplémentaire n'est nécessaire.
    // On calcule ce booléen UNE seule fois pour éviter tout double-flip.
    private bool needsVerticalFlip;

    void Start()
    {
        if (cam == null)
        {
            Debug.LogError("Aucune caméra assignée !");
            enabled = false;
            return;
        }

        ros = ROSConnection.GetOrCreateInstance();

        camW = Screen.width;
        camH = Screen.height;

        double fov = cam.fieldOfView * Math.PI / 180.0;

        fy = camH / (2.0 * Math.Tan(fov / 2.0));
        fx = fy;

        cx = (camW - 1) / 2.0;
        cy = (camH - 1) / 2.0;

        needsVerticalFlip = !SystemInfo.graphicsUVStartsAtTop;

        Debug.Log($"Camera resolution: {camW} x {camH}");
        Debug.Log(
            $"Camera calibration: " +
            $"fx={fx:F3}, fy={fy:F3}, " +
            $"cx={cx:F3}, cy={cy:F3}"
        );
        Debug.Log($"Graphics API flip needed: {needsVerticalFlip}");

        renderTexture = new RenderTexture(
            camW,
            camH,
            24,
            RenderTextureFormat.ARGB32
        );

        renderTexture.Create();


        if (publishRawImage)
            ros.RegisterPublisher<ImageMsg>(topicImage);

        if (publishCompressedImage)
            ros.RegisterPublisher<CompressedImageMsg>(topicCompressed);

        ros.RegisterPublisher<CameraInfoMsg>(topicCameraInfo);

        nextPublishTime = Time.time;

        initialised = true;
        StartCoroutine(PublishLoop());
    }

    // Unity stops a behaviour's coroutines when it is disabled and does not
    // restart them, and CameraModeSwitcher disables this camera whenever the
    // view changes. Restarting the loop here keeps publishing alive across
    // those switches; Update() used to resume on its own.
    void OnEnable()
    {
        if (initialised)
            StartCoroutine(PublishLoop());
    }

    // Capturing from Update() calls cam.Render() mid-frame, while HDRP is still
    // building the frame. On Linux/Vulkan that faults inside the water system:
    //   CameraPublisher:PublishImage -> Camera:Render -> HDRenderPipeline:Render
    //   -> UpdateWaterSurfaces -> UpdateGPUWaterSimulation -> RenderTexture:GenerateMips
    // (SIGSEGV at addr 0x20). WaitForEndOfFrame moves the capture past the frame's
    // own rendering, turning it into a clean second pass -- the same fix already
    // applied to InspectionDetector for an identical mid-frame crash.
    private System.Collections.IEnumerator PublishLoop()
    {
        while (true)
        {
            yield return new WaitForEndOfFrame();

            DrainPendingJpegs();

            if ((!publishRawImage && !publishCompressedImage) ||
                Time.time < nextPublishTime ||
                readbackInProgress)
                continue;

            PublishImage();

            nextPublishTime = Time.time + 1f / publishRate;
        }
    }

    void PublishImage()
    {
        readbackInProgress = true;

        RenderTexture previous = cam.targetTexture;

        cam.targetTexture = renderTexture;
        cam.Render();
        cam.targetTexture = previous;

        // Plus de Blit intermédiaire ici : on lit directement renderTexture.
        // La correction d'orientation (s'il y en a besoin) est appliquée
        // une seule fois, en CPU, dans OnCompleteReadback.
        AsyncGPUReadback.Request(
            renderTexture,
            0,
            TextureFormat.RGB24,
            OnCompleteReadback
        );
    }

    void OnCompleteReadback(
        AsyncGPUReadbackRequest request
    )
    {
        readbackInProgress = false;

        if (request.hasError)
        {
            Debug.LogError("Erreur AsyncGPUReadback");
            return;
        }

        byte[] data =
            request.GetData<byte>().ToArray();

        // Unique point de correction verticale, appliqué une seule fois.
        if (needsVerticalFlip)
            data = FlipVertical(data, camW, camH, 3);

        if (publishRawImage)
            PublishRaw(data);

        if (publishCompressedImage)
            PublishCompressed(data);

        PublishCameraInfo();
    }

    // Inverse l'ordre des lignes d'une image RGB24 stockée en row-major.
    // Appelée au maximum une fois par frame publiée (jamais en doublon
    // avec une éventuelle correction faite ailleurs, comme un Blit).
    private static byte[] FlipVertical(byte[] src, int width, int height, int bpp)
    {
        int stride = width * bpp;
        byte[] dst = new byte[src.Length];

        for (int y = 0; y < height; y++)
        {
            int srcOffset = y * stride;
            int dstOffset = (height - 1 - y) * stride;
            Buffer.BlockCopy(src, srcOffset, dst, dstOffset, stride);
        }

        return dst;
    }

    void PublishRaw(byte[] data)
    {
        ImageMsg msg = new ImageMsg();

        msg.header =
            new RosMessageTypes.Std.HeaderMsg();

        msg.header.frame_id =
            "camera_frame";

        msg.height =
            (uint)camH;

        msg.width =
            (uint)camW;

        msg.encoding =
            "rgb8";

        msg.is_bigendian =
            0;

        msg.step =
            (uint)(camW * 3);

        msg.data =
            data;

        ros.Publish(
            topicImage,
            msg
        );
    }

    void PublishCompressed(byte[] data)
    {
        int w = camW, h = camH, q = jpegQuality;
        Task.Run(() =>
        {
            byte[] jpg = ImageConversion.EncodeArrayToJPG(
                data, GraphicsFormat.R8G8B8_UNorm, (uint)w, (uint)h, 0, q);
            pendingJpegs.Enqueue(jpg);
        });
    }

    // Envoi ROS : au main thread, mais sans jamais attendre. Ce qui n'est pas
    // pret a cette frame partira a la suivante.
    void DrainPendingJpegs()
    {
        while (pendingJpegs.TryDequeue(out byte[] jpg))
        {
            CompressedImageMsg msg = new CompressedImageMsg();
            msg.header = new RosMessageTypes.Std.HeaderMsg();
            msg.header.frame_id = "camera_frame";
            msg.format = "jpeg";
            msg.data = jpg;
            ros.Publish(topicCompressed, msg);
        }
    }

    void PublishCameraInfo()
    {
        CameraInfoMsg msg =
            new CameraInfoMsg();

        msg.header =
            new RosMessageTypes.Std.HeaderMsg();

        msg.header.frame_id =
            "camera_frame";

        msg.height =
            (uint)camH;

        msg.width =
            (uint)camW;

        msg.distortion_model =
            "plumb_bob";

        msg.d =
            new double[5];

        msg.k =
            new double[]
            {
                fx, 0, cx,
                0, fy, cy,
                0, 0, 1
            };

        msg.r =
            new double[]
            {
                1, 0, 0,
                0, 1, 0,
                0, 0, 1
            };

        msg.p =
            new double[]
            {
                fx, 0, cx, 0,
                0, fy, cy, 0,
                0, 0, 1, 0
            };

        ros.Publish(
            topicCameraInfo,
            msg
        );
    }

    void OnDestroy()
    {
        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
        }

    }
}