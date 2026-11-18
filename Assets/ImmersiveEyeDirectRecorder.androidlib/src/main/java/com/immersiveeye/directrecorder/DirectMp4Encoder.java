package com.immersiveeye.directrecorder;

import android.media.Image;
import android.media.MediaCodec;
import android.media.MediaCodecInfo;
import android.media.MediaFormat;
import android.media.MediaMuxer;
import android.util.Log;

import java.io.File;
import java.nio.ByteBuffer;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.Future;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicBoolean;

/**
 * Standalone Unity texture -> RGBA readback -> YUV420 hardware H.264 encoder.
 * No MediaProjection, screen-capture intent, overlay, or Android permission dialog.
 * All codec/muxer writes happen on a single worker; at most one frame is queued.
 */
public final class DirectMp4Encoder {
    private static final String TAG = "IE_DIRECT_MP4";
    private ExecutorService worker;
    private MediaCodec codec;
    private MediaMuxer muxer;
    private MediaCodec.BufferInfo bufferInfo;
    private int trackIndex = -1;
    private boolean muxerStarted;
    private volatile boolean recording;
    private volatile boolean failed;
    private final AtomicBoolean queued = new AtomicBoolean(false);
    private int width, height, fps;
    private boolean flipVertical;
    private long lastPtsUs = -1L;
    private String path;
    private int encoded;

    public synchronized boolean start(final String outputPath, final int w, final int h,
                                       final int requestedFps, final boolean flip) {
        if (worker != null || w < 2 || h < 2 || (w & 1) != 0 || (h & 1) != 0)
            return false;
        width = w;
        height = h;
        fps = Math.max(1, requestedFps);
        flipVertical = flip;
        path = outputPath;
        failed = false;
        worker = Executors.newSingleThreadExecutor();
        try {
            Future<Boolean> future = worker.submit(() -> initialise());
            recording = future.get(10, TimeUnit.SECONDS);
            if (!recording) {
                worker.shutdown();
                worker = null;
            }
            return recording;
        } catch (Exception e) {
            Log.e(TAG, "START_FAILED", e);
            failed = true;
            recording = false;
            worker.shutdownNow();
            worker = null;
            return false;
        }
    }

    private boolean initialise() {
        try {
            File out = new File(path);
            File parent = out.getParentFile();
            if (parent == null || (!parent.isDirectory() && !parent.mkdirs()))
                throw new IllegalStateException("Cannot create output directory");
            if (out.exists()) throw new IllegalStateException("Refusing to overwrite existing MP4");
            MediaFormat format = MediaFormat.createVideoFormat("video/avc", width, height);
            format.setInteger(MediaFormat.KEY_COLOR_FORMAT,
                    MediaCodecInfo.CodecCapabilities.COLOR_FormatYUV420Flexible);
            format.setInteger(MediaFormat.KEY_BIT_RATE, 1200000);
            format.setInteger(MediaFormat.KEY_FRAME_RATE, fps);
            format.setInteger(MediaFormat.KEY_I_FRAME_INTERVAL, 2);
            codec = MediaCodec.createEncoderByType("video/avc");
            codec.configure(format, null, null, MediaCodec.CONFIGURE_FLAG_ENCODE);
            codec.start();
            bufferInfo = new MediaCodec.BufferInfo();
            muxer = new MediaMuxer(path, MediaMuxer.OutputFormat.MUXER_OUTPUT_MPEG_4);
            Log.i(TAG, "ENCODER_STARTED " + path + " size=" + width + "x" + height + " fps=" + fps);
            return true;
        } catch (Throwable e) {
            Log.e(TAG, "ENCODER_INITIALIZATION_ERROR", e);
            releaseEverything();
            return false;
        }
    }

    /** Returns quickly; false means the bounded worker queue already has a frame. */
    public boolean submitFrame(final byte[] rgba, final long ptsUs) {
        if (!recording || failed || worker == null || rgba == null || rgba.length != width * height * 4)
            return false;
        if (!queued.compareAndSet(false, true)) return false;
        try {
            worker.execute(() -> {
                try {
                    if (!failed) encodeRgba(rgba, ptsUs);
                } catch (Throwable e) {
                    failed = true;
                    Log.e(TAG, "ENCODE_FRAME_FAILED", e);
                } finally {
                    queued.set(false);
                }
            });
            return true;
        } catch (Throwable e) {
            queued.set(false);
            failed = true;
            Log.e(TAG, "QUEUE_ERROR", e);
            return false;
        }
    }

    private void encodeRgba(byte[] rgba, long rawPtsUs) {
        // Short bounded wait: never put the Unity main thread behind an encoder buffer.
        int inputIndex = codec.dequeueInputBuffer(10000);
        if (inputIndex < 0) {
            drain(false);
            inputIndex = codec.dequeueInputBuffer(10000);
            if (inputIndex < 0) return;
        }
        Image image = codec.getInputImage(inputIndex);
        if (image == null) throw new IllegalStateException("Encoder does not expose YUV input image");
        try { copyRgbaToYuv420(rgba, image); }
        finally { image.close(); }
        long pts = Math.max(rawPtsUs, lastPtsUs + 1L);
        lastPtsUs = pts;
        codec.queueInputBuffer(inputIndex, 0, width * height * 3 / 2, pts, 0);
        encoded++;
        drain(false);
    }

