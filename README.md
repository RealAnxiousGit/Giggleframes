# Giggleframes

A performance mod for **Giggleland** (Steam). It adds an **Advanced** page to the game's Graphics settings with the heavy rendering options the game doesn't expose: volumetric fog, volumetric lighting, shadow quality, ambient occlusion, reflections, post-processing and more. It also adds a DirectX 11 / DirectX 12 switch and presets tuned for integrated graphics.

**Version:** Pre-release 0.10 (plugin version 0.10.0)
**Tested on:** Giggleland v1.0.2 (Steam build 26276541, Unity 2022.3.62f2, IL2CPP), BepInEx 6.0.0-be.791, Windows 11.

---

## What it does

Giggleland runs on Unity's Universal Render Pipeline and ships several expensive effects: Kronnect Volumetric Fog & Mist 2, Volumetric Lights, Beautify post-processing, Shiny screen-space reflections, URP's SSAO and a 4096×4096 shadow atlas for local lights. The game's own Graphics page covers resolution, render scale, shadow distance and a quality preset, but none of those effects can be turned down on their own. On a laptop with integrated graphics that means 10–15 fps in busy areas.

Giggleframes adds one row, **Advanced**, under *Calibrate Brightness* on the Graphics page. It opens a page built from the game's own rows, so it uses the same font, sounds, keyboard and controller navigation as the rest of the menu. It works from the main menu and from the pause menu in game.

Every option starts at **Preset**, which leaves the game's value alone. A fresh install looks exactly like the unmodded game until you change something, with one exception: on its first launch the mod checks your hardware, and if it finds integrated graphics or a low-end GPU it applies the **Low-Spec** preset once. Changes apply right away and are saved to a config file. The mod reapplies them on every scene load and whenever the game resets its own settings.

## Features

- **Advanced page** in Graphics settings (main menu and pause menu), styled like the game's own pages
- **Render API**: DirectX 11 or DirectX 12, with a *Restart / Later* dialog. Restart reopens the game on the new API by itself
- **Upscaling filter**: Bilinear, FSR 1.0 or Point, plus an **FSR Sharpness** slider. The game's Medium preset already renders at 0.5× scale, so FSR mostly makes it look sharper
- **Volumetric Fog**: Off / Low / Medium / High (raymarch quality, downscaling, detail noise, high-quality scattering)
- **Volumetric Lighting**: Off / Low / Medium / High (raymarch quality, per-light shadow maps, downscaling)
- **Shadow Quality**: Off / Low / Medium / High / Ultra (main shadow resolution and soft shadows)
- **Shadow Cascades**: 1–4
- **Local Light Shadows**: Off / Low / Medium / High (shadow atlas size for point and spot lights)
- **Lighting Quality**: Low / Medium / High / Ultra (per-object light limit, reflection probe blending)
- **Ambient Occlusion**: Off / Low / Medium / High (URP SSAO sample count, downsampling, blur)
- **Screen Space Reflections**: Off / Low / Medium / High
- **Post-Processing Quality**: Low / Medium / High (Beautify turbo mode, bloom and depth-of-field downsampling)
- **Bloom**, **Lens Flares**, **Depth of Field**: On / Off
- **Level of Detail**: LOD bias from Lowest to High
- **Vegetation Detail**: terrain grass and detail density and distance
- **Texture Memory**: texture streaming budget (512 MB to 4 GB)
- **Show FPS Counter**: a small frame-rate and frame-time readout in the top-left corner
- **Presets**: *Apply Low-Spec Preset*, *Apply Balanced Preset*, *Reset Advanced to Game Defaults*
- **First-run hardware check** that picks Low-Spec for integrated or weak GPUs

## Performance

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
3. **Texture Memory → 512 MB or 1 GB** limits the textures that can stream.
4. Choose **DirectX 11** if RAM matters more than frame rate.

## Installing

### 1. Install BepInEx 6 (IL2CPP)

Giggleland is an IL2CPP Unity game, which needs the bleeding-edge BepInEx 6 builds, not BepInEx 5.

