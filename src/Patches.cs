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
            if (Settings.DumpOnLoad.Value) Diag.DumpPanel(__instance);
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
