using System.Text;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GigglelandPerf
{
    /// Log dumps used to learn the live game; off unless Debug.DumpOnLoad is set.
    static class Diag
    {
        public static void DumpPanel(SettingsListPanel p)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[panel] {Path(p.transform)} active={p.gameObject.activeInHierarchy}");
            foreach (var c in p.GetComponents<Component>()) sb.AppendLine($"  comp {c.GetIl2CppType().FullName}");
            Listeners(sb, "onShown", p.onShown);
            Listeners(sb, "onHidden", p.onHidden);
            Listeners(sb, "onBack", p.onBack);
            var defs = p.rowDefs;
            for (int i = 0; defs != null && i < defs.Count; i++)
            {
                var d = defs[i];
                var s = d.setting;
                sb.AppendLine($"  row {i}: key={s?.key} label='{s?.label}' type={s?.type} " +
                              $"min={s?.minValue} max={s?.maxValue} val={s?.floatValue} opts={(s?.options == null ? 0 : s.options.Length)} " +
                              $"obj={(d.rowObject ? Path(d.rowObject) : "null")}");
            }
            if (defs != null && defs.Count > 0 && defs[0].rowObject) Tree(defs[0].rowObject.parent, sb, 0, 3);
            Plugin.Log.LogInfo(sb.ToString());
        }

        /// Where the memory goes: process totals, Unity heaps, and estimated sizes of loaded textures,
        /// render targets, meshes and audio (Profiler.GetRuntimeMemorySizeLong is 0 in release players).
        public static void DumpMemory()
        {
            const double MB = 1024 * 1024;
            var sb = new StringBuilder("[mem]\n");
            using (var proc = System.Diagnostics.Process.GetCurrentProcess())
                sb.AppendLine($"  process: private {proc.PrivateMemorySize64 / MB:0} MB, working set {proc.WorkingSet64 / MB:0} MB");
            sb.AppendLine($"  unity: allocated {UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / MB:0} MB, " +
                          $"reserved {UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong() / MB:0} MB, " +
                          $"il2cpp heap {UnityEngine.Profiling.Profiler.GetMonoHeapSizeLong() / MB:0} MB, " +
                          $"managed (mod) {System.GC.GetTotalMemory(false) / MB:0} MB");
            sb.AppendLine($"  textures: current {Texture.currentTextureMemory / MB:0} MB, desired {Texture.desiredTextureMemory / MB:0} MB, " +
                          $"non-streaming {Texture.nonStreamingTextureMemory / MB:0} MB (count {Texture.nonStreamingTextureCount}), " +
                          $"streaming {Texture.streamingTextureCount}, budget {QualitySettings.streamingMipmapsMemoryBudget:0} MB, " +
                          $"streamMips={QualitySettings.streamingMipmapsActive}, mipLimit={QualitySettings.globalTextureMipmapLimit}");

            var tex = new System.Collections.Generic.List<(double mb, string what)>();
            double texTotal = 0, readable = 0;
            foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<Texture2D>()))
            {
                var t = o.TryCast<Texture2D>();
                if (t == null) continue;
                double mb = t.width * (double)t.height * Bpp(t.format.ToString()) / 8 * (t.mipmapCount > 1 ? 1.33 : 1) / MB;
                texTotal += mb;
                if (t.isReadable) readable += mb;
                tex.Add((mb, $"{t.name} {t.width}x{t.height} {t.format} mips={t.mipmapCount}{(t.isReadable ? " readable" : "")}"));
            }
            double rtTotal = 0;
            var rts = new System.Collections.Generic.List<(double mb, string what)>();
            foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<RenderTexture>()))
            {
                var r = o.TryCast<RenderTexture>();
                if (r == null) continue;
                double mb = r.width * (double)r.height * System.Math.Max(1, r.volumeDepth) * Bpp(r.graphicsFormat.ToString())
                            / 8 * (r.useMipMap ? 1.33 : 1) / MB + (r.depth > 0 ? r.width * (double)r.height * 4 / MB : 0);
                rtTotal += mb;
                rts.Add((mb, $"{r.name} {r.width}x{r.height}x{r.volumeDepth} {r.graphicsFormat} depth={r.depth} created={r.IsCreated()}"));
            }
            double meshTotal = 0; int meshes = 0, readableMeshes = 0;
            foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<Mesh>()))
            {
                var m = o.TryCast<Mesh>();
                if (m == null) continue;
                meshes++;
                double mb = (m.vertexCount * 48.0 + m.GetIndexCount(0) * 2.0 * System.Math.Max(1, m.subMeshCount)) / MB;
                if (m.isReadable) { readableMeshes++; mb *= 2; }
                meshTotal += mb;
            }
            double audioTotal = 0; int clips = 0;
            var audio = new System.Collections.Generic.List<(double mb, string what)>();
            foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<AudioClip>()))
            {
                var a = o.TryCast<AudioClip>();
                if (a == null) continue;
                clips++;
                // Decompress-on-load clips sit in RAM as 16-bit PCM; streamed/compressed ones stay small.
                double mb = a.loadType == AudioClipLoadType.DecompressOnLoad ? a.samples * (double)a.channels * 2 / MB : 0;
                audioTotal += mb;
                if (mb > 0) audio.Add((mb, $"{a.name} {a.length:0}s {a.channels}ch {a.frequency}Hz"));
            }
            sb.AppendLine($"  Texture2D: {tex.Count} loaded, ~{texTotal:0} MB (CPU-readable copies ~{readable:0} MB)");
            sb.AppendLine($"  RenderTexture: {rts.Count}, ~{rtTotal:0} MB");
            sb.AppendLine($"  Mesh: {meshes} (readable {readableMeshes}), ~{meshTotal:0} MB");
            sb.AppendLine($"  AudioClip: {clips}, decompressed in RAM ~{audioTotal:0} MB");
            Top(sb, "biggest textures", tex, 15);
            Top(sb, "biggest render targets", rts, 12);
            Top(sb, "biggest decompressed audio", audio, 8);
            Plugin.Log.LogInfo(sb.ToString());
        }

        static void Top(StringBuilder sb, string title, System.Collections.Generic.List<(double mb, string what)> l, int n)
        {
            l.Sort((a, b) => b.mb.CompareTo(a.mb));
            sb.AppendLine($"  {title}:");
            for (int i = 0; i < l.Count && i < n; i++) sb.AppendLine($"    {l[i].mb,7:0.0} MB  {l[i].what}");
        }

        // Bits per pixel from a TextureFormat / GraphicsFormat name; rough, good enough to rank.
        static double Bpp(string f)
        {
            f = f.ToUpperInvariant();
            if (f.Contains("DXT1") || f.Contains("BC1") || f.Contains("BC4") || f.Contains("ETC_") || f.Contains("ETC2_RGB")) return 4;
            if (f.Contains("DXT5") || f.Contains("BC2") || f.Contains("BC3") || f.Contains("BC5") || f.Contains("BC6") || f.Contains("BC7")) return 8;
            if (f.Contains("FLOAT") && f.Contains("RGBA") || f.Contains("R32G32B32A32")) return 128;
            if (f.Contains("HALF") && f.Contains("RGBA") || f.Contains("R16G16B16A16")) return 64;
            if (f == "RGB24" || f.Contains("R8G8B8A8") || f.Contains("B8G8R8A8") || f.Contains("RGBA32") || f.Contains("ARGB32") ||
                f.Contains("BGRA32") || f.Contains("B10G11R11") || f.Contains("A2B10G10R10") || f.Contains("RGHALF") || f.Contains("R16G16_") ||
                f == "RFLOAT" || f.Contains("R32_") || f.Contains("D24") || f.Contains("D32")) return 32;
            if (f.Contains("R8G8_") || f == "RHALF" || f.Contains("R16_") || f == "R16" || f.Contains("RG16") || f.Contains("D16")) return 16;
            if (f.Contains("R8") || f.Contains("ALPHA8")) return 8;
            return 32;
        }

        public static void DumpRenderState()
        {
            var sb = new StringBuilder("[render]\n");
            sb.AppendLine($"  quality={QualitySettings.GetQualityLevel()} names={string.Join(",", QualitySettings.names)} " +
                          $"vsync={QualitySettings.vSyncCount} fps={Application.targetFrameRate} lodBias={QualitySettings.lodBias} " +
                          $"tex={QualitySettings.globalTextureMipmapLimit} aniso={QualitySettings.anisotropicFiltering} " +
                          $"pixelLights={QualitySettings.pixelLightCount} particleBudget={QualitySettings.particleRaycastBudget} " +
                          $"streamMips={QualitySettings.streamingMipmapsActive} res={Screen.width}x{Screen.height}");
            var urp = GraphicsSettings.currentRenderPipeline?.TryCast<UniversalRenderPipelineAsset>();
            if (urp != null)
            {
                sb.AppendLine($"  urp={urp.name} scale={urp.renderScale} upscale={urp.upscalingFilter} msaa={urp.msaaSampleCount} hdr={urp.supportsHDR} " +
                              $"shadowDist={urp.shadowDistance} cascades={urp.shadowCascadeCount} mainRes={urp.mainLightShadowmapResolution} " +
                              $"soft={urp.supportsSoftShadows}/{urp.softShadowQuality} addMode={urp.additionalLightsRenderingMode} " +
                              $"addPerObj={urp.m_AdditionalLightsPerObjectLimit} addShadows={urp.supportsAdditionalLightShadows} " +
                              $"addRes={urp.additionalLightsShadowmapResolution} depth={urp.supportsCameraDepthTexture} opaque={urp.supportsCameraOpaqueTexture} " +
                              $"lut={urp.colorGradingLutSize} probeBlend={urp.m_ReflectionProbeBlending} lodXfade={urp.m_EnableLODCrossFade}");
                var list = urp.m_RendererDataList;
                for (int i = 0; list != null && i < list.Length; i++)
                {
                    var rd = list[i];
                    if (rd == null) continue;
                    sb.AppendLine($"  renderer[{i}] {rd.name} ({rd.GetIl2CppType().Name})");
                    var feats = rd.rendererFeatures;
                    for (int f = 0; feats != null && f < feats.Count; f++)
                        if (feats[f] != null) sb.AppendLine($"    feature {feats[f].name} ({feats[f].GetIl2CppType().FullName}) active={feats[f].isActive}");
                }
            }

            var lights = Object.FindObjectsOfType<Light>();
            int shadowed = 0, realtime = 0;
            foreach (var l in lights) { if (l.shadows != LightShadows.None) shadowed++; if (l.bakingOutput.lightmapBakeType != LightmapBakeType.Baked) realtime++; }
            sb.AppendLine($"  lights={lights.Length} shadowed={shadowed} nonBaked={realtime}");
            sb.AppendLine($"  fogVolumes={VolumetricFogAndMist2.VolumetricFog.volumetricFogs?.Count} volLights={VolumetricLights.VolumetricLight.volumetricLights?.Count}");
            foreach (var v in Object.FindObjectsOfType<Volume>())
            {
                if (v.sharedProfile == null) continue;
                var comps = v.sharedProfile.components;
                var names = new StringBuilder();
                for (int i = 0; i < comps.Count; i++) names.Append(comps[i].GetIl2CppType().Name).Append(comps[i].active ? "+ " : "- ");
                sb.AppendLine($"  volume {Path(v.transform)} global={v.isGlobal} w={v.weight} prio={v.priority}: {names}");
            }
            foreach (var cam in Camera.allCameras)
            {
                var data = cam.GetComponent<UniversalAdditionalCameraData>();
                sb.AppendLine($"  camera {cam.name} far={cam.farClipPlane} hdr={cam.allowHDR} post={(data ? data.renderPostProcessing : false)} " +
                              $"aa={(data ? data.antialiasing : 0)} renderer={(data ? data.scriptableRenderer?.GetIl2CppType().Name : "-")}");
            }
            foreach (var t in Object.FindObjectsOfType<Terrain>())
                sb.AppendLine($"  terrain {t.name} pixelErr={t.heightmapPixelError} detailDist={t.detailObjectDistance} detailDensity={t.detailObjectDensity} treeDist={t.treeDistance}");
            Plugin.Log.LogInfo(sb.ToString());
        }

        /// What the camera has to draw: every camera (including ones rendering into textures), renderer and
        /// shadow-caster counts by size, reflection probes, particles and the batching switches.
        public static void DumpScene()
        {
            var sb = new StringBuilder("[scene]\n");
            var urp = GraphicsSettings.currentRenderPipeline?.TryCast<UniversalRenderPipelineAsset>();
            sb.AppendLine($"  srpBatcher={urp?.useSRPBatcher} dynBatch={urp?.supportsDynamicBatching} " +
                          $"streamMips={QualitySettings.streamingMipmapsActive} budget={QualitySettings.streamingMipmapsMemoryBudget} " +
                          $"maxReduction={QualitySettings.streamingMipmapsMaxLevelReduction} maxFileIO={QualitySettings.streamingMipmapsMaxFileIORequests} " +
                          $"addAllCams={QualitySettings.streamingMipmapsAddAllCameras} maxLod={QualitySettings.maximumLODLevel}");
            foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<Camera>()))
            {
                var cam = o.TryCast<Camera>();
                if (cam == null || !cam.gameObject.scene.IsValid()) continue;
                var data = cam.GetComponent<UniversalAdditionalCameraData>();
                sb.AppendLine($"  camera {Path(cam.transform)} on={cam.isActiveAndEnabled} target={(cam.targetTexture ? cam.targetTexture.name + " " + cam.targetTexture.width + "x" + cam.targetTexture.height : "screen")} " +
                              $"far={cam.farClipPlane} occl={cam.useOcclusionCulling} mask=0x{cam.cullingMask:x} type={(data ? data.renderType.ToString() : "-")} " +
                              $"shadows={(data ? data.renderShadows : false)} depthTex={(data ? data.requiresDepthTexture : false)} layerCull={cam.layerCullSpherical}");
            }
            int mr = 0, smr = 0, cast = 0, smallCast = 0, offCast = 0, noLod = 0;
            var layers = new int[32];
            foreach (var r in Object.FindObjectsOfType<Renderer>())
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (r.TryCast<MeshRenderer>() != null) mr++;
                else if (r.TryCast<SkinnedMeshRenderer>() != null) smr++;
                else continue;
                layers[r.gameObject.layer]++;
                if (r.shadowCastingMode == ShadowCastingMode.Off) { offCast++; continue; }
                cast++;
                if (r.bounds.size.magnitude < 1f) smallCast++;
            }
            foreach (var r in Object.FindObjectsOfType<MeshRenderer>()) if (r.GetComponentInParent<LODGroup>() == null) noLod++;
            sb.AppendLine($"  renderers: mesh={mr} skinned={smr} shadowCasters={cast} (under 1 m: {smallCast}) noShadow={offCast} " +
                          $"withoutLOD={noLod} lodGroups={Object.FindObjectsOfType<LODGroup>().Length}");
            var ls = new StringBuilder();
            for (int i = 0; i < 32; i++) if (layers[i] > 0) ls.Append($"{LayerMask.LayerToName(i)}({i})={layers[i]} ");
            sb.AppendLine("  layers: " + ls);
            foreach (var p in Object.FindObjectsOfType<ReflectionProbe>())
                sb.AppendLine($"  probe {Path(p.transform)} mode={p.mode} refresh={p.refreshMode} slicing={p.timeSlicingMode} res={p.resolution}");
            int ps = 0, psPlaying = 0;
            foreach (var p in Object.FindObjectsOfType<ParticleSystem>()) { ps++; if (p.isPlaying) psPlaying++; }
            sb.AppendLine($"  particles={ps} playing={psPlaying}");
            Plugin.Log.LogInfo(sb.ToString());
        }

        // ---------- camera render counter ----------

        static System.Collections.Generic.Dictionary<string, int> camRenders;
        static int camFrames, camStartFrame;
        static bool camHooked;

        /// Counts which cameras render over the next n frames (scripts can render disabled cameras by hand).
        public static void CountCameras(int n)
        {
            camRenders = new System.Collections.Generic.Dictionary<string, int>();
            camFrames = n;
            camStartFrame = Time.frameCount;
            if (camHooked) return;
            camHooked = true;
            RenderPipelineManager.add_beginCameraRendering((System.Action<ScriptableRenderContext, Camera>)((_, cam) =>
            {
                if (camRenders == null) return;
                if (Time.frameCount - camStartFrame >= camFrames)
                {
                    var sb = new StringBuilder($"[cameras] renders over {camFrames} frames:\n");
                    var l = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, int>>(camRenders);
                    l.Sort((a, b) => b.Value.CompareTo(a.Value));
                    foreach (var kv in l) sb.AppendLine($"  {kv.Value,5}  {kv.Key}");
                    camRenders = null;
                    Plugin.Log.LogInfo(sb.ToString());
                    return;
                }
                string k = cam ? Path(cam.transform) : "?";
                camRenders[k] = camRenders.TryGetValue(k, out var c) ? c + 1 : 1;
            }));
        }

        static void Listeners(StringBuilder sb, string name, UnityEngine.Events.UnityEventBase e)
        {
            for (int i = 0; e != null && i < e.GetPersistentEventCount(); i++)
            {
                var t = e.GetPersistentTarget(i);
                sb.AppendLine($"  {name}: {(t ? t.name + " (" + t.GetIl2CppType().Name + ")" : "null")}.{e.GetPersistentMethodName(i)}");
            }
        }

        static void Tree(Transform t, StringBuilder sb, int depth, int max)
        {
            if (t == null || depth > max) return;
            var comps = new StringBuilder();
            foreach (var c in t.GetComponents<Component>()) comps.Append(c.GetIl2CppType().Name).Append(' ');
            sb.AppendLine($"  {new string(' ', depth * 2)}- {t.name} [{comps}]");
            for (int i = 0; i < t.childCount && i < 40; i++) Tree(t.GetChild(i), sb, depth + 1, max);
        }

        public static string Path(Transform t)
        {
            var s = t.name;
            for (var p = t.parent; p != null; p = p.parent) s = p.name + "/" + s;
            return s;
        }
    }
}
