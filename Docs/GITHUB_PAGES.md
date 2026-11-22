# GitHub Pages deployment

The project includes a GitHub Actions workflow at `.github/workflows/deploy-pages.yml`.
It runs unit tests, publishes the standalone Blazor WebAssembly frontend, adjusts its
`<base href>` to `/ZX_sharp/`, adds `.nojekyll`, and uploads/deploys static files.

## One-time repository setup
1. Merge the workflow PR into `master`.
2. Open **Settings → Pages → Build and deployment → Source**.
3. Select **GitHub Actions** (not 'Deploy from a branch').
4. Open **Actions → Deploy ZX_Blazor to GitHub Pages** and run **Run workflow** on `master` if it didn't start automatically.
5. When the deployment succeeds, visit https://voffka81.github.io/ZX_sharp/.

## Automatic publishing
Pushing changes to `master` that touch the Blazor app, emulator core,
tests or Pages workflow triggers a new deployment. The unit test job must pass
before any new version is deployed. Other changes to the repo do not trigger
a Pages deployment; run the workflow manually if needed.

The workflow does not publish from pull request branches.

## Important
The frontend is currently a prototype without sound, tape file selection,
or snapshot loading. It references an emulator core that contains a ROM.
Verify that the ROM can lawfully be redistributed publicly before enabling
GitHub Pages. Pages does not supply a server-side .NET process: emulation
runs in the visitor's browser.
