using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace GigglelandPerf
{
    /// Uncapped frame-time sample for A/B tests: logs average FPS and the 1% low.
    /// A spin bench also turns the player through 360° and reports each direction separately,
    /// with the draw counts, so busy views can be told apart from empty ones.
    static class Bench
    {
        const int Sectors = 8;

        static readonly List<float> frames = new();
        static string label;
        static float until, warmUntil, start;
        static int savedFps, savedVsync;
        static bool running, spin;
        static float baseYaw;
        static FirstPersonController player;
        static readonly float[] secTime = new float[Sectors], secMax = new float[Sectors];
        static readonly int[] secFrames = new int[Sectors], secSpikes = new int[Sectors];
        static readonly double[] secBatches = new double[Sectors], secSetPass = new double[Sectors],
            secTris = new double[Sectors], secCasters = new double[Sectors], secPending = new double[Sectors];
        static ProfilerRecorder batches, setPass, tris, casters;
        static readonly Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<FrameTiming> timing = new(1);
        static double tMain, tRender, tGpu, tWait;
        static int tCount;

        public static void Start(string name, float seconds) => Begin(name, seconds, false);

        public static void Spin(string name, float seconds)
        {
            player = Object.FindObjectOfType<FirstPersonController>();
            if (player == null) { Plugin.Log.LogWarning("spin: no player"); return; }
            baseYaw = player.transform.eulerAngles.y;
            Begin(name, seconds, true);
        }

        static void Begin(string name, float seconds, bool spinning)
        {
            label = name;
            spin = spinning;
            frames.Clear();
            System.Array.Clear(secTime); System.Array.Clear(secMax); System.Array.Clear(secFrames); System.Array.Clear(secSpikes);
            System.Array.Clear(secBatches); System.Array.Clear(secSetPass); System.Array.Clear(secTris); System.Array.Clear(secCasters);
            System.Array.Clear(secPending);
            savedFps = Application.targetFrameRate;
            savedVsync = QualitySettings.vSyncCount;
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 0;
            warmUntil = Time.unscaledTime + 2f;
            start = warmUntil;
            until = warmUntil + seconds;
            Recorders(true);
            tMain = tRender = tGpu = tWait = 0; tCount = 0;
            running = true;
        }

        static void Recorders(bool on)
        {
            try
            {
                if (on)
                {
                    batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
                    setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
                    tris = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
                    casters = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Shadow Casters Count");
                }
                else { batches.Dispose(); setPass.Dispose(); tris.Dispose(); casters.Dispose(); }
            }
            catch (System.Exception e) { Plugin.Log.LogWarning("profiler recorders: " + e.Message); }
        }

        static long Last(ProfilerRecorder r)
        {
            try { return r.Valid ? r.LastValue : -1; } catch { return -1; }
        }

        public static void Tick()
        {
            if (!running) return;
            float now = Time.unscaledTime;
            if (spin && player != null)
            {
                float t = Mathf.Clamp01((now - start) / (until - start));
                player.transform.rotation = Quaternion.Euler(0f, baseYaw + 360f * t, 0f);
            }
            if (now < warmUntil) return;
            float dt = Time.unscaledDeltaTime;
            frames.Add(dt);
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, timing) == 1)
            {
                var ft = timing[0];
                tMain += ft.cpuMainThreadFrameTime; tRender += ft.cpuRenderThreadFrameTime;
                tGpu += ft.gpuFrameTime; tWait += ft.cpuMainThreadPresentWaitTime; tCount++;
            }
            {
                // The frame that just finished showed the previous yaw; close enough at 8 sectors.
                int s = spin ? Mathf.Clamp((int)((now - start) / (until - start) * Sectors), 0, Sectors - 1) : 0;
                secFrames[s]++;
                secTime[s] += dt;
                secMax[s] = Mathf.Max(secMax[s], dt);
                if (dt > 0.1f) secSpikes[s]++;
                secBatches[s] += Last(batches);
                secSetPass[s] += Last(setPass);
                secTris[s] += Last(tris);
                secCasters[s] += Last(casters);
                secPending[s] += Texture.streamingTexturePendingLoadCount + Texture.streamingTextureLoadingCount;
            }
            if (now < until) return;

            running = false;
            Application.targetFrameRate = savedFps;
            QualitySettings.vSyncCount = savedVsync;
            float sum = 0f;
            foreach (var f in frames) sum += f;
            frames.Sort();
            int n = Mathf.Max(1, frames.Count / 100);
            float worst = 0f;
            for (int i = frames.Count - n; i < frames.Count; i++) worst += frames[i];
            Plugin.Log.LogInfo($"[bench] {label}: avg {frames.Count / sum:0.0} fps ({1000f * sum / frames.Count:0.00} ms), " +
                               $"1% low {n / worst:0.0} fps, {frames.Count} frames" +
                               (spin ? "" : $", batches {secBatches[0] / frames.Count:0}, setpass {secSetPass[0] / frames.Count:0}, tris {secTris[0] / frames.Count / 1000:0}k"));
            if (tCount > 0)
                Plugin.Log.LogInfo($"[bench] {label} timing ({(FrameTimingManager.IsFeatureEnabled() ? "on" : "off")}): main {tMain / tCount:0.0} ms, " +
                                   $"render thread {tRender / tCount:0.0} ms, gpu {tGpu / tCount:0.0} ms, present wait {tWait / tCount:0.0} ms");
            if (spin)
            {
                if (player != null) player.transform.rotation = Quaternion.Euler(0f, baseYaw, 0f);
                for (int s = 0; s < Sectors; s++)
                {
                    int c = Mathf.Max(1, secFrames[s]);
                    Plugin.Log.LogInfo($"[spin] {label} {s * 360 / Sectors}°: {secFrames[s] / Mathf.Max(secTime[s], 1e-4f):0.0} fps, " +
                                       $"max {1000f * secMax[s]:0} ms, spikes {secSpikes[s]}, batches {secBatches[s] / c:0}, " +
                                       $"setpass {secSetPass[s] / c:0}, tris {secTris[s] / c / 1000:0}k, casters {secCasters[s] / c:0}, " +
                                       $"streaming {secPending[s] / c:0.0}");
                }
            }
            Recorders(false);
        }
    }
}
