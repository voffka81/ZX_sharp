# Continuous integration

GitHub Actions workflow: [`.github/workflows/dotnet-ci.yml`](../.github/workflows/dotnet-ci.yml)

Runs on every push to `master`, pull request targeting `master`, and manual `workflow_dispatch`.

- **Build (Windows):** installs the .NET 10 SDK, restores and builds `ZX_sharp.sln` in Release mode, including the WPF app.
- **Tests (Linux):** restores and runs `Tests/ZXSharp.Tests/ZXSharp.Tests.csproj` explicitly, because the test project is not currently listed in `ZX_sharp.sln`. Test results (`.trx`) are uploaded even if tests fail.

## Known baseline

The test suite currently includes intentionally failing regression tests for 16-bit memory boundary wrapping at `0xFFFF` and repeated TAP loading. These failures must **not** be suppressed or marked as passing: fix the emulator and keep the regression tests. Until fixed, the test job is expected to be red, while the build job can pass independently.

## Local verification

```sh
dotnet restore ZX_sharp.sln
dotnet build ZX_sharp.sln -c Release
dotnet test Tests/ZXSharp.Tests/ZXSharp.Tests.csproj -c Release
```

Building WPF requires Windows; core unit tests can run on Linux, macOS or Windows with the .NET 10 SDK. GitHub Actions checks are advisory until branch protection rules explicitly require them.
