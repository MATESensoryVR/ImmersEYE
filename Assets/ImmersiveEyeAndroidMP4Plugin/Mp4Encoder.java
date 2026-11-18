package com.etvr.immersiveeye;

import android.media.Image;
import android.media.MediaCodec;
import android.media.MediaCodecInfo;
import android.media.MediaFormat;
import android.media.MediaMuxer;
import android.os.SystemClock;
import android.util.Log;

import java.io.File;
import java.nio.ByteBuffer;
import java.util.concurrent.ArrayBlockingQueue;
import java.util.concurrent.TimeUnit;

/**
 * Experimental video-only H.264 MP4 encoder for Unity's Android runtime.
 * Accepts RGBA frames copied from AsyncGPUReadback; not a VR eye-tracking API.
 * Uses bounded queue and a worker thread so video encoding does not run on Unity's main thread.
 * Needs validation on the actual device; codecs have different hardware capabilities.
 */
public final class Mp4Encoder {
    private static final String TAG = "ImmersiveEyeMP4";
    private static final String MIME = "video/avc";
    private static final int WAIT_US = 10000;
    private static final class Frame {
        final byte[] rgba;
        final long ptsUs;
        Frame(byte[] bytes, long pts) { rgba = bytes; ptsUs = pts; }
    }

    private MediaCodec codec;
    private MediaMuxer muxer;
    private Thread worker;
    private final ArrayBlockingQueue<Frame> queue = new ArrayBlockingQueue<>(2);
    private volatile boolean running = false;
    private volatile boolean stopRequested = false;
    private volatile boolean healthy = false;
    private volatile boolean finished = false;
    private volatile String lastError = "";
    private int width, height, muxTrack = -1;
    private boolean muxStarted = false;
    private String filePath;
    private long startTimeNs, lastPtsUs = -1;

    public synchronized boolean start(String outputPath, int w, int h, int fps) {
        if (running || worker != null) return false;
        if (w < 2 || h < 2 || (w % 2 != 0) || (h % 2 != 0) || fps < 1) return false;
        try {
            width = w; height = h; filePath = outputPath;
            File parent = new File(outputPath).getParentFile();
            if (parent != null && !parent.exists() && !parent.mkdirs()) {
                throw new IllegalStateException("Cannot create video directory: " + parent);
            }
            MediaFormat format = MediaFormat.createVideoFormat(MIME, width, height);
            format.setInteger(MediaFormat.KEY_COLOR_FORMAT,
                MediaCodecInfo.CodecCapabilities.COLOR_FormatYUV420Flexible);
            format.setInteger(MediaFormat.KEY_BIT_RATE, Math.max(600000, width * height * fps / 6));
            format.setInteger(MediaFormat.KEY_FRAME_RATE, fps);
            format.setInteger(MediaFormat.KEY_I_FRAME_INTERVAL, 2);
            codec = MediaCodec.createEncoderByType(MIME);
            codec.configure(format, null, null, MediaCodec.CONFIGURE_FLAG_ENCODE);
            codec.start();
            muxer = new MediaMuxer(outputPath, MediaMuxer.OutputFormat.MUXER_OUTPUT_MPEG_4);
            muxTrack = -1;
            muxStarted = false;
            stopRequested = false;
            finished = false;
            healthy = true;
            running = true;
            lastError = "";
            lastPtsUs = -1;
            startTimeNs = SystemClock.elapsedRealtimeNanos();
            queue.clear();
            worker = new Thread(new Runnable() {
                @Override public void run() { encodeLoop(); }
            }, "ImmersiveEyeMP4Encoder");
            worker.start();
            Log.i(TAG, "started " + width + "x" + height + " @ " + fps);
            return true;
        } catch (Exception ex) {
            fail("encoder start failed", ex);
            releaseAll();
            return false;
        }
    }

