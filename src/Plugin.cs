using System;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GigglelandPerf
{
    [BepInPlugin(Guid, "Giggleframes", Version)]
    public class Plugin : BasePlugin
    {
        public const string Guid = "gigglelandperf";
        public const string Version = "0.20.1"; // Pre-Release 0.20.1

        internal static ManualLogSource Log;
        internal static Plugin Instance;

        public override void Load()
        {
            Instance = this;
            Log = base.Log;
            Settings.Bind(Config);

            var harmony = new Harmony(Guid);
            // One class at a time, so a bad target only loses its own patch.
            foreach (var t in typeof(Plugin).Assembly.GetTypes())
            {
                if (t.GetCustomAttributes(typeof(HarmonyPatch), false).Length == 0 || Settings.Skipped(t.Name)) continue;
                try { harmony.CreateClassProcessor(t).Patch(); }
                catch (Exception e) { Log.LogError($"patch {t.Name} failed: {e.Message}"); }
            }

            RenderApi.Init();
            Culling.Init();
            AddComponent<Runner>();
            SceneManager.sceneLoaded += (Action<Scene, LoadSceneMode>)OnSceneLoaded;
            Log.LogInfo($"Giggleframes {Version} loaded");
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Log.LogInfo($"scene loaded: {scene.name} ({mode})");
            Applier.RescanVolumes();
            Culling.Rescan(5f);
            Runner.ApplyInFrames(3);
            if (Settings.DumpOnLoad.Value) Runner.DumpInFrames(30);
        }
    }

    /// Drives delayed work from the main thread.
    public class Runner : MonoBehaviour
    {
        public Runner(IntPtr ptr) : base(ptr) { }

        static int applyIn = -1, dumpIn = -1;
        float nextKeep;

        public static void ApplyInFrames(int n) => applyIn = n;
        public static void DumpInFrames(int n) => dumpIn = n;

        void Update()
        {
            Safe(RenderApi.CheckStartup, "render api");
            if (applyIn >= 0 && applyIn-- == 0) Safe(Applier.ApplyAll, "apply");
            if (dumpIn >= 0 && dumpIn-- == 0) Safe(Diag.DumpRenderState, "dump");
            Safe(AdvancedPage.Tick, "page");
            Safe(Dev.Tick, "dev");
            Bench.Tick();

            // Scenes spawn fog volumes, lights and post volumes late, and scripts flip features;
            // re-applying only writes values that drifted, so this stays cheap.
            if (Time.unscaledTime >= nextKeep)
            {
                nextKeep = Time.unscaledTime + 2f;
                Safe(Applier.ApplyAll, "keep");
            }

            Safe(Culling.Tick, "culling");
            Safe(FpsCounter.Tick, "fps");
        }

        // Only GUI.Label: GUILayout helpers are stripped from IL2CPP builds like this one.
        void OnGUI() => FpsCounter.OnGUI();

        static void Safe(Action a, string what)
        {
            try { a(); }
            catch (Exception e) { Plugin.Log.LogError($"{what}: {e}"); }
        }
    }
}
