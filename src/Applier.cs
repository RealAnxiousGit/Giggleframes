using System;
using System.Collections.Generic;
using Beautify.Universal;
using ShinySSRR;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using VolumetricFogAndMist2;
using VolumetricLights;
using VFog = VolumetricFogAndMist2.VolumetricFog;
using VLight = VolumetricLights.VolumetricLight;
using AO = UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusionSettings;

namespace GigglelandPerf
{
    /// Writes the Advanced settings into the URP asset, renderer features and scene objects.
    ///
    /// Every write goes through a snapshot of the game's own value, so a row set back to
    /// "Preset" restores exactly what the game had. The game's own apply functions are wrapped
    /// (see GameApplyPatch): our values are taken out before they run and put back after.
    static class Applier
    {
        // ---------- pipeline-level slots ----------

        abstract class Slot
        {
            public abstract void Capture();
            public abstract void Apply();
            public abstract void Restore();
        }

        sealed class Slot<T> : Slot
        {
            readonly Func<bool> active;
            readonly Func<T> get;
            readonly Action<T> set;
            readonly Func<T, T> want;
            T baseVal;
            bool has, dirty;

            public Slot(Func<bool> active, Func<T> get, Action<T> set, Func<T, T> want)
            { this.active = active; this.get = get; this.set = set; this.want = want; }

            public override void Capture() { baseVal = get(); has = true; dirty = false; }

            public override void Apply()
            {
                if (!has) Capture();
                if (active())
                {
                    var w = want(baseVal);
                    if (!EqualityComparer<T>.Default.Equals(get(), w)) set(w);
                    dirty = true;
                }
                else Restore();
            }

            public override void Restore()
            {
                if (!dirty) return;
                set(baseVal);
                dirty = false;
            }
        }

        static readonly List<Slot> slots = new();
        static int assetId;
        static int gameDepth;
        static bool firstRun = true;

        static UniversalRenderPipelineAsset Urp => GraphicsSettings.currentRenderPipeline?.TryCast<UniversalRenderPipelineAsset>();

        static T Feature<T>() where T : ScriptableRendererFeature
        {
            var list = Urp?.m_RendererDataList;
            for (int i = 0; list != null && i < list.Length; i++)
            {
                var feats = list[i]?.rendererFeatures;
                for (int f = 0; feats != null && f < feats.Count; f++)
                {
                    var t = feats[f]?.TryCast<T>();
                    if (t != null) return t;
                }
            }
            return null;
        }

