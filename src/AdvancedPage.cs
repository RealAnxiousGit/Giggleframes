using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using GS = SettingsListPanel.GraphicsSetting;
using RowDef = SettingsListPanel.RowDef;
using RowType = SettingsListPanel.GraphicsSettingType;

namespace GigglelandPerf
{
    /// Adds an "Advanced" row to the game's Graphics page and builds the Advanced page itself
    /// as a copy of the Graphics page, so it uses the game's own row widgets, fonts and navigation.
    static class AdvancedPage
    {
        const string AdvancedKey = "advanced";
        const string PanelName = "AdvancedGraphicsPanel";

        static readonly Dictionary<int, SettingsListPanel> advancedOf = new();  // graphics panel id -> advanced
        static readonly Dictionary<int, SettingsListPanel> graphicsOf = new();  // advanced panel id -> graphics
        static bool suppress;

        public static bool IsAdvanced(SettingsListPanel p) => p != null && graphicsOf.ContainsKey(p.GetInstanceID());

        /// SettingsListPanel.Awake prefix. Runs before the panel binds its rows.
        public static void OnPanelAwake(SettingsListPanel p)
        {
            if (p.GetComponent<GraphicsSettingsApplier>() == null) return; // only the real Graphics page
            int id = p.GetInstanceID();
            if (advancedOf.ContainsKey(id)) return;

            var adv = BuildAdvancedPanel(p);
            if (adv == null) return;
            advancedOf[id] = adv;
            graphicsOf[adv.GetInstanceID()] = p;
            InjectAdvancedRow(p);
            Plugin.Log.LogInfo($"Advanced page added to {Diag.Path(p.transform)}");
        }

        static void InjectAdvancedRow(SettingsListPanel g)
        {
            var defs = g.rowDefs;
            int insertAt = -1;
            RectTransform template = null;
            for (int i = 0; i < defs.Count; i++)
            {
                var k = defs[i].setting?.key;
                // Below Calibrate Brightness, so the two actions aren't easily confused.
                if (k == "calibrate") { insertAt = i + 1; template = defs[i].rowObject; }
                if (k == AdvancedKey) return;
            }
            if (template == null)
            {
                for (int i = defs.Count - 1; i >= 0 && template == null; i--)
                    if (defs[i].setting?.type == RowType.Action) { insertAt = i + 1; template = defs[i].rowObject; }
            }
            if (template == null) { Plugin.Log.LogWarning("no action row to copy on the Graphics page"); return; }

            var s = NewSetting(AdvancedKey, "Advanced", RowType.Action);
            s.onInvoke = (Il2CppSystem.Action)(Action)(() => Open(g));
            var row = CloneRow(template, template.parent, "Row_advanced");
            row.SetSiblingIndex(template.GetSiblingIndex() + 1);
            defs.Insert(insertAt, NewDef(row, s));
        }

