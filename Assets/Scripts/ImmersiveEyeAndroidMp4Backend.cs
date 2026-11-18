using System;
using System.Runtime.InteropServices;
using System.IO;
using System.Text;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// v10: Low duty-cycle camera rendering to a GPU H.264 encoder input Surface.
/// Uses the same ordinary URP camera path that produced visible footage in v6,
/// but never leaves the secondary camera enabled at headset refresh rate.
/// A camera render is requested by enabling it during LateUpdate only at the
/// selected recording frame rate, and endCameraRendering submits the result.
/// No MediaProjection, CPU readback, YUV conversion, or headset camera changes.
/// </summary>
[DefaultExecutionOrder(1500)]
[DisallowMultipleComponent]
public sealed class ImmersiveEyeAndroidMp4Backend : MonoBehaviour, IImmersiveEyeVideoBackend
{
    [Header("Low-duty-cycle GPU Camera MP4 (Android GLES3)")]
    [SerializeField, Range(320, 1280)] private int outputWidth = 512;
    [SerializeField, Range(180, 720)] private int outputHeight = 288;
    [SerializeField, Range(5, 24)] private int maximumFps = 8;
    [Header("Video-only fidelity and performance")]
    [SerializeField, Range(1.0f, 1.5f)] private float recordingZoom = 1.20f;
    [Tooltip("Disable only the observation camera's additional shadow pass; XR scene and MP4 pixels stay 1280x720.")]
    [SerializeField] private bool skipObservationCameraShadows = true;
    [Header("Read Only")]
    [SerializeField] private string status = "IDLE";
    [SerializeField] private int queuedFrames;
    [SerializeField] private int skippedFrames;
    [SerializeField] private int cameraRenderedFrames;

    private Camera observationCamera;
    private RenderTexture captureTexture;
    private RenderTexture previousTargetTexture;
    private AndroidJavaObject encoder;
    private AndroidJavaClass gpuJavaClass;
    private bool recording;
    private bool cameraAwaitingRender;
    private bool subscribed;
    private double startTime, nextFrameAt;
    private int fps;
    private IntPtr callback;
    private UniversalAdditionalCameraData observationUrpData;
    private bool hadOriginalRenderShadows;
    private string frameSyncCsvPath;
    private StringBuilder frameSync;
    // IE_HD_PERF_V11: preserve HD, 8fps, correct 1.20x zoom, avoid video-camera shadows, record sync data.

#if UNITY_ANDROID && !UNITY_EDITOR
    [DllImport("ImmersiveEyeGpuEncoder", EntryPoint = "IE_GetRenderEventFunc")]
    private static extern IntPtr GetRenderEventFunc();
#endif

    public string Status => status;

    public bool StartCapture(RenderTexture unusedSource, string outputPath, int requestedFps)
    {
        if (recording || encoder != null) return Fail("Recorder already started");
        if (string.IsNullOrWhiteSpace(outputPath)) return Fail("Missing output path");
#if UNITY_ANDROID && !UNITY_EDITOR
        if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.OpenGLES3)
            return Fail("OpenGLES3 required; current=" + SystemInfo.graphicsDeviceType);
        try
        {
            var owner = GetComponent<ImmersiveEyeVideoRecorder>();
            if (owner == null) owner = FindFirstObjectByType<ImmersiveEyeVideoRecorder>();
            observationCamera = owner != null ? owner.VideoCaptureCamera : null;
            if (observationCamera == null || observationCamera == Camera.main)
                throw new InvalidOperationException("VideoCaptureCamera missing or is XR Main Camera");
            if (!observationCamera.gameObject.activeInHierarchy)
                throw new InvalidOperationException("VideoCaptureCamera GameObject inactive");

            int w = Mathf.Clamp(outputWidth & ~1, 320, 1280);
            int h = Mathf.Clamp(outputHeight & ~1, 180, 720);
            fps = Mathf.Clamp(Mathf.Min(requestedFps, maximumFps), 5, 24);

            captureTexture = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32,
                                               RenderTextureReadWrite.Default);
            captureTexture.name = "ImmersiveEye_GpuCameraGated_v10";
            captureTexture.antiAliasing = 1;
            captureTexture.useMipMap = false;
            captureTexture.autoGenerateMips = false;
            if (!captureTexture.Create()) throw new InvalidOperationException("RenderTexture allocation failed");

