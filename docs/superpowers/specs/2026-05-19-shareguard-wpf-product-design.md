# ShareGuard WPF Product Design

> Date: 2026-05-19
> Status: Approved design draft
> Scope: Full Windows product PRD strategy, delivered by tracer-bullet phases
> Primary reference: `C:\Users\ADMIN\source\repos\ShareGuard`

## 1. Product Strategy

ShareGuard WPF is the official Windows desktop client for ShareGuard, not a separate product. Shared privacy behavior defaults to the existing mobile app in `C:\Users\ADMIN\source\repos\ShareGuard`.

The WPF product keeps the same product principles:

- Privacy-first: strip sensitive metadata and unknown trackers by default.
- Creator-friendly: keep verified affiliate parameters by default.
- Zero-cloud: process user content locally, with no uploads.
- Transparent when needed: stay simple for clean content, show findings when privacy-relevant data was removed or kept.
- Open-source trust: behavior should remain auditable and documented.

The platform difference is entry points. Android uses Share Sheet middleware. Windows uses a desktop app window, drag/drop, file picker, clipboard actions, tray/hotkey actions, and eventually Explorer context menu integration. These entry points change the UX, not the shared privacy model.

Delivery follows a tracer-bullet strategy. Each phase must either produce a usable end-to-end flow or reduce a major Windows-specific risk. The first tracer bullet is the image file privacy slice: drag an image into ShareGuard WPF, detect sensitive metadata, create a clean copy, show findings, and preserve the original file untouched.

## 2. Users and UX Principles

The MVP primarily serves privacy-conscious everyday users. They should be able to drag a file or paste a URL, get a clean result, and understand the result without knowing EXIF, IPTC, XMP, or URL rule internals.

The second audience is power users, creators, journalists, and users who process batches. They need batch processing, history, hotkeys, shell integration, report/export flows, and stronger control over affiliate handling. These capabilities belong in the roadmap after the first vertical slices are usable.

The UX principle is simple first, transparent on demand. Result views should initially group findings into categories such as GPS, Camera, Software, Date/Time, Author, Tracking Params, and Affiliate Kept. Detailed field-level data can be expandable, but it should not be the default presentation.

For files, the default behavior is never to modify the original. Early phases always create a clean copy. Replace-original behavior is an advanced setting for a later phase because a privacy tool should not surprise users by changing source files.

The app should open as a working tool, not a landing page. The first screen should expose a drop zone, browse action, paste URL action, and recent status/history.

## 3. Product Capabilities and Windows Entry Points

Core capabilities mirror the mobile app:

- Image metadata stripping.
- URL tracker cleaning.
- Verified affiliate preservation modes.
- Video, PDF, and Office metadata stripping.
- Local-only history.
- Settings for theme, language, affiliate mode, rule updates, and output policy.
- Secure rule updates.
- Verification before declaring content clean.

Windows entry points:

1. Main Window Drag/Drop
   - Primary MVP entry point.
   - User drags files into the app or chooses them with Browse.
   - This is the first tracer bullet because it avoids packaging and shell-extension risk.

2. Clipboard URL Cleaning
   - Desktop equivalent of the mobile Home clipboard/paste flow.
   - Start with manual paste or Clean Clipboard Now.
   - Auto-monitoring is opt-in later because background clipboard behavior has trust implications.

3. System Tray and Hotkey
   - Desktop convenience layer.
   - Tray exposes Open, Clean Clipboard, and Settings.
   - A hotkey such as Ctrl+Shift+G can clean the current clipboard once the URL cleaner is stable.

4. Explorer Context Menu
   - Important product goal: right-click file, Clean with ShareGuard.
   - Runs as a parallel experimental track because MSIX, IExplorerCommand, COM, and installer behavior can be high-risk.
   - It must not block the drag/drop MVP path.

5. Batch Folder and Folder Watch
   - Power-user desktop features.
   - Batch folder processing follows after multi-file drag/drop.
   - Folder watch is later and opt-in because automatic file processing can be surprising.

6. History and Settings
   - History stores only metadata summaries: type, timestamp, findings count/category, and status.
   - History never stores original file contents or original URLs beyond what is required for a privacy-safe summary.