    // Nonblocking: drops frames if the device encoder falls behind.
    public boolean enqueue(byte[] rgba) {
        if (!running || stopRequested || !healthy || rgba == null || rgba.length != width * height * 4) return false;
        long us = (SystemClock.elapsedRealtimeNanos() - startTimeNs) / 1000L;
        return queue.offer(new Frame(rgba, us));
    }

    public boolean stop() {
        if (worker == null) return false;
        stopRequested = true;
        try {
            worker.join(9000);
            if (worker.isAlive()) {
                Log.e(TAG, "MP4 worker timed out; video may be incomplete");
                return false;
            }
        } catch (InterruptedException interrupted) {
            Thread.currentThread().interrupt();
            return false;
        }
        worker = null;
        File f = new File(filePath);
        boolean ok = healthy && finished && f.isFile() && f.length() > 1024;
        Log.i(TAG, "stopped, finalized=" + ok + " bytes=" + (f.exists() ? f.length() : 0) +
            " error=" + lastError);
        return ok;
    }

    public String getLastError() { return lastError; }

    private void encodeLoop() {
        try {
            while (!stopRequested || !queue.isEmpty()) {
                Frame frame = queue.poll(40, TimeUnit.MILLISECONDS);
                if (frame != null) writeFrame(frame);
                drain(false);
            }
            sendEOS();
            drain(true);
        } catch (Exception ex) {
            fail("encoding worker failed", ex);
        } finally {
            releaseAll();
            running = false;
            finished = true;
        }
    }

    private void writeFrame(Frame frame) throws Exception {
        int index = codec.dequeueInputBuffer(WAIT_US);
        if (index < 0) return; // Video may drop a frame; never stall Unity rendering.
        Image image = codec.getInputImage(index);
        if (image == null) throw new IllegalStateException("Device encoder did not expose flexible YUV input image");
        try {
            fillYuv420(image, frame.rgba);
        } finally {
            image.close();
        }
        long pts = Math.max(frame.ptsUs, lastPtsUs + 1);
        lastPtsUs = pts;
        codec.queueInputBuffer(index, 0, width * height * 3 / 2, pts, 0);
    }

    private void fillYuv420(Image image, byte[] rgba) {
        Image.Plane[] planes = image.getPlanes();
        if (planes.length < 3) throw new IllegalStateException("YUV encoder returned less than 3 planes");
        ByteBuffer yBuf = planes[0].getBuffer();
        ByteBuffer uBuf = planes[1].getBuffer();
        ByteBuffer vBuf = planes[2].getBuffer();
        int ys = planes[0].getRowStride(), yp = planes[0].getPixelStride();
        int us = planes[1].getRowStride(), up = planes[1].getPixelStride();
        int vs = planes[2].getRowStride(), vp = planes[2].getPixelStride();

        // Unity's GPU readback convention for this RenderTexture uses bottom-first rows.
        for (int row = 0; row < height; row += 2) {
            int top = (height - 1 - row) * width * 4;
            int bottom = (height - 2 - row) * width * 4;
            for (int col = 0; col < width; col += 2) {
                int p0 = top + col * 4;
                int p1 = p0 + 4;
                int p2 = bottom + col * 4;
                int p3 = p2 + 4;
                int r0 = rgba[p0] & 255, g0 = rgba[p0+1] & 255, b0 = rgba[p0+2] & 255;
                int r1 = rgba[p1] & 255, g1 = rgba[p1+1] & 255, b1 = rgba[p1+2] & 255;
                int r2 = rgba[p2] & 255, g2 = rgba[p2+1] & 255, b2 = rgba[p2+2] & 255;
                int r3 = rgba[p3] & 255, g3 = rgba[p3+1] & 255, b3 = rgba[p3+2] & 255;
                yBuf.put(row * ys + col * yp, (byte) luma(r0,g0,b0));
                yBuf.put(row * ys + (col+1) * yp, (byte) luma(r1,g1,b1));
                yBuf.put((row+1) * ys + col * yp, (byte) luma(r2,g2,b2));
                yBuf.put((row+1) * ys + (col+1) * yp, (byte) luma(r3,g3,b3));
                int r = (r0+r1+r2+r3 + 2)/4;
                int g = (g0+g1+g2+g3 + 2)/4;
                int b = (b0+b1+b2+b3 + 2)/4;
                int cr = row/2, cc = col/2;
                uBuf.put(cr * us + cc * up, (byte) chromaU(r,g,b));
                vBuf.put(cr * vs + cc * vp, (byte) chromaV(r,g,b));
            }
        }
    }

