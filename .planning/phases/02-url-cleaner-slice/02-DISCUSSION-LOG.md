# Phase 2: URL Cleaner Slice - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-20
**Phase:** 02-url-cleaner-slice
**Areas discussed:** Clipboard Monitoring & Auto-Clean, URL Cleaning Logic, WPF UI & History Integration

---

## Clipboard Monitoring & Auto-Clean

| Option | Description | Selected |
|--------|-------------|----------|
| Auto-clean clipboard and notify | Automatically strip parameters when copied, overwrite clipboard, and show toast notification. | ✓ |
| Clipboard monitoring with prompt | Show a prompt or notification and ask the user before cleaning or overwriting. | |
| Manual clean only | No clipboard monitoring in background; user must paste URLs manually. | |

**User's choice:** Auto-clean clipboard and notify
**Notes:** Decided to clean silently and overwrite, but show a native Windows toast notification to ensure the user knows why their clipboard contents changed. A settings toggle to disable clipboard monitoring will be planned for the settings phase (Phase 5).

---

## URL Cleaning Logic

| Option | Description | Selected |
|--------|-------------|----------|
| Use Flurl to parse and filter query params | Leverage the Flurl package to parse URL query parameters, matching them case-insensitively against a blocklist, and reconstruct the URL properly preserving fragments (`#`). | ✓ |
| Regex-based parameter stripping | Apply global regex patterns to strip parameters from the URL string. | |
| Basic string split | Hand-roll query parameter split and filter logic using basic string split operations. | |

**User's choice:** Use Flurl to parse and filter query params
**Notes:** Hand-rolled regex and string splits are highly discouraged due to edge cases (e.g., URL encodings, fragments, array parameters). Flurl handles all of these natively and safely.

---

## WPF UI & History Integration

| Option | Description | Selected |
|--------|-------------|----------|
| Inline results display + manual paste | Keep a single-window flow in the WPF app with a textbox for manual entry, and inline results panel showing before/after URL diffs and history. | ✓ |
| Separate URL cleaning window | Open a distinct dialog or window for manual URL pasting and cleaning. | |

**User's choice:** Inline results display + manual paste
**Notes:** Preserves the streamlined single-window experience from Phase 1. Overwriting clipboard works in the background, but the UI should also support pasting a URL manually to clean it.

---

## the agent's Discretion

- The choice of the low-level Windows API clipboard listener (e.g. Win32 `AddClipboardFormatListener` vs standard WPF clipboard events).
- The exact layout, design, and styling of the URL manual input text field and the cleaning indicator in the MainWindow.

## Deferred Ideas

- Load dynamic rulesets from external URL/JSON configuration files (deferred to Phase 3 or later).
- Auto-cleaning other schemes/protocols or handling tracking redirectors (deferred to Phase 4 or later).
- Custom hotkey to trigger URL cleaning on clipboard (deferred to Phase 5).