        static void BuildSlots()
        {
            slots.Clear();
            var s = Settings.Upscaling;
            Add(() => s.Overridden, () => Urp.upscalingFilter, v => Urp.upscalingFilter = v,
                b => s.I switch { 1 => UpscalingFilterSelection.Linear, 2 => UpscalingFilterSelection.FSR, 3 => UpscalingFilterSelection.Point, _ => b });
            Add(() => s.I == 2, () => Urp.fsrOverrideSharpness, v => Urp.fsrOverrideSharpness = v, _ => true);
            Add(() => s.I == 2, () => Urp.fsrSharpness, v => Urp.fsrSharpness = v, _ => Settings.FsrSharpness.Value.Value);

            var sh = Settings.Shadows; // Preset, Off, Low, Medium, High, Ultra
            Add(() => sh.Overridden, () => Urp.m_MainLightShadowsSupported, v => Urp.m_MainLightShadowsSupported = v, b => sh.I != 1 && b);
            Add(() => sh.I >= 2, () => Urp.m_MainLightShadowmapResolution, v => Urp.m_MainLightShadowmapResolution = v,
                b => sh.I switch { 2 => UnityEngine.Rendering.Universal.ShadowResolution._1024, 5 => UnityEngine.Rendering.Universal.ShadowResolution._4096, _ => UnityEngine.Rendering.Universal.ShadowResolution._2048 });
            Add(() => sh.I >= 2, () => Urp.m_SoftShadowsSupported, v => Urp.m_SoftShadowsSupported = v, _ => sh.I >= 3);
            Add(() => sh.I >= 3, () => Urp.m_SoftShadowQuality, v => Urp.m_SoftShadowQuality = v,
                _ => sh.I switch { 3 => SoftShadowQuality.Low, 4 => SoftShadowQuality.Medium, _ => SoftShadowQuality.High });

            var c = Settings.Cascades;
            Add(() => c.Overridden, () => Urp.m_ShadowCascadeCount, v => Urp.m_ShadowCascadeCount = v, _ => c.I);

            var ls = Settings.LocalShadows; // Preset, Off, Low, Medium, High
            // Off is done per light (ApplyLocalLights), not with m_AdditionalLightShadowsSupported: flipping
            // that flag while the game runs hangs the GPU on DirectX 12 (device removed, 887a0006).
            // Off keeps the atlas but shrinks it: at 4096 it holds 128 MB even with no shadowed light.
            Add(() => ls.Overridden, () => Urp.m_AdditionalLightsShadowmapResolution, v => Urp.m_AdditionalLightsShadowmapResolution = v,
                _ => ls.I switch { 1 => UnityEngine.Rendering.Universal.ShadowResolution._256, 2 => UnityEngine.Rendering.Universal.ShadowResolution._1024, 3 => UnityEngine.Rendering.Universal.ShadowResolution._2048, _ => UnityEngine.Rendering.Universal.ShadowResolution._4096 });

            var li = Settings.Lighting; // Preset, Low, Medium, High, Ultra
            Add(() => li.Overridden, () => Urp.m_AdditionalLightsPerObjectLimit, v => Urp.m_AdditionalLightsPerObjectLimit = v, _ => li.I * 2);
            Add(() => li.Overridden, () => Urp.m_ReflectionProbeBlending, v => Urp.m_ReflectionProbeBlending = v, b => li.I >= 3 && b);

            var ao = Settings.AO; // Preset, Off, Low, Medium, High
            Add(() => ao.Overridden, () => Feature<ScreenSpaceAmbientOcclusion>()?.isActive ?? false,
                v => Feature<ScreenSpaceAmbientOcclusion>()?.SetActive(v), _ => ao.I != 1);
            AddAo(a => a.Downsample, (a, v) => a.Downsample = v, b => ao.I is 2 or 3 || b);
            AddAo(a => a.Samples, (a, v) => a.Samples = v, b => ao.I switch { 2 => AO.AOSampleOption.Low, 3 => AO.AOSampleOption.Medium, _ => b });
            AddAo(a => a.BlurQuality, (a, v) => a.BlurQuality = v, b => ao.I switch { 2 => AO.BlurQualityOptions.Low, 3 => AO.BlurQualityOptions.Medium, _ => b });
            AddAo(a => a.AOMethod, (a, v) => a.AOMethod = v, b => ao.I == 2 ? AO.AOMethodOptions.InterleavedGradient : b);
            // Depth-only source skips the depth-normals prepass, the largest part of SSAO's cost.
            AddAo(a => a.Source, (a, v) => a.Source = v, b => ao.I == 2 ? AO.DepthSource.Depth : b);

            var ssr = Settings.SSR;
            Add(() => ssr.Overridden, () => Feature<ShinySSRR.ShinySSRR>()?.isActive ?? false,
                v => Feature<ShinySSRR.ShinySSRR>()?.SetActive(v), _ => ssr.I != 1);

            var vl = Settings.VolLights; // Preset, Off, Low, Medium, High
            Add(() => vl.Overridden, () => Feature<VolumetricLightsRenderFeature>()?.isActive ?? false,
                v => Feature<VolumetricLightsRenderFeature>()?.SetActive(v), _ => vl.I != 1);
            Add(() => vl.I is 2 or 3, () => Feature<VolumetricLightsRenderFeature>()?.downscaling ?? 1f,
                v => { var f = Feature<VolumetricLightsRenderFeature>(); if (f != null) f.downscaling = v; },
                b => Mathf.Max(b, vl.I == 2 ? 2f : 1.5f));

            var lod = Settings.LodBias; // Preset, Lowest, Low, Medium, High
            Add(() => lod.Overridden, () => QualitySettings.lodBias, v => QualitySettings.lodBias = v,
                _ => lod.I switch { 1 => 0.5f, 2 => 0.75f, 3 => 1f, _ => 1.5f });

            var tex = Settings.TextureBudget; // Preset, 512 MB, 1 GB, 2 GB, 4 GB
            Add(() => tex.Overridden, () => QualitySettings.streamingMipmapsMemoryBudget, v => QualitySettings.streamingMipmapsMemoryBudget = v,
                _ => 256f * (1 << tex.I));
        }

        static void Add<T>(Func<bool> active, Func<T> get, Action<T> set, Func<T, T> want) =>
            slots.Add(new Slot<T>(active, get, set, want));

        static void AddAo<T>(Func<AO, T> get, Action<AO, T> set, Func<T, T> want)
        {
            AO Settings() => Feature<ScreenSpaceAmbientOcclusion>()?.m_Settings;
            Add(() => GigglelandPerf.Settings.AO.I is 2 or 3 && Settings() != null,
                () => Settings() is { } a ? get(a) : default,
                v => { if (Settings() is { } a) set(a, v); }, want);
        }

