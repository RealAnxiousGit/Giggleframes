using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace GigglelandPerf
{
    // Rule for this game: never patch a method that a UnityEvent can invoke (button/row callbacks,
    // onShown/onHidden, calibration confirm/cancel). In this IL2CPP build a Harmony-patched method
    // called through a UnityEvent persistent listener crashes coreclr (MODLOG gotcha 3).

    [HarmonyPatch(typeof(SettingsListPanel), nameof(SettingsListPanel.Awake))]
    static class PanelAwakePatch
    {
        static void Prefix(SettingsListPanel __instance) => AdvancedPage.OnPanelAwake(__instance);
    }

    [HarmonyPatch(typeof(SettingsListPanel), nameof(SettingsListPanel.BuildRowsFromDefs))]
    static class DumpRowsPatch
    {
        static void Postfix(SettingsListPanel __instance)
        {
            if (AdvancedPage.IsAdvanced(__instance))
            {
                Dropdown.StyleRows(__instance);
                AdvancedPage.RefreshTexts(__instance);
            }
            if (Settings.DumpOnLoad.Value) Diag.DumpPanel(__instance);
        }
    }

    /// Drop-down lists on the Advanced page (Dropdown.cs). The page's own input methods are redirected
    /// while a list is open; all of these are called from the panel's Update or from runtime click
    /// listeners, never from a persistent UnityEvent.
    [HarmonyPatch(typeof(SettingsListPanel))]
    static class DropdownPatches
    {
        static int inInput;   // >0 while keyboard/controller input is being handled

        [HarmonyPrefix, HarmonyPatch(nameof(SettingsListPanel.HandleNavInput))]
        static void NavIn() => inInput++;

        [HarmonyPostfix, HarmonyPatch(nameof(SettingsListPanel.HandleNavInput))]
        static void NavOut() => inInput = System.Math.Max(0, inInput - 1);

        [HarmonyPrefix, HarmonyPatch(nameof(SettingsListPanel.NavigateSelection))]
        static bool Navigate(SettingsListPanel __instance, int dir)
        {
            if (!Dropdown.IsOpen(__instance)) return true;
            Dropdown.Move(dir);
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(nameof(SettingsListPanel.SubmitSelected))]
        static bool Submit(SettingsListPanel __instance)
        {
            if (Dropdown.IsOpen(__instance)) { Dropdown.Choose(); return false; }
            if (!AdvancedPage.IsAdvanced(__instance) || !Dropdown.IsOptionsRow(__instance, __instance.selectedIndex)) return true;
            Dropdown.Open(__instance, __instance.selectedIndex);
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(nameof(SettingsListPanel.AdjustSelected))]
        static bool Adjust(SettingsListPanel __instance, out bool __state) => AdjustIn(__instance, out __state);

        [HarmonyPostfix, HarmonyPatch(nameof(SettingsListPanel.AdjustSelected))]
        static void AdjustDone(bool __state) => AdjustOut(__state);

        [HarmonyPrefix, HarmonyPatch(nameof(SettingsListPanel.RepeatAdjust))]
        static bool Repeat(SettingsListPanel __instance, out bool __state) => AdjustIn(__instance, out __state);

        [HarmonyPostfix, HarmonyPatch(nameof(SettingsListPanel.RepeatAdjust))]
        static void RepeatDone(bool __state) => AdjustOut(__state);

        static bool AdjustIn(SettingsListPanel p, out bool counted)
        {
            counted = !Dropdown.IsOpen(p);
            if (counted) inInput++;
            return counted;
        }

        static void AdjustOut(bool counted)
        {
            if (counted) inInput = System.Math.Max(0, inInput - 1);
        }

        [HarmonyPrefix, HarmonyPatch(nameof(SettingsListPanel.DoBack))]
        static bool Back(SettingsListPanel __instance)
        {
            if (!Dropdown.IsOpen(__instance)) return true;
            Dropdown.Close();
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(nameof(SettingsListPanel.UpdateHoverFromCursor))]
        static bool Hover(SettingsListPanel __instance) => !Dropdown.IsOpen(__instance);

        /// A click on an Advanced row (or its caret) cycles it in the game; open the list instead.
        /// Left/Right from the keyboard or controller still step through the choices.
        [HarmonyPrefix, HarmonyPatch(nameof(SettingsListPanel.CycleOption))]
        static bool Cycle(SettingsListPanel __instance, SettingsListPanel.Row row)
        {
            if (Dropdown.IsOpen(__instance)) return false;
            if (inInput > 0 || !AdvancedPage.IsAdvanced(__instance)) return true;
            int i = Dropdown.IndexOf(__instance, row);
            if (i < 0) return true;
            __instance.SetSelected(i);
            Dropdown.Open(__instance, i);
            return false;
        }
    }

    /// The game's own graphics writers, all called from code. Ours are lifted off before they
    /// run and re-applied after.
    [HarmonyPatch]
    static class GameApplyPatch
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            var names = new[]
            {
                (typeof(QualityTier), nameof(QualityTier.Apply)),
                (typeof(GraphicsRuntimeApplier), nameof(GraphicsRuntimeApplier.ApplyAll)),
                (typeof(GraphicsSettingsApplier), nameof(GraphicsSettingsApplier.ApplyOne)),
                (typeof(GraphicsSettingsApplier), nameof(GraphicsSettingsApplier.ApplyAllFromPanel)),
                (typeof(GraphicsSettingsApplier), nameof(GraphicsSettingsApplier.ApplyQualityPreset)),
                (typeof(GraphicsSettingsApplier), nameof(GraphicsSettingsApplier.LoadFromPrefsAndApply)),
            };
            foreach (var (t, n) in names)
            {
                var m = AccessTools.Method(t, n);
                if (Settings.Skipped(t.Name + "." + n)) continue;
                if (m != null) yield return m;
                else Plugin.Log.LogWarning($"missing {t.Name}.{n}");
            }
        }

        static void Prefix() => Applier.BeforeGameApply();
        static void Postfix() => Applier.AfterGameApply();
    }
}
