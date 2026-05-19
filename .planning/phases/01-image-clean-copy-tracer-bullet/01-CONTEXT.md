# Phase 1: Image Clean Copy Tracer Bullet - Context

**Gathered:** 2026-05-20
**Status:** Ready for planning

<domain>
## Phase Boundary

Phase 1 delivers the first end-to-end tracer bullet flow for image file privacy. The user can drag and drop a JPEG, PNG, or WEBP image into the WPF application (or select one via a browse button). The application processes the image using SixLabors.ImageSharp to strip privacy-leaking metadata (such as GPS/location data, camera details, timestamps, and software signatures) without modifying the original source file. It creates a clean copy in the same directory as the original, resolves name conflicts via auto-incrementing, displays a categorized summary of stripped metadata inline in the main window, provides quick-access buttons to open the folder, and logs a summary to the local history.

</domain>

<decisions>
## Implementation Decisions

### Output Saving & Naming
- **D-01:** The cleaned copy of the image must be saved in the same directory as the original file.
- **D-02:** Cleaned files must be named by appending `.clean` before the original file extension (e.g., `image.jpg` becomes `image.clean.jpg`).
- **D-03:** If a cleaned copy with the target name already exists, the application must resolve the conflict by auto-incrementing the name (e.g., `image.clean (1).jpg`, `image.clean (2).jpg`).
- **D-04:** Customization of target directories and naming conventions will be hardcoded as defaults for Phase 1 and deferred to Phase 5.

### Metadata Findings UI Display
- **D-05:** Cleaning results and findings must be displayed inline in the main window, replacing/expanding the drop zone area to keep a single-window flow.
- **D-06:** Stripped metadata findings must be grouped into human-readable categories (e.g., GPS/Location, Camera/Device, Date & Time, Software/XMP) with expandable detailed field names showing what was removed.
- **D-07:** The UI must display the file paths for both the original file and the newly created clean file, alongside a quick-access button to "Open folder" containing the clean copy.

### the agent's Discretion
- The exact layout structure and visual style of the inline result panel.
- The visual design of the expandable detailed list of stripped tags.
- The specific implementation logic for the auto-incrementing suffix to handle filename collisions.
- The local history persistence schema and format (e.g., lightweight JSON in AppData).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Architecture & Design
- `docs/architecture.md` — Approved architecture blueprint (Clean Architecture + MVVM, solution structure, tech stack, implementation rules).
- `docs/superpowers/specs/2026-05-19-shareguard-wpf-product-design.md` — Full product design & research (entry points, Android parity concepts, tracer-bullet roadmap).

### Phase 1 Research
- `.planning/phases/01-image-clean-copy-tracer-bullet/01-RESEARCH.md` — Technology choices (ImageSharp, CommunityToolkit.Mvvm), file locking considerations, and metadata stripping code examples.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `Shareguard-wpf/ViewModels/MainViewModel.cs` — Initial MainViewModel using CommunityToolkit.Mvvm `ObservableObject` and `[ObservableProperty]`.
- `Shareguard-wpf/App.xaml.cs` — WPF application entry point hosting the .NET Generic Host and registering services for dependency injection.

### Established Patterns
- Clean Architecture separation of concerns: core domain interfaces in `ShareGuard.Domain`, application service commands in `ShareGuard.Application`, concrete implementations (e.g., ImageSharp metadata cleaning) in `ShareGuard.Infrastructure`, and WPF views/ViewModels in `Shareguard-wpf`.
- Centralized package version management in `Directory.Packages.props`.

### Integration Points
- `MainViewModel.cs` properties and commands (`Loaded` event command, status message, title).
- `MainWindow.xaml` grid layout for holding the drag-and-drop zone and results panel.
- `DependencyInjection.cs` static classes and extension methods in the Application and Infrastructure layers to register services.

</code_context>

<specifics>
## Specific Ideas

- The main window should show a clear drag-and-drop zone with a fallback "Browse" button.
- When an image is dropped, a progress indicator or loading state should be shown while ImageSharp strips the metadata on a background thread.
- Upon completion, the result screen should clearly show the location of the clean copy.

</specifics>

<deferred>
## Deferred Ideas

- User-customizable output directories and renaming formats (deferred to Phase 5).
- Advanced image formats like HEIC (deferred to Phase 4).
- Explorer context menu integration (deferred to Phase 7).
- Clipboard monitoring and system tray residency (deferred to Phase 5).
- Local history page with filters and details (deferred to Phase 3).

</deferred>

---

*Phase: 01-image-clean-copy-tracer-bullet*
*Context gathered: 2026-05-20*
