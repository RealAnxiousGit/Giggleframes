# MODLOG: Giggleland performance mod

## Facts
- Install: `F:\SteamLibrary\steamapps\common\Giggleland`, Steam app 3370490, buildid 26276541.
- Unity 2022.3.62f2 (7670c08855a9), IL2CPP, URP, D3D11. Company/product: `Hooked` / `Giggleland`.
- Single-player, no anti-cheat. Steamworks.NET only.
- Saves: `%USERPROFILE%\AppData\LocalLow\Hooked\Giggleland\Saves`. Prefs in `HKCU\Software\Hooked\Giggleland`.
- Backups: `backups/Saves-2026-10-29`, `backups/Giggleland-prefs-2026-10-29.reg` (restore: copy folder back, `reg import`).
- User hardware: Intel Core 7 150U (10C/12T, 15 W), Intel integrated graphics (ID 0xa7ac, driver 31.0.101.4953), 15.7 GB RAM, 1920x1080 @ 59 Hz. The game used ~5.3 GB RAM at the main menu.

## Loader
- BepInEx 6.0.0-be.791+b782da4 Unity.IL2CPP win-x64 from builds.bepinex.dev. First launch generated 134 interop assemblies, `Chainloader startup complete`.
- Same Unity version as the SiNiSistar 2 knowledge-base note (be.788), which helped pick the build.

## Decompile
- `ilspycmd 9.1` on `BepInEx/interop/*.dll` into `~/Giggleland-decomp` (outside the repo, never shipped). Interop stubs carry names and field types, not method bodies.
- graphify over the settings classes: only import edges, because interop stubs have no bodies. Runtime dumps are the better source.

## How the game's settings work
- `SettingsPanel` (category list: General, Controls, Graphics, Accessibility, Back) holds `graphicsPanel`, `controlsPanel`, `generalPanel`, `accessibilityPanel`, all `SettingsListPanel`.
- `SettingsListPanel.rowDefs` = list of `RowDef { RectTransform rowObject; GraphicsSetting setting }`, bound in `BuildRowsFromDefs`. Row types: Slider, Toggle, Options, Action, Rebind, Tip. Access by key: `GetSetting/SetFloat/SetBool/SetOptionIndex/SetRowVisible`. Event `SettingChanged(GraphicsSetting)`.
- `GraphicsSettingsApplier` (has `panel`) handles keys K_QUALITY, K_RESOLUTION, K_DISPLAYMODE, K_VSYNC, K_FRAMERATE, K_MSAA, K_RENDERSCALE, K_SHADOWDIST, K_CASCADES, K_TEXQUALITY, K_ANISO, K_HDR, K_DEPTH, K_OPAQUE, K_RESET.
- `GraphicsRuntimeApplier.ApplyAll` re-applies on scene load. `QualityTier.Apply(int)` swaps the URP asset fields (main shadow res, soft shadows, SSAO).
- Render extras: Kronnect Volumetric Fog & Mist 2, Volumetric Lights and Beautify, all compiled into `Unity.RenderPipelines.Universal.Runtime`; Shiny SSRR in `ScreenSpaceReflections.dll`; URP SSAO feature.

## Live render state found (user's PC, Medium preset, before the mod)
- Render scale 0.5 with bilinear "Auto" upscaling, MSAA off, HDR on, shadow distance 15 m, 3 cascades, 2048 main
  shadow map, soft shadows Medium, additional-light shadows ON with a 4096 atlas, 8 per-pixel lights per object.
- Renderer features: Volumetric Lights, SSAO, ShinySSRR, Beautify, glitch/JPG/VHS effects, portals, decals.
- Global Volume: ShinySSRR + Beautify + FilmGrain + glitch volumes. Window is 1600x900 borderless.

