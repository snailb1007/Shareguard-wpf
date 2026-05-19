# Phase 1: Image Clean Copy Tracer Bullet - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-20
**Phase:** 1-Image Clean Copy Tracer Bullet
**Areas discussed:** Clean Copy Save & Naming Policy, Metadata Findings UI Display

---

## Clean Copy Save & Naming Policy

### Question: Where should cleaned copies be saved by default?

| Option | Description | Selected |
|--------|-------------|----------|
| **(Recommended) Same directory as the original file** | Keeps the cleaned file located next to the original for easy access | ✓ |
| A dedicated, user-configured folder | e.g. `C:\Users\ADMIN\Pictures\ShareGuard\` or a customized path | |
| Always prompt with a Save File Dialog | Forces the user to choose a destination path every time | |
| You decide | The agent will design a sensible default/fallback | |

**User's choice:** Same directory as the original file
**Notes:** Keeps the cleaned file located next to original for easy access.

---

### Question: How should the cleaned copies be named by default?

| Option | Description | Selected |
|--------|-------------|----------|
| **(Recommended) Append '.clean' to the filename** | e.g. `image.jpg` becomes `image.clean.jpg` | ✓ |
| Append '_clean' to the filename | e.g. `image.jpg` becomes `image_clean.jpg` | |
| Keep the exact same name, but write to a 'ShareGuard' subfolder | e.g. `ShareGuard/image.jpg` in the same directory | |
| You decide | The agent will pick a clean, standard format | |

**User's choice:** Append '.clean' to the filename (e.g., 'image.jpg' becomes 'image.clean.jpg')
**Notes:** Prevents overwriting original file.

---

### Question: How should naming conflicts be resolved if the clean file already exists?

| Option | Description | Selected |
|--------|-------------|----------|
| **(Recommended) Auto-increment the filename** | e.g. `image.clean (1).jpg`, `image.clean (2).jpg` | ✓ |
| Overwrite the existing clean file without prompting | Overwrites whatever is there | |
| Prompt the user | Shows a WPF message box/dialog to ask | |
| You decide | The agent will handle conflicts robustly in the code | |

**User's choice:** Auto-increment the filename (e.g., 'image.clean (1).jpg')
**Notes:** Standard conflict resolution behavior.

---

### Question: Should folder and naming customization settings be deferrable?

| Option | Description | Selected |
|--------|-------------|----------|
| **(Recommended) Hardcode these behaviors for Phase 1 and defer customization settings UI to Phase 5** | Defer customization settings to Phase 5 | ✓ |
| Implement custom setting configuration in the UI immediately in Phase 1 | Build settings panel in Phase 1 | |
| You decide | Agent determines | |

**User's choice:** Hardcode these behaviors for Phase 1 and defer customization settings UI to Phase 5 (Desktop Convenience / Settings)
**Notes:** Keeps Phase 1 focused on the core tracer bullet.

---

## Metadata Findings UI Display

### Question: What level of detail should the UI show for stripped metadata?

| Option | Description | Selected |
|--------|-------------|----------|
| **(Recommended) Grouped categories with expandable detailed field names** | GPS, Camera, etc. with expandable list (simple first, transparent on demand) | ✓ |
| Raw tag detail list | Displays a detailed table showing all raw tags that were stripped and their values | |
| Simple outcome summary | Displays only a simple message like '12 metadata items stripped' | |
| You decide | Agent determines | |

**User's choice:** Grouped categories (e.g., GPS, Camera, Date & Time, Software) with expandable detailed field names (simple first, transparent on demand)
**Notes:** Simple first, transparent on demand, matching the mobile app.

---

### Question: Where should the cleaning findings be displayed in the layout?

| Option | Description | Selected |
|--------|-------------|----------|
| **(Recommended) Inline in the main window** | Replacing or expanding the drop zone area with the result details | ✓ |
| A popup modal/dialog window | Opens a separate overlay or dialog window | |
| You decide | Agent determines | |

**User's choice:** Inline in the main window (replacing or expanding the drop zone area with the result details, keeping a single-window flow)
**Notes:** Keeps UI clean and single-window focused.

---

### Question: How should the original and clean files be displayed/accessed in the UI?

| Option | Description | Selected |
|--------|-------------|----------|
| **(Recommended) Show paths for both files with a quick-access button to 'Open folder'** | Path fields plus quick button | ✓ |
| Show a side-by-side comparison with details | File size differences and thumbnails for both | |
| You decide | Agent determines | |

**User's choice:** Show paths for both files with a quick-access button to 'Open folder' containing the clean copy
**Notes:** Provides convenient access to the resulting file.

---

## the agent's Discretion
- Visual styling of the inline result panel.
- Schema for local history persistence.
- Specific implementation of auto-incrementing filename collision handler.

## Deferred Ideas
- Folders and naming customization (deferred to Phase 5).
- Advanced file format support (HEIC, etc., deferred to Phase 4).
- Clipboard monitoring and system tray residency (deferred to Phase 5).
- History page/viewer UI and filtering (deferred to Phase 3).
