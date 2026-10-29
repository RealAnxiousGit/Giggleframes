using System;
using System.IO;
using UnityEngine;

namespace GigglelandPerf
{
    /// Test hook: one command per line in BepInEx/gigglelandperf.cmd, read once a second and deleted.
    /// Only active when Debug.DevCommands is on.
    static class Dev
    {
        static string FilePath => Path.Combine(BepInEx.Paths.BepInExRootPath, "gigglelandperf.cmd");
        static float next;

        public static void Tick()
        {
            if (!Settings.DevCommands.Value || Time.unscaledTime < next) return;
            next = Time.unscaledTime + 1f;
            if (!File.Exists(FilePath)) return;
            string[] lines;
            try { lines = File.ReadAllLines(FilePath); File.Delete(FilePath); }
            catch { return; }
            foreach (var l in lines)
            {
                if (string.IsNullOrWhiteSpace(l)) continue;
                try { Run(l.Trim().Split(' ')); }
                catch (Exception e) { Plugin.Log.LogError($"dev '{l}': {e}"); }
            }
        }

        static SettingsListPanel Visible()
        {
            foreach (var p in UnityEngine.Object.FindObjectsOfType<SettingsListPanel>())
                if (p.IsVisible) return p;
            return null;
        }

        static void Run(string[] a)
        {
            Plugin.Log.LogInfo("dev> " + string.Join(" ", a));
            switch (a[0])
            {
                case "menuclick":
                    foreach (var m in UnityEngine.Object.FindObjectsOfType<MenuPanel>())
                    {
                        Plugin.Log.LogInfo($"menu {Diag.Path(m.transform)} enabled={m.isActiveAndEnabled}");
                        if (m.isActiveAndEnabled) m.FireClick(int.Parse(a[1]), a.Length > 2 && a[2] == "1");
                    }
                    break;
                case "settingsclick":
                    foreach (var p in UnityEngine.Object.FindObjectsOfType<SettingsPanel>())
                        if (p.IsVisible) p.FireClick(int.Parse(a[1]));
                    break;
                case "show":
                    foreach (var p in UnityEngine.Object.FindObjectsOfType<SettingsPanel>())
                    {
                        Plugin.Log.LogInfo($"settings panel {Diag.Path(p.transform)} visible={p.IsVisible}");
                        p.Show();
                    }
                    break;
                case "category":
                    UnityEngine.Object.FindObjectOfType<SettingsPanel>()?.HandleCategory(
                        Enum.Parse<SettingsPanel.SettingsCategory>(a[1]));
                    break;
                case "nav": Visible()?.NavigateSelection(int.Parse(a[1])); break;
                case "select":
                    var vp = Visible();
                    var defs = vp?.rowDefs;
                    int idx = -1;
                    for (int i = 0; defs != null && i < defs.Count; i++) if (defs[i].setting?.key == a[1]) idx = i;
                    if (idx < 0) { Plugin.Log.LogWarning("no row " + a[1]); break; }
                    vp.SetSelected(idx);
                    vp.EnsureVisible(idx);
                    Plugin.Log.LogInfo($"selected {a[1]} (#{idx}), label '{defs[idx].rowObject.Find("Label")?.GetComponent<TMPro.TMP_Text>()?.text}'");
                    break;
                case "fonts":
                    var fp = Visible();
                    foreach (var t in fp.GetComponentsInChildren<TMPro.TMP_Text>(false))
                        Plugin.Log.LogInfo($"font {t.transform.parent?.name}/{t.name}: {t.font?.name} {t.fontSize} '{t.text}'");
                    break;
                case "adjust": Visible()?.AdjustSelected(int.Parse(a[1])); break;
                case "where":
                    // Screen pixel (from the top-left) of a row's value text, or a drop-down item: for real mouse clicks.
                    RectTransform wrt = null;
                    if (a[1] == "item") wrt = GameObject.Find("GiggleframesDropdown/List/Item_" + a[2])?.GetComponent<RectTransform>();
                    else { var wr = Visible()?.FindRow(a[1]); wrt = wr?.valueText != null ? wr.valueText.rectTransform : wr?.root; }
                    if (wrt == null) { Plugin.Log.LogWarning("where: nothing"); break; }
                    var wc = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector3>(4);
                    wrt.GetWorldCorners(wc);
                    var wcv = wrt.GetComponentInParent<Canvas>()?.rootCanvas;
                    var cam = wcv != null && wcv.renderMode != RenderMode.ScreenSpaceOverlay ? wcv.worldCamera : null;
                    var sp0 = RectTransformUtility.WorldToScreenPoint(cam, (wc[0] + wc[2]) * 0.5f);
                    Plugin.Log.LogInfo($"where {string.Join(" ", a, 1, a.Length - 1)}: {sp0.x:F0} {Screen.height - sp0.y:F0} of {Screen.width}x{Screen.height}");
                    break;
                case "submit": Visible()?.SubmitSelected(); break;
                case "rowclick":
                    // Same call the row's own click handler makes.
                    var rp = Visible();
                    var row = rp?.FindRow(a[1]);
                    if (row != null) rp.CycleOption(row, 1);
                    break;
                case "slide":
                    // What a mouse drag does: set the UI slider's value.
                    var sp = Visible();
                    var srow = sp?.FindRow(a[1]);
                    if (srow?.slider != null) srow.slider.value = float.Parse(a[2], System.Globalization.CultureInfo.InvariantCulture);
                    Plugin.Log.LogInfo($"slider {a[1]} = {srow?.slider?.value}, setting {srow?.setting?.floatValue}, text '{srow?.valueText?.text}'");
                    break;
                case "itemclick":
                    var item = GameObject.Find("GiggleframesDropdown/List/Item_" + a[1]);
                    item?.GetComponent<UnityEngine.EventSystems.EventTrigger>()?.OnPointerClick(
                        new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current));
                    Plugin.Log.LogInfo("item click " + (item ? "sent" : "no item"));
                    break;
                case "back": Visible()?.GoBack(); break;
                case "visible":
                    var v = Visible();
                    Plugin.Log.LogInfo("visible panel: " + (v ? Diag.Path(v.transform) : "none"));
                    break;
                case "set":
                    var o = Settings.Find(a[1]);
                    if (o != null) o.Index.Value = int.Parse(a[2]);
                    Applier.ApplyAll();
                    break;
                case "preset": AdvancedPage.Preset(string.Join(" ", a, 1, a.Length - 1)); break;
                case "fps": Settings.FpsCounter.Index.Value = a[1] == "1" ? 1 : 0; break;
                case "culling": Plugin.Log.LogInfo(Culling.Status); break;
                case "probe": Plugin.Log.LogInfo("probe " + (a.Length > 1 ? a[1] : "") + ": " + Diag.Probe()); break;
                case "dump": Diag.DumpRenderState(); break;
                case "mem": Diag.DumpMemory(); break;
                case "mip": QualitySettings.globalTextureMipmapLimit = int.Parse(a[1]); break;
                case "unload": Resources.UnloadUnusedAssets(); System.GC.Collect(); break;
                case "calib":
                    foreach (var found in Resources.FindObjectsOfTypeAll(Il2CppInterop.Runtime.Il2CppType.Of<BrightnessCalibrationScreen>()))
                    {
                        var cs = found.TryCast<BrightnessCalibrationScreen>();
                        if (cs == null || !cs.IsOpen) continue;
                        if (a[1] == "cancel") cs.Cancel(); else cs.Confirm();
                        Plugin.Log.LogInfo("calibration " + a[1]);
                    }
                    break;
                case "popup":
                    foreach (var found in Resources.FindObjectsOfTypeAll(Il2CppInterop.Runtime.Il2CppType.Of<ConfirmationPopup>()))
                    {
                        var cp = found.TryCast<ConfirmationPopup>();
                        if (cp == null || !cp.IsOpen) continue;
                        Plugin.Log.LogInfo("popup open: " + cp.titleText?.text + " | " + cp.messageText?.text);
                        if (a.Length > 1 && a[1] == "later") cp.OnCancel();
                        if (a.Length > 1 && a[1] == "restart") cp.OnConfirm();
                    }
                    break;
                case "pause":
                    // The pause screen's object is inactive until the pause key turns it on; do the same,
                    // then press its own Settings button.
                    foreach (var found in Resources.FindObjectsOfTypeAll(Il2CppInterop.Runtime.Il2CppType.Of<PauseManager>()))
                    {
                        var pm = found.TryCast<PauseManager>();
                        if (pm == null || !pm.gameObject.scene.IsValid()) continue;
                        pm.gameObject.SetActive(true);
                        Plugin.Log.LogInfo("pause screen: " + Diag.Path(pm.transform));
                    }
                    if (a.Length > 1 && a[1] == "settings") UnityEngine.Object.FindObjectOfType<PauseSettingsBridge>()?.OpenSettings();
                    break;
                case "mainmenu":
                    foreach (var found in Resources.FindObjectsOfTypeAll(Il2CppInterop.Runtime.Il2CppType.Of<PauseManager>()))
                    {
                        var pm = found.TryCast<PauseManager>();
                        if (pm == null || !pm.gameObject.scene.IsValid()) continue;
                        pm.gameObject.SetActive(true);
                        pm.onClickMainMenu();
                        break;
                    }
                    break;
                case "scene":
                    Plugin.Log.LogInfo("scene: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
                    break;
                case "applier":
                    GraphicsSettingsApplier gsa = null;
                    foreach (var found in Resources.FindObjectsOfTypeAll(Il2CppInterop.Runtime.Il2CppType.Of<GraphicsSettingsApplier>()))
                    {
                        var c = found.TryCast<GraphicsSettingsApplier>();
                        if (c != null && c.gameObject.scene.IsValid()) gsa = c;
                    }
                    Plugin.Log.LogInfo($"applier {(gsa ? "found" : "missing")}, enabled={(gsa ? gsa.enabled : false)}");
                    if (a[1] == "revert") gsa.Revert();
                    else if (a[1] == "reset") gsa.ResetToDefaults();
                    else if (a[1] == "apply") gsa.Apply();
                    Plugin.Log.LogInfo("applier " + a[1] + " done");
                    break;
                case "spin": Bench.Spin(a.Length > 1 ? a[1] : "spin", a.Length > 2 ? float.Parse(a[2]) : 16f); break;
                case "scenestats": Diag.DumpScene(); break;
                case "cams": Diag.CountCameras(a.Length > 1 ? int.Parse(a[1]) : 60); break;
                case "look":
                    var fpc = UnityEngine.Object.FindObjectOfType<FirstPersonController>();
                    if (fpc == null) break;
                    if (a.Length > 1) fpc.transform.rotation = Quaternion.Euler(0f, float.Parse(a[1]), 0f);
                    Plugin.Log.LogInfo($"yaw {fpc.transform.eulerAngles.y:0}");
                    break;
                case "exp": Experiment(a[1], a.Length > 2 ? a[2] : "1"); break;
                case "bench": Bench.Start(a.Length > 1 ? a[1] : "bench", a.Length > 2 ? float.Parse(a[2]) : 10f); break;
                case "shot":
                    var path = Path.Combine(BepInEx.Paths.BepInExRootPath, "shots", a[1] + ".png");
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    ScreenCapture.CaptureScreenshot(path);
                    break;
            }
        }

