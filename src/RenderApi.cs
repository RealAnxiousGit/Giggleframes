using System;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace GigglelandPerf
{
    /// Render API choice (DirectX 11 / DirectX 12 / Vulkan through DXVK).
    ///
    /// Unity picks the graphics API from the command line before any mod code runs, so the choice
    /// takes a restart: the game exe is started again with -force-d3d11 / -force-d3d12.
    /// Unity's own Vulkan renderer is not in this build (-force-vulkan fails to start), so "Vulkan"
    /// is DXVK: its d3d11.dll and dxgi.dll go next to the exe and translate DirectX 11 to Vulkan.
    /// The DLLs ship in the plugin's dxvk folder; the restart helper copies them in or takes them out
    /// while the game is closed, because Windows can't replace a DLL the running game has loaded.
    static class RenderApi
    {
        public const int AppId = 3370490;
        const string Marker = "-gigglelandperf-relaunch";
        static readonly string[] Args = { "-force-d3d11", "-force-d3d12", "-force-d3d11" };
        static readonly string[] DxvkFiles = { "d3d11.dll", "dxgi.dll", "dxvk.conf" };
        static bool checkedStartup;

        static string GameRoot => BepInEx.Paths.GameRootPath;
        static string DxvkSource => System.IO.Path.Combine(System.IO.Path.GetDirectoryName(typeof(RenderApi).Assembly.Location) ?? "", "dxvk");

        public static bool DxvkAvailable => DxvkFiles.All(f => System.IO.File.Exists(System.IO.Path.Combine(DxvkSource, f)));

        /// DXVK is running when the d3d11.dll this process loaded is the one next to the exe.
        public static bool DxvkLoaded
        {
            get
            {
                try
                {
                    foreach (ProcessModule m in Process.GetCurrentProcess().Modules)
                        if (string.Equals(m.ModuleName, "d3d11.dll", StringComparison.OrdinalIgnoreCase))
                            return string.Equals(System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetDirectoryName(m.FileName) ?? ""),
                                                 System.IO.Path.TrimEndingDirectorySeparator(GameRoot), StringComparison.OrdinalIgnoreCase);
                }
                catch { }
                return false;
            }
        }

        public static int Current => SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D12 ? 1 : DxvkLoaded ? 2 : 0;

        /// Our DXVK copy sits next to the exe (byte-for-byte; someone else's d3d11/dxgi, such as ReShade's, doesn't count).
        static bool OurFile(string name)
        {
            try
            {
                var a = System.IO.Path.Combine(GameRoot, name);
                var b = System.IO.Path.Combine(DxvkSource, name);
                if (!System.IO.File.Exists(a) || !System.IO.File.Exists(b)) return false;
                return System.IO.File.ReadAllBytes(a).AsSpan().SequenceEqual(System.IO.File.ReadAllBytes(b));
            }
            catch { return false; }
        }

        /// First frame: if the game came up on another API than the saved one, relaunch once with
        /// the right argument. Two failed tries in a row fall back to DirectX 11.
        public static void CheckStartup()
        {
            if (checkedStartup) return;
            checkedStartup = true;

            int want = Settings.RenderApi.I;
            string cmd = Environment.CommandLine;
            Plugin.Log.LogInfo($"render API: running {SystemInfo.graphicsDeviceType}{(DxvkLoaded ? " via DXVK" : "")}, saved {Settings.RenderApi.Choices[want]}");
            // The restart helper's watchdog waits for this file (see RestartScript).
            try { System.IO.File.WriteAllText(StartedFile, SystemInfo.graphicsDeviceType.ToString()); } catch { }

            if (Current == want)
            {
                Settings.RenderApiTries.Value = 0;
                return;
            }
            if (want == 2 && !DxvkAvailable)
            {
                Plugin.Log.LogWarning($"render API: DXVK files missing from {DxvkSource}, using DirectX 11");
                Settings.RenderApi.Index.Value = 0;
                if (Current == 0) return;
            }
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

        static string StartedFile => System.IO.Path.Combine(BepInEx.Paths.CachePath, "giggleframes-started.txt");

        /// Quit and start the game's exe again with the saved API. Not through a steam:// link: Steam
        /// shows a security prompt for every link that carries launch options. The new process
        /// inherits SteamAppId from this one, so Steam treats it as its own launch. A hidden
        /// PowerShell script waits for this process to exit first (the game is single-instance).
        public static void Restart()
        {
            if (Settings.RenderApi.I == 2 && !DxvkAvailable)
            {
                Plugin.Log.LogError("Vulkan needs the dxvk folder from the Giggleframes zip; staying on " + Settings.RenderApi.Choices[Current]);
                Settings.RenderApi.Index.Value = Current;
                return;
            }
            string arg = Args[Settings.RenderApi.I];
            var self = Process.GetCurrentProcess();
            string exe = self.MainModule?.FileName ?? System.IO.Path.Combine(GameRoot, "Giggleland.exe");
            if (Environment.GetEnvironmentVariable("SteamAppId") == null)
                Environment.SetEnvironmentVariable("SteamAppId", AppId.ToString());
            Plugin.Log.LogInfo($"restarting with {arg}{(Settings.RenderApi.I == 2 ? " and DXVK" : "")}");
            try
            {
                string script = System.IO.Path.Combine(BepInEx.Paths.CachePath, "giggleframes-restart.ps1");
                System.IO.Directory.CreateDirectory(BepInEx.Paths.CachePath);
                System.IO.File.WriteAllText(script, RestartScript(self.Id, exe, arg));
                var psi = new ProcessStartInfo("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{script}\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                // Doorstop marks this process with DOORSTOP_INITIALIZED and friends; a child that
                // inherits them skips loading BepInEx, so the relaunched game would run unmodded.
                foreach (var key in new System.Collections.Generic.List<string>(psi.Environment.Keys))
                    if (key.StartsWith("DOORSTOP_", StringComparison.OrdinalIgnoreCase)) psi.Environment.Remove(key);
                psi.Environment["DXVK_LOG_LEVEL"] = "warn";
                Process.Start(psi);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("could not schedule the restart: " + e.Message);
                return;
            }
            Application.Quit();
        }

        /// The restart helper. Once this process has exited it copies DXVK in (Vulkan) or takes our copy
        /// out (the other APIs; only files identical to the shipped ones, so a ReShade dxgi.dll stays),
        /// then starts the game again.
        ///
        /// When DXVK can't run (the driver lacks a Vulkan feature it needs), Unity fails to create its
        /// device and exits before any plugin loads, so the mod itself can't recover. The script
        /// watches instead: if the game exits before the mod has written StartedFile, it takes DXVK
        /// out, saves DirectX 11 and starts the game once more.
        static string RestartScript(int pid, string exe, string arg)
        {
            var copy = new System.Text.StringBuilder();
            var remove = new System.Text.StringBuilder();
            foreach (var f in DxvkFiles)
            {
                string dst = Q(System.IO.Path.Combine(GameRoot, f)), src = Q(System.IO.Path.Combine(DxvkSource, f));
                copy.AppendLine($"Copy-Item -LiteralPath {src} -Destination {dst} -Force");
                remove.AppendLine($"if ((Test-Path -LiteralPath {dst}) -and (Test-Path -LiteralPath {src}) -and " +
                                  $"((Get-FileHash -LiteralPath {dst}).Hash -eq (Get-FileHash -LiteralPath {src}).Hash)) " +
                                  $"{{ Remove-Item -LiteralPath {dst} -Force }}");
            }
            string cfg = Q(System.IO.Path.Combine(BepInEx.Paths.ConfigPath, Plugin.Guid + ".cfg"));
            bool vulkan = Settings.RenderApi.I == 2;
            string watchdog = !vulkan ? "" : $@"for ($i = 0; $i -lt 300; $i++) {{
  Start-Sleep -Seconds 1
  if (Test-Path -LiteralPath {Q(StartedFile)}) {{ exit }}
  if ($p.HasExited) {{
{remove}
(Get-Content -LiteralPath {cfg}) -replace '^renderApi = .*', 'renderApi = 0' | Set-Content -LiteralPath {cfg}
Start-Process -FilePath {Q(exe)} -ArgumentList '-force-d3d11 {Marker}' -WorkingDirectory {Q(GameRoot)}
exit
  }}
}}";
            return $@"$ErrorActionPreference = 'SilentlyContinue'
Wait-Process -Id {pid} -Timeout 60
Start-Sleep -Seconds 1
Remove-Item -LiteralPath {Q(StartedFile)} -Force
{(vulkan ? copy : remove)}
$p = Start-Process -FilePath {Q(exe)} -ArgumentList '{arg} {Marker}' -WorkingDirectory {Q(GameRoot)} -PassThru
{watchdog}
";
        }

        static string Q(string path) => "'" + path.Replace("'", "''") + "'";
    }
}
