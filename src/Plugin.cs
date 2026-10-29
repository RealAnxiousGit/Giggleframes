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
        public const string Version = "0.10.0"; // Pre-release 0.10

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

            AddComponent<Runner>();
            SceneManager.sceneLoaded += (Action<Scene, LoadSceneMode>)OnSceneLoaded;
            Log.LogInfo($"Giggleframes {Version} loaded");
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Log.LogInfo($"scene loaded: {scene.name} ({mode})");
            Applier.RescanVolumes();
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

            if (Settings.ShowFps.Value)
            {
                fpsFrames++;
                fpsTime += Time.unscaledDeltaTime;
                if (fpsTime >= 0.5f)
                {
                    fpsText = $"{fpsFrames / fpsTime:0} FPS  {1000f * fpsTime / fpsFrames:0.0} ms";
                    fpsFrames = 0;
                    fpsTime = 0f;
                }
            }
        }

        int fpsFrames;
        float fpsTime;
        string fpsText = "";
        GUIStyle fpsStyle;

        // Only GUI.Label: GUILayout helpers are stripped from IL2CPP builds like this one.
        void OnGUI()
        {
            if (!Settings.ShowFps.Value || fpsText.Length == 0) return;
            fpsStyle ??= new GUIStyle { fontSize = 18, normal = { textColor = Color.white } };
            var shadow = new GUIStyle(fpsStyle) { normal = { textColor = Color.black } };
            GUI.Label(new Rect(11, 9, 300, 30), fpsText, shadow);
            GUI.Label(new Rect(10, 8, 300, 30), fpsText, fpsStyle);
        }

        static void Safe(Action a, string what)
        {
            try { a(); }
            catch (Exception e) { Plugin.Log.LogError($"{what}: {e}"); }
        }
    }
}