    private static int clamp(int x) { return Math.max(0, Math.min(255, x)); }
    private static int luma(int r,int g,int b) { return clamp(((66*r + 129*g + 25*b + 128)>>8) + 16); }
    private static int chromaU(int r,int g,int b) { return clamp(((-38*r - 74*g + 112*b + 128)>>8) + 128); }
    private static int chromaV(int r,int g,int b) { return clamp(((112*r - 94*g - 18*b + 128)>>8) + 128); }

    private void sendEOS() throws Exception {
        for (int attempt=0; attempt<300; attempt++) {
            int index = codec.dequeueInputBuffer(WAIT_US);
            if (index >= 0) {
                long pts = Math.max(lastPtsUs+1, (SystemClock.elapsedRealtimeNanos()-startTimeNs)/1000L);
                codec.queueInputBuffer(index, 0, 0, pts, MediaCodec.BUFFER_FLAG_END_OF_STREAM);
                return;
            }
            drain(false);
        }
        throw new IllegalStateException("Could not queue end-of-stream buffer");
    }

    private void drain(boolean untilEnd) throws Exception {
        MediaCodec.BufferInfo info = new MediaCodec.BufferInfo();
        int idle = 0;
        while (true) {
            int index = codec.dequeueOutputBuffer(info, untilEnd ? WAIT_US : 0);
            if (index == MediaCodec.INFO_TRY_AGAIN_LATER) {
                if (!untilEnd || ++idle > 500) return;
            } else if (index == MediaCodec.INFO_OUTPUT_FORMAT_CHANGED) {
                if (muxStarted) throw new IllegalStateException("Encoder output format changed twice");
                muxTrack = muxer.addTrack(codec.getOutputFormat());
                muxer.start();
                muxStarted = true;
            } else if (index >= 0) {
                if ((info.flags & MediaCodec.BUFFER_FLAG_CODEC_CONFIG) != 0) info.size = 0;
                if (info.size > 0) {
                    if (!muxStarted) throw new IllegalStateException("Encoded video before muxer start");
                    ByteBuffer data = codec.getOutputBuffer(index);
                    if (data == null) throw new IllegalStateException("Missing codec output buffer");
                    data.position(info.offset);
                    data.limit(info.offset + info.size);
                    muxer.writeSampleData(muxTrack, data, info);
                }
                codec.releaseOutputBuffer(index, false);
                if ((info.flags & MediaCodec.BUFFER_FLAG_END_OF_STREAM) != 0) return;
            }
        }
    }

    private void releaseAll() {
        if (codec != null) {
            try { codec.stop(); } catch (Exception ignored) { }
            try { codec.release(); } catch (Exception ignored) { }
            codec = null;
        }
        if (muxer != null) {
            if (muxStarted) {
                try { muxer.stop(); } catch (Exception ex) { healthy = false; Log.e(TAG, "mux stop failed", ex); }
            } else {
                healthy = false; // No actual encoded samples were finalized.
            }
            try { muxer.release(); } catch (Exception ignored) { }
            muxer = null;
        }
        queue.clear();
        muxStarted = false;
    }

    private void fail(String detail, Exception ex) {
        healthy = false;
        lastError = detail + ": " + ex.toString();
        Log.e(TAG, detail, ex);
    }
}