    private void copyRgbaToYuv420(byte[] rgba, Image image) {
        Image.Plane[] planes = image.getPlanes();
        if (planes == null || planes.length < 3)
            throw new IllegalStateException("Expected three YUV420 planes");
        ByteBuffer y = planes[0].getBuffer();
        ByteBuffer u = planes[1].getBuffer();
        ByteBuffer v = planes[2].getBuffer();
        int yr = planes[0].getRowStride(), yp = planes[0].getPixelStride();
        int ur = planes[1].getRowStride(), up = planes[1].getPixelStride();
        int vr = planes[2].getRowStride(), vp = planes[2].getPixelStride();
        for (int by = 0; by < height; by += 2) {
            int srcRow0 = flipVertical ? height - 1 - by : by;
            int srcRow1 = flipVertical ? height - 2 - by : by + 1;
            for (int bx = 0; bx < width; bx += 2) {
                int rSum = 0, gSum = 0, bSum = 0;
                for (int dy = 0; dy < 2; dy++) {
                    int srcRow = dy == 0 ? srcRow0 : srcRow1;
                    int base = (srcRow * width + bx) * 4;
                    for (int dx = 0; dx < 2; dx++) {
                        int p = base + dx * 4;
                        int r = rgba[p] & 255, g = rgba[p + 1] & 255, b = rgba[p + 2] & 255;
                        int yy = clamp((77 * r + 150 * g + 29 * b + 128) >> 8);
                        y.put((by + dy) * yr + (bx + dx) * yp, (byte) yy);
                        rSum += r; gSum += g; bSum += b;
                    }
                }
                int r = (rSum + 2) / 4, g = (gSum + 2) / 4, b = (bSum + 2) / 4;
                int uu = clamp(((-43 * r - 85 * g + 128 * b + 128) >> 8) + 128);
                int vv = clamp(((128 * r - 107 * g - 21 * b + 128) >> 8) + 128);
                u.put((by / 2) * ur + (bx / 2) * up, (byte) uu);
                v.put((by / 2) * vr + (bx / 2) * vp, (byte) vv);
            }
        }
    }

    private static int clamp(int v) { return v < 0 ? 0 : Math.min(255, v); }

    private void drain(boolean waitForEos) {
        int idle = 0;
        while (idle < (waitForEos ? 150 : 2)) {
            int index = codec.dequeueOutputBuffer(bufferInfo, waitForEos ? 20000 : 0);
            if (index == MediaCodec.INFO_TRY_AGAIN_LATER) { idle++; continue; }
            if (index == MediaCodec.INFO_OUTPUT_FORMAT_CHANGED) {
                if (muxerStarted) throw new IllegalStateException("Output format changed twice");
                trackIndex = muxer.addTrack(codec.getOutputFormat());
                muxer.start();
                muxerStarted = true;
            } else if (index >= 0) {
                ByteBuffer sample = codec.getOutputBuffer(index);
                if ((bufferInfo.flags & MediaCodec.BUFFER_FLAG_CODEC_CONFIG) != 0)
                    bufferInfo.size = 0;
                if (bufferInfo.size > 0) {
                    if (!muxerStarted || sample == null)
                        throw new IllegalStateException("Encoder output before muxer track");
                    sample.position(bufferInfo.offset);
                    sample.limit(bufferInfo.offset + bufferInfo.size);
                    muxer.writeSampleData(trackIndex, sample, bufferInfo);
                }
                boolean eos = (bufferInfo.flags & MediaCodec.BUFFER_FLAG_END_OF_STREAM) != 0;
                codec.releaseOutputBuffer(index, false);
                if (eos) return;
                idle = 0;
            }
        }
    }

    public synchronized boolean stop() {
        if (worker == null) return false;
        recording = false;
        boolean result = false;
        try {
            Future<Boolean> future = worker.submit(() -> finishEncoding());
            result = future.get(8, TimeUnit.SECONDS);
        } catch (Exception e) {
            failed = true;
            Log.e(TAG, "STOP_FAILED", e);
        } finally {
            worker.shutdown();
            worker = null;
        }
        return result;
    }

    private boolean finishEncoding() {
        try {
            if (codec == null) return false;
            int idx = codec.dequeueInputBuffer(1000000);
            if (idx >= 0) {
                codec.queueInputBuffer(idx, 0, 0, Math.max(0, lastPtsUs + 1L),
                    MediaCodec.BUFFER_FLAG_END_OF_STREAM);
                drain(true);
            } else {
                Log.w(TAG, "Could not queue encoder EOS");
                failed = true;
            }
            boolean success = !failed && muxerStarted && encoded >= 2;
            releaseEverything();
            success = success && !failed;
            if (success) {
                File out = new File(path);
                success = out.exists() && out.length() > 1024;
            }
            Log.i(TAG, "FINISH saved=" + success + " frames=" + encoded + " path=" + path);
            return success;
        } catch (Throwable e) {
            Log.e(TAG, "FINALIZE_ERROR", e);
            releaseEverything();
            return false;
        }
    }

    private void releaseEverything() {
        if (codec != null) {
            try { codec.stop(); } catch (Throwable ignored) { }
            try { codec.release(); } catch (Throwable ignored) { }
            codec = null;
        }
        if (muxer != null) {
            if (muxerStarted) { try { muxer.stop(); } catch (Throwable ignored) { failed = true; } }
            try { muxer.release(); } catch (Throwable ignored) { }
            muxer = null;
        }
    }
}
