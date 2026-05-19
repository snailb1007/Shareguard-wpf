# Phase 3: Multi-file and History Slice Design

Date: 2026-05-20
Status: Approved for planning

## Goal

Phase 3 adds the MVP batch-processing and persistent local history slice for ShareGuard WPF.

The phase should let users queue multiple image files, process them without modifying originals, see per-file results, and reopen the app later with a persistent history of previous image and URL cleaning operations.

## Scope

In scope:

- Batch processing for image files supported by the Phase 1 image cleanup pipeline.
- Persistent operation history for image clean operations and URL clean operations from Phase 2.
- A batch result grid that shows status, output path, finding counts, and quick actions.
- A recent history view/list loaded from a local SQLite database.
- Per-item failure handling so one failed file does not stop the full batch.

Out of scope:

- PDF, Office, video, and advanced file type cleaning.
- Full-text history search, export, sync, or cloud backup.
- Replacing originals or changing the Phase 1 clean-copy naming policy.
- Dynamic URL ruleset download or redirect unwrapping.

## Recommended Approach

Use a focused Phase 3 slice: batch image processing plus unified persistent history.

This keeps Phase 3 aligned with REQ-04 while preserving the roadmap boundary. It uses existing Phase 1 and Phase 2 behavior instead of creating a generic router for future file types too early.

Alternatives considered:

- Generic batch router now: more flexible, but it pulls Phase 4 concerns into Phase 3 and risks premature abstractions.
- History first, batch second: lower risk, but it under-delivers the multi-file success criterion.

## Architecture

### Domain

Define stable history abstractions in `ShareGuard.Domain`:

- `HistoryEntry`
- `HistoryOperationType`
- `HistoryOperationStatus`
- `IHistoryRepository`

The domain model should describe what happened without referencing EF Core, SQLite, WPF, or Windows APIs.

Recommended history fields:

- `Id`
- `OccurredAtUtc`
- `OperationType`
- `Status`
- `SourceSummary`
- `OutputSummary`
- `FindingCount`
- `RemovedItemCount`
- `ErrorCode`
- `ErrorMessage`

File operations may store source and output paths because users need to locate generated clean copies. URL operations should avoid storing the raw dirty URL by default. Store a privacy-safe summary such as host, removed parameter count, and clean URL only if Phase 2 already exposes that behavior intentionally.

### Application

Add an application service boundary:

- `IBatchProcessingService`
- `BatchProcessingService`
- `BatchProcessingRequest`
- `BatchProcessingResult`
- `BatchFileResult`
- `BatchProcessingProgress`

`BatchProcessingService` should orchestrate work, not strip metadata itself. It should call the Phase 1 image cleanup service for each supported image file, report progress after each file completes, and write history entries through `IHistoryRepository`.

Batch processing must use bounded concurrency. Do not use unbounded `Task.WhenAll(files.Select(...))` for arbitrary user-selected file lists. Use `Parallel.ForEachAsync` or equivalent throttled scheduling with a conservative default based on processor count.

### Infrastructure

Use SQLite with EF Core for persistent history.

Store the database in:

```text
%LocalAppData%\ShareGuard\history.db
```

Register the context with `IDbContextFactory<ShareGuardHistoryDbContext>` so background and parallel operations create separate `DbContext` instances. EF Core `DbContext` instances must not be shared across concurrent file operations.

Schema changes should be managed by EF Core migrations. Avoid hand-written SQLite table creation and avoid growing JSON files for history storage.

### WPF

Keep WPF responsible for presentation and user interaction only.

The main ViewModel should expose:

- Selected/queued files.
- Current batch progress.
- Per-file result rows.
- Recent history rows.
- Commands for adding files, clearing queue, starting batch, cancelling batch, and opening an output folder.

The main window should include:

- A multi-file queue or result grid.
- Batch summary counters: total, completed, failed, skipped.
- A recent history list loaded on startup and refreshed after successful operations.
- A simple filter for operation type or status if it fits the existing layout without introducing a full history page.

Use WPF virtualization for any history list that can grow beyond a small recent set.

## Data Flow

1. User drops or selects multiple image files.
2. WPF ViewModel creates a `BatchProcessingRequest`.
3. Application validates files, marks unsupported items as skipped or failed, and processes supported image files with bounded parallelism.
4. Each file delegates to the existing image cleanup service.
5. Each result is written to history through `IHistoryRepository`.
6. Progress is reported to the ViewModel after each file completes.
7. The batch grid shows completed, failed, and skipped rows.
8. The recent history list reloads from SQLite after the batch completes.

URL cleaning from Phase 2 should log through the same history repository, producing `HistoryOperationType.UrlClean` entries.

## Error Handling

Batch processing is per-item resilient. A locked, corrupt, unsupported, missing, or access-denied file must not stop the rest of the batch.

Each failed row should show:

- File name or safe source summary.
- `Failed` status.
- A short user-readable error.

The application may log technical details internally, but history should store concise safe error details rather than stack traces.

Cancellation should stop scheduling additional work and allow in-flight file operations to observe the cancellation token. Already completed results remain visible and logged.

Original files must never be modified, including in failure and cancellation paths.

## Privacy Rules

ShareGuard remains local-only. Phase 3 must not introduce network calls for cleaning, history, or analytics.

History should be useful without becoming a privacy liability:

- Store file paths only for local file operations where quick access is a user-facing requirement.
- Do not store raw dirty URLs by default.
- Do not store file contents, metadata payloads, or full exception dumps.
- Keep history under the user's local profile.

## Testing Strategy

Application tests:

- Processes multiple supported image files and returns one result per file.
- Continues processing when one item fails.
- Marks unsupported files without invoking image cleanup.
- Reports progress per completed item.
- Honors cancellation.
- Uses bounded concurrency rather than unbounded task fan-out.

Infrastructure tests:

- Creates and queries history entries in a temporary SQLite database.
- Supports latest-history queries ordered by most recent first.
- Persists image and URL operation types.
- Uses independent contexts through a context factory.

ViewModel tests:

- Adds multiple files to the queue.
- Starts batch processing and updates counters/result rows.
- Refreshes history after completion.
- Handles cancellation and partial failures.

Architecture tests:

- Domain has no EF Core, SQLite, WPF, or Infrastructure references.
- Application has no WPF references.
- Infrastructure owns EF Core implementation details.

## Acceptance Criteria

- User can queue and process multiple supported image files in one batch.
- Each source image is preserved unchanged.
- Each successful output uses the existing clean-copy naming policy.
- Failed files do not prevent other files from completing.
- The UI shows per-file status and output/failure summaries.
- Operation history persists after app restart.
- History includes both image and URL clean operations.
- No network calls are introduced for Phase 3 functionality.

## Planning Notes

The implementation plan should start by completing or verifying Phase 1 and Phase 2 integration points:

- Image cleanup service contract exists and can process one file.
- URL cleaning path can call a history logging abstraction.
- Dependency injection registration is in place for application and infrastructure services.

If Phase 1 still uses a temporary JSON-lines history logger, Phase 3 should migrate the history boundary to `IHistoryRepository` and keep compatibility only where needed for tests.
