# ZX_Blazor — browser prototype

A standalone .NET 10 Blazor WebAssembly frontend for the existing SpectrumPC ZX Spectrum 48K core.
The original WPF app remains unchanged.

## Run locally

Install the .NET 10 SDK and WebAssembly workload if prompted, then:

```sh
dotnet run --project ZX_Blazor/ZX_Blazor.csproj
```

Navigate to the local URL printed by `dotnet run`. Click on the canvas to focus it, use keyboard input. Shift maps to CAPS SHIFT and Ctrl maps to SYMBOL SHIFT. Run/Pause and Reset are provided.

## Loading local programs

Choose a local `.tap` (tape image) or `.z80` (48K snapshot) using **Load TAP / Z80**. Files are read entirely in the browser: they are **not uploaded to a server**. Maximum file size: 4 MB.

- **Z80**: the 48K snapshot is applied immediately; continue from the saved state.
- **TAP**: the tape is mounted but does not auto-play. In Spectrum BASIC, type `LOAD ""` and press Enter, then click **Play tape**. Use **Stop tape** to stop playback. The file is mounted until another tape is loaded; mounting a new TAP replaces the previous tape.

Tape input relies on the emulator's existing tape timing and the browser scheduler; further timing refinements may be needed for reliable game loading.

## Build / publish

```sh
dotnet build ZX_Blazor/ZX_Blazor.csproj -c Release
dotnet publish ZX_Blazor/ZX_Blazor.csproj -c Release
```

The app has no backend and runs the SpectrumPC core in the browser. The frame is converted from the core's `0x00RRGGBB` `int[]` pixels to RGBA, then uploaded as a single typed array to a canvas via JS interop.

**Prototype limitations:** no audio output, precise frame pacing, cross-platform gamepad mapping or automatic tape loading. GitHub Pages deployment is configured separately. UI runs CPU execution in the browser's main execution context; performance and responsiveness must be benchmarked on target devices. The SpectrumPC core currently contains a ROM; check distribution permissions before deploying publicly.

GitHub Pages deployment occurs from `master` via `.github/workflows/deploy-pages.yml` after changes are merged.