1. Download **BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.791+b782da4.zip** from the [BepInEx bleeding-edge builds](https://builds.bepinex.dev/projects/bepinex_be) (build #791, the Unity.IL2CPP win-x64 artifact). Direct link: <https://builds.bepinex.dev/projects/bepinex_be/791/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.791%2Bb782da4.zip>
2. Open the game folder: in Steam, right-click Giggleland → *Manage* → *Browse local files*.
3. Extract the zip into that folder, so that `winhttp.dll`, `doorstop_config.ini` and the `BepInEx` folder sit next to `Giggleland.exe`.
4. Start the game once and wait for the main menu. **The first start takes a few minutes** while BepInEx generates its interop assemblies. Then quit.

### 2. Install Giggleframes

1. Download `Giggleframes-Pre-release-0.10.zip` from [Releases](https://github.com/RealAnxiousGit/Giggleframes/releases).
2. Extract it into the game folder. The plugin should end up at
   `Giggleland\BepInEx\plugins\Giggleframes\Giggleframes.dll`.
3. Start the game and open **Settings → Graphics → Advanced**.

`BepInEx\LogOutput.log` should contain `Giggleframes 0.10.0 loaded`.

### Uninstalling

Delete `BepInEx\plugins\Giggleframes`. To remove BepInEx as well, delete `winhttp.dll`, `doorstop_config.ini`, `.doorstop_version`, `changelog.txt`, the `dotnet` folder and the `BepInEx` folder. Your saves are not touched.

## Render API notes

- Unity picks the graphics API before any mod code runs, so switching needs a restart. *Restart* closes the game and starts `Giggleland.exe` again with `-force-d3d11` or `-force-d3d12`. *Later* keeps the choice for the next launch.
- When you start the game from Steam and the saved API isn't the one running, the mod restarts the game once onto the saved API. If the saved API fails to start twice in a row, the mod falls back to DirectX 11.
- A `-force-d3d11`, `-force-d3d12`, `-force-vulkan` or `-force-gl` that you put in Steam's launch options always wins over the menu.
- **Vulkan is not offered.** The game build ships no Vulkan renderer, and `-force-vulkan` fails with "InitializeEngineGraphics failed".

## Configuration

Settings are stored in `BepInEx\config\gigglelandperf.cfg` and can be edited with the game closed.

| Section | Key | Meaning |
|---|---|---|
| Advanced | `renderApi`, `upscaling`, `volumetricFog`, … | Option index; `0` = Preset (game value) for most rows |
| Advanced | `ShowFps` | FPS counter |
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

### Project layout

| File | Purpose |
|---|---|
| `src/Plugin.cs` | BepInEx entry point, Harmony patching, per-frame runner, FPS counter |
| `src/Settings.cs` | Option definitions, config binding, presets, first-run hardware check |
| `src/Applier.cs` | Applies options to the URP asset, renderer features, fog, volumetric lights, post volumes, terrain and lights; restores the game's values when an option goes back to Preset |
| `src/AdvancedPage.cs` | Builds the Advanced page by cloning the game's Graphics page and adds the Advanced row |
| `src/Patches.cs` | Hooks on the game's settings code so the mod reapplies after the game does |
| `src/RenderApi.cs` | Render API restart and startup check |
| `src/Bench.cs`, `src/Dev.cs`, `src/Diag.cs` | Benchmark, test commands, render and memory dumps |
| `tools/` | Test scripts (below) |

### Testing tools

With `DevCommands = true`, the mod reads one command per line from `BepInEx\gigglelandperf.cmd` once a second. Examples: `menuclick 0 1` (Continue), `select advanced`, `submit`, `preset Low-Spec`, `bench name 10`, `mem`, `dump`, `shot name`. The scripts in `tools/` drive the game this way (Git Bash on Windows):

- `tools/ui_test.sh` walks the whole settings menu, including calibration, Advanced, Back, presets and the restart dialog, and stops if the game crashes
- `tools/perf_lab.sh dx11|dx12 [rounds] [seconds]` starts the game on an API, loads the save, benches each preset and appends the results to `lab/results.md`, then restores your config
- `tools/bisect_dx12.sh` changes settings one at a time on DirectX 12 and reports the first one that hangs the GPU

The scripts assume the Steam install path in their first lines; edit `G=` for yours.

## Known issues

- Pre-release. Tested on one machine (Intel integrated graphics). Reports from other GPUs are welcome in Issues.
- Switching Render API restarts the game; unsaved progress since the last checkpoint is lost, as with quitting.
- After a game update, BepInEx may need to regenerate its interop assemblies (slow first start again). If the game's settings code changed, the mod may need an update.

## Credits

- [BepInEx](https://github.com/BepInEx/BepInEx), [HarmonyX](https://github.com/BepInEx/HarmonyX) and [Il2CppInterop](https://github.com/BepInEx/Il2CppInterop)
- Built with the help of Claude (Anthropic), working from in-game testing on real hardware.

Giggleframes is a fan-made mod and is not affiliated with Hooked Digital. It contains no game files or game code.

## License

[MIT](LICENSE)