        static SettingsListPanel BuildAdvancedPanel(SettingsListPanel g)
        {
            // Copy under an inactive parent so the copy's Awake/Start wait until it is ready.
            var holder = new GameObject("GigglelandPerfHolder");
            holder.SetActive(false);
            try
            {
                var go = UnityEngine.Object.Instantiate(g.gameObject, holder.transform);
                go.name = PanelName;
                Kill<GraphicsSettingsApplier>(go);
                Kill<BrightnessCalibrationScreen>(go);
                Kill<DeviceSettingsApplier>(go);

                // onShown keeps the copied FadeOutLogo. onHidden would fade the logo in over the
                // Graphics page on the way back, and onBack is ours.
                var adv = go.GetComponent<SettingsListPanel>();
                adv.onHidden = new UnityEvent();
                adv.onBack = new UnityEvent();

                RectTransform tOpt = null, tSlider = null, tToggle = null, tAction = null, backRow = null;
                var old = adv.rowDefs;
                for (int i = 0; i < old.Count; i++)
                {
                    var d = old[i];
                    switch (d.setting?.key)
                    {
                        case "quality": tOpt = d.rowObject; break;
                        case "renderScale": tSlider = d.rowObject; break;
                        case "hdr": tToggle = d.rowObject; break;
                        case "reset": tAction = d.rowObject; break;
                        case "Back": backRow = d.rowObject; break;
                    }
                }
                if (tOpt == null || tSlider == null || tToggle == null || tAction == null || backRow == null)
                {
                    Plugin.Log.LogWarning("Graphics page layout changed; Advanced page not built");
                    UnityEngine.Object.Destroy(go);
                    return null;
                }

                var content = tOpt.parent;
                var defs = new Il2CppSystem.Collections.Generic.List<RowDef>();

                foreach (var o in Settings.All)
                {
                    GS s;
                    RectTransform row;
                    if (o.Choices != null)
                    {
                        s = NewSetting(o.Key, o.Label, RowType.Options);
                        s.options = new Il2CppStringArray(o.Choices);
                        s.optionIndex = o.I;
                        row = CloneRow(tOpt, content, "Row_" + o.Key);
                    }
                    else
                    {
                        s = NewSetting(o.Key, o.Label, RowType.Slider);
                        s.minValue = o.Min;
                        s.maxValue = o.Max;
                        s.floatValue = o.Value.Value;
                        s.numberFormat = "0.00";
                        row = CloneRow(tSlider, content, "Row_" + o.Key);
                    }
                    defs.Add(NewDef(row, s));
                }

                var fps = NewSetting("showFps", "Show FPS Counter", RowType.Toggle);
                fps.boolValue = Settings.ShowFps.Value;
                defs.Add(NewDef(CloneRow(tToggle, content, "Row_showFps"), fps));

                defs.Add(ActionRow(tAction, content, "presetLow", "Apply Low-Spec Preset", () => Preset("Low-Spec")));
                defs.Add(ActionRow(tAction, content, "presetBalanced", "Apply Balanced Preset", () => Preset("Balanced")));
                defs.Add(ActionRow(tAction, content, "presetGame", "Reset Advanced to Game Defaults", () => Preset("Game")));

                // Back (row, Esc, gamepad) all run the panel's own GoBack, which hides the page.
                // Without a settingsPanel it doesn't reopen the category list; Tick() reopens Graphics.
                adv.settingsPanel = null;
                var back = NewSetting("Back", "Back", RowType.Action);
                back.onInvoke = (Il2CppSystem.Action)(Action)(() => adv.GoBack());
                StripHandlers(backRow);
                backRow.SetAsLastSibling();
                defs.Add(NewDef(backRow, back));

                for (int i = 0; i < old.Count; i++)
                    if (old[i].rowObject != null && old[i].rowObject != backRow)
                        UnityEngine.Object.DestroyImmediate(old[i].rowObject.gameObject);
                adv.rowDefs = defs;
                adv.add_SettingChanged((Il2CppSystem.Action<GS>)(Action<GS>)OnChanged);

                go.transform.SetParent(g.transform.parent, false);
                go.transform.SetSiblingIndex(g.transform.GetSiblingIndex() + 1);
                return adv;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("building Advanced page failed: " + e);
                return null;
            }
            finally
            {
                UnityEngine.Object.Destroy(holder);
            }
        }

        static RowDef ActionRow(RectTransform template, Transform content, string key, string label, Action onClick)
        {
            var s = NewSetting(key, label, RowType.Action);
            s.onInvoke = (Il2CppSystem.Action)onClick;
            return NewDef(CloneRow(template, content, "Row_" + key), s);
        }

        static GS NewSetting(string key, string label, RowType type)
        {
            // Native code reads these strings without null checks.
            var s = new GS
            {
                key = key, label = label, type = type,
                minValue = 0f, maxValue = 1f, numberFormat = "0", suffix = "",
                tipMessage = "", confirmTitle = "", confirmMessage = "", confirmButtonText = "", cancelButtonText = "",
                onAction = new UnityEvent(),
                options = new Il2CppStringArray(0),
            };
            return s;
        }

        /// The panel never writes setting.label to the row, so copied rows would keep the template's text.
        static RowDef NewDef(RectTransform row, GS s)
        {
            var label = row.Find("Label")?.GetComponent<TMP_Text>();
            if (label != null) label.text = s.label;
            return new RowDef { rowObject = row, setting = s };
        }

        static RectTransform CloneRow(RectTransform template, Transform parent, string name)
        {
            var go = UnityEngine.Object.Instantiate(template.gameObject, parent);
            go.name = name;
            go.SetActive(true);
            var rt = go.GetComponent<RectTransform>();
            StripHandlers(rt);
            return rt;
        }

