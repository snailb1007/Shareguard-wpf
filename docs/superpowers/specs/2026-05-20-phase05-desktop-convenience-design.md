# Phase 05 Desktop Convenience Design

Date: 2026-05-20
Status: Approved design
Scope: System tray residency, global hotkey, desktop convenience settings, and output policy customization.

## Summary

Phase 05 adds Windows-native convenience behavior around the existing ShareGuard WPF cleaning flows. It does not change the privacy pipeline, stripper behavior, history database schema, packaging strategy, or Explorer context menu work.

The phase uses the recommended service-based architecture:

- Keep tray, hotkey, window lifecycle, and settings concerns in the WPF layer.
- Keep domain, application, and infrastructure layers free of WPF, HWND, tray icon, and hotkey dependencies.
- Expose user actions through shared ViewModel commands so tray menu items, hotkeys, and visible UI controls all call the same behavior.
- Persist desktop preferences locally under AppData as JSON.

## Goals

- Minimize or close ShareGuard to the system tray without exiting the app.
- Restore ShareGuard from the tray reliably.
- Provide tray menu actions for common desktop workflows.
- Register a global hotkey for cleaning the current clipboard.
- Add explicit settings for clipboard monitoring, notifications, tray behavior, and output naming/location.
- Preserve the local-only privacy posture and never store original content in settings.

## Non-Goals

- MSIX or MSI packaging.
- Explorer context menu integration.
- Rule auto-update.
- Reworking existing strippers.
- Replacing existing history storage.
- Adding cloud sync, telemetry, or remote configuration.

Packaging remains Phase 06. Explorer context menu remains Phase 07.

## Recommended Approach

Use a thin service-based desktop convenience layer inside the existing WPF app.

The selected approach is preferred over putting all behavior in `MainWindow.xaml.cs` because it keeps the codebase aligned with MVVM and makes deterministic logic testable. It is also preferred over creating a new platform project during this phase because that would add structure before the current repo needs it.

## Components

### `IAppWindowService`

Owns main-window lifecycle actions:

- Show the main window.
- Restore from minimized state.
- Activate/focus the window.
- Hide the main window to tray.
- Allow explicit application shutdown.

The main window should keep only unavoidable WPF event bridging. Business decisions such as whether close means hide or exit should be delegated to settings and window service behavior.

### `ITrayService`

Owns tray icon lifecycle and tray menu wiring.

Implementation choice: `H.NotifyIcon.Wpf`.

Rationale:

- It is a modern WPF tray icon package for current .NET desktop apps.
- Context7 documentation shows direct XAML support for `TaskbarIcon`, `IconSource`, `ContextMenu`, popup activation, and tooltip behavior.
- It avoids raw `Shell_NotifyIcon` interop and avoids directly using Windows Forms `NotifyIcon` from WPF.

Tray menu items:

- Open ShareGuard
- Clean Clipboard Now
- Pause/Resume Clipboard Monitor
- Settings
- Exit

The tray icon must be disposed on application exit to avoid stale tray icons.

### `IHotkeyService`

Owns global hotkey registration and unregistration.

Implementation choice: wrap Win32 `RegisterHotKey` and `UnregisterHotKey` in a WPF service.

Default hotkey: `Ctrl+Shift+G`.

The service should avoid low-level keyboard hooks. If registration fails because the hotkey is already taken, the app should keep running, disable the hotkey setting for the current session, and surface a clear non-blocking status or notification.

### `IUserSettingsService`

Owns local desktop settings persistence.

Storage:

- AppData JSON file.
- Safe defaults if the file is missing.
- Safe defaults if the file is malformed.
- No original file contents.
- No original URLs.
- No cloud storage.
- No registry dependency for app preferences.

Settings:

- Clipboard monitoring enabled.
- Notifications enabled.
- Minimize to tray.
- Close to tray.
- Start minimized.
- Global hotkey enabled.
- Global hotkey gesture, initially `Ctrl+Shift+G`.
- Output policy: same folder or custom folder.
- Custom output directory.
- Clean filename suffix, default `.clean`.

### Output Policy

Phase 01 hardcoded same-folder output and `.clean` naming. Phase 05 makes those user configurable while preserving safe defaults.

Recommended behavior:

- Default remains same folder with `.clean` suffix.
- If custom output folder is enabled and accessible, cleaned copies are saved there.
- If the custom folder is missing, inaccessible, or deleted, fall back to same-folder output for that operation and report the fallback.
- Source files are still never modified.
- Existing collision handling remains required.

## Behavior

### Startup

ShareGuard starts with the main window visible by default. This is the safest default because background clipboard behavior and tray residency should be discoverable.

If the user enables start minimized, the app may start hidden in the tray after the tray service has initialized successfully.

### Minimize And Close

Recommended defaults:

- Minimize hides the app to tray when minimize-to-tray is enabled.
- Close hides the app to tray when close-to-tray is enabled.
- Explicit tray Exit shuts down the application.