## Status (2026-10-29, Pre-release 0.10)
Shipped as Giggleframes 0.10.0 (`BepInEx/plugins/Giggleframes/Giggleframes.dll`, config still `gigglelandperf.cfg`).
Verified in game with the dev command file + screenshots:
- Advanced row sits below Calibrate Brightness; opens the cloned page on the main menu and the pause menu
  (`PlayerUI/PauseSettings/GraphicsPanel`). Same font as the game rows (Baloo2-ExtraBold SDF 30/24).
- Back row, Esc, calibration Cancel/Confirm, presets, Reset popup: `tools/ui_test.sh` passes.
- Render API: DX11 <-> DX12 via the Restart dialog and via the startup check, both with BepInEx loaded.
- Fresh config: first run detects Intel iGPU -> Low-Spec.
- Perf (lab/results.md, Rory's Rally, 1600x900, 0.5 scale): DX11 11-17 fps, DX12 22-30 fps.
- Memory: ~8.2 GB private on DX11, ~8.9 GB on DX12 in gameplay; BepInEx+mod ~80 MB of that. Texture Quality
  High (game setting, mip limit 1) saves ~1 GB. 1115 non-streaming textures = 1.8 GB. FMOD Master.bank 372 MB.

## Gotchas
1. Settings pages deactivate when hidden (`deactivateOnHide`): FindObjectOfType/GameObject.Find miss them; use
   Resources.FindObjectsOfTypeAll + scene check.
2. `SettingsListPanel` never writes `setting.label` to the row; set the row's `Label` TMP text yourself.
3. Harmony hook on `GraphicsSettingsApplier.Revert` -> access violation in coreclr (coreclr.dll+0x1d1fdd) when Revert
   runs from the brightness-calibration back path. Calling Revert directly was fine. Bisected with
   `Debug.SkipPatches` + `tools/repro_calib.sh`; fix: don't hook Revert.
4. Graphics page onShown/onHidden = ButtonEvents.FadeOutLogo/FadeInLogo; FadeInLogo fires after the hide animation.
5. `SettingsPanel.HandleCategory` does nothing from outside; `SettingsPanel.FireClick(2)` opens Graphics;
   `MenuPanel.FireClick(3, true)` opens Settings.
6. Steam `steam://rungameid` launches got swallowed; `steam.exe -applaunch 3370490` works.

7. Never Harmony-patch a method the game calls through a UnityEvent persistent listener (Revert, FadeInLogo,
   GoBack...): coreclr access violation at coreclr.dll+0x1d1fdd. Poll state from Update instead.
8. Doorstop sets DOORSTOP_INITIALIZED in the process environment; a child process that inherits it starts
   WITHOUT BepInEx. Strip DOORSTOP_* before relaunching the game.
9. steam://run/<id>//<args> makes Steam show a security prompt about launch options every time. Relaunch the exe
   directly (it inherits SteamAppId). Vulkan isn't in the build: -force-vulkan -> "InitializeEngineGraphics failed".
10. DX12 (Intel iGPU): flipping UniversalRenderPipelineAsset.m_AdditionalLightShadowsSupported at runtime hangs the
    GPU (device removed 887a0006). Starting with it off is fine. Turn shadows off per light instead.

## Tools
- Build: `.tools/dotnet` from the Berry Bury Berry project: `dotnet build -c Release -p:CopyToGame=true` (game closed).
- `tools/members.py` (interop class summary), `tools/dumpinfo.py` (minidump fault module), `tools/repro_calib.sh`.
- Dev commands (Debug.DevCommands=true): write lines to `BepInEx/gigglelandperf.cmd`: menuclick, settingsclick,
  select, nav, adjust, submit, back, visible, fonts, set, preset, fps, dump, mem, mip, unload, pause, popup, calib,
  bench, shot, applier.
- `tools/launch.sh`, `cmd.sh`, `start_game.sh`, `ui_test.sh`, `perf_lab.sh`, `bisect_dx12.sh`.

## Route
BepInEx 6 IL2CPP plugin + Harmony. Inject an "Advanced" action row into the Graphics page that opens a cloned
`SettingsListPanel` with the mod's own rows; settings stored in a BepInEx config file and re-applied on scene load.
