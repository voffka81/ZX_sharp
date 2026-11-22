# ZX_sharp regression tests — issue #5 / Feature #1

Run from the repository root with the .NET 9 SDK:

```shell
dotnet test Tests/ZXSharp.Tests/ZXSharp.Tests.csproj
```

The test project references only the cross-platform `SpectrumPC` core, not WPF.

## Intentional red baseline

These tests describe required behavior and are **expected to fail** against the current `master` implementation:

- `Word_ReadAtFFFF_WrapsTo0000` — currently reads outside the 16-bit address space.
- `Word_WriteAtFFFF_WrapsAndPreservesRom` — currently attempts a write outside the 16-bit address space.
- `ReloadTapFile_ReplacesPreviousBlocks` — currently accumulates blocks.
- `ReloadTapePlayer_ReplacesPreviousPulseTimeline` — currently accumulates timeline events and blocks.

All other tests are intended as green regression coverage. No test outcome is asserted without executing it in an actual .NET SDK environment.

## Next

Run and record the baseline on a machine with .NET 9 SDK. Keep red tests active, then fix production behavior in separate small commits/PRs while making these tests green. Extend coverage for snapshots, reset, and concurrent UI commands once test seams and deterministic execution are available.
