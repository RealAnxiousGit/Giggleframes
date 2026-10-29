using System;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;

namespace GigglelandPerf
{
    /// Render API choice (DirectX 11 / DirectX 12).
    ///
    /// Unity picks the graphics API from the command line before any mod code runs, so the choice
    /// takes a restart: the game exe is started again with -force-d3d11 / -force-d3d12.
    /// Vulkan is not offered: this build ships no Vulkan shaders and -force-vulkan fails to start.
    static class RenderApi
    {
        public const int AppId = 3370490;
        const string Marker = "-gigglelandperf-relaunch";
        static readonly string[] Args = { "-force-d3d11", "-force-d3d12" };
        static bool checkedStartup;

        public static int Current => SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D12 ? 1 : 0;

        /// First frame: if the game came up on another API than the saved one, relaunch once with
        /// the right argument. Two failed tries in a row fall back to DirectX 11.
        public static void CheckStartup()
        {
            if (checkedStartup) return;
            checkedStartup = true;

            int want = Settings.RenderApi.I;
            string cmd = Environment.CommandLine;
            Plugin.Log.LogInfo($"render API: running {SystemInfo.graphicsDeviceType}, saved {Settings.RenderApi.Choices[want]}");

            if (Current == want) { Settings.RenderApiTries.Value = 0; return; }
            // A -force-* the player put in Steam's launch options wins over the menu.
            if (!cmd.Contains(Marker) && (cmd.Contains("-force-d3d") || cmd.Contains("-force-vulkan") || cmd.Contains("-force-gl")))
            {
                Plugin.Log.LogInfo("render API: launch options force an API, leaving it alone");
                return;
            }
            if (Settings.RenderApiTries.Value >= 2)
            {
                Plugin.Log.LogWarning($"render API: {Settings.RenderApi.Choices[want]} failed to start twice, using DirectX 11");
                Settings.RenderApi.Index.Value = 0;
                Settings.RenderApiTries.Value = 0;
                return;
            }
            Settings.RenderApiTries.Value++;
            Restart();
        }

        public static void AskRestart(SettingsListPanel panel)
        {
            var popup = panel != null ? panel.confirmPopup : null;
            if (popup == null)
            {
                foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppInterop.Runtime.Il2CppType.Of<ConfirmationPopup>()))
                    if (o.TryCast<ConfirmationPopup>() is { } p && p.gameObject.scene.IsValid()) popup = p;
            }
            string msg = $"{Settings.RenderApi.Choices[Settings.RenderApi.I]} is saved and will be used the next time the game starts.";
            if (popup == null)
            {
                Plugin.Log.LogWarning("no confirmation popup found; " + msg);
                return;
            }
            popup.Confirm("Restart Required", msg + " Restart now?",
                (Il2CppSystem.Action)(Action)Restart, "Restart", "Later", null);
        }

        /// Quit and start the game's exe again with the saved API. Not through a steam:// link: Steam
        /// shows a security prompt for every link that carries launch options. The new process
        /// inherits SteamAppId from this one, so Steam treats it as its own launch. A hidden
        /// PowerShell waits for this process to exit first (the game is single-instance).
        public static void Restart()
        {
            string arg = Args[Settings.RenderApi.I];
            var self = Process.GetCurrentProcess();
            string exe = self.MainModule?.FileName ?? System.IO.Path.Combine(BepInEx.Paths.GameRootPath, "Giggleland.exe");
            if (Environment.GetEnvironmentVariable("SteamAppId") == null)
                Environment.SetEnvironmentVariable("SteamAppId", AppId.ToString());
            string ps = $"Wait-Process -Id {self.Id} -Timeout 60 -ErrorAction SilentlyContinue; Start-Sleep -Seconds 1; " +
                        $"Start-Process -FilePath '{exe.Replace("'", "''")}' -ArgumentList '{arg} {Marker}' -WorkingDirectory '{BepInEx.Paths.GameRootPath.Replace("'", "''")}'";
            Plugin.Log.LogInfo($"restarting with {arg}");
            try
            {
                var psi = new ProcessStartInfo("powershell.exe", $"-NoProfile -WindowStyle Hidden -Command \"{ps}\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                // Doorstop marks this process with DOORSTOP_INITIALIZED and friends; a child that
                // inherits them skips loading BepInEx, so the relaunched game would run unmodded.
                foreach (var key in new System.Collections.Generic.List<string>(psi.Environment.Keys))
                    if (key.StartsWith("DOORSTOP_", StringComparison.OrdinalIgnoreCase)) psi.Environment.Remove(key);
                Process.Start(psi);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("could not schedule the restart: " + e.Message);
                return;
            }
            Application.Quit();
        }
    }
}
