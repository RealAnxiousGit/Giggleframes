# Changelog

## Pre-Release 0.20 (plugin 0.20.0)

### Busy views no longer drag the frame rate down as hard
Looking across a big room full of props was the slowest thing in the game: on the test laptop the
main camera went from about 800 draw calls facing a wall to 8,600 facing the room, and from 33 fps
to 22 fps. Two new options cut what the camera draws, and both are part of the presets:

- **Draw Distance** (slider, 10 m to 55 m in 5 m steps, or *Game*): caps how far the main camera draws.
- **Small Object Culling** (Off / Low / Medium / High): stops drawing small props that would only cover
  a few pixels at their distance. Characters, effects and anything within 3 m always draw.

Busy view in Rory's Rally, DirectX 12: *Game Default* 22.5 fps, *Low-Spec* 29 fps (+30%), *Very Low* 31 fps (+39%),
with draw calls down from 8,700 to 3,400 and 2,200. Side-by-side screenshots look almost identical.

### Vulkan (DXVK)
- New **Render API** choice: **Vulkan (DXVK)**. Giggleland's build has no Unity Vulkan renderer, so this
  uses [DXVK](https://github.com/doitsujin/dxvk) 2.6.2, which translates DirectX 11 to Vulkan. The DLLs ship
  in `BepInEx/plugins/Giggleframes/dxvk`; the mod copies them next to the game when you pick Vulkan and takes them
  out again when you switch back (it only removes files identical to its own, so a ReShade `dxgi.dll` is safe).
- A `dxvk.conf` comes with it: graphics-pipeline-library shader compiling (fewer hitches when new
  objects come into view) and shader compiling on every core. `d3d11.relaxedBarriers` was tested and
  measured no faster, so it stays off.
- On the test laptop (Low-Spec, busy room): DirectX 12 27.4 fps, Vulkan 22 fps, DirectX 11 20.3 fps; all three
  about 33-34 fps facing away. DirectX 12 stays the best pick for Intel integrated graphics.
- DXVK 2.6.2 is the newest version that runs on Vulkan 1.3 drivers without `VK_KHR_maintenance5`, such as
  2023 Intel iGPU drivers. DXVK 3.x needs a Vulkan 1.4 driver.
- Safety net: if the game can't start on Vulkan (driver too old), the restart helper notices, removes
  DXVK, saves DirectX 11 and starts the game again.

### New menus
- Every option on the Advanced page is now a **drop-down list** in the game's own font instead of
  left/right arrows. Click the row or press Submit to open it; Up/Down/Submit pick, Back closes. Left/Right
  still step through the values without opening the list.
- Numeric options are **sliders** like the game's Render Scale: FSR Sharpness, Shadow Cascades, Draw Distance
  and Texture Memory. They move in whole steps and stop at their safe limits.
- **Graphics Preset** drop-down at the top of the page: *Game Default*, *Very Low* (new), *Low-Spec*, *Balanced*.
  It switches to *Custom* when you change any single option. Replaces the three *Apply…* buttons.
- **FPS counter**: bold game font with an outline, moved to the top-right corner (the objective text lives
  top-left). Green at 60 fps and up, yellow from 30 to 59, red below 30.

### Presets
- New **Very Low** preset: everything off or lowest, 15 m draw distance, high culling.
- **Low-Spec** now adds a 20 m draw distance and medium culling, and no longer lowers Texture Memory (the game's
  1.8 GB of non-streaming textures already exceed a 1 GB budget, so it only made the streamer work harder).
- **Balanced** adds a 40 m draw distance and low culling.

### Fixes and investigation
- **Crash when exiting to the main menu (DirectX 12)**: the crash dumps point at Unity's own DirectX 12 texture
  code on the render thread (`TexturesD3D12::FrameCleanup`, `TransferNativeTexture`), not at the mod and not at
  running out of memory. It did not happen in 5 back-to-back exit-to-menu cycles with this version. If you
  still see it, DirectX 11 or Vulkan (DXVK) avoid that code path entirely.
- Settings from 0.10 carry over (FPS counter, FSR sharpness, preset name).

### For developers
- New test commands: `spin`, `scenestats`, `cams`, `look`, `exp`, `mainmenu`, `rowclick`, `itemclick`, `slide`, `culling`.
- New scripts: `tools/busy_lab.sh`, `tools/menu_cycle.sh`, `tools/api_bench.sh`, `tools/get_dxvk.sh`.

## Pre-release 0.10 (plugin 0.10.0)
First release: Advanced graphics page (fog, volumetric lighting, shadows, lighting, AO, reflections,
post-processing, LOD, vegetation, texture memory), DirectX 11/12 switch, Low-Spec and Balanced presets,
FPS counter, first-run hardware check.