The first close-to-tray action should show a short non-blocking notification or status message so the user understands the app is still running.

Application shutdown should use explicit shutdown behavior. The app should not terminate merely because the main window is hidden.

### Clean Clipboard Now

`Clean Clipboard Now` should be a shared ViewModel command used by:

- Main window UI.
- Tray menu.
- Global hotkey.

If the clipboard contains a dirty supported URL, ShareGuard cleans it, writes the cleaned URL back to the clipboard, shows a notification when notifications are enabled, and logs history using the existing flow.

If the clipboard is empty, inaccessible, clean, or unsupported, the app should show quiet status feedback instead of an error dialog.

### Clipboard Monitoring Toggle

The existing clipboard monitor remains the implementation basis. Phase 05 adds durable settings and tray/UI controls for enabling or pausing it.

Toggling clipboard monitoring should be explicit and visible. Background behavior should never become hidden or impossible to disable.

### Settings View

The settings UI should expose the Phase 05 preferences directly. It should not become a general preferences overhaul.

Recommended sections:

- Background behavior: clipboard monitor, notifications, start minimized.
- Tray behavior: minimize to tray, close to tray.
- Hotkey: enabled, gesture display, conflict status.
- Output: same folder or custom folder, browse custom folder, suffix.

## Data Flow

### Tray Command Flow

```text
Tray menu item
-> bound ViewModel command
-> existing service or window service
-> status/notification/history update
```

### Hotkey Flow

```text
Win32 WM_HOTKEY
-> IHotkeyService callback
-> CleanClipboardNowCommand
-> existing URL cleaner / clipboard monitor behavior
-> notification and history logging
```

### Output Policy Flow

```text
File cleanup request
-> user settings output policy
-> output path resolver
-> existing cleaner writes clean copy
-> existing history logging
```

## Error Handling

- Tray icon creation failure: app continues normally and reports tray unavailable.
- Tray icon disposal failure: swallow during shutdown after best-effort cleanup.
- Hotkey registration conflict: app continues, disables hotkey for the session, and reports the conflict.
- Missing settings file: load defaults.
- Malformed settings file: load defaults and delay rewriting until settings are saved.
- Missing custom output directory: fall back to same-folder output for the operation.
- Clipboard access failure: non-fatal, consistent with current clipboard service behavior.

No Phase 05 error should put user files at risk or cause source files to be modified.

## Testing

### Automated Tests

Add focused tests for deterministic behavior:

- Settings defaults load when no file exists.
- Malformed settings JSON falls back to safe defaults.
- Settings save and reload round trip.
- Output policy resolves same-folder output.
- Output policy resolves custom-folder output.
- Output policy falls back when custom folder is unavailable.
- Hotkey conflict handling through an abstraction.
- ViewModel command toggles clipboard monitoring state.
- ViewModel command invokes the shared clean-clipboard flow.

### Manual UAT

Verify WPF shell behavior manually:

- App minimizes to tray and restores.
- App close hides to tray when enabled.
- Tray Open restores and focuses the window.
- Tray Clean Clipboard Now cleans a dirty URL.
- Tray Pause/Resume updates clipboard monitor state.
- Tray Exit shuts down and disposes tray/hotkey resources.
- `Ctrl+Shift+G` invokes Clean Clipboard Now.
- Hotkey conflict behavior is clear if reproducible.
- Custom output directory is used for clean copies.
- Missing custom output directory falls back safely.

## Implementation Constraints

- Do not modify original source files.
- Do not store original URLs or file contents in settings.
- Do not introduce network behavior.
- Do not use low-level keyboard hooks.
- Do not hand-roll raw tray icon interop.
- Keep WPF-specific code out of application/domain/infrastructure layers.
- Keep code-behind limited to unavoidable WPF event bridging.

## Acceptance Criteria

- ShareGuard can minimize or close to tray and restore from tray.
- Tray menu exposes Open ShareGuard, Clean Clipboard Now, Pause/Resume Clipboard Monitor, Settings, and Exit.
- `Ctrl+Shift+G` triggers Clean Clipboard Now.
- Hotkey conflict is handled without crashing.
- Clipboard monitoring and notifications can be enabled or disabled through persisted settings.
- Output policy supports same-folder and custom-folder modes.
- Custom output folder failure falls back safely.
- Application exit disposes tray, hotkey, and clipboard monitor resources.
- Automated tests cover settings, output policy, and command behavior.
- Manual UAT covers tray and hotkey shell behavior.

## References

- `.planning/ROADMAP.md`
- `.planning/REQUIREMENTS.md`
- `.planning/phases/05-desktop-convenience/05-RESEARCH.md`
- `docs/superpowers/specs/2026-05-19-shareguard-wpf-product-design.md`
- Context7: `/havendv/h.notifyicon`
- Context7: `/dotnet/wpf`
