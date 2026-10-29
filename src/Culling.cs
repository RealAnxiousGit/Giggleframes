using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GigglelandPerf
{
    /// Busy views (a large room full of props) cost far more than empty ones: on the test laptop
    /// 8,600 draws vs 800. Two options cut what the main camera draws:
    ///
    /// Draw Distance caps the main camera's far plane. The game rewrites the camera every frame,
    /// so the cap goes in right before the camera renders.
    ///
    /// Small Object Culling hides props that would cover only a few pixels: a renderer is skipped when
    /// its size is under a fraction of its distance. It uses Renderer.forceRenderingOff, which the game
    /// never touches, and works through the list a slice per frame so it never costs a spike.
    static class Culling
    {
        static bool hooked;
        static float lastFar;

        public static void Init()
        {
            if (hooked) return;
            hooked = true;
            RenderPipelineManager.add_beginCameraRendering((Action<ScriptableRenderContext, Camera>)BeforeCamera);
        }

        static float FarCap => Settings.DrawDistance.I < Settings.DrawDistance.Max ? 5f * Settings.DrawDistance.I : 0f;

        static void BeforeCamera(ScriptableRenderContext _, Camera cam)
        {
            try
            {
                float cap = FarCap;
                if (cap > 0f && cam != null && cam.cameraType == CameraType.Game && cam.targetTexture == null && cam == Camera.main
                    && cam.farClipPlane > cap)
                    cam.farClipPlane = cap;
                if (cam != null && cam == Camera.main) lastFar = cam.farClipPlane;
            }
            catch (Exception e) { Plugin.Log.LogWarning("draw distance: " + e.Message); }
        }

        // ---------- small object culling ----------

        const int PerFrame = 1500;
        const float AlwaysDrawWithin = 3f;   // metres; anything this close stays, whatever its size

        static float Ratio => Settings.Culling.I switch { 1 => 0.02f, 2 => 0.035f, 3 => 0.05f, _ => 0f };

        static readonly List<Renderer> list = new();
        static readonly List<float> sizes = new();
        static readonly List<bool> hidden = new();
        static int cursor, hiddenCount;
        static float rescanAt = 3f;

        /// Scene loads rebuild the list a few seconds later, once the scene has settled.
        public static void Rescan(float delay) => rescanAt = Time.unscaledTime + delay;

        public static void Tick()
        {
            float ratio = Ratio;
            if (ratio <= 0f)
            {
                if (hiddenCount > 0) ShowAll();
                return;
            }
            if (rescanAt >= 0f && Time.unscaledTime >= rescanAt) Build();

            var cam = Camera.main;
            if (cam == null || list.Count == 0) return;
            var cp = cam.transform.position;
            int end = Math.Min(list.Count, cursor + PerFrame);
            for (int i = cursor; i < end; i++)
            {
                var r = list[i];
                if (r == null) continue;
                try
                {
                    float d = Vector3.Distance(cp, r.bounds.center);
                    bool hide = d > AlwaysDrawWithin && sizes[i] < ratio * d;
                    if (hide == hidden[i]) continue;
                    r.forceRenderingOff = hide;
                    hidden[i] = hide;
                    hiddenCount += hide ? 1 : -1;
                }
                catch { list[i] = null; }
            }
            cursor = end >= list.Count ? 0 : end;
        }

        static void Build()
        {
            rescanAt = -1f;
            ShowAll();
            list.Clear(); sizes.Clear(); hidden.Clear();
            // Static props only: characters (skinned), particles, lines and UI keep drawing.
            foreach (var r in UnityEngine.Object.FindObjectsOfType<MeshRenderer>())
            {
                if (r == null || r.gameObject.layer != 0) continue;
                float size = r.bounds.size.magnitude;
                if (size <= 0f || size > 4f) continue;   // big things are cheap to keep and noticeable to lose
                list.Add(r); sizes.Add(size); hidden.Add(false);
            }
            cursor = 0;
            Plugin.Log.LogInfo($"small object culling: {list.Count} candidates");
        }

        static void ShowAll()
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (!hidden[i]) continue;
                hidden[i] = false;
                try { if (list[i] != null) list[i].forceRenderingOff = false; } catch { }
            }
            hiddenCount = 0;
        }

        public static string Status => $"culling {list.Count} candidates, {hiddenCount} hidden, main camera far {lastFar:F0} m";
    }
}
