using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace GigglelandPerf
{
    /// One row on the Advanced page. Options rows store an index; index 0 always means
    /// "leave it to the game" (Preset / On), so a fresh install looks exactly like vanilla.
    class Option
    {
        public string Key, Label;
        public string[] Choices;          // options row
        public bool NotInPresets;         // presets leave it alone (Render API)
        public float Min, Max, Default;   // slider row (Choices == null)
        public ConfigEntry<int> Index;
        public ConfigEntry<float> Value;

        public int I => Index.Value;
        public bool Overridden => Choices != null && Index.Value != 0;
    }

    static class Settings
    {
        public static ConfigEntry<bool> DumpOnLoad, DevCommands, ShowFps, FirstRunDone;
        public static ConfigEntry<int> RenderApiTries;
        public static ConfigEntry<string> AppliedPreset, SkipPatches;

        public static readonly List<Option> All = new();
        static readonly Dictionary<string, Option> byKey = new();

        public static Option RenderApi, Upscaling, FsrSharpness, VolFog, VolLights, Shadows, Cascades, LocalShadows,
            Lighting, AO, SSR, Post, Bloom, Flares, DoF, LodBias, Vegetation, TextureBudget;

        static readonly string[] Levels = { "Preset", "Off", "Low", "Medium", "High" };

        public static void Bind(ConfigFile cfg)
        {
            DumpOnLoad = cfg.Bind("Debug", "DumpOnLoad", false, "Log the settings UI and render state after each scene load.");
            SkipPatches = cfg.Bind("Debug", "SkipPatches", "", "Comma-separated patch classes or Type.Method hooks to leave out (troubleshooting).");
            DevCommands = cfg.Bind("Debug", "DevCommands", false, "Read test commands from BepInEx/gigglelandperf.cmd.");
            ShowFps = cfg.Bind("Advanced", "ShowFps", false, "Show a small frame-rate counter in the top-left corner.");
            FirstRunDone = cfg.Bind("Internal", "FirstRunDone", false, "Set after the hardware check picked a starting preset.");
            AppliedPreset = cfg.Bind("Internal", "AppliedPreset", "", "Last preset applied from the Advanced page.");

            RenderApiTries = cfg.Bind("Internal", "RenderApiTries", 0, "Relaunches tried for the saved Render API without it starting.");
            RenderApi = Opt(cfg, "renderApi", "Render API", "DirectX 11", "DirectX 12");
            RenderApi.NotInPresets = true;
            Upscaling = Opt(cfg, "upscaling", "Upscaling Filter", "Preset", "Bilinear", "FSR 1.0", "Point");
            FsrSharpness = Slider(cfg, "fsrSharpness", "FSR Sharpness", 0f, 1f, 0.8f);
            VolFog = Opt(cfg, "volumetricFog", "Volumetric Fog", Levels);
            VolLights = Opt(cfg, "volumetricLights", "Volumetric Lighting", Levels);
            Shadows = Opt(cfg, "shadowQuality", "Shadow Quality", "Preset", "Off", "Low", "Medium", "High", "Ultra");
            Cascades = Opt(cfg, "shadowCascades", "Shadow Cascades", "Preset", "1", "2", "3", "4");
            LocalShadows = Opt(cfg, "localShadows", "Local Light Shadows", Levels);
            Lighting = Opt(cfg, "lightingQuality", "Lighting Quality", "Preset", "Low", "Medium", "High", "Ultra");
            AO = Opt(cfg, "ambientOcclusion", "Ambient Occlusion", Levels);
            SSR = Opt(cfg, "reflections", "Screen Space Reflections", Levels);
            Post = Opt(cfg, "postQuality", "Post-Processing Quality", "Preset", "Low", "Medium", "High");
            Bloom = Opt(cfg, "bloom", "Bloom", "On", "Off");
            Flares = Opt(cfg, "lensFlares", "Lens Flares", "On", "Off");
            DoF = Opt(cfg, "depthOfField", "Depth of Field", "On", "Off");
            LodBias = Opt(cfg, "lodBias", "Level of Detail", "Preset", "Lowest", "Low", "Medium", "High");
            Vegetation = Opt(cfg, "vegetation", "Vegetation Detail", "Preset", "Low", "Medium", "High");
            TextureBudget = Opt(cfg, "textureBudget", "Texture Memory", "Preset", "512 MB", "1 GB", "2 GB", "4 GB");
        }

        static Option Opt(ConfigFile cfg, string key, string label, params string[] choices)
        {
            var o = new Option { Key = key, Label = label, Choices = choices };
            o.Index = cfg.Bind("Advanced", key, 0,
                new ConfigDescription(label + ": " + string.Join(", ", choices), new AcceptableValueRange<int>(0, choices.Length - 1)));
            return Add(o);
        }

        static Option Slider(ConfigFile cfg, string key, string label, float min, float max, float def)
        {
            var o = new Option { Key = key, Label = label, Min = min, Max = max, Default = def };
            o.Value = cfg.Bind("Advanced", key, def, new ConfigDescription(label, new AcceptableValueRange<float>(min, max)));
            return Add(o);
        }

        static Option Add(Option o)
        {
            All.Add(o);
            byKey[o.Key] = o;
            return o;
        }

        public static bool Skipped(string name) =>
            Array.Exists(SkipPatches.Value.Split(','), s => s.Trim() == name);

        public static Option Find(string key) => key != null && byKey.TryGetValue(key, out var o) ? o : null;

        // Presets. Values are option indices (see the Choices arrays above).
        public static void ApplyPreset(string name)
        {
            foreach (var o in All) if (o.Choices != null && !o.NotInPresets) o.Index.Value = 0;
            FsrSharpness.Value.Value = FsrSharpness.Default;
            switch (name)
            {
                case "Low-Spec":
                    // Tuned on an Intel Core 7 150U with integrated graphics at 1080p.
                    Upscaling.Index.Value = 2;    // FSR 1.0: the game already ships at 0.5 render scale on Medium
                    VolFog.Index.Value = 2;       // Low
                    VolLights.Index.Value = 2;    // Low
                    Shadows.Index.Value = 2;      // Low: 1024, hard shadows
                    Cascades.Index.Value = 2;     // 2
                    LocalShadows.Index.Value = 1; // Off: the 4096 atlas is the single biggest shadow cost
                    Lighting.Index.Value = 2;     // Medium: 4 lights per object
                    AO.Index.Value = 1;           // Off
                    SSR.Index.Value = 1;          // Off
                    Post.Index.Value = 1;         // Low
                    Flares.Index.Value = 1;       // Off
                    DoF.Index.Value = 1;          // Off
                    LodBias.Index.Value = 2;      // Low
                    Vegetation.Index.Value = 1;   // Low
                    TextureBudget.Index.Value = 2;// 1 GB
                    break;
                case "Balanced":
                    Upscaling.Index.Value = 2;
                    VolFog.Index.Value = 3;
                    VolLights.Index.Value = 3;
                    Shadows.Index.Value = 3;
                    Cascades.Index.Value = 2;
                    LocalShadows.Index.Value = 2;
                    Lighting.Index.Value = 3;
                    AO.Index.Value = 2;
                    SSR.Index.Value = 2;
                    Post.Index.Value = 2;
                    LodBias.Index.Value = 3;
                    Vegetation.Index.Value = 2;
                    TextureBudget.Index.Value = 3;
                    break;
            }
            AppliedPreset.Value = name;
        }

        /// Integrated or weak GPUs get the Low-Spec preset on first launch; everything else stays vanilla.
        public static string DetectTier(out string reason)
        {
            string gpu = SystemInfo.graphicsDeviceName ?? "";
            string g = gpu.ToLowerInvariant();
            int ramMb = SystemInfo.systemMemorySize;
            int cores = SystemInfo.processorCount;

            bool integrated =
                (g.Contains("intel") && !g.Contains("arc")) ||
                g.Contains("radeon(tm) graphics") || g.Contains("radeon graphics") || g.Contains("vega") ||
                g.Contains("radeon 780m") || g.Contains("radeon 760m") || g.Contains("radeon 680m") || g.Contains("radeon 660m") ||
                g.Contains("microsoft basic") || g.Contains("llvmpipe") || g.Contains("adreno") || g.Contains("mali");
            bool lowEndDedicated =
                g.Contains("gt 1030") || g.Contains("gt 730") || g.Contains("gt 710") || g.Contains("mx1") || g.Contains("mx2") ||
                g.Contains("mx3") || g.Contains("mx4") || g.Contains("mx5") || g.Contains("rx 550") || g.Contains("rx 560") ||
                g.Contains("rx 6400") || g.Contains("gtx 750") || g.Contains("gtx 950") || g.Contains("gtx 1050");

            reason = $"GPU '{gpu}', {ramMb} MB RAM, {cores} threads";
            if (integrated || lowEndDedicated || ramMb < 8000 || cores <= 4) return "Low-Spec";
            if (g.Contains("gtx 1060") || g.Contains("gtx 1650") || g.Contains("rx 570") || g.Contains("rx 580") || g.Contains("rx 6500"))
                return "Balanced";
            return null;
        }
    }
}