            previousTargetTexture = observationCamera.targetTexture;
            observationUrpData = observationCamera.GetComponent<UniversalAdditionalCameraData>();
            if (observationUrpData != null)
            {
                hadOriginalRenderShadows = observationUrpData.renderShadows;
                if (skipObservationCameraShadows) observationUrpData.renderShadows = false;
            }
            frameSyncCsvPath = Path.ChangeExtension(outputPath, ".frames.csv");
            frameSync = new StringBuilder(16384);
            frameSync.AppendLine("frame_index,media_pts_ns,utc_timestamp,unity_unscaled_time_s,unity_frame_number,camera_px,camera_py,camera_pz,camera_qx,camera_qy,camera_qz,camera_qw,vertical_fov_degrees,video_width,video_height");
            observationCamera.stereoTargetEye = StereoTargetEyeMask.None;
            observationCamera.enabled = false;
            observationCamera.targetTexture = captureTexture;
            callback = GetRenderEventFunc();
            if (callback == IntPtr.Zero) throw new InvalidOperationException("GPU plugin event unavailable");
            IntPtr texturePtr = captureTexture.GetNativeTexturePtr();
            if (texturePtr == IntPtr.Zero) throw new InvalidOperationException("GPU texture handle missing");
            using (var javaClass = new AndroidJavaClass("com.immersiveeye.directrecorder.GpuMp4Encoder"))
                javaClass.CallStatic("nativeSetTexture", unchecked((int)texturePtr.ToInt64()), w, h);
            encoder = new AndroidJavaObject("com.immersiveeye.directrecorder.GpuMp4Encoder");
            if (!encoder.Call<bool>("start", outputPath, w, h, fps))
                throw new InvalidOperationException("MediaCodec Surface start failed");
            gpuJavaClass = new AndroidJavaClass("com.immersiveeye.directrecorder.GpuMp4Encoder");

            queuedFrames = skippedFrames = cameraRenderedFrames = 0;
            cameraAwaitingRender = false;
            recording = true;
            startTime = Time.realtimeSinceStartupAsDouble;
            nextFrameAt = startTime;
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
            subscribed = true;
            status = "RECORDING";
            Debug.Log("IE_GPU_SURFACE|V10_CAMERA_GATED_START|" + w + "x" + h + "|fps=" + fps + "|" + outputPath);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("IE_GPU_SURFACE|V10_START_FAILED|" + e);
            if (encoder != null) { try { encoder.Call<bool>("stop"); } catch { } }
            status = "START_FAILED";
            ReleaseResources();
            return false;
        }
#else
        return Fail("Android headset only");
#endif
    }

    private void LateUpdate()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!recording || encoder == null || observationCamera == null || cameraAwaitingRender)
            return;
        double now = Time.realtimeSinceStartupAsDouble;
        if (now < nextFrameAt) return;
        nextFrameAt = now + 1.0 / fps;

        // Camera is ordinarily disabled; it is active for a single URP render.
        // Its head pose already follows the XR camera via the existing
        // ImmersiveEyeCameraFollowHead component, which runs earlier in the frame.
        Camera xrCamera = Camera.main;
        if (xrCamera != null && xrCamera != observationCamera)
        {
            observationCamera.transform.SetPositionAndRotation(
                xrCamera.transform.position, xrCamera.transform.rotation);
            // v10 overwrote the user's recording-only zoom every frame.
            // Explicit 1.20x pinhole zoom on the observer; never touch XR Main Camera.
            float sourceHalf = xrCamera.fieldOfView * Mathf.Deg2Rad * 0.5f;
            observationCamera.fieldOfView = 2f * Mathf.Atan(
                Mathf.Tan(sourceHalf) / Mathf.Max(1f, recordingZoom)) * Mathf.Rad2Deg;
            observationCamera.cullingMask = xrCamera.cullingMask;
        }
        cameraAwaitingRender = true;
        observationCamera.enabled = true;