## 4. Architecture and Data Flow

The architecture is WPF shell plus platform-agnostic core.

### Projects

`ShareGuard.Core`

Contains domain models, stripper interfaces, routing, rule engine, findings, verification contracts, and shared services. It must not reference WPF or Win32 UI APIs.

Core concepts should stay equivalent to mobile:

- `ShareItem`
- `StripResult.Success` / `StripResult.Failed`
- `Finding`
- `FindingAction`
- `AffiliateMode`
- `FailureType`
- `HandleConfidence`
- Stripper priority routing

`ShareGuard.App`

Contains WPF UI, ViewModels, dependency injection setup, views, result dialogs, history, and settings. ViewModels follow a UDF-style shape: state flows down to the UI, events flow up to ViewModels, and processing happens through injected services.

`ShareGuard.Platform.Windows`

Contains Windows-specific services: file picker, output path policy, clipboard service, tray service, hotkey registration, and shell/protocol bridge. This may begin as folders inside the WPF app, but the boundary should remain explicit so core logic does not become tied to WPF or Win32.

`ShareGuard.Shell` / `ShareGuard.Installer`

Contains the Explorer context menu and packaging experiment. It stays separate so shell integration risk does not slow core product delivery.

### First Tracer-Bullet Flow

```text
Dropped file path
-> FileItem
-> BatchProcessor / StripperRouter
-> ImageStripper
-> Verification
-> CleanCopyWriter
-> StripResult
-> Result UI
-> History summary
```

Rules:

- The original file is read-only.
- A clean copy is created using a clear naming policy.
- If stripping succeeds but verification detects a privacy leak, the result is not a success.
- Unsupported known formats must be reported honestly.
- Corruption risk must not write over user files.

## 5. Tracer-Bullet Roadmap

### Phase 0: Product Baseline

Goal: establish a buildable WPF/Core structure.

Acceptance criteria:

- WPF app opens.
- Core test project exists.
- Shared domain models are ported from mobile concepts.
- WPF PRD/spec references the mobile source of truth.

### Phase 1: Image Clean Copy Tracer Bullet

Goal: the first real end-to-end flow.

Acceptance criteria:

- User drags a JPEG into the app.
- App creates a clean copy without modifying the source.
- Sensitive metadata is stripped.
- Result UI shows grouped findings.
- History summary is saved locally.
- Unit tests cover `ImageStripper` and output naming.

### Phase 2: URL Cleaner Slice

Goal: desktop equivalent of the mobile Home paste/clipboard flow.

Acceptance criteria:

- User pastes a URL or manually cleans the clipboard.
- Analytics parameters are stripped.
- Verified affiliate parameters are kept by default.
- Findings are grouped as stripped or kept.
- The clean URL can be copied back to the clipboard.
- Relevant mobile URL/rule tests are ported.

### Phase 3: Multi-file and History Slice

Goal: make the app useful for desktop file workflows.

Acceptance criteria:

- User can drag/drop multiple files.
- Progress is visible.
- Batch failure policy is explicit.
- History supports basic filtering.
- History stores no original content.

### Phase 4: Advanced File Types

Goal: expand metadata stripping toward mobile parity.

Acceptance criteria:

- Office and PDF stripping are implemented before higher-risk video work unless research says otherwise.
- Each file type has verification and failure behavior.
- Unsupported known formats are reported clearly.

### Phase 5: Desktop Convenience

Goal: add Windows-native productivity features.

Acceptance criteria:

- Tray menu exposes Open, Clean Clipboard, and Settings.
- Hotkey can clean clipboard.
- Clipboard auto-monitoring, if added, requires explicit opt-in and visible controls.

### Parallel Track: Explorer Context Menu Spike

Goal: reduce shell integration risk early.

Acceptance criteria:

- Prototype context menu or protocol bridge can pass a file path into ShareGuard.
- The spike documents MSIX/classic installer implications.
- This track does not block Phases 1-3.

### Phase 6: Packaging and Distribution

Goal: deliver an installable Windows app.

Acceptance criteria:

