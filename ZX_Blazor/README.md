# ZX_Blazor — browser prototype

A standalone .NET 10 Blazor WebAssembly frontend for the existing SpectrumPC ZX Spectrum 48K core.
The original WPF app remains unchanged.

## Run locally

Install the .NET 10 SDK and WebAssembly workload if prompted, then:

```sh
dotnet run --project ZX_Blazor/ZX_Blazor.csproj
```

Navigate to the local URL printed by `dotnet run`. Click on the canvas to focus it, use keyboard input. Shift maps to CAPS SHIFT and Ctrl maps to SYMBOL SHIFT. Run/Pause and Reset are provided.

## Build / publish

```sh
dotnet build ZX_Blazor/ZX_Blazor.csproj -c Release
dotnet publish ZX_Blazor/ZX_Blazor.csproj -c Release
```

The app has no backend and runs the SpectrumPC core in the browser. The frame is converted from the core's `0x00RRGGBB` `int[]` pixels to RGBA, then uploaded as a single typed array to a canvas via JS interop.

**Prototype limitations:** no audio output, TAP file chooser, snapshot loading, precise frame pacing, cross-platform gamepad mapping or GitHub Pages deployment yet. UI runs CPU execution in the browser's main execution context; performance and responsiveness must be benchmarked on target devices. The SpectrumPC core currently contains a ROM; check distribution permissions before deploying publicly.

For GitHub Pages, a follow-up must set the correct repository base path and configure a Pages build/deploy workflow. This PR intentionally does not publish anything.
