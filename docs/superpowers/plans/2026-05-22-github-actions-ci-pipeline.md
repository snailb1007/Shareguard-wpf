# GitHub Actions CI Pipeline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create a GitHub Actions CI workflow that compiles, tests, and packages the ShareGuard WPF application on every push/PR to the `dev` branch.

**Architecture:** Single monolithic job on `windows-2022` that restores via MSBuild (to handle `.vcxproj` + `.wapproj`), runs unit/integration tests via `dotnet test --no-build` on individual `.csproj` files, generates a transient self-signed certificate, builds a signed MSIX package, uploads it as a workflow artifact, and always cleans up the certificate. Concurrency controls cancel redundant in-progress runs.

**Tech Stack:** GitHub Actions, MSBuild, .NET 10.0 SDK, PowerShell scripts (`New-DevCertificate.ps1`, `Build-MsixPackage.ps1`), `actions/checkout@v4`, `actions/setup-dotnet@v4`, `microsoft/setup-msbuild@v2`, `actions/upload-artifact@v4`.

---

## File Structure

| File | Responsibility |
|------|---------------|
| `.github/workflows/build.yml` | **[NEW]** GitHub Actions CI workflow — triggers, job definition, all build/test/package/cleanup steps |

This is a single-file deliverable. No source code changes. All build logic is already encapsulated in the repository's existing PowerShell scripts and MSBuild project files.

---

### Task 1: Create the GitHub Actions Workflow File

**Files:**
- Create: `.github/workflows/build.yml`

