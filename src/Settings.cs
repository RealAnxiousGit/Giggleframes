using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace GigglelandPerf
{
    /// One row on the Advanced page: a drop-down, or a slider for numeric settings. The value is stored
    /// as an int; for most rows 0 means "leave it to the game" (Preset / Game / On / Off), so a fresh
    /// install looks exactly like vanilla. A slider runs from Min to Min + Choices.Length - 1 and shows
    /// Choices[value - Min] as its text.
    class Option
    {
        public string Key, Label;
        public string[] Choices;
        public bool Slider;
        public int Min;
        public bool NotInPresets;         // presets leave it alone (Render API, FPS counter, the preset row itself)
        public int PresetDefault;         // value a preset starts from before setting its own rows
        public ConfigEntry<int> Index;

        public int I => Index.Value;
        public int Max => Min + Choices.Length - 1;
        public string Text => Choices[Mathf.Clamp(I - Min, 0, Choices.Length - 1)];
        public bool Overridden => Index.Value != 0;
    }

    static class Settings
    {
        public static ConfigEntry<bool> DumpOnLoad, DevCommands, FirstRunDone;
        public static ConfigEntry<int> RenderApiTries;
        public static ConfigEntry<string> SkipPatches;

        public static readonly List<Option> All = new();
        static readonly Dictionary<string, Option> byKey = new();

        public static Option GraphicsPreset, RenderApi, Upscaling, FsrSharpness, VolFog, VolLights, Shadows, Cascades, LocalShadows,
            Lighting, AO, SSR, Post, Bloom, Flares, DoF, LodBias, DrawDistance, Culling, Vegetation, TextureBudget, FpsCounter;

        static readonly string[] Levels = { "Preset", "Off", "Low", "Medium", "High" };

        /// Graphics Preset choices; "Custom" is shown once any other row is changed by hand.
        public static readonly string[] Presets = { "Custom", "Game Default", "Very Low", "Low-Spec", "Balanced" };

        public static void Bind(ConfigFile cfg)
        {
            DumpOnLoad = cfg.Bind("Debug", "DumpOnLoad", false, "Log the settings UI and render state after each scene load.");
            SkipPatches = cfg.Bind("Debug", "SkipPatches", "", "Comma-separated patch classes or Type.Method hooks to leave out (troubleshooting).");
            DevCommands = cfg.Bind("Debug", "DevCommands", false, "Read test commands from BepInEx/gigglelandperf.cmd.");
            FirstRunDone = cfg.Bind("Internal", "FirstRunDone", false, "Set after the hardware check picked a starting preset.");
            RenderApiTries = cfg.Bind("Internal", "RenderApiTries", 0, "Relaunches tried for the saved Render API without it starting.");

            // 0.10 stored these as a bool, a float and a preset name; carry the values over once.
            var oldFps = cfg.Bind("Advanced", "ShowFps", false);
            var oldSharp = cfg.Bind("Advanced", "fsrSharpness", 0.8f);
            var oldPreset = cfg.Bind("Internal", "AppliedPreset", "");
            int presetIdx = Math.Max(0, Array.IndexOf(Presets, oldPreset.Value == "Game" ? "Game Default" : oldPreset.Value));

            GraphicsPreset = Opt(cfg, "graphicsPreset", "Graphics Preset", presetIdx, Presets);
            GraphicsPreset.NotInPresets = true;
            RenderApi = Opt(cfg, "renderApi", "Render API", 0, "DirectX 11", "DirectX 12", "Vulkan (DXVK)");
            RenderApi.NotInPresets = true;
            Upscaling = Opt(cfg, "upscaling", "Upscaling Filter", 0, "Preset", "Bilinear", "FSR 1.0", "Point");
            FsrSharpness = SliderOpt(cfg, "fsrSharpnessLevel", "FSR Sharpness", 0, Mathf.Clamp(Mathf.RoundToInt(oldSharp.Value * 10f), 0, 10),
                "0%", "10%", "20%", "30%", "40%", "50%", "60%", "70%", "80%", "90%", "100%");
            FsrSharpness.PresetDefault = 8;
            VolFog = Opt(cfg, "volumetricFog", "Volumetric Fog", 0, Levels);
            VolLights = Opt(cfg, "volumetricLights", "Volumetric Lighting", 0, Levels);
            Shadows = Opt(cfg, "shadowQuality", "Shadow Quality", 0, "Preset", "Off", "Low", "Medium", "High", "Ultra");
            Cascades = SliderOpt(cfg, "shadowCascades", "Shadow Cascades", 0, 0, "Game", "1", "2", "3", "4");
            LocalShadows = Opt(cfg, "localShadows", "Local Light Shadows", 0, Levels);
            Lighting = Opt(cfg, "lightingQuality", "Lighting Quality", 0, "Preset", "Low", "Medium", "High", "Ultra");
            AO = Opt(cfg, "ambientOcclusion", "Ambient Occlusion", 0, Levels);
            SSR = Opt(cfg, "reflections", "Screen Space Reflections", 0, Levels);
            Post = Opt(cfg, "postQuality", "Post-Processing Quality", 0, "Preset", "Low", "Medium", "High");
            Bloom = Opt(cfg, "bloom", "Bloom", 0, "On", "Off");
            Flares = Opt(cfg, "lensFlares", "Lens Flares", 0, "On", "Off");
            DoF = Opt(cfg, "depthOfField", "Depth of Field", 0, "On", "Off");
            LodBias = Opt(cfg, "lodBias", "Level of Detail", 0, "Preset", "Lowest", "Low", "Medium", "High");
            // Slider in 5 m steps from 10 m; the far end leaves the game's own distance alone. The game's
            // main camera draws to 50 m, so the slider stops at 45 m (0.20 had 50 and 55 m, which did nothing;
            // the config clamps their 11 and 12 to the new top step, Game).
            var oldDraw = cfg.Bind("Advanced", "drawDistance", 0);   // 0.20 test builds: Preset, 40, 30, 20, 15 m
            DrawDistance = SliderOpt(cfg, "drawDistanceSteps", "Draw Distance", 2, oldDraw.Value switch { 1 => 8, 2 => 6, 3 => 4, 4 => 3, _ => 10 },
                "10 m", "15 m", "20 m", "25 m", "30 m", "35 m", "40 m", "45 m", "Game");
            DrawDistance.PresetDefault = 10;
            Culling = Opt(cfg, "smallObjectCulling", "Small Object Culling", 0, "Off", "Low", "Medium", "High");
            Vegetation = Opt(cfg, "vegetation", "Vegetation Detail", 0, "Preset", "Low", "Medium", "High");
            TextureBudget = SliderOpt(cfg, "textureBudget", "Texture Memory", 0, 0, "Game", "512 MB", "1 GB", "2 GB", "4 GB");
            FpsCounter = Opt(cfg, "fpsCounter", "Show FPS Counter", oldFps.Value ? 1 : 0, "Off", "On");
            FpsCounter.NotInPresets = true;

            cfg.Remove(oldDraw.Definition);
            cfg.Remove(oldFps.Definition);
            cfg.Remove(oldSharp.Definition);
            cfg.Remove(oldPreset.Definition);
            cfg.Save();
        }

        static Option Opt(ConfigFile cfg, string key, string label, int def, params string[] choices) =>
            Add(cfg, new Option { Key = key, Label = label, Choices = choices }, def);

        static Option SliderOpt(ConfigFile cfg, string key, string label, int min, int def, params string[] choices) =>
            Add(cfg, new Option { Key = key, Label = label, Choices = choices, Slider = true, Min = min }, def);

        static Option Add(ConfigFile cfg, Option o, int def)
        {
            string values = string.Join(", ", System.Linq.Enumerable.Select(o.Choices, (c, i) => $"{o.Min + i} = {c}"));
            o.Index = cfg.Bind("Advanced", o.Key, def,
                new ConfigDescription(o.Label + ": " + values, new AcceptableValueRange<int>(o.Min, o.Max)));
            All.Add(o);
            byKey[o.Key] = o;
            return o;
        }

        public static bool Skipped(string name) =>
            Array.Exists(SkipPatches.Value.Split(','), s => s.Trim() == name);

        public static Option Find(string key) => key != null && byKey.TryGetValue(key, out var o) ? o : null;

        public static string PresetName => GraphicsPreset.Choices[GraphicsPreset.I];

        /// Presets. Values are option indices (see the Choices arrays above). "Game" is kept for old scripts.
        public static void ApplyPreset(string name)
        {
            if (name == "Game") name = "Game Default";
            foreach (var o in All) if (!o.NotInPresets) o.Index.Value = o.PresetDefault;
            switch (name)
            {
                case "Very Low":
                    // Everything Low-Spec does, plus the busy-view savings: short draw distance,
                    // aggressive small-object culling, lowest LODs and no main-light shadows.
                    Upscaling.Index.Value = 2;    // FSR 1.0
                    VolFog.Index.Value = 1;       // Off
                    VolLights.Index.Value = 1;    // Off
                    Shadows.Index.Value = 2;      // Low
                    Cascades.Index.Value = 1;     // 1
                    LocalShadows.Index.Value = 1; // Off
                    Lighting.Index.Value = 1;     // Low: 2 lights per object
                    AO.Index.Value = 1;           // Off
                    SSR.Index.Value = 1;          // Off
                    Post.Index.Value = 1;         // Low
                    Bloom.Index.Value = 1;        // Off
                    Flares.Index.Value = 1;       // Off
                    DoF.Index.Value = 1;          // Off
                    LodBias.Index.Value = 1;      // Lowest
                    DrawDistance.Index.Value = 3; // 15 m
                    Culling.Index.Value = 3;      // High
                    Vegetation.Index.Value = 1;   // Low
                    break;
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
                    DrawDistance.Index.Value = 4; // 20 m
                    Culling.Index.Value = 2;      // Medium
                    Vegetation.Index.Value = 1;   // Low
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
                    DrawDistance.Index.Value = 8; // 40 m
                    Culling.Index.Value = 1;      // Low
                    Vegetation.Index.Value = 2;
                    break;
            }
            int idx = Array.IndexOf(Presets, name);
            GraphicsPreset.Index.Value = idx < 0 ? 0 : idx;
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
