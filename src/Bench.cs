using System.Collections.Generic;
using UnityEngine;

namespace GigglelandPerf
{
    /// Uncapped frame-time sample for A/B tests: logs average FPS and the 1% low.
    static class Bench
    {
        static readonly List<float> frames = new();
        static string label;
        static float until, warmUntil;
        static int savedFps, savedVsync;
        static bool running;

        public static void Start(string name, float seconds)
        {
            label = name;
            frames.Clear();
            savedFps = Application.targetFrameRate;
            savedVsync = QualitySettings.vSyncCount;
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 0;
            warmUntil = Time.unscaledTime + 2f;
            until = warmUntil + seconds;
            running = true;
        }

        public static void Tick()
        {
            if (!running) return;
            float now = Time.unscaledTime;
            if (now < warmUntil) return;
            frames.Add(Time.unscaledDeltaTime);
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
                               $"1% low {n / worst:0.0} fps, {frames.Count} frames");
        }
    }
}