        static int camMask = -2;
        static float camFar = -1f, camScale = -1f, dynRes = -1f;
        static bool camHooked;

        // The game rewrites the main camera every frame, so overrides go in right before it renders.
        static void HookCam()
        {
            if (camHooked) return;
            camHooked = true;
            UnityEngine.Rendering.RenderPipelineManager.add_beginCameraRendering((System.Action<UnityEngine.Rendering.ScriptableRenderContext, Camera>)((_, cam) =>
            {
                if (cam == null || cam.name != "MainCamera") return;
                if (camMask != -2) cam.cullingMask = camMask;
                if (camFar > 0) cam.farClipPlane = camFar;
                if (dynRes > 0) cam.allowDynamicResolution = true;
                if (camScale > 0) { var u = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.TryCast<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>(); u.renderScale = camScale; }
            }));
        }

        /// One-off A/B switches for finding what a busy view costs; not saved anywhere.
        static void Experiment(string what, string v)
        {
            float f = float.Parse(v, System.Globalization.CultureInfo.InvariantCulture);
            var urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline?.TryCast<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>();
            var main = Camera.main;
            switch (what)
            {
                case "holdcam":
                    foreach (var c in Resources.FindObjectsOfTypeAll(Il2CppInterop.Runtime.Il2CppType.Of<Camera>()))
                    {
                        var cam = c.TryCast<Camera>();
                        if (cam != null && cam.name == "ObjectHoldCam") cam.enabled = f > 0;
                    }
                    break;
                case "scale": urp.renderScale = f; break;
                case "far": main.farClipPlane = f; break;
                case "mainshadows": urp.m_MainLightShadowsSupported = f > 0; break;
                case "shadowdist": urp.shadowDistance = f; break;
                case "mask": camMask = (int)f; HookCam(); break;
                case "camfar": camFar = f; HookCam(); break;
                case "camscale": camScale = f; HookCam(); break;
                case "dynres": dynRes = f; HookCam(); ScalableBufferManager.ResizeBuffers(f, f); break;
                case "animators":
                    foreach (var an in UnityEngine.Object.FindObjectsOfType<Animator>()) an.enabled = f > 0;
                    break;
                case "occl": main.useOcclusionCulling = f > 0; break;
                case "camshadows": main.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderShadows = f > 0; break;
                case "maxlod": QualitySettings.maximumLODLevel = (int)f; break;
                case "lodbias": QualitySettings.lodBias = f; break;
                case "cull":
                    // Screen-size cull: hide renderers whose bounds are smaller than f of their distance.
                    int n = 0;
                    var cp = main.transform.position;
                    foreach (var r in UnityEngine.Object.FindObjectsOfType<Renderer>())
                    {
                        var b = r.bounds;
                        bool hide = f > 0 && b.size.magnitude < f * Vector3.Distance(cp, b.center);
                        r.forceRenderingOff = hide;
                        if (hide) n++;
                    }
                    Plugin.Log.LogInfo($"cull {f}: {n} hidden");
                    break;
                case "smallshadows":
                    int m = 0;
                    foreach (var r in UnityEngine.Object.FindObjectsOfType<Renderer>())
                        if (r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.On && r.bounds.size.magnitude < f)
                        { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; m++; }
                    Plugin.Log.LogInfo($"smallshadows {f}: {m} off");
                    break;
            }
            Plugin.Log.LogInfo($"exp {what} {v}");
        }
    }
}
