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
                case "submit": Visible()?.SubmitSelected(); break;
                case "back": Visible()?.GoBack(); break;
                case "visible":
                    var v = Visible();
                    Plugin.Log.LogInfo("visible panel: " + (v ? Diag.Path(v.transform) : "none"));
                    break;
                case "set":
                    var o = Settings.Find(a[1]);
                    if (o?.Choices != null) o.Index.Value = int.Parse(a[2]);
                    else if (o != null) o.Value.Value = float.Parse(a[2], System.Globalization.CultureInfo.InvariantCulture);
                    Applier.ApplyAll();
                    break;
                case "preset": Settings.ApplyPreset(a[1]); Applier.ApplyAll(); break;
                case "fps": Settings.ShowFps.Value = a[1] == "1"; break;
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
                case "bench": Bench.Start(a.Length > 1 ? a[1] : "bench", a.Length > 2 ? float.Parse(a[2]) : 10f); break;
                case "shot":
                    var path = Path.Combine(BepInEx.Paths.BepInExRootPath, "shots", a[1] + ".png");
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    ScreenCapture.CaptureScreenshot(path);
                    break;
            }
        }
    }
}
