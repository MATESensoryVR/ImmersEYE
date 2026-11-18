package com.immersiveeye.directrecorder;

import android.media.MediaCodec;
import android.media.MediaCodecInfo;
import android.media.MediaFormat;
import android.media.MediaMuxer;
import android.view.Surface;
import android.util.Log;

import java.io.File;
import java.nio.ByteBuffer;
import java.util.concurrent.atomic.AtomicBoolean;

/**
 * MediaCodec surface-input H.264 encoder. Unity GLES3 GPU frames are submitted by
 * native plugin directly to this Surface. No CPU RGBA readback or YUV conversion.
 * No MediaProjection or Android recording permission. Android API 29+.
 */
public final class GpuMp4Encoder {
    private static final String TAG = "IE_GPU_SURFACE";
    static { System.loadLibrary("ImmersiveEyeGpuEncoder"); }

    private static native boolean nativeSetSurface(Surface surface, int width, int height);
    public static native void nativeSetTexture(int textureName, int width, int height);
    public static native boolean nativeQueueFrame(long timestampNs);
    public static native void nativeStopCapture();
    public static native int nativeSubmitted();

    private MediaCodec codec;
    private MediaMuxer muxer;
    private Surface surface;
    private Thread drainThread;
    private final AtomicBoolean failed = new AtomicBoolean(false);
    private volatile boolean eosSeen = false;
    private volatile boolean started = false;
    private volatile boolean muxerStarted = false;
    private volatile int samplesWritten = 0;
    private String outputPath;

    public synchronized boolean start(String path, int width, int height, int fps) {
        if (started || codec != null || width < 2 || height < 2 || (width & 1) != 0 || (height & 1) != 0)
            return false;
        outputPath = path;
        try {
            File file = new File(path);
            File parent = file.getParentFile();
            if (parent == null || (!parent.isDirectory() && !parent.mkdirs()))
                throw new IllegalStateException("Cannot create output folder");
            if (file.exists()) throw new IllegalStateException("Output MP4 already exists");

            MediaFormat format = MediaFormat.createVideoFormat("video/avc", width, height);
            format.setInteger(MediaFormat.KEY_COLOR_FORMAT,
                              MediaCodecInfo.CodecCapabilities.COLOR_FormatSurface);
            format.setInteger(MediaFormat.KEY_BIT_RATE, 1800000);
            format.setInteger(MediaFormat.KEY_FRAME_RATE, fps);
            format.setInteger(MediaFormat.KEY_I_FRAME_INTERVAL, 1);
            codec = MediaCodec.createEncoderByType("video/avc");
            codec.configure(format, null, null, MediaCodec.CONFIGURE_FLAG_ENCODE);
            surface = codec.createInputSurface();
            if (!nativeSetSurface(surface, width, height))
                throw new IllegalStateException("Could not attach encoder Surface to GPU bridge");
            muxer = new MediaMuxer(path, MediaMuxer.OutputFormat.MUXER_OUTPUT_MPEG_4);
            codec.start();
            eosSeen = false;
            failed.set(false);
            samplesWritten = 0;
            muxerStarted = false;
            started = true;
            drainThread = new Thread(this::drainLoop, "ImmersiveEye-H264-GPU-Drain");
            drainThread.start();
            Log.i(TAG, "ENCODER_START " + width + "x" + height + " fps=" + fps + " path=" + path);
            return true;
        } catch (Throwable e) {
            Log.e(TAG, "ENCODER_START_FAILED", e);
            failed.set(true);
            nativeStopCapture();
            releaseCodec();
            return false;
        }
    }

    private void drainLoop() {
        final MediaCodec.BufferInfo info = new MediaCodec.BufferInfo();
        int track = -1;
        try {
            // Remain responsive without blocking Unity's main/render thread.
            while (!eosSeen && !failed.get()) {
                int index = codec.dequeueOutputBuffer(info, 10000);
                if (index == MediaCodec.INFO_TRY_AGAIN_LATER) continue;
                if (index == MediaCodec.INFO_OUTPUT_FORMAT_CHANGED) {
                    if (muxerStarted) throw new IllegalStateException("Repeated codec format change");
                    track = muxer.addTrack(codec.getOutputFormat());
                    muxer.start();
                    muxerStarted = true;
                } else if (index >= 0) {
                    ByteBuffer data = codec.getOutputBuffer(index);
                    if ((info.flags & MediaCodec.BUFFER_FLAG_CODEC_CONFIG) != 0) info.size = 0;
                    if (info.size > 0) {
                        if (!muxerStarted || data == null)
                            throw new IllegalStateException("Encoded buffer before track setup");
                        data.position(info.offset);
                        data.limit(info.offset + info.size);
                        muxer.writeSampleData(track, data, info);
                        ++samplesWritten;
                    }
                    eosSeen = (info.flags & MediaCodec.BUFFER_FLAG_END_OF_STREAM) != 0;
                    codec.releaseOutputBuffer(index, false);
                }
            }
        } catch (Throwable e) {
            failed.set(true);
            Log.e(TAG, "ENCODER_DRAIN_FAILED", e);
        }
    }

    public synchronized boolean stop() {
        if (!started) return false;
        started = false;
        nativeStopCapture();
        try {
            codec.signalEndOfInputStream();
            if (drainThread != null) drainThread.join(4500);
        } catch (Throwable e) {
            failed.set(true);
            Log.e(TAG, "ENCODER_STOP_FAILED", e);
        }
        if (drainThread != null && drainThread.isAlive()) {
            failed.set(true);
            Log.e(TAG, "ENCODER_DRAIN_TIMEOUT");
            // Avoid release racing the worker. Do not hang the research application.
            return false;
        }
        final boolean saved = !failed.get() && eosSeen && muxerStarted && samplesWritten > 0;
        Log.i(TAG, "ENCODER_STOP saved=" + saved + " packets=" + samplesWritten + " gpuFrames=" + nativeSubmitted());
        releaseCodec();
        return saved;
    }

    private void releaseCodec() {
        try { if (codec != null) codec.stop(); } catch (Throwable ignore) { }
        try { if (codec != null) codec.release(); } catch (Throwable ignore) { }
        codec = null;
        try { if (surface != null) surface.release(); } catch (Throwable ignore) { }
        surface = null;
        try { if (muxer != null && muxerStarted) muxer.stop(); } catch (Throwable ignore) { }
        try { if (muxer != null) muxer.release(); } catch (Throwable ignore) { }
        muxer = null;
        drainThread = null;
        muxerStarted = false;
        if (outputPath != null && new File(outputPath).length() == 0L)
            new File(outputPath).delete();
    }
}
