# Phase 2: URL Cleaner Slice - Context

**Gathered:** 2026-05-20
**Status:** Ready for planning

<domain>
## Phase Boundary

Phase 2 delivers the core URL tracking parameter cleaning and clipboard monitoring functionality. The application monitors the system clipboard for new text content. If a valid HTTP/HTTPS URL is copied, the clipboard monitoring service captures it, passes it to the `UrlCleanerService`, and if tracking parameters (such as `utm_*`, `fbclid`, `gclid`, `igshid`, etc.) are detected, strips them using `Flurl` without altering the host, path, or fragment. The cleaned URL is automatically written back to the clipboard, a native Windows toast notification is shown to inform the user, and a summary is logged to the local operation history. The user can also paste a URL manually into the UI to clean it.

</domain>

<decisions>
## Implementation Decisions

### Clipboard Monitoring & Auto-Clean
- **D-01:** ShareGuard will monitor the system clipboard. When a valid HTTP/HTTPS URL containing tracking parameters is detected, it will be automatically cleaned.
- **D-02:** The cleaned URL will automatically overwrite the dirty URL on the clipboard.
- **D-03:** A native Windows toast notification will be displayed when a URL is auto-cleaned, notifying the user (e.g. "URL cleaned & updated in clipboard"). A setting to toggle this behavior will be planned for Phase 5.
- **D-04:** Non-URL text and clean URLs (no tracking parameters found) will be ignored by the clipboard service.

### URL Cleaning Logic
- **D-05:** Query parameter manipulation must be done using `Flurl.Url` rather than manual string splitting or regex parsing to avoid breaking fragments (`#`), URL encodings, or query structure.
- **D-06:** The initial parameter cleaning ruleset will be a hardcoded list of common tracking parameters (e.g., `utm_source`, `utm_medium`, `utm_campaign`, `utm_term`, `utm_content`, `fbclid`, `gclid`, `igshid`, `_gl`, `tt_medium`, `twclid`). Parameters are matched case-insensitively.
- **D-07:** Malformed URLs or strings that fail parsing under `Uri.TryCreate` or `Flurl` will be ignored and left untouched (no clipboard overwrite, no UI error popup).

### WPF UI & History Logging
- **D-08:** The WPF main window will include a manual entry area (a textbox and a button) where users can paste and clean a URL manually.
- **D-09:** Any successful cleaning operation (either auto-clipboard or manual) will show the "Before" and "After" URLs in the UI results panel.
- **D-10:** Cleaned URLs and a count of removed parameters will be logged to the history database/log (using the Lightweight JSON schema established in Phase 1).

### the agent's Discretion
- The specific Windows API clipboard monitoring mechanism (e.g., Win32 `SetClipboardViewer` or `AddClipboardFormatListener` wrapped in a WPF message loop helper).
- The visual design of the URL cleaning panel in WPF (textbox, clean button, results display).
- The exact wording and style of the Windows toast notification.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Architecture & Design
- `docs/architecture.md` — Approved architecture blueprint (Clean Architecture + MVVM, solution structure, tech stack, implementation rules).
- `docs/prd.md` — Full PRD for the application.

### Phase 2 Research
- `.planning/phases/02-url-cleaner-slice/RESEARCH.md` — Technology choices (Flurl package), clipboard monitoring API choices, and tracking parameter lists.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `ShareGuard.Domain` & `ShareGuard.Application` — Layers where `IUrlCleanerService` and `UrlCleanerService` will be defined and implemented.
- `Shareguard-wpf/App.xaml.cs` — The DI generic host where clipboard monitoring service will be registered.
- `Shareguard-wpf/ViewModels/MainViewModel.cs` — ViewModel where commands/properties for clipboard clean notifications and manual URL input will reside.

### Established Patterns
- Clean Architecture separation of concerns: core domain interfaces in `ShareGuard.Domain`, application service commands in `ShareGuard.Application`, concrete implementations in `ShareGuard.Infrastructure`, and WPF views/ViewModels in `Shareguard-wpf`.
- Centralized package version management in `Directory.Packages.props`.

### Integration Points
- `MainViewModel.cs` properties and commands (`Loaded` event command, status message, title).
- `MainWindow.xaml` layout where the clipboard monitoring toggle and manual clean buttons/textbox can be added.
- `DependencyInjection.cs` static classes in Application and Infrastructure layers to register services.

</code_context>

<specifics>
## Specific Ideas

- The UI should have a toggle (check box) in the main window for "Monitor Clipboard" so users can easily pause/resume this feature.
- The toast notification should include a quick-action button if supported, or simply fade out after 3 seconds.

</specifics>

<deferred>
## Deferred Ideas

- Dynamic/configurable blocklist rules loaded from an external JSON or downloaded from ClearURLs (deferred to Phase 3 or later).
- Auto-cleaning other protocol schemes or deep cleaning of specific redirect URLs (e.g., tracking redirectors like `https://gate.sc/?url=...` - deferred to Phase 4 or later).
- Custom hotkey to clean clipboard (deferred to Phase 5).

</deferred>

---

*Phase: 02-url-cleaner-slice*
*Context gathered: 2026-05-20*