#endif
    }

    private void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!recording || !cameraAwaitingRender || observationCamera != camera) return;
        cameraRenderedFrames++;
        cameraAwaitingRender = false;
        observationCamera.enabled = false;
        try
        {
            double now = Time.realtimeSinceStartupAsDouble;
            long timestampNs = Math.Max(0L, (long)((now - startTime) * 1000000000.0));
            if (gpuJavaClass != null && gpuJavaClass.CallStatic<bool>("nativeQueueFrame", timestampNs))
            {
                GL.IssuePluginEvent(callback, 1);
                queuedFrames++;
                // Recorded alongside MP4, with the same monotonic PTS submitted to MediaCodec.
                // Actual encoder/display sync still needs validation with a timing marker.
                if (frameSync != null)
                {
                    var pos = camera.transform.position;
                    var rot = camera.transform.rotation;
                    var ci = CultureInfo.InvariantCulture;
                    frameSync.Append(queuedFrames - 1).Append(',').Append(timestampNs).Append(',')
                        .Append(DateTime.UtcNow.ToString("O")).Append(',')
                        .Append(Time.unscaledTimeAsDouble.ToString("F6", ci)).Append(',')
                        .Append(Time.frameCount).Append(',')
                        .Append(pos.x.ToString("R", ci)).Append(',')
                        .Append(pos.y.ToString("R", ci)).Append(',')
                        .Append(pos.z.ToString("R", ci)).Append(',')
                        .Append(rot.x.ToString("R", ci)).Append(',')
                        .Append(rot.y.ToString("R", ci)).Append(',')
                        .Append(rot.z.ToString("R", ci)).Append(',')
                        .Append(rot.w.ToString("R", ci)).Append(',')
                        .Append(camera.fieldOfView.ToString("R", ci)).Append(',')
                        .Append(captureTexture != null ? captureTexture.width : 0).Append(',')
                        .Append(captureTexture != null ? captureTexture.height : 0).AppendLine();
                }
                if (queuedFrames == 1)
                    Debug.Log("IE_GPU_SURFACE|V10_FIRST_CAMERA_FRAME_QUEUED");
            }
            else skippedFrames++;
        }
        catch (Exception e)
        {
            Debug.LogError("IE_GPU_SURFACE|V10_FRAME_FAILED|" + e);
            status = "FRAME_FAILED";
            recording = false;
            observationCamera.enabled = false;
        }
#endif
    }

    public bool StopCapture()
    {
        recording = false;
        if (observationCamera != null) observationCamera.enabled = false;
        bool saved = false;
        try
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (encoder != null) saved = encoder.Call<bool>("stop");
            if (callback != IntPtr.Zero) GL.IssuePluginEvent(callback, 2);
#endif
            if (saved && frameSync != null && queuedFrames > 0 && !string.IsNullOrEmpty(frameSyncCsvPath))
            {
                try { File.WriteAllText(frameSyncCsvPath, frameSync.ToString(), new UTF8Encoding(false)); }
                catch (Exception e) { Debug.LogError("IE_FRAME_SYNC|WRITE_FAILED|" + e.Message); }
            }
            status = saved ? "MP4_SAVED" : "MP4_NOT_SAVED";
            Debug.Log("IE_GPU_SURFACE|V10_STOP|" + status +
                      "|cameraFrames=" + cameraRenderedFrames +
                      "|queued=" + queuedFrames + "|skipped=" + skippedFrames);
        }
        catch (Exception e)
        {
            status = "STOP_FAILED";
            Debug.LogError("IE_GPU_SURFACE|V10_STOP_FAILED|" + e);
        }
        finally { ReleaseResources(); }
        return saved;
    }

    private bool Fail(string reason)
    {
        status = reason;
        Debug.LogError("IE_GPU_SURFACE|V10_UNAVAILABLE|" + reason);
        return false;
    }

    private void ReleaseResources()
    {
        recording = false;
        cameraAwaitingRender = false;
        if (subscribed)
        {
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
            subscribed = false;
        }
        if (observationCamera != null)
        {
            observationCamera.enabled = false;
            observationCamera.targetTexture = previousTargetTexture;
        }
        if (observationUrpData != null)
        {
            if (skipObservationCameraShadows) observationUrpData.renderShadows = hadOriginalRenderShadows;
            observationUrpData = null;
        }
        previousTargetTexture = null;
        observationCamera = null;
        frameSync = null;
        frameSyncCsvPath = null;
        if (encoder != null) { try { encoder.Dispose(); } catch { } encoder = null; }
        if (gpuJavaClass != null) { try { gpuJavaClass.Dispose(); } catch { } gpuJavaClass = null; }
        if (captureTexture != null)
        {
            captureTexture.Release();
            Destroy(captureTexture);
            captureTexture = null;
        }
        callback = IntPtr.Zero;
    }

    private void OnDisable() { if (encoder != null) StopCapture(); }
    private void OnDestroy() { if (encoder != null) StopCapture(); }
}