**References to read first:**
- [`08-RESEARCH.md`](file:///c:/Users/ADMIN/source/repos/Shareguard-wpf/.planning/phases/08-setup-automated-build-on-dev-branch-on-github/08-RESEARCH.md) — architecture decisions, pitfalls, and the code example
- [`New-DevCertificate.ps1`](file:///c:/Users/ADMIN/source/repos/Shareguard-wpf/scripts/New-DevCertificate.ps1) — parameter interface: `-OutputPath`, `-Password`
- [`Build-MsixPackage.ps1`](file:///c:/Users/ADMIN/source/repos/Shareguard-wpf/scripts/Build-MsixPackage.ps1) — parameter interface: `-Configuration`, `-Platform`, `-CertificatePath`, `-CertificatePassword`
- [`Shareguard-wpf.slnx`](file:///c:/Users/ADMIN/source/repos/Shareguard-wpf/Shareguard-wpf.slnx) — solution contains `.vcxproj` and `.wapproj`, confirming MSBuild-only build path
- [`.gitignore`](file:///c:/Users/ADMIN/source/repos/Shareguard-wpf/.gitignore) — confirms `certs/` and `artifacts/` are ignored

- [ ] **Step 1: Create the `.github/workflows/` directory and `build.yml` file**

  Create the file `.github/workflows/build.yml` with the following exact content:

  ```yaml
  name: CI Build and Test

  on:
    push:
      branches: [ "dev" ]
    pull_request:
      branches: [ "dev" ]

  concurrency:
    group: ${{ github.workflow }}-${{ github.ref }}
    cancel-in-progress: true

  jobs:
    build-and-test:
      name: Build & Verify Solution
      runs-on: windows-2022

      steps:
      - name: Checkout Source Code
        uses: actions/checkout@v4
        with:
          fetch-depth: 1

      - name: Setup .NET 10.0 SDK
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
          cache: true
          cache-dependency-path: '**/Directory.Packages.props'

      - name: Setup MSBuild
        uses: microsoft/setup-msbuild@v2

      - name: Restore Solution NuGet Packages
        run: |
          msbuild Shareguard-wpf.slnx /t:Restore /p:Configuration=Release /p:Platform=x64 /verbosity:minimal

      - name: Build Entire Solution
        run: |
          msbuild Shareguard-wpf.slnx /p:Configuration=Release /p:Platform=x64 /verbosity:minimal

      - name: Run Domain Unit Tests
        run: |
          dotnet test ShareGuard.Domain.Tests/ShareGuard.Domain.Tests.csproj --configuration Release --no-build --verbosity normal

      - name: Run Application Unit Tests
        run: |
          dotnet test ShareGuard.Application.Tests/ShareGuard.Application.Tests.csproj --configuration Release --no-build --verbosity normal

      - name: Run App Integration Tests
        run: |
          dotnet test ShareGuard.App.Tests/ShareGuard.App.Tests.csproj --configuration Release --no-build --verbosity normal

      - name: Generate Temporary Signing Certificate
        shell: pwsh
        run: |
          .\scripts\New-DevCertificate.ps1 -OutputPath "certs" -Password "ShareGuardDev123!"

      - name: Build and Sign MSIX Package
        shell: pwsh
        run: |
          .\scripts\Build-MsixPackage.ps1 -Configuration Release -Platform x64 -CertificatePath certs/ShareGuard_Dev.pfx -CertificatePassword "ShareGuardDev123!"

      - name: Upload MSIX Package Artifact
        uses: actions/upload-artifact@v4
        with:
          name: ShareGuard-MSIX-Release
          path: artifacts/**/*.msix
          if-no-files-found: error
          retention-days: 7

      - name: Cleanup Certificates and Secret Assets
        if: always()
        shell: pwsh
        run: |
          if (Test-Path "certs") {
              Remove-Item -Path "certs" -Recurse -Force -ErrorAction SilentlyContinue
              Write-Host "Temporary certificates directory deleted."
          }
  ```

  **Why each section matters:**

  | Section | Rationale |
  |---------|-----------|
  | `concurrency` + `cancel-in-progress: true` | Prevents wasted minutes when you push again quickly — the old run is cancelled |
  | `windows-2022` instead of `windows-latest` | Pins to a known VS/MSBuild/SDK environment so builds don't break when GitHub rolls the image |
  | `fetch-depth: 1` | Shallow clone — fastest checkout for CI (no history needed) |
  | `cache: true` on `setup-dotnet` | Avoids re-downloading NuGet packages every run, pointing at `Directory.Packages.props` for CPM cache invalidation |
  | MSBuild for restore + build | `.vcxproj` and `.wapproj` are not compatible with `dotnet build` — must use full Visual Studio MSBuild |
  | `/p:Platform=x64` | C++ and packaging projects don't support `Any CPU` — explicit platform is mandatory |
  | `dotnet test` on individual `.csproj` | `dotnet test` on the `.slnx` would fail on `.vcxproj`/`.wapproj` — targeted project testing avoids this |
  | `--no-build` | Tests run against the MSBuild-compiled binaries — no duplicate compilation |
  | Transient certificate via `New-DevCertificate.ps1` | Local-CI parity — same script developers run locally. No secrets stored in GitHub for dev builds |
  | `if: always()` on cleanup | Guarantees certificate deletion even if the build or packaging step fails |

- [ ] **Step 2: Validate YAML syntax locally**

  Run the following PowerShell command to verify the file was written correctly and contains all critical sections:

  ```powershell
  # Verify file exists
  Test-Path ".github/workflows/build.yml"
  # Expected: True

  # Verify trigger configuration
  Get-Content ".github/workflows/build.yml" | Select-String -Pattern 'branches: \[ "dev" \]'
  # Expected: 2 matches (push + pull_request)

  # Verify concurrency is configured
  Get-Content ".github/workflows/build.yml" | Select-String -Pattern "cancel-in-progress: true"
  # Expected: 1 match

  # Verify pinned runner
  Get-Content ".github/workflows/build.yml" | Select-String -Pattern "windows-2022"
  # Expected: 1 match

  # Verify MSBuild build commands
  Get-Content ".github/workflows/build.yml" | Select-String -Pattern "msbuild Shareguard-wpf.slnx"
  # Expected: 2 matches (restore + build)

  # Verify all 3 test projects are targeted
  Get-Content ".github/workflows/build.yml" | Select-String -Pattern "dotnet test"
  # Expected: 3 matches

  # Verify packaging uses transient dev cert
  Get-Content ".github/workflows/build.yml" | Select-String -Pattern "Build-MsixPackage.ps1"
  # Expected: 1 match

  # Verify always() cleanup
  Get-Content ".github/workflows/build.yml" | Select-String -Pattern "if: always()"
  # Expected: 1 match
  ```

- [ ] **Step 3: Commit the workflow file**

  ```bash
  git add .github/workflows/build.yml
  git commit -m "ci: add GitHub Actions CI pipeline for dev branch

  - Triggers on push and PR to dev branch
  - Concurrency group cancels redundant runs
  - Pinned to windows-2022 for reproducible builds
  - MSBuild restore + build (required for .vcxproj + .wapproj)
  - Runs Domain, Application, and App test suites
  - Generates transient self-signed cert, builds signed MSIX
  - Uploads MSIX artifact with 7-day retention
  - Always cleans up certificates on exit"
  ```

---

### Task 2: Push to `dev` and Verify First CI Run

**Files:**
- None (git operations only)

- [ ] **Step 1: Push the commit to the `dev` branch**

  ```bash
  git push origin dev
  ```

- [ ] **Step 2: Open GitHub Actions and monitor the first run**

  Navigate to: `https://github.com/<owner>/Shareguard-wpf/actions`

  **Expected outcome:** A new workflow run named "CI Build and Test" appears, triggered by the push to `dev`.

  **Monitor each step in order:**

  | Step | Expected Result |
  |------|-----------------|
  | Checkout Source Code | ✅ Completes in ~5s |
  | Setup .NET 10.0 SDK | ✅ Installs .NET 10.0.x, cache MISS on first run |
  | Setup MSBuild | ✅ Locates MSBuild from VS installation |
  | Restore Solution NuGet Packages | ✅ Restores all packages for all projects |
  | Build Entire Solution | ✅ Compiles C#, C++, and packaging projects |
  | Run Domain Unit Tests | ✅ All tests pass |
  | Run Application Unit Tests | ✅ All tests pass |
  | Run App Integration Tests | ✅ All tests pass |
  | Generate Temporary Signing Certificate | ✅ Creates `certs/ShareGuard_Dev.pfx` |
  | Build and Sign MSIX Package | ✅ Produces signed `.msix` in `artifacts/` |
  | Upload MSIX Package Artifact | ✅ Artifact "ShareGuard-MSIX-Release" visible |
  | Cleanup Certificates | ✅ Deletes `certs/` directory |

- [ ] **Step 3: Download and verify the uploaded artifact**

  From the GitHub Actions run page, download the "ShareGuard-MSIX-Release" artifact. Verify:
  1. The ZIP contains at least one `.msix` file
  2. The file size is non-trivial (indicating a real package, not an empty stub)

- [ ] **Step 4: Verify concurrency cancellation works**

  Push two commits in quick succession:
  ```bash
  git commit --allow-empty -m "ci: test concurrency cancellation (1 of 2)"
  git push origin dev
  git commit --allow-empty -m "ci: test concurrency cancellation (2 of 2)"
  git push origin dev
  ```

  **Expected:** The first workflow run should be cancelled automatically. Only the second run completes.

  After verifying, clean up the empty commits:
  ```bash
  git reset --soft HEAD~2
  git push --force-with-lease origin dev
  ```

---

## Verification Summary

| Check | Method | Expected |
|-------|--------|----------|
| Workflow file exists | `Test-Path .github/workflows/build.yml` | `True` |
| Triggers on dev push | Push to dev → Actions tab | Run appears |
| Triggers on dev PR | Create PR targeting dev | Run appears |
| Build succeeds | GitHub Actions log | All steps green |
| Tests pass | GitHub Actions log | 3 test steps green |
| MSIX artifact uploaded | GitHub Actions artifacts | "ShareGuard-MSIX-Release" downloadable |
| Certs cleaned up | Cleanup step log | "Temporary certificates directory deleted." |
| Concurrency cancellation | Two rapid pushes | First run cancelled |
| Cache hit on second run | Second push → setup-dotnet log | "Cache restored" message |
