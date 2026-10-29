# Giggleframes

A performance mod for **Giggleland** (Steam). It adds an **Advanced** page to the game's Graphics settings with the heavy rendering options the game doesn't expose: volumetric fog, volumetric lighting, shadow quality, ambient occlusion, reflections, post-processing and more. It also adds a DirectX 11 / DirectX 12 / Vulkan (DXVK) switch, options that keep busy rooms playable, and presets tuned for integrated graphics.

**Version:** Pre-Release 0.20 (plugin version 0.20.0). See the [changelog](CHANGELOG.md).
**Tested on:** Giggleland v1.0.2 (Steam build 26276541, Unity 2022.3.62f2, IL2CPP), BepInEx 6.0.0-be.791, Windows 11.

---

## What it does

Giggleland runs on Unity's Universal Render Pipeline and ships several expensive effects: Kronnect Volumetric Fog & Mist 2, Volumetric Lights, Beautify post-processing, Shiny screen-space reflections, URP's SSAO and a 4096×4096 shadow atlas for local lights. The game's own Graphics page covers resolution, render scale, shadow distance and a quality preset, but none of those effects can be turned down on their own. On a laptop with integrated graphics that means 10–15 fps in busy areas.

Giggleframes adds one row, **Advanced**, under *Calibrate Brightness* on the Graphics page. It opens a page built from the game's own rows, so it uses the same font, sounds, keyboard and controller navigation as the rest of the menu. Options are drop-down lists, and numeric ones are sliders like the game's Render Scale. It works from the main menu and from the pause menu in game.

The slowest thing in Giggleland is looking across a large room full of props: on the test laptop the camera goes from about 800 draw calls facing a wall to 8,600 facing the room. *Draw Distance* and *Small Object Culling* cut that down, and the presets use them.

Every option starts at **Preset**, which leaves the game's value alone. A fresh install looks exactly like the unmodded game until you change something, with one exception: on its first launch the mod checks your hardware, and if it finds integrated graphics or a low-end GPU it applies the **Low-Spec** preset once. Changes apply right away and are saved to a config file. The mod reapplies them on every scene load and whenever the game resets its own settings.

## Features

- **Advanced page** in Graphics settings (main menu and pause menu), styled like the game's own pages
- **Graphics Preset** drop-down at the top: *Game Default*, *Very Low*, *Low-Spec*, *Balanced*; shows *Custom* once you change a single option
- **Drop-down lists** for every choice (click or Submit to open, Up/Down/Submit to pick, Back to close; Left/Right still step) and **sliders** for numeric values, snapping to whole steps inside safe limits
- **Render API**: DirectX 11, DirectX 12 or **Vulkan (DXVK)**, with a *Restart / Later* dialog. Restart reopens the game on the new API by itself
- **Draw Distance** (slider, 10-55 m or *Game*): how far the main camera draws
- **Small Object Culling**: Off / Low / Medium / High; skips props too small to matter at their distance
- **Upscaling filter**: Bilinear, FSR 1.0 or Point, plus an **FSR Sharpness** slider. The game's Medium preset already renders at 0.5x scale, so FSR mostly makes it look sharper
- **Volumetric Fog**: Off / Low / Medium / High (raymarch quality, downscaling, detail noise, high-quality scattering)
- **Volumetric Lighting**: Off / Low / Medium / High (raymarch quality, per-light shadow maps, downscaling)
- **Shadow Quality**: Off / Low / Medium / High / Ultra (main shadow resolution and soft shadows)
- **Shadow Cascades** (slider): 1-4
- **Local Light Shadows**: Off / Low / Medium / High (shadow atlas size for point and spot lights)
- **Lighting Quality**: Low / Medium / High / Ultra (per-object light limit, reflection probe blending)
- **Ambient Occlusion**: Off / Low / Medium / High (URP SSAO sample count, downsampling, blur)
- **Screen Space Reflections**: Off / Low / Medium / High
- **Post-Processing Quality**: Low / Medium / High (Beautify turbo mode, bloom and depth-of-field downsampling)
- **Bloom**, **Lens Flares**, **Depth of Field**: On / Off
- **Level of Detail**: LOD bias from Lowest to High
- **Vegetation Detail**: terrain grass and detail density and distance
- **Texture Memory** (slider): texture streaming budget (512 MB to 4 GB)
- **Show FPS Counter**: bold, outlined, top-right; green at 60+ fps, yellow 30-59, red under 30
- **First-run hardware check** that picks Low-Spec for integrated or weak GPUs