        // ---------- entry points ----------

        public static void ApplyAll()
        {
            var urp = Urp;
            if (urp == null) return;

            if (firstRun)
            {
                firstRun = false;
                if (!Settings.FirstRunDone.Value)
                {
                    var tier = Settings.DetectTier(out var why);
                    Plugin.Log.LogInfo($"first run: {why} -> {(tier ?? "no changes")}");
                    if (tier != null) Settings.ApplyPreset(tier);
                    Settings.FirstRunDone.Value = true;
                }
            }

            if (slots.Count == 0 || urp.GetInstanceID() != assetId)
            {
                assetId = urp.GetInstanceID();
                BuildSlots();
                foreach (var s in slots) s.Capture();
            }
            foreach (var s in slots) Try(s.Apply);
            ApplySceneObjects();
        }

        /// The game is about to write graphics settings: hand it back its own values first.
        public static void BeforeGameApply()
        {
            if (gameDepth++ == 0) foreach (var s in slots) Try(s.Restore);
        }

        /// The game finished: record what it chose as the new baseline, then put ours back on top.
        public static void AfterGameApply()
        {
            if (--gameDepth > 0) return;
            gameDepth = 0;
            foreach (var s in slots) Try(s.Capture);
            ApplyAll();
        }

        static void Try(Action a)
        {
            try { a(); } catch (Exception e) { Plugin.Log.LogWarning("apply: " + e.Message); }
        }

        // ---------- scene objects (fog volumes, volumetric lights, post volumes, terrain) ----------

        static readonly Dictionary<string, object> snap = new();
        static List<Volume> volumes = new();
        static List<Light> lights = new();
        static float nextVolumeScan;

        public static void RescanVolumes() => nextVolumeScan = 0f;

        /// Writes `want(base)` while `active`, restores the base when it stops. Returns true on a write.
        static bool Obj<T>(UnityEngine.Object o, string field, bool active, Func<T> get, Action<T> set, Func<T, T> want)
        {
            string k = o.GetInstanceID() + ":" + field;
            if (active)
            {
                if (!snap.TryGetValue(k, out var b)) snap[k] = b = get();
                var w = want((T)b);
                if (EqualityComparer<T>.Default.Equals(get(), w)) return false;
                set(w);
                return true;
            }
            if (!snap.TryGetValue(k, out var orig)) return false;
            snap.Remove(k);
            set((T)orig);
            return true;
        }

        public static void ApplySceneObjects()
        {
            ApplyFog();
            ApplyVolumetricLights();
            if (Time.unscaledTime >= nextVolumeScan)
            {
                nextVolumeScan = Time.unscaledTime + 10f;
                volumes = new List<Volume>(UnityEngine.Object.FindObjectsOfType<Volume>());
                lights = new List<Light>(UnityEngine.Object.FindObjectsOfType<Light>());
            }
            foreach (var v in volumes) if (v != null) Try(() => ApplyVolume(v));
            ApplyLocalLights();
            ApplyTerrain();
        }

        static void ApplyFog()
        {
            var lvl = Settings.VolFog.I; // Preset, Off, Low, Medium, High
            var mgr = VolumetricFogManager._instance;
            if (mgr != null)
            {
                Obj(mgr, "down", lvl is 2 or 3, () => mgr.downscaling, v => mgr.downscaling = v, b => Mathf.Max(b, lvl == 2 ? 2f : 1.5f));
                Obj(mgr, "hqScatter", lvl == 2, () => mgr.scatteringHighQuality, v => mgr.scatteringHighQuality = v, _ => false);
            }
            var fogs = VFog.volumetricFogs;
            for (int i = 0; fogs != null && i < fogs.Count; i++)
            {
                var f = fogs[i];
                if (f == null) continue;
                Obj(f, "enabled", lvl == 1, () => f.enabled, v => f.enabled = v, _ => false);
                var p = f.profile;
                if (p == null) continue;
                bool changed = Obj(p, "rq", lvl is 2 or 3, () => p.raymarchQuality, v => p.raymarchQuality = v, b => Math.Min(b, lvl == 2 ? 3 : 5));
                changed |= Obj(p, "detail", lvl == 2, () => p.useDetailNoise, v => p.useDetailNoise = v, _ => false);
                if (changed && f.enabled) f.UpdateMaterialProperties();
            }
        }