        /// Copied rows carry the original row's click handlers; the panel adds its own when it binds.
        static void StripHandlers(RectTransform row)
        {
            foreach (var t in row.GetComponentsInChildren<EventTrigger>(true)) t.triggers.Clear();
            foreach (var b in row.GetComponentsInChildren<Button>(true)) b.onClick = new Button.ButtonClickedEvent();
        }

        static void Kill<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent(Il2CppType.Of<T>());
            if (c != null) UnityEngine.Object.DestroyImmediate(c);
        }

        // ---------- behaviour ----------

        static void Open(SettingsListPanel g)
        {
            if (!advancedOf.TryGetValue(g.GetInstanceID(), out var adv) || adv == null) return;
            Refresh(adv);
            // The Graphics page fades the menu logo back in when its hide animation ends, which
            // would be on top of the Advanced page. Mute that listener until we come back.
            SetListeners(g.onHidden, false);
            g.Hide();
            adv.Show();
        }

        static SettingsListPanel VisibleAdvanced()
        {
            foreach (var adv in advancedOf.Values) if (adv != null && adv.IsVisible) return adv;
            return null;
        }

        static readonly HashSet<int> wasVisible = new();

        /// Every frame: an Advanced page that just closed hands control back to its Graphics page.
        /// Polling instead of hooking GoBack, which UnityEvents call (see Patches.cs).
        public static void Tick()
        {
            foreach (var adv in advancedOf.Values)
            {
                if (adv == null) continue;
                int id = adv.GetInstanceID();
                if (adv.IsVisible) { wasVisible.Add(id); continue; }
                if (wasVisible.Remove(id)) ReturnToGraphics(adv);
            }
        }

        static void ReturnToGraphics(SettingsListPanel adv)
        {
            if (!graphicsOf.TryGetValue(adv.GetInstanceID(), out var g) || g == null) return;
            if (!g.IsVisible) g.Show();
            SetListeners(g.onHidden, true);
        }

        static readonly Dictionary<IntPtr, UnityEventCallState[]> mutedStates = new();

        static void SetListeners(UnityEventBase e, bool on)
        {
            if (e == null) return;
            int n = e.GetPersistentEventCount();
            if (!on)
            {
                if (mutedStates.ContainsKey(e.Pointer)) return;
                var saved = new UnityEventCallState[n];
                for (int i = 0; i < n; i++) { saved[i] = e.GetPersistentListenerState(i); e.SetPersistentListenerState(i, UnityEventCallState.Off); }
                mutedStates[e.Pointer] = saved;
            }
            else if (mutedStates.Remove(e.Pointer, out var saved))
            {
                for (int i = 0; i < n && i < saved.Length; i++) e.SetPersistentListenerState(i, saved[i]);
            }
        }

        static void OnChanged(GS s)
        {
            if (suppress || s == null) return;
            if (s.key == "showFps") { Settings.ShowFps.Value = s.boolValue; return; }
            var o = Settings.Find(s.key);
            if (o == null) return;
            if (o == Settings.RenderApi)
            {
                o.Index.Value = s.optionIndex;
                Settings.RenderApiTries.Value = 0;
                if (o.I != RenderApi.Current) RenderApi.AskRestart(VisibleAdvanced());
                return;
            }
            if (o.Choices != null) o.Index.Value = s.optionIndex;
            else o.Value.Value = s.floatValue;
            Settings.AppliedPreset.Value = "Custom";
            Applier.ApplyAll();
        }

        static void Preset(string name)
        {
            Settings.ApplyPreset(name);
            Applier.ApplyAll();
            foreach (var adv in advancedOf.Values) if (adv != null) Refresh(adv);
            Plugin.Log.LogInfo($"preset applied: {name}");
        }

        static void Refresh(SettingsListPanel adv)
        {
            suppress = true;
            try
            {
                foreach (var o in Settings.All)
                {
                    if (o.Choices != null) adv.SetOptionIndex(o.Key, o.I);
                    else adv.SetFloat(o.Key, o.Value.Value);
                }
                adv.SetBool("showFps", Settings.ShowFps.Value);
            }
            finally { suppress = false; }
        }
    }
}