## Performance

### Busy rooms (0.20)

Facing the big room in Rory's Rally on DirectX 12 (6-second runs, same machine as below):

| Preset | Avg fps | Draw calls |
|---|---|---|
| Game Default | 22.5 | 8,700 |
| Low-Spec | 29 | 3,400 |
| Very Low | 31 | 2,200 |

### Render APIs (0.20)

Low-Spec preset, Rory's Rally, three 6-second runs each (`lab/api-0.20.txt`):

| API | Facing the busy room | Facing away |
|---|---|---|
| DirectX 12 | 27.4 fps | 34 fps |
| Vulkan (DXVK 2.6.2) | 22 fps | 34 fps |
| DirectX 11 | 20.3 fps | 33 fps |

On this Intel iGPU, DirectX 12 is the best choice. Vulkan is a step up from DirectX 11 in busy views and is there for GPUs and drivers where DirectX 12 misbehaves.

### Overall (0.10)

Measured with the mod's built-in benchmark (10-second runs after a warm-up) in the Rory's Rally area. Hardware: Intel Core 7 150U with its integrated Intel graphics, 16 GB RAM, game at 1600×900 with the game's Medium preset (0.5× render scale).

| Preset | DirectX 11 | DirectX 12 |
|---|---|---|
| Game defaults | 11–14 fps | 22–26 fps |
| Balanced | 14–17 fps | 28–30 fps |
| Low-Spec | 14–17 fps | 27–30 fps |

On this machine DirectX 12 is the biggest single win, at roughly twice the frame rate of DirectX 11. Your results will depend on your GPU and driver. DirectX 11 stays the default because it's what the game ships with.

## Memory use

Giggleland itself uses a lot of RAM: about 8–9 GB during gameplay. BepInEx and this mod add about 80 MB. Where it goes, measured on the machine above:

- **Textures (~2.2 GB resident):** the game loads around 2,700 textures, many of them 4096×4096. About 1,100 of them (1.8 GB) are not set up for texture streaming in the game build, so no streaming budget can shrink them.
- **Integrated graphics have no video memory of their own.** Every texture and render target lives in system RAM and counts against the game in Task Manager. DirectX 12 uses about 0.7 GB more than DirectX 11.
- **Audio:** the FMOD sound bank is 372 MB.

What helps:

1. **Texture Quality → High** in the game's own Graphics page (half resolution) saves about 1 GB. At the default 0.5× render scale it is hard to tell apart from Full. *Medium* saves a little more.
2. **Local Light Shadows → Off** (part of Low-Spec) shrinks the 4096×4096 local shadow atlas, saving about 130 MB.
3. **Texture Memory → 512 MB or 1 GB** limits the textures that can stream (the 1.8 GB that can't stream is unaffected).
4. Choose **DirectX 11** if RAM matters more than frame rate.

## Installing

### 1. Install BepInEx 6 (IL2CPP)

Giggleland is an IL2CPP Unity game, which needs the bleeding-edge BepInEx 6 builds, not BepInEx 5.

1. Download **BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.791+b782da4.zip** from the [BepInEx bleeding-edge builds](https://builds.bepinex.dev/projects/bepinex_be) (build #791, the Unity.IL2CPP win-x64 artifact). Direct link: <https://builds.bepinex.dev/projects/bepinex_be/791/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.791%2Bb782da4.zip>
2. Open the game folder: in Steam, right-click Giggleland → *Manage* → *Browse local files*.
3. Extract the zip into that folder, so that `winhttp.dll`, `doorstop_config.ini` and the `BepInEx` folder sit next to `Giggleland.exe`.
4. Start the game once and wait for the main menu. **The first start takes a few minutes** while BepInEx generates its interop assemblies. Then quit.

### 2. Install Giggleframes

1. Download `Giggleframes-Pre-Release-0.20.zip` from [Releases](https://github.com/RealAnxiousGit/Giggleframes/releases).
2. Extract it into the game folder. The plugin should end up at
   `Giggleland\BepInEx\plugins\Giggleframes\Giggleframes.dll`, with DXVK in `...\Giggleframes\dxvk`.
   If you're updating from 0.10, overwrite the old files; your settings carry over.
3. Start the game and open **Settings → Graphics → Advanced**.

`BepInEx\LogOutput.log` should contain `Giggleframes 0.20.0 loaded`.

### Uninstalling

Switch the Render API back to DirectX 11 or 12 first if you use Vulkan (or delete `d3d11.dll`, `dxgi.dll` and `dxvk.conf` next to `Giggleland.exe`). Then delete `BepInEx\plugins\Giggleframes`. To remove BepInEx as well, delete `winhttp.dll`, `doorstop_config.ini`, `.doorstop_version`, `changelog.txt`, the `dotnet` folder and the `BepInEx` folder. Your saves are not touched.

## Render API notes

- Unity picks the graphics API before any mod code runs, so switching needs a restart. *Restart* closes the game and starts `Giggleland.exe` again with `-force-d3d11` or `-force-d3d12`. *Later* keeps the choice for the next launch.
- When you start the game from Steam and the saved API isn't the one running, the mod restarts the game once onto the saved API. If the saved API fails to start twice in a row, the mod falls back to DirectX 11.
- A `-force-d3d11`, `-force-d3d12`, `-force-vulkan` or `-force-gl` that you put in Steam's launch options always wins over the menu.

### Vulkan (DXVK)

- The game build ships no Unity Vulkan renderer (`-force-vulkan` fails with "InitializeEngineGraphics failed"), so *Vulkan* uses [DXVK](https://github.com/doitsujin/dxvk) **2.6.2**: the game runs its DirectX 11 renderer and DXVK translates it to Vulkan.
- Choosing it copies `d3d11.dll`, `dxgi.dll` and a tuned `dxvk.conf` from `BepInEx\plugins\Giggleframes\dxvk` next to `Giggleland.exe` while the game is closed. Switching back removes them, but only if they're the files the mod put there.
- Needs a Vulkan 1.3 driver. 2.6.2 is the newest DXVK that runs without `VK_KHR_maintenance5` (older Intel iGPU drivers); DXVK 3.x needs Vulkan 1.4. If the game can't start on Vulkan, the restart helper removes DXVK and starts the game on DirectX 11.
- The first minutes on Vulkan can hitch more while DXVK builds its shader pipelines; it gets smoother as you play.
- DXVK writes `Giggleland_d3d11.log` and `Giggleland_dxgi.log` next to the game (warnings only).

## Configuration

Settings are stored in `BepInEx\config\gigglelandperf.cfg` and can be edited with the game closed.

| Section | Key | Meaning |
|---|---|---|
| Advanced | `renderApi`, `upscaling`, `volumetricFog`, … | Option index; `0` = Preset (game value) for most rows |
| Advanced | `graphicsPreset` | 0 Custom, 1 Game Default, 2 Very Low, 3 Low-Spec, 4 Balanced |
| Advanced | `drawDistanceSteps` | Draw distance in 5 m steps (2 = 10 m ... 11 = 55 m, 12 = game) |
| Advanced | `fpsCounter` | FPS counter (0 off, 1 on) |
| Debug | `DevCommands` | Enables the test command file (see below) |
| Debug | `DumpOnLoad` | Logs the render state after each scene load |
| Debug | `SkipPatches` | Comma-separated patch classes to leave out, for troubleshooting |

## Building from source

Requirements:

- [.NET SDK](https://dotnet.microsoft.com/download) 6.0 or newer (the project targets `net6.0`, which is what BepInEx 6 IL2CPP runs)
- Giggleland with BepInEx installed and **started once**, because the build references the interop assemblies BepInEx generates in `BepInEx\interop`

```bash
git clone https://github.com/RealAnxiousGit/Giggleframes.git
cd Giggleframes
dotnet build -c Release -p:GameDir="C:\Program Files (x86)\Steam\steamapps\common\Giggleland"
```

- `GameDir` defaults to `F:\SteamLibrary\steamapps\common\Giggleland`; pass your own path.
- The DLL is written to `artifacts\build\Giggleframes.dll`.
- Add `-p:CopyToGame=true` to copy it into `BepInEx\plugins\Giggleframes` (close the game first).
- For the Vulkan option, run `tools/get_dxvk.sh` once first: it downloads DXVK 2.6.2 from GitHub, checks its SHA-256 and puts the 64-bit `d3d11.dll`, `dxgi.dll` and license in `dxvk/` (git-ignored). The build copies them, with `res/dxvk.conf`, into the plugin's `dxvk` folder.

### Project layout

| File | Purpose |
|---|---|
| `src/Plugin.cs` | BepInEx entry point, Harmony patching, per-frame runner, FPS counter |
| `src/Settings.cs` | Option definitions, config binding, presets, first-run hardware check |
| `src/Culling.cs` | Draw Distance (far-plane cap at render time) and Small Object Culling (time-sliced) |
| `src/Dropdown.cs` | Drop-down lists for the Advanced page's rows, slider snapping |
| `src/FpsCounter.cs` | Coloured FPS counter |
| `src/Applier.cs` | Applies options to the URP asset, renderer features, fog, volumetric lights, post volumes, terrain and lights; restores the game's values when an option goes back to Preset |
| `src/AdvancedPage.cs` | Builds the Advanced page by cloning the game's Graphics page and adds the Advanced row |
| `src/Patches.cs` | Hooks on the game's settings code so the mod reapplies after the game does |
| `src/RenderApi.cs` | Render API restart, startup check, DXVK install/removal and the Vulkan watchdog |
| `res/dxvk.conf` | DXVK settings installed with the Vulkan option |
| `src/Bench.cs`, `src/Dev.cs`, `src/Diag.cs` | Benchmark, test commands, render and memory dumps |
| `tools/` | Test scripts (below) |

### Testing tools

With `DevCommands = true`, the mod reads one command per line from `BepInEx\gigglelandperf.cmd` once a second. Examples: `menuclick 0 1` (Continue), `select advanced`, `submit`, `preset Low-Spec`, `bench name 10`, `mem`, `dump`, `shot name`. The scripts in `tools/` drive the game this way (Git Bash on Windows):

- `tools/ui_test.sh` walks the whole settings menu, including calibration, Advanced, Back, presets and the restart dialog, and stops if the game crashes
- `tools/perf_lab.sh dx11|dx12 [rounds] [seconds]` starts the game on an API, loads the save, benches each preset and appends the results to `lab/results.md`, then restores your config
- `tools/bisect_dx12.sh` changes settings one at a time on DirectX 12 and reports the first one that hangs the GPU
- `tools/busy_lab.sh <yaw> "<on>|<off>;..."` benches experiments facing one direction; `tools/api_bench.sh dx11|dx12|vk [preset]` compares APIs
- `tools/menu_cycle.sh <api> [cycles]` goes to the main menu and back into the game repeatedly, watching for a crash

The scripts assume the Steam install path in their first lines; edit `G=` for yours.

## Known issues

- Pre-release. Tested on one machine (Intel integrated graphics). Reports from other GPUs are welcome in Issues.
- Switching Render API restarts the game; unsaved progress since the last checkpoint is lost, as with quitting.
- Exiting to the main menu on DirectX 12 crashed once in a long session on the test machine. The dumps point at Unity's own DirectX 12 texture code, not the mod; it didn't reproduce in 5 back-to-back cycles. DirectX 11 and Vulkan don't use that code.
- On Vulkan (DXVK), Unity's video decoding isn't supported by DXVK, so any in-game video may not play.
- After a game update, BepInEx may need to regenerate its interop assemblies (slow first start again). If the game's settings code changed, the mod may need an update.

## Credits

- [BepInEx](https://github.com/BepInEx/BepInEx), [HarmonyX](https://github.com/BepInEx/HarmonyX) and [Il2CppInterop](https://github.com/BepInEx/Il2CppInterop)
- [DXVK](https://github.com/doitsujin/dxvk) by Philip Rebohle and contributors (zlib license, included as `dxvk/LICENSE` in the release)
- Built with the help of Claude (Anthropic), working from in-game testing on real hardware.

Giggleframes is a fan-made mod and is not affiliated with Hooked Digital. It contains no game files or game code.

## License

[MIT](LICENSE)
