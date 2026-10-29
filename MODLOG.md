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

## 0.20 work (2026-10-29)
### Busy-view stutter (Rory's Rally, DX12, user settings, `tools/busy_lab.sh` + `spin` bench)
- Spin: facing a wall ~800 batches / 0.4M tris / 33 fps; facing the big room 7.5-8.6k batches / 3.8M tris / 23 fps.
- Only two cameras render per frame (MainCamera far=50, ObjectHoldCam), counted with `cams`. 16.9k mesh
  renderers, 14.3k shadow casters, 4k LODGroups, SRP batcher on, graphics jobs on (boot.config native).
- The game rewrites MainCamera every frame (far, mask...): overrides must go in beginCameraRendering.
- MainCamera cullingMask=0 -> 57 fps: drawing the scene is ~26 ms of the busy frame.
- Render-time far 15 m: 2.9k batches, +19%. Far 25 m: nothing. Render scale 0.3/0.25: +20/25%.
- Small-object cull (size < k * distance, forceRenderingOff): k=0.05 -> -26% batches, ~+5%.
- No real effect: shadows off, local shadows off, AO off, LOD bias 0.5, maximumLODLevel, animators off,
  ScalableBufferManager dynamic res. FrameTimingManager is disabled in this build (no CPU/GPU split).
- Thermal noise on the 15 W chip is +-2 fps; compare within one run.
### Exit-to-menu crash
- Dumps in %LOCALAPPDATA%\CrashDumps; symbols from https://symbolserver.unity3d.com (UnityPlayer_Win64_player_il2cpp_x64.pdb,
  .pdb not .pd_), resolved with dbghelp via ctypes. Both 10:36 and 12:10 crashes: access violation on the render
  thread in TexturesD3D12::FrameCleanup / TransferNativeTexture (DX12 texture lifetime), after "Unloading 7020
  unused Assets" on the way back to MainMenu. Not out-of-memory (no allocation failure logged).
- Exit-to-menu stress (`tools/menu_cycle.sh dx12 5`): no crash in 5 cycles. Left as a known Unity DX12 issue.
### Results / decisions
- Draw Distance (render-time far cap) + Small Object Culling (MeshRenderers on layer 0, 0 < size <= 4 m, hidden when
  size < k * distance, beyond 3 m; 1500 per frame). Busy view DX12: Game 22.5, Low-Spec 29, Very Low 31 fps.
- Advanced rows: Options rows become drop-downs (Dropdown.cs; HandleNavInput/NavigateSelection/SubmitSelected/
  AdjustSelected/RepeatAdjust/DoBack/UpdateHoverFromCursor/CycleOption prefixes, none UnityEvent targets).
  Numeric rows are game sliders with wholeNumbers and our value text via SetValueText.
- Vulkan = DXVK. Gotcha 11: DXVK 3.x needs Vulkan 1.4, 2.7 needs VK_KHR_maintenance5; the user's Intel driver
  101.4953 has neither -> ship 2.6.2. Gotcha 12: when DXVK can't create a device Unity exits BEFORE the BepInEx
  chainloader, so no plugin code can recover; the restart helper (BepInEx/cache/giggleframes-restart.ps1) watches
  for BepInEx/cache/giggleframes-started.txt and rolls back to DX11 (verified with DXVK 3.1.1 on purpose).
- API lab (lab/api-0.20.txt, Low-Spec, busy/away): DX12 27.4/34, VK 22/34, DX11 20.3/33. relaxedBarriers: no gain.
- Released as Pre-Release 0.20 (tools/package.sh, zip includes dxvk/).