        static void ApplyVolumetricLights()
        {
            var lvl = Settings.VolLights.I;
            var list = VLight.volumetricLights;
            for (int i = 0; list != null && i < list.Count; i++)
            {
                var l = list[i];
                if (l == null) continue;
                Obj(l, "enabled", lvl == 1, () => l.enabled, v => l.enabled = v, _ => false);
                bool changed = Obj(l, "rq", lvl is 2 or 3, () => l.raymarchQuality, v => l.raymarchQuality = v, b => Math.Min(b, lvl == 2 ? 4 : 6));
                // Shadowed volumetric lights render an extra shadow map each; Low drops it.
                changed |= Obj(l, "shadows", lvl == 2, () => l.enableShadows, v => l.enableShadows = v, _ => false);
                if (changed && l.enabled) l.UpdateMaterialProperties();
            }
        }

        static void ApplyLocalLights()
        {
            bool off = Settings.LocalShadows.I == 1;
            foreach (var l in lights)
                if (l != null && l.type != LightType.Directional)
                    Obj(l, "shadows", off, () => l.shadows, v => l.shadows = v, _ => LightShadows.None);
        }

        static void ApplyVolume(Volume v)
        {
            var prof = v.sharedProfile;
            if (prof == null) return;
            var comps = prof.components;
            for (int i = 0; i < comps.Count; i++)
            {
                var b = comps[i].TryCast<Beautify.Universal.Beautify>();
                if (b != null) { ApplyBeautify(b); continue; }
                var r = comps[i].TryCast<ShinyScreenSpaceRaytracedReflections>();
                if (r != null) ApplyReflections(r);
            }
        }

        static void P<T>(VolumeComponent owner, string name, VolumeParameter<T> p, bool active, Func<T, T> want)
        {
            if (p == null) return;
            Obj(owner, name + ".o", active, () => p.overrideState, s => p.overrideState = s, _ => true);
            Obj(owner, name, active, () => p.value, x => p.value = x, want);
        }

        static void ApplyBeautify(Beautify.Universal.Beautify b)
        {
            int post = Settings.Post.I; // Preset, Low, Medium, High
            P(b, "turbo", b.turboMode, post is 1 or 2, _ => true);
            P(b, "quickBloom", b.bloomQuickerBlur, post == 1, _ => true);
            P(b, "dofDown", b.depthOfFieldDownsampling, post == 1, x => Math.Max(x, 2));
            P(b, "bloom", b.bloomIntensity, Settings.Bloom.I == 1, _ => 0f);
            P(b, "sunFlares", b.sunFlaresIntensity, Settings.Flares.I == 1, _ => 0f);
            P(b, "anaFlares", b.anamorphicFlaresIntensity, Settings.Flares.I == 1, _ => 0f);
            P(b, "dof", b.depthOfField, Settings.DoF.I == 1, _ => false);
        }

        static void ApplyReflections(ShinyScreenSpaceRaytracedReflections r)
        {
            int lvl = Settings.SSR.I; // Preset, Off, Low, Medium, High
            bool reduce = lvl is 2 or 3;
            P(r, "down", r.downsampling, reduce, x => Math.Max(x, lvl == 2 ? 3 : 2));
            P(r, "samples", r.sampleCount, reduce, x => Math.Min(x, lvl == 2 ? 12 : 24));
            P(r, "blurDown", r.blurDownsampling, lvl == 2, x => Math.Max(x, 2));
            P(r, "refine", r.refineThickness, lvl == 2, _ => false);
        }

        static void ApplyTerrain()
        {
            int lvl = Settings.Vegetation.I; // Preset, Low, Medium, High
            var terrains = Terrain.activeTerrains;
            for (int i = 0; terrains != null && i < terrains.Length; i++)
            {
                var t = terrains[i];
                if (t == null) continue;
                bool on = lvl is 1 or 2;
                Obj(t, "density", on, () => t.detailObjectDensity, v => t.detailObjectDensity = v, b => b * (lvl == 1 ? 0.35f : 0.65f));
                Obj(t, "detailDist", on, () => t.detailObjectDistance, v => t.detailObjectDistance = v, b => Mathf.Min(b, lvl == 1 ? 35f : 70f));
                Obj(t, "pixelErr", on, () => t.heightmapPixelError, v => t.heightmapPixelError = v, b => Mathf.Max(b, lvl == 1 ? 12f : 7f));
                Obj(t, "treeBillboard", on, () => t.treeBillboardDistance, v => t.treeBillboardDistance = v, b => Mathf.Min(b, lvl == 1 ? 40f : 80f));
            }
        }
    }
}