- Installer strategy is chosen: MSIX, classic installer, or both.
- Code signing and update story are documented.
- External tool dependencies such as ExifTool or FFmpeg are optional unless licensing and installer-size tradeoffs are accepted.

## 6. Risks and Mitigations

### Shell Integration Risk

Explorer context menu work can pull the app into MSIX, COM, installer, and signing complexity too early.

Mitigation: keep shell integration as a parallel spike while drag/drop remains the MVP path.

### Metadata Correctness Risk

A clean file that still leaks sensitive metadata is worse than a visible failure.

Mitigation: port the mobile 3-tier verification principle to WPF:

- Pre-flight reference before stripping.
- Structural check after stripping.
- Privacy leak check with an independent parser when possible.

Privacy leak means the app must not report success.

### Dependency and License Risk

ExifTool and FFmpeg are powerful but increase installer size and licensing complexity.

Mitigation: prefer pure-.NET implementations in early phases. External tools can become optional advanced capability later.

### Desktop Trust Risk

Clipboard monitoring, tray residency, hotkeys, and background behavior can feel invasive.

Mitigation: manual actions first. Background clipboard monitoring must be opt-in, visible, and easy to disable.

### Cross-Platform Drift

WPF behavior can diverge from mobile over time.

Mitigation: WPF specs reference mobile docs, models, and tests. Intentional WPF-specific deviations require a short rationale.

## 7. Validation Strategy

- Unit tests for core strippers and URL rule behavior.
- Golden sample files for before/after metadata checks.
- Port relevant mobile URL stripper and rule-engine tests.
- Manual UAT per vertical slice, starting with drag/drop image cleaning.
- History/privacy checks to verify no original content is stored.

## 8. Documentation Strategy

`docs/prd.md` remains the broad WPF PRD/research document.

This design spec records the approved product direction and delivery strategy.

Future implementation plans should refer to this spec and the mobile sources rather than re-litigating shared product behavior.

## 9. Mobile Source References

Use these mobile files as the source of truth for shared behavior unless a WPF-specific reason exists to deviate:

- `C:\Users\ADMIN\source\repos\ShareGuard\docs\PRD.md`
- `C:\Users\ADMIN\source\repos\ShareGuard\docs\ShareGuard-Spec.md`
- `C:\Users\ADMIN\source\repos\ShareGuard\docs\description\pipeline_share_receiver.md`
- `C:\Users\ADMIN\source\repos\ShareGuard\docs\description\pipeline_home.md`
- `C:\Users\ADMIN\source\repos\ShareGuard\docs\description\pipeline_history.md`
- `C:\Users\ADMIN\source\repos\ShareGuard\docs\description\pipeline_settings.md`
- `C:\Users\ADMIN\source\repos\ShareGuard\app\src\main\java\dev\snailb1007\shareguard\domain\model\StripResult.kt`
- `C:\Users\ADMIN\source\repos\ShareGuard\app\src\main\java\dev\snailb1007\shareguard\domain\model\Finding.kt`
- `C:\Users\ADMIN\source\repos\ShareGuard\app\src\main\java\dev\snailb1007\shareguard\domain\model\ShareItem.kt`
- `C:\Users\ADMIN\source\repos\ShareGuard\app\src\main\java\dev\snailb1007\shareguard\strippers\core\Stripper.kt`
- `C:\Users\ADMIN\source\repos\ShareGuard\app\src\main\java\dev\snailb1007\shareguard\domain\model\AffiliateMode.kt`
- `C:\Users\ADMIN\source\repos\ShareGuard\app\src\main\java\dev\snailb1007\shareguard\domain\model\DetectionThreshold.kt`

## 10. Open WPF-Specific Questions

These should be answered during implementation planning or dedicated spikes:

- Which clean-copy naming policy should be the default: `.shareguard`, `.clean`, or output folder?
- Should the first installer target MSIX, classic installer, or both?
- Which WPF UI library should be used for the first production UI, if any?
- Should Explorer context menu use MSIX `IExplorerCommand`, classic registry integration, or a protocol bridge first?
- What is the minimum supported Windows version for release builds?
