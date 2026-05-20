# Phase 3: Multi-file and History Slice Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Enable batch processing of multiple files in parallel and persist operation history in a local SQLite database using Entity Framework Core, including a paginated and virtualized history viewer.

**Architecture:** Domain entities and interface `IHistoryRepository` in Domain layer, SQLite `DbContext` and EF Core repository implementation in Infrastructure layer, CQRS-style history services (`IHistoryService`) and parallel processor (`IMultiFileProcessorService` using `Parallel.ForEachAsync`) in Application layer, and a WPF tabbed layout ("Clean" and "History" tabs) with UI virtualization.

**Tech Stack:** Microsoft.EntityFrameworkCore.Sqlite 10.0.8, Microsoft.EntityFrameworkCore.Design 10.0.8, System.Threading.Tasks.Parallel (ForEachAsync), WPF VirtualizingStackPanel, xUnit v3, CommunityToolkit.Mvvm

---

## File Structure

| File | Responsibility |
|------|---------------|
| `Directory.Packages.props` | [MODIFY] Add EF Core SQLite and Design versions |
| `ShareGuard.Infrastructure/ShareGuard.Infrastructure.csproj` | [MODIFY] Add EF Core PackageReferences |
| `Shareguard-wpf/Shareguard-wpf.csproj` | [MODIFY] Add EF Core Design PackageReference |
| `ShareGuard.Application.Tests/ShareGuard.Application.Tests.csproj` | [MODIFY] Add Infrastructure project reference for db testing |
| `ShareGuard.Domain/Entities/HistoryEvent.cs` | [NEW] Domain model for database operation history |
| `ShareGuard.Domain/Interfaces/IHistoryRepository.cs` | [NEW] Domain repository interface for SQLite history operations |
| `ShareGuard.Infrastructure/Data/ShareGuardDbContext.cs` | [NEW] EF Core SQLite DbContext |
| `ShareGuard.Infrastructure/Repositories/HistoryRepository.cs` | [NEW] DB-backed implementation of IHistoryRepository |
| `ShareGuard.Infrastructure/DependencyInjection.cs` | [MODIFY] Register SQLite DbContextFactory and IHistoryRepository |
| `ShareGuard.Application/Commands/LogHistoryCommand.cs` | [NEW] Command containing data to log a clean operation |
| `ShareGuard.Application/Queries/GetHistoryQuery.cs` | [NEW] Query representing paginated history request |
| `ShareGuard.Application/Interfaces/IHistoryService.cs` | [NEW] Application service interface separating read/write operations |
| `ShareGuard.Application/Services/HistoryService.cs` | [NEW] History command/query coordinator |
| `ShareGuard.Application/Services/IMultiFileProcessorService.cs` | [NEW] Application service interface for batch processing |
| `ShareGuard.Application/Services/MultiFileProcessorService.cs` | [NEW] Parallel file processing orchestrator |
| `ShareGuard.Application/DependencyInjection.cs` | [MODIFY] Register HistoryService and MultiFileProcessorService |
| `ShareGuard.Application/Services/ImageCleanupService.cs` | [MODIFY] Update to inject IHistoryService instead of obsolete LocalHistoryLogger |
| `ShareGuard.Infrastructure/Services/LocalHistoryLogger.cs` | [DELETE] Remove deprecated JSON history logger |
| `ShareGuard.Application.Tests/HistoryRepositoryTests.cs` | [NEW] SQLite In-Memory database integration tests |
| `ShareGuard.Application.Tests/MultiFileProcessorServiceTests.cs` | [NEW] Parallel processing unit tests |
| `Shareguard-wpf/ViewModels/MainViewModel.cs` | [MODIFY] Add batch operations, progress tracking, and history properties |
| `Shareguard-wpf/MainWindow.xaml` | [MODIFY] Introduce TabControl with virtualized list view for history and batch results grid |
| `Shareguard-wpf/MainWindow.xaml.cs` | [MODIFY] Parse multi-file drag-and-drop events |
| `Shareguard-wpf/App.xaml.cs` | [MODIFY] Execute database migrations on application startup |

---

### Task 1: Reference EF Core Dependencies and Project Setup

**Files:**
- Modify: `Directory.Packages.props`
- Modify: `ShareGuard.Infrastructure/ShareGuard.Infrastructure.csproj`
- Modify: `Shareguard-wpf/Shareguard-wpf.csproj`
- Modify: `ShareGuard.Application.Tests/ShareGuard.Application.Tests.csproj`

- [ ] **Step 1: Centralize EF Core Package Versions**

Open `c:\Users\ADMIN\source\repos\Shareguard-wpf\Directory.Packages.props` and add `<PackageVersion>` elements for Entity Framework Core SQLite and Design inside the existing `<ItemGroup>`, right after the DI Abstractions:

```xml
    <!-- Database -->
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.8" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.8" />
```

- [ ] **Step 2: Add PackageReferences to Class Libraries**

Open `c:\Users\ADMIN\source\repos\Shareguard-wpf\ShareGuard.Infrastructure\ShareGuard.Infrastructure.csproj` and add the SQLite and Design PackageReferences:

```xml
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" />
  </ItemGroup>
```

Open `c:\Users\ADMIN\source\repos\Shareguard-wpf\Shareguard-wpf\Shareguard-wpf.csproj` and add the Design PackageReference so that migration commands can run:

```xml
  <ItemGroup>
    <PackageReference Include="CommunityToolkit.Mvvm" />
    <PackageReference Include="Microsoft.Extensions.Hosting" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" />
  </ItemGroup>
```

- [ ] **Step 3: Update Test Project Dependencies**

Open `c:\Users\ADMIN\source\repos\Shareguard-wpf\ShareGuard.Application.Tests\ShareGuard.Application.Tests.csproj` and add a reference to `ShareGuard.Infrastructure` so it can test database code, along with SQLite:

```xml
  <ItemGroup>
    <ProjectReference Include="..\ShareGuard.Application\ShareGuard.Application.csproj" />
    <ProjectReference Include="..\ShareGuard.Infrastructure\ShareGuard.Infrastructure.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="xunit.v3" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit.runner.visualstudio">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="coverlet.collector">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" />
  </ItemGroup>
```

- [ ] **Step 4: Restore and Verify Packages**

Run the following command to restore and build:

```powershell
dotnet build Shareguard-wpf.slnx
```

Expected: NuGet restores packages successfully, and the solution compiles with no errors.

- [ ] **Step 5: Commit changes**

```powershell
git add Directory.Packages.props ShareGuard.Infrastructure/ShareGuard.Infrastructure.csproj Shareguard-wpf/Shareguard-wpf.csproj ShareGuard.Application.Tests/ShareGuard.Application.Tests.csproj
git commit -m "build: reference EF Core SQLite 10.0.8 packages and test dependencies"
```

---

### Task 2: Create History Event Entity and Repository Interface

**Files:**
- Create: `ShareGuard.Domain/Entities/HistoryEvent.cs`
- Create: `ShareGuard.Domain/Interfaces/IHistoryRepository.cs`

- [ ] **Step 1: Write failing compile check test**

Create `c:\Users\ADMIN\source\repos\Shareguard-wpf\ShareGuard.Domain.Tests\HistoryRepositoryCompilationTests.cs`:

```csharp
using Xunit;

namespace ShareGuard.Domain.Tests;

public class HistoryRepositoryCompilationTests
{
    [Fact]
    public void VerifyEntitiesExist()
    {
        // This test will fail to compile initially because HistoryEvent is missing
        Assert.True(false, "HistoryEvent type not defined yet");
    }
}
```

- [ ] **Step 2: Run test to verify compile failure**

```powershell
dotnet test ShareGuard.Domain.Tests/ShareGuard.Domain.Tests.csproj --filter "VerifyEntitiesExist"
```

Expected: Fails to compile or run, stating that classes do not exist.

- [ ] **Step 3: Define HistoryEvent Entity**

Create `c:\Users\ADMIN\source\repos\Shareguard-wpf\ShareGuard.Domain\Entities\HistoryEvent.cs`:

```csharp
namespace ShareGuard.Domain.Entities;

/// <summary>
/// Represents a record of a file or URL cleaning action in the local database.
/// </summary>
public class HistoryEvent
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string OriginalPath { get; set; } = string.Empty;
    public string CleanPath { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; }
    public int FindingsCount { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
}
```

- [ ] **Step 4: Create IHistoryRepository Interface**

Create `c:\Users\ADMIN\source\repos\Shareguard-wpf\ShareGuard.Domain\Interfaces\IHistoryRepository.cs`:

```csharp
using ShareGuard.Domain.Entities;

namespace ShareGuard.Domain.Interfaces;

/// <summary>
/// Domain-level interface defining persistence actions for operation history.
/// </summary>
public interface IHistoryRepository
{
    /// <summary>
    /// Persists a new history event record.
    /// </summary>
    Task AddAsync(HistoryEvent historyEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches a paged list of history events ordered descending by processing timestamp.
    /// </summary>
    Task<IEnumerable<HistoryEvent>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the total count of history records in the database.
    /// </summary>
    Task<int> GetTotalCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Wipes all history records.
    /// </summary>
    Task ClearAllAsync(CancellationToken cancellationToken = default);
}
```

- [ ] **Step 5: Replace compile test and run**

Replace `c:\Users\ADMIN\source\repos\Shareguard-wpf\ShareGuard.Domain.Tests\HistoryRepositoryCompilationTests.cs` with:

```csharp
using ShareGuard.Domain.Entities;
using ShareGuard.Domain.Interfaces;
using Xunit;

namespace ShareGuard.Domain.Tests;

public class HistoryRepositoryCompilationTests
{
    [Fact]
    public void VerifyEntitiesExist()
    {
        var evt = new HistoryEvent { Id = Guid.NewGuid(), FileName = "test.png" };
        Assert.NotNull(evt);
        Assert.Equal("test.png", evt.FileName);
    }
}
```

Run test:
```powershell
dotnet test ShareGuard.Domain.Tests/ShareGuard.Domain.Tests.csproj --filter "VerifyEntitiesExist"
```

Expected: PASS

- [ ] **Step 6: Commit changes**

```powershell
git add ShareGuard.Domain/Entities/HistoryEvent.cs ShareGuard.Domain/Interfaces/IHistoryRepository.cs ShareGuard.Domain.Tests/HistoryRepositoryCompilationTests.cs
git commit -m "feat: define HistoryEvent entity and IHistoryRepository domain interface"
```

---

### Task 3: Implement ShareGuardDbContext and HistoryRepository

**Files:**
- Create: `ShareGuard.Infrastructure/Data/ShareGuardDbContext.cs`
- Create: `ShareGuard.Infrastructure/Repositories/HistoryRepository.cs`
- Modify: `ShareGuard.Infrastructure/DependencyInjection.cs`

- [ ] **Step 1: Create ShareGuardDbContext**

Create `c:\Users\ADMIN\source\repos\Shareguard-wpf\ShareGuard.Infrastructure\Data\ShareGuardDbContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using ShareGuard.Domain.Entities;

namespace ShareGuard.Infrastructure.Data;

public class ShareGuardDbContext : DbContext
{
    public DbSet<HistoryEvent> HistoryEvents => Set<HistoryEvent>();

    public ShareGuardDbContext(DbContextOptions<ShareGuardDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<HistoryEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.OriginalPath).IsRequired();
            entity.Property(e => e.CleanPath).IsRequired();
            entity.Property(e => e.ProcessedAt).IsRequired();
            entity.Property(e => e.IsSuccess).IsRequired();
        });
    }
}
```

- [ ] **Step 2: Create HistoryRepository**

Create `c:\Users\ADMIN\source\repos\Shareguard-wpf\ShareGuard.Infrastructure\Repositories\HistoryRepository.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using ShareGuard.Domain.Entities;
using ShareGuard.Domain.Interfaces;
using ShareGuard.Infrastructure.Data;

namespace ShareGuard.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of IHistoryRepository. Uses DbContextFactory to ensure thread safety
/// when multiple operations are processed in parallel.
/// </summary>
public class HistoryRepository : IHistoryRepository
{
    private readonly IDbContextFactory<ShareGuardDbContext> _contextFactory;

    public HistoryRepository(IDbContextFactory<ShareGuardDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task AddAsync(HistoryEvent historyEvent, CancellationToken cancellationToken = default)
    {
        using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await context.HistoryEvents.AddAsync(historyEvent, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<HistoryEvent>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.HistoryEvents
            .AsNoTracking()
            .OrderByDescending(e => e.ProcessedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetTotalCountAsync(CancellationToken cancellationToken = default)
    {
        using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.HistoryEvents.CountAsync(cancellationToken);
    }

    public async Task ClearAllAsync(CancellationToken cancellationToken = default)
    {
        using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM HistoryEvents", cancellationToken);
    }
}
```

- [ ] **Step 3: Modify Infrastructure Dependency Injection**

Replace `c:\Users\ADMIN\source\repos\Shareguard-wpf\ShareGuard.Infrastructure\DependencyInjection.cs` to register the db context factory and repository:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShareGuard.Domain.Interfaces;
using ShareGuard.Infrastructure.Data;
using ShareGuard.Infrastructure.Repositories;

namespace ShareGuard.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        // Place SQLite database in AppData folder
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string dbFolder = Path.Combine(appData, "ShareGuard");
        string dbPath = Path.Combine(dbFolder, "history.db");

        // Ensure directory exists
        Directory.CreateDirectory(dbFolder);

        // Add context factory to guarantee thread safety during parallel operations
        services.AddDbContextFactory<ShareGuardDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        // Register repository
        services.AddSingleton<IHistoryRepository, HistoryRepository>();

        return services;
    }
}
```

- [ ] **Step 4: Build project**

```powershell
dotnet build ShareGuard.Infrastructure/ShareGuard.Infrastructure.csproj
```

Expected: Build succeeds with zero errors.

- [ ] **Step 5: Generate Initial EF Core Migration**

Install the `dotnet-ef` global CLI tool if it is not already installed:

```powershell
dotnet tool install --global dotnet-ef
```

Generate the database migration files under `ShareGuard.Infrastructure` using `Shareguard-wpf` as the startup target:

```powershell
dotnet ef migrations add InitialCreate --project ShareGuard.Infrastructure --startup-project Shareguard-wpf
```

Expected: Command executes successfully and outputs the initial migration C# files in `ShareGuard.Infrastructure/Migrations`.

- [ ] **Step 6: Commit changes**

```powershell
git add ShareGuard.Infrastructure/Data/ShareGuardDbContext.cs ShareGuard.Infrastructure/Repositories/HistoryRepository.cs ShareGuard.Infrastructure/DependencyInjection.cs ShareGuard.Infrastructure/Migrations
git commit -m "feat: implement SQLite DbContext and HistoryRepository with database schema migrations"
```

---

### Task 4: Implement CQRS Commands, Queries, and History Service

**Files:**
- Create: `ShareGuard.Application/Commands/LogHistoryCommand.cs`
- Create: `ShareGuard.Application/Queries/GetHistoryQuery.cs`
- Create: `ShareGuard.Application/Interfaces/IHistoryService.cs`
- Create: `ShareGuard.Application/Services/HistoryService.cs`
- Modify: `ShareGuard.Application/DependencyInjection.cs`

- [ ] **Step 1: Write compilation test**

Create `c:\Users\ADMIN\source\repos\Shareguard-wpf\ShareGuard.Application.Tests\HistoryServiceCompilationTests.cs`:

```csharp
using Xunit;

namespace ShareGuard.Application.Tests;

public class HistoryServiceCompilationTests
{
    [Fact]
    public void TestCompile()
    {
        Assert.True(false, "IHistoryService not created");
    }
}
```

- [ ] **Step 2: Run test to confirm it fails**

```powershell
dotnet test ShareGuard.Application.Tests/ShareGuard.Application.Tests.csproj --filter "HistoryServiceCompilationTests"
```

Expected: FAIL

- [ ] **Step 3: Define LogHistoryCommand**

Create `c:\Users\ADMIN\source\repos\Shareguard-wpf\ShareGuard.Application\Commands\LogHistoryCommand.cs`:

```csharp
namespace ShareGuard.Application.Commands;

/// <summary>
/// Command to write a new run summary to the history database.
/// </summary>
public record LogHistoryCommand(
    string FileName,
    string OriginalPath,
    string CleanPath,
    int FindingsCount,
    bool IsSuccess,
    string? ErrorMessage = null);
```

- [ ] **Step 4: Define GetHistoryQuery**

Create `c:\Users\ADMIN\source\repos\Shareguard-wpf\ShareGuard.Application\Queries\GetHistoryQuery.cs`:

```csharp
namespace ShareGuard.Application.Queries;

/// <summary>
/// Query to request paginated history results.
/// </summary>
public record GetHistoryQuery(int PageNumber, int PageSize);
```

- [ ] **Step 5: Create IHistoryService Interface**

Create `c:\Users\ADMIN\source\repos\Shareguard-wpf\ShareGuard.Application\Interfaces\IHistoryService.cs`:

```csharp
using ShareGuard.Application.Commands;
using ShareGuard.Application.Queries;
using ShareGuard.Domain.Entities;

namespace ShareGuard.Application.Interfaces;

/// <summary>
/// Orchestrates reads and writes to the operation history repository.
/// </summary>
public interface IHistoryService
{
    Task LogHistoryAsync(LogHistoryCommand command, CancellationToken cancellationToken = default);
    Task<IEnumerable<HistoryEvent>> GetHistoryAsync(GetHistoryQuery query, CancellationToken cancellationToken = default);
    Task<int> GetTotalCountAsync(CancellationToken cancellationToken = default);
    Task ClearHistoryAsync(CancellationToken cancellationToken = default);
}
```

- [ ] **Step 6: Implement HistoryService**

Create `c:\Users\ADMIN\source\repos\Shareguard-wpf\ShareGuard.Application\Services\HistoryService.cs`:

```csharp
using ShareGuard.Application.Commands;
using ShareGuard.Application.Interfaces;
using ShareGuard.Application.Queries;
using ShareGuard.Domain.Entities;
using ShareGuard.Domain.Interfaces;

namespace ShareGuard.Application.Services;

public class HistoryService : IHistoryService
{
    private readonly IHistoryRepository _historyRepository;

    public HistoryService(IHistoryRepository historyRepository)
    {
        _historyRepository = historyRepository;
    }

    public async Task LogHistoryAsync(LogHistoryCommand command, CancellationToken cancellationToken = default)
    {
        var historyEvent = new HistoryEvent
        {
            Id = Guid.NewGuid(),
            FileName = command.FileName,
            OriginalPath = command.OriginalPath,
            CleanPath = command.CleanPath,
            ProcessedAt = DateTime.UtcNow,
            FindingsCount = command.FindingsCount,
            IsSuccess = command.IsSuccess,
            ErrorMessage = command.ErrorMessage
        };

        await _historyRepository.AddAsync(historyEvent, cancellationToken);
    }

    public async Task<IEnumerable<HistoryEvent>> GetHistoryAsync(GetHistoryQuery query, CancellationToken cancellationToken = default)
    {
        if (query.PageNumber <= 0) throw new ArgumentException("Page number must be positive.", nameof(query));
        if (query.PageSize <= 0) throw new ArgumentException("Page size must be positive.", nameof(query));

        return await _historyRepository.GetPagedAsync(query.PageNumber, query.PageSize, cancellationToken);
    }

    public async Task<int> GetTotalCountAsync(CancellationToken cancellationToken = default)
    {
        return await _historyRepository.GetTotalCountAsync(cancellationToken);
    }

    public async Task ClearHistoryAsync(CancellationToken cancellationToken = default)
    {
        await _historyRepository.ClearAllAsync(cancellationToken);
    }
}
```

- [ ] **Step 7: Update Application DependencyInjection**

Modify `c:\Users\ADMIN\source\repos\Shareguard-wpf\ShareGuard.Application\DependencyInjection.cs` to register the service:

```csharp
using Microsoft.Extensions.DependencyInjection;
using ShareGuard.Application.Interfaces;
using ShareGuard.Application.Services;

namespace ShareGuard.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // URL cleaner from Phase 2 (assumed registered)
        // services.AddSingleton<IUrlCleanerService, UrlCleanerService>();

        // Register history service
        services.AddSingleton<IHistoryService, HistoryService>();

        return services;
    }
}
```

- [ ] **Step 8: Replace compile test and run**

Replace `c:\Users\ADMIN\source\repos\Shareguard-wpf\ShareGuard.Application.Tests\HistoryServiceCompilationTests.cs` with:

```csharp
using ShareGuard.Application.Interfaces;
using ShareGuard.Application.Services;
using ShareGuard.Domain.Interfaces;
using Moq;
using Xunit;

namespace ShareGuard.Application.Tests;

public class HistoryServiceCompilationTests
{
    [Fact]
    public void TestCompile()
    {
        var mockRepo = new Mock<IHistoryRepository>();
        var service = new HistoryService(mockRepo.Object);
        Assert.NotNull(service);
    }
}
```

Run test:
```powershell
dotnet test ShareGuard.Application.Tests/ShareGuard.Application.Tests.csproj --filter "HistoryServiceCompilationTests"
```

Expected: PASS

- [ ] **Step 9: Commit changes**

```powershell
git add ShareGuard.Application/Commands/LogHistoryCommand.cs ShareGuard.Application/Queries/GetHistoryQuery.cs ShareGuard.Application/Interfaces/IHistoryService.cs ShareGuard.Application/Services/HistoryService.cs ShareGuard.Application/DependencyInjection.cs ShareGuard.Application.Tests/HistoryServiceCompilationTests.cs
git commit -m "feat: add application commands, queries, and IHistoryService implementation"
```

---

### Task 5: Implement Parallel Multi-File Processor Service

**Files:**
- Create: `ShareGuard.Application/Services/IMultiFileProcessorService.cs`
- Create: `ShareGuard.Application/Services/MultiFileProcessorService.cs`
- Modify: `ShareGuard.Application/DependencyInjection.cs`

- [ ] **Step 1: Define ProcessingStatus Record**

First, create `c:\Users\ADMIN\source\repos\Shareguard-wpf\ShareGuard.Application\Services\IMultiFileProcessorService.cs` and define the progress status payload and service interface:

```csharp
namespace ShareGuard.Application.Services;

/// <summary>
/// Status update containing information about a finished file inside a batch.
/// </summary>
public record ProcessingStatus(
    int CurrentCount,
    int TotalCount,
    string FilePath,
    bool Success,
    string CleanPath,
    int FindingsCount,
    string? ErrorMessage = null);

/// <summary>
/// Coordinates batch operations by cleaning multiple files in parallel.
/// </summary>
public interface IMultiFileProcessorService
{
    /// <summary>
    /// Strips metadata from multiple files concurrently using Parallel.ForEachAsync.
    /// Reports progress safely to the calling thread.
    /// </summary>
    Task ProcessFilesAsync(
        IEnumerable<string> filePaths,
        IProgress<ProcessingStatus> progress,
        CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2: Implement MultiFileProcessorService**

Create `c:\Users\ADMIN\source\repos\Shareguard-wpf\ShareGuard.Application\Services\MultiFileProcessorService.cs`:

```csharp
using ShareGuard.Application.Services;

namespace ShareGuard.Application.Services;

public class MultiFileProcessorService : IMultiFileProcessorService
{
    private readonly IImageCleanupService _imageCleanupService;

    public MultiFileProcessorService(IImageCleanupService imageCleanupService)
    {
        _imageCleanupService = imageCleanupService;
    }

    public async Task ProcessFilesAsync(
        IEnumerable<string> filePaths,
        IProgress<ProcessingStatus> progress,
        CancellationToken cancellationToken = default)
    {
        var filesList = filePaths.ToList();
        if (filesList.Count == 0) return;

        int totalCount = filesList.Count;
        int currentProcessedCount = 0;

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            CancellationToken = cancellationToken
        };

        // Leverage modern Parallel.ForEachAsync (.NET 10)
        await Parallel.ForEachAsync(filesList, options, async (filePath, ct) =>
        {
            bool success = false;
            string cleanPath = string.Empty;
            int findingsCount = 0;
            string? errorMessage = null;

            try
            {
                // CleanImageAsync is from Phase 1, it handles cleaning individual images
                // and internally logs to the history DB context.
                var result = await _imageCleanupService.CleanImageAsync(filePath, ct);
                success = result.Success;
                cleanPath = result.CleanPath;
                findingsCount = result.Findings.Count;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
            }

            int currentCount = Interlocked.Increment(ref currentProcessedCount);

            // Thread-safe progress reporting. Progress<T> handles UI-thread marshaling.
            progress?.Report(new ProcessingStatus(
                currentCount,
                totalCount,
                filePath,
                success,
                cleanPath,
                findingsCount,
                errorMessage));
        });
    }
}
```

- [ ] **Step 3: Register service in DependencyInjection.cs**

Open `c:\Users\ADMIN\source\repos\Shareguard-wpf\ShareGuard.Application\DependencyInjection.cs` and register the processor in `AddApplicationServices()`:

```csharp
        services.AddSingleton<IHistoryService, HistoryService>();
        services.AddSingleton<IMultiFileProcessorService, MultiFileProcessorService>();
```

- [ ] **Step 4: Build project**

```powershell
dotnet build ShareGuard.Application/ShareGuard.Application.csproj
```

Expected: Build succeeds.

- [ ] **Step 5: Commit changes**

```powershell
git add ShareGuard.Application/Services/IMultiFileProcessorService.cs ShareGuard.Application/Services/MultiFileProcessorService.cs ShareGuard.Application/DependencyInjection.cs
git commit -m "feat: implement IMultiFileProcessorService using Parallel.ForEachAsync"
```

---

### Task 6: Refactor ImageCleanupService and Remove JSON Logger

**Files:**
- Modify: `ShareGuard.Application/Services/ImageCleanupService.cs`
- Delete: `ShareGuard.Infrastructure/Services/LocalHistoryLogger.cs`

- [ ] **Step 1: Refactor ImageCleanupService to log to database**

Modify `c:\Users\ADMIN\source\repos\Shareguard-wpf\ShareGuard.Application\Services\ImageCleanupService.cs` to inject `IHistoryService` instead of `IHistoryLogger` and replace JSON file writes with database command logging:

```csharp
using ShareGuard.Application.Commands;
using ShareGuard.Application.Interfaces;
using ShareGuard.Domain.Interfaces;
using ShareGuard.Domain.Models;

namespace ShareGuard.Application.Services;

public class ImageCleanupService : IImageCleanupService
{
    private readonly IImageCleaner _imageCleaner;
    private readonly IHistoryService _historyService;

    public ImageCleanupService(IImageCleaner imageCleaner, IHistoryService historyService)
    {
        _imageCleaner = imageCleaner;
        _historyService = historyService;
    }

    public async Task<StripResult> CleanImageAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        // 1. Verify extension
        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        if (extension != ".jpg" && extension != ".jpeg" && extension != ".png" && extension != ".webp")
        {
            var failResult = StripResult.Failed(sourcePath, "Unsupported image type.");
            await LogToDbAsync(sourcePath, string.Empty, 0, false, failResult.ErrorMessage, cancellationToken);
            return failResult;
        }

        // 2. Safely generate destination file path
        var directory = Path.GetDirectoryName(sourcePath) ?? string.Empty;
        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(sourcePath);
        var cleanPath = Path.Combine(directory, $"{fileNameWithoutExt}.clean{extension}");
        int index = 1;
        while (File.Exists(cleanPath))
        {
            cleanPath = Path.Combine(directory, $"{fileNameWithoutExt}.clean ({index}){extension}");
            index++;
        }

        try
        {
            // 3. Invoke metadata stripper engine
            var findings = await _imageCleaner.CleanImageAsync(sourcePath, cleanPath, cancellationToken);
            var successResult = StripResult.Succeeded(cleanPath, findings);

            // 4. Log successful clean to database
            await LogToDbAsync(sourcePath, cleanPath, findings.Count, true, null, cancellationToken);

            return successResult;
        }
        catch (Exception ex)
        {
            var errorResult = StripResult.Failed(sourcePath, ex.Message);
            await LogToDbAsync(sourcePath, string.Empty, 0, false, ex.Message, cancellationToken);
            return errorResult;
        }
    }

    private async Task LogToDbAsync(string src, string dest, int count, bool ok, string? err, CancellationToken ct)
    {
        var command = new LogHistoryCommand(
            FileName: Path.GetFileName(src),
            OriginalPath: src,
            CleanPath: dest,
            FindingsCount: count,
            IsSuccess: ok,
            ErrorMessage: err
        );

        await _historyService.LogHistoryAsync(command, ct);
    }
}
```

- [ ] **Step 2: Clean up obsolete logger in Infrastructure DI**

Delete the file: `c:\Users\ADMIN\source\repos\Shareguard-wpf\ShareGuard.Infrastructure\Services\LocalHistoryLogger.cs`

Open `c:\Users\ADMIN\source\repos\Shareguard-wpf\ShareGuard.Infrastructure\DependencyInjection.cs` and ensure the registration of `IHistoryLogger` is completely removed from `AddInfrastructureServices`. Only `IHistoryRepository` should remain.

- [ ] **Step 3: Build**

```powershell
dotnet build Shareguard-wpf.slnx
```

Expected: Build succeeds with zero errors.

- [ ] **Step 4: Commit changes**

```powershell
git rm ShareGuard.Infrastructure/Services/LocalHistoryLogger.cs
git add ShareGuard.Application/Services/ImageCleanupService.cs ShareGuard.Infrastructure/DependencyInjection.cs
git commit -m "refactor: replace file-based JSON logging with DB-based history service in ImageCleanupService"
```

---

### Task 7: Write Integration & Unit Tests for DB & Multi-file Operations

**Files:**
- Create: `ShareGuard.Application.Tests/HistoryRepositoryTests.cs`
- Create: `ShareGuard.Application.Tests/MultiFileProcessorServiceTests.cs`

- [ ] **Step 1: Create SQLite DB Repository Integration Tests**

Create `c:\Users\ADMIN\source\repos\Shareguard-wpf\ShareGuard.Application.Tests\HistoryRepositoryTests.cs` to test the repository with SQLite in-memory provider:

```csharp
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ShareGuard.Domain.Entities;
using ShareGuard.Infrastructure.Data;
using ShareGuard.Infrastructure.Repositories;
using Xunit;

namespace ShareGuard.Application.Tests;

public class HistoryRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ShareGuardDbContext> _contextOptions;

    public HistoryRepositoryTests()
    {
        // Use SQLite in-memory to test exact SQL behavior
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        _contextOptions = new DbContextOptionsBuilder<ShareGuardDbContext>()
            .UseSqlite(_connection)
            .Options;

        // Ensure database schema is created
        using var context = new ShareGuardDbContext(_contextOptions);
        context.Database.EnsureCreated();
    }

    [Fact]
    public async Task AddAsync_ShouldPersistEvent()
    {
        var factory = new TestDbContextFactory(_contextOptions);
        var sut = new HistoryRepository(factory);
        var evt = new HistoryEvent
        {
            Id = Guid.NewGuid(),
            FileName = "photo.jpg",
            OriginalPath = "C:\\photo.jpg",
            CleanPath = "C:\\photo.clean.jpg",
            ProcessedAt = DateTime.UtcNow,
            FindingsCount = 4,
            IsSuccess = true
        };

        await sut.AddAsync(evt);

        using var context = new ShareGuardDbContext(_contextOptions);
        var count = await context.HistoryEvents.CountAsync();
        var entity = await context.HistoryEvents.FindAsync(evt.Id);

        Assert.Equal(1, count);
        Assert.NotNull(entity);
        Assert.Equal("photo.jpg", entity.FileName);
    }

    [Fact]
    public async Task GetPagedAsync_ShouldReturnPagedResultsDescending()
    {
        var factory = new TestDbContextFactory(_contextOptions);
        var sut = new HistoryRepository(factory);

        var baseTime = DateTime.UtcNow;
        for (int i = 1; i <= 5; i++)
        {
            await sut.AddAsync(new HistoryEvent
            {
                Id = Guid.NewGuid(),
                FileName = $"file{i}.jpg",
                OriginalPath = "C:\\",
                CleanPath = "C:\\",
                ProcessedAt = baseTime.AddMinutes(i),
                IsSuccess = true
            });
        }

        var results = (await sut.GetPagedAsync(1, 3)).ToList();

        Assert.Equal(3, results.Count);
        Assert.Equal("file5.jpg", results[0].FileName); // Latest first
        Assert.Equal("file4.jpg", results[1].FileName);
        Assert.Equal("file3.jpg", results[2].FileName);
    }

    [Fact]
    public async Task ClearAllAsync_ShouldEmptyDatabase()
    {
        var factory = new TestDbContextFactory(_contextOptions);
        var sut = new HistoryRepository(factory);
        await sut.AddAsync(new HistoryEvent
        {
            Id = Guid.NewGuid(),
            FileName = "test.jpg",
            ProcessedAt = DateTime.UtcNow,
            IsSuccess = true
        });

        await sut.ClearAllAsync();

        var count = await sut.GetTotalCountAsync();
        Assert.Equal(0, count);
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}

public class TestDbContextFactory : IDbContextFactory<ShareGuardDbContext>
{
    private readonly DbContextOptions<ShareGuardDbContext> _options;

    public TestDbContextFactory(DbContextOptions<ShareGuardDbContext> options)
    {
        _options = options;
    }

    public ShareGuardDbContext CreateDbContext()
    {
        return new ShareGuardDbContext(_options);
    }
}
```

- [ ] **Step 2: Create Multi-File Processor Unit Tests**

Create `c:\Users\ADMIN\source\repos\Shareguard-wpf\ShareGuard.Application.Tests\MultiFileProcessorServiceTests.cs` using a mock setup to verify parallel processing:

```csharp
using Moq;
using ShareGuard.Application.Services;
using ShareGuard.Domain.Models;
using Xunit;

namespace ShareGuard.Application.Tests;

public class MultiFileProcessorServiceTests
{
    [Fact]
    public async Task ProcessFilesAsync_ShouldProcessAllFilesAndReportProgress()
    {
        // Arrange
        var mockCleanup = new Mock<IImageCleanupService>();
        mockCleanup
            .Setup(s => s.CleanImageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string path, CancellationToken ct) => StripResult.Succeeded(path + ".clean", new List<Finding>()));

        var sut = new MultiFileProcessorService(mockCleanup.Object);
        var files = new[] { "C:\\img1.jpg", "C:\\img2.jpg", "C:\\img3.jpg" };
        var reportedList = new List<ProcessingStatus>();
        var progress = new Progress<ProcessingStatus>(status => reportedList.Add(status));

        // Act
        await sut.ProcessFilesAsync(files, progress);

        // Assert
        Assert.Equal(3, reportedList.Count);
        Assert.Contains(reportedList, r => r.FilePath == "C:\\img1.jpg" && r.Success);
        Assert.Contains(reportedList, r => r.FilePath == "C:\\img2.jpg" && r.Success);
        Assert.Contains(reportedList, r => r.FilePath == "C:\\img3.jpg" && r.Success);
        mockCleanup.Verify(s => s.CleanImageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }
}
```

- [ ] **Step 3: Run the test suite**

```powershell
dotnet test ShareGuard.Application.Tests/ShareGuard.Application.Tests.csproj --verbosity normal
```

Expected: All tests pass.

- [ ] **Step 4: Commit changes**

```powershell
git add ShareGuard.Application.Tests/HistoryRepositoryTests.cs ShareGuard.Application.Tests/MultiFileProcessorServiceTests.cs
git commit -m "test: add db repository integration tests and parallel file processing unit tests"
```

---

### Task 8: Update MainViewModel with Batch Processing & History Properties

**Files:**
- Modify: `Shareguard-wpf/ViewModels/MainViewModel.cs`

- [ ] **Step 1: Replace MainViewModel.cs**

Open `c:\Users\ADMIN\source\repos\Shareguard-wpf\Shareguard-wpf\ViewModels\MainViewModel.cs` and replace its entire content to integrate the new database-backed history and parallel processor properties/commands:

```csharp
using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShareGuard.App.Services;
using ShareGuard.Application.Commands;
using ShareGuard.Application.Interfaces;
using ShareGuard.Application.Queries;
using ShareGuard.Application.Services;
using ShareGuard.Domain.Entities;
using ShareGuard.Domain.Interfaces;

namespace ShareGuard.App.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IUrlCleanerService _urlCleaner;
    private readonly IClipboardMonitorService _clipboardMonitor;
    private readonly INotificationService _notification;
    private readonly IHistoryService _historyService;
    private readonly IMultiFileProcessorService _multiFileProcessor;

    [ObservableProperty]
    private string _title = "ShareGuard";

    [ObservableProperty]
    private string _statusMessage = "Ready";

    // URL Cleaner properties (Phase 2)
    [ObservableProperty]
    private string _manualUrlInput = string.Empty;

    [ObservableProperty]
    private string _beforeUrl = string.Empty;

    [ObservableProperty]
    private string _afterUrl = string.Empty;

    [ObservableProperty]
    private int _removedCount;

    [ObservableProperty]
    private bool _showResults;

    [ObservableProperty]
    private bool _isMonitoring;

    // Batch File Processing properties (Phase 3)
    [ObservableProperty]
    private bool _isBatchProcessing;

    [ObservableProperty]
    private int _totalFilesToProcess;

    [ObservableProperty]
    private int _currentFilesProcessed;

    [ObservableProperty]
    private double _batchProgressPercentage;

    [ObservableProperty]
    private string _currentFileName = string.Empty;

    [ObservableProperty]
    private bool _showBatchResults;

    public ObservableCollection<BatchItemResultViewModel> CurrentBatchResults { get; } = new();

    // History Database properties (Phase 3)
    public ObservableCollection<HistoryEventViewModel> HistoryEvents { get; } = new();

    [ObservableProperty]
    private int _currentHistoryPage = 1;

    [ObservableProperty]
    private int _totalHistoryPages = 1;

    private const int HistoryPageSize = 10;

    public MainViewModel(
        IUrlCleanerService urlCleaner,
        IClipboardMonitorService clipboardMonitor,
        INotificationService notification,
        IHistoryService historyService,
        IMultiFileProcessorService multiFileProcessor)
    {
        _urlCleaner = urlCleaner;
        _clipboardMonitor = clipboardMonitor;
        _notification = notification;
        _historyService = historyService;
        _multiFileProcessor = multiFileProcessor;

        _clipboardMonitor.UrlCleaned += OnUrlCleaned;
    }

    [RelayCommand]
    private async Task Loaded()
    {
        StatusMessage = "ShareGuard is ready to protect your privacy.";
        await LoadHistoryAsync();
    }

    // Manual URL Cleaning Command
    [RelayCommand]
    private async Task CleanManualUrl()
    {
        if (string.IsNullOrWhiteSpace(ManualUrlInput))
        {
            StatusMessage = "Please enter a URL to clean.";
            return;
        }

        if (_urlCleaner.CleanUrl(ManualUrlInput, out var cleanUrl, out var removed))
        {
            BeforeUrl = ManualUrlInput;
            AfterUrl = cleanUrl;
            RemovedCount = removed;
            ShowResults = true;
            StatusMessage = $"Cleaned! {removed} tracking parameter(s) removed.";

            // Save URL cleaning operation to SQLite database
            await _historyService.LogHistoryAsync(new LogHistoryCommand(
                FileName: "Manual URL input",
                OriginalPath: ManualUrlInput,
                CleanPath: cleanUrl,
                FindingsCount: removed,
                IsSuccess: true
            ));
            await LoadHistoryAsync();
        }
        else
        {
            BeforeUrl = ManualUrlInput;
            AfterUrl = ManualUrlInput;
            RemovedCount = 0;
            ShowResults = true;
            StatusMessage = "URL is already clean — no tracking parameters found.";
        }
    }

    // Batch File Processing Command
    [RelayCommand]
    private async Task ProcessFiles(IEnumerable<string> filePaths)
    {
        var pathList = filePaths.ToList();
        if (pathList.Count == 0) return;

        IsBatchProcessing = true;
        ShowBatchResults = false;
        CurrentBatchResults.Clear();
        BatchProgressPercentage = 0;
        CurrentFilesProcessed = 0;
        TotalFilesToProcess = pathList.Count;
        StatusMessage = $"Processing {TotalFilesToProcess} file(s)...";

        var progress = new Progress<ProcessingStatus>(status =>
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                CurrentFilesProcessed = status.CurrentCount;
                BatchProgressPercentage = ((double)status.CurrentCount / status.TotalCount) * 100;
                CurrentFileName = Path.GetFileName(status.FilePath);
                StatusMessage = $"Processing file {status.CurrentCount} of {status.TotalCount}...";

                CurrentBatchResults.Add(new BatchItemResultViewModel
                {
                    FileName = Path.GetFileName(status.FilePath),
                    OriginalPath = status.FilePath,
                    CleanPath = status.CleanPath,
                    FindingsCount = status.FindingsCount,
                    Success = status.Success
                });
            });
        });

        try
        {
            await _multiFileProcessor.ProcessFilesAsync(pathList, progress);
            StatusMessage = $"Completed batch cleaning of {TotalFilesToProcess} file(s).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Batch processing failed: {ex.Message}";
        }
        finally
        {
            IsBatchProcessing = false;
            ShowBatchResults = true;
            await LoadHistoryAsync(); // Reload SQL history
        }
    }

    // Opens file browser to select multiple files
    [RelayCommand]
    private async Task BrowseFiles()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Multiselect = true,
            Filter = "Image Files (*.jpg;*.jpeg;*.png;*.webp)|*.jpg;*.jpeg;*.png;*.webp"
        };

        if (dialog.ShowDialog() == true)
        {
            await ProcessFiles(dialog.FileNames);
        }
    }

    [RelayCommand]
    private void ClearBatchResults()
    {
        ShowBatchResults = false;
        CurrentBatchResults.Clear();
        StatusMessage = "Ready";
    }

    // Database History Pagination Commands
    [RelayCommand]
    private async Task LoadHistoryAsync()
    {
        try
        {
            int totalCount = await _historyService.GetTotalCountAsync();
            TotalHistoryPages = Math.Max(1, (int)Math.Ceiling((double)totalCount / HistoryPageSize));

            if (CurrentHistoryPage > TotalHistoryPages)
            {
                CurrentHistoryPage = TotalHistoryPages;
            }

            var query = new GetHistoryQuery(CurrentHistoryPage, HistoryPageSize);
            var items = await _historyService.GetHistoryAsync(query);

            HistoryEvents.Clear();
            foreach (var item in items)
            {
                HistoryEvents.Add(new HistoryEventViewModel
                {
                    Id = item.Id,
                    FileName = item.FileName,
                    OriginalPath = item.OriginalPath,
                    CleanPath = item.CleanPath,
                    ProcessedAt = item.ProcessedAt.ToLocalTime().ToString("g"),
                    FindingsCount = item.FindingsCount,
                    IsSuccess = item.IsSuccess
                });
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load history: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task NextHistoryPage()
    {
        if (CurrentHistoryPage < TotalHistoryPages)
        {
            CurrentHistoryPage++;
            await LoadHistoryAsync();
        }
    }

    [RelayCommand]
    private async Task PrevHistoryPage()
    {
        if (CurrentHistoryPage > 1)
        {
            CurrentHistoryPage--;
            await LoadHistoryAsync();
        }
    }

    [RelayCommand]
    private async Task ClearHistory()
    {
        try
        {
            await _historyService.ClearHistoryAsync();
            CurrentHistoryPage = 1;
            await LoadHistoryAsync();
            StatusMessage = "History logs cleared successfully.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to clear history: {ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenFileFolder(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
        try
        {
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to open directory: {ex.Message}";
        }
    }

    partial void OnIsMonitoringChanged(bool value)
    {
        if (value)
        {
            StatusMessage = "Clipboard monitoring active — copy a URL to auto-clean it.";
        }
        else
        {
            _clipboardMonitor.StopMonitoring();
            StatusMessage = "Clipboard monitoring paused.";
        }
    }

    private async void OnUrlCleaned(string before, string after, int count)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(async () =>
        {
            BeforeUrl = before;
            AfterUrl = after;
            RemovedCount = count;
            ShowResults = true;
            StatusMessage = $"Auto-cleaned! {count} tracking parameter(s) stripped from clipboard.";

            await _historyService.LogHistoryAsync(new LogHistoryCommand(
                FileName: "Clipboard URL",
                OriginalPath: before,
                CleanPath: after,
                FindingsCount: count,
                IsSuccess: true
            ));
            await LoadHistoryAsync();
        });

        _notification.Show("URL Cleaned", $"Removed {count} tracking parameter(s)");
    }

    public void Dispose()
    {
        _clipboardMonitor.UrlCleaned -= OnUrlCleaned;
        _clipboardMonitor.Dispose();
        GC.SuppressFinalize(this);
    }
}

public class BatchItemResultViewModel
{
    public string FileName { get; init; } = string.Empty;
    public string OriginalPath { get; init; } = string.Empty;
    public string CleanPath { get; init; } = string.Empty;
    public int FindingsCount { get; init; }
    public bool Success { get; init; }
    public string StatusText => Success ? "Cleaned" : "Failed";
    public string StatusColor => Success ? "#22C55E" : "#EF4444";
}

public class HistoryEventViewModel
{
    public Guid Id { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string OriginalPath { get; init; } = string.Empty;
    public string CleanPath { get; init; } = string.Empty;
    public string ProcessedAt { get; init; } = string.Empty;
    public int FindingsCount { get; init; }
    public bool IsSuccess { get; init; }
    public string StatusText => IsSuccess ? "Cleaned" : "Failed";
    public string StatusColor => IsSuccess ? "#22C55E" : "#EF4444";
}
```

- [ ] **Step 2: Build ViewModels**

```powershell
dotnet build Shareguard-wpf/Shareguard-wpf.csproj
```

Expected: Project compiles cleanly with no compiler warnings/errors.

- [ ] **Step 3: Commit changes**

```powershell
git add Shareguard-wpf/ViewModels/MainViewModel.cs
git commit -m "feat: upgrade MainViewModel to manage batch processing and paged database history"
```

---

### Task 9: Design Tabbed Layout and Virtualized Views in MainWindow XAML

**Files:**
- Modify: `Shareguard-wpf/MainWindow.xaml`

- [ ] **Step 1: Replace MainWindow.xaml Content**

Open `c:\Users\ADMIN\source\repos\Shareguard-wpf\Shareguard-wpf\MainWindow.xaml` and replace its entire content to implement the TabControl UI structure, custom TabItem style, batch progress panels, results list, and the virtualized history database logger panel:

```xml
<Window x:Class="ShareGuard.App.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
        xmlns:viewmodels="clr-namespace:ShareGuard.App.ViewModels"
        mc:Ignorable="d"
        Title="{Binding Title}"
        Height="650" Width="1000"
        Background="#0F172A">

    <Window.Resources>
        <BooleanToVisibilityConverter x:Key="BoolToVis" />

        <!-- Inverse boolean to visibility -->
        <Style x:Key="InverseVisStyle" TargetType="FrameworkElement">
            <Setter Property="Visibility" Value="Visible" />
            <Style.Triggers>
                <DataTrigger Binding="{Binding IsBatchProcessing}" Value="True">
                    <Setter Property="Visibility" Value="Collapsed" />
                </DataTrigger>
                <DataTrigger Binding="{Binding ShowBatchResults}" Value="True">
                    <Setter Property="Visibility" Value="Collapsed" />
                </DataTrigger>
            </Style.Triggers>
        </Style>

        <!-- Tab Item Custom Style -->
        <Style x:Key="ModernTabItem" TargetType="TabItem">
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="TabItem">
                        <Border x:Name="border"
                                Background="Transparent"
                                BorderBrush="Transparent"
                                BorderThickness="0,0,0,2"
                                Padding="20,10"
                                Margin="0,0,16,0"
                                Cursor="Hand">
                            <ContentPresenter x:Name="contentPresenter"
                                              ContentSource="Header"
                                              HorizontalAlignment="Center"
                                              VerticalAlignment="Center"
                                              RecognizesAccessKey="True" />
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsSelected" Value="True">
                                <Setter TargetName="border" Property="BorderBrush" Value="#3B82F6" />
                                <Setter Property="Foreground" Value="#3B82F6" />
                                <Setter Property="FontWeight" Value="SemiBold" />
                            </Trigger>
                            <Trigger Property="IsSelected" Value="False">
                                <Setter Property="Foreground" Value="#94A3B8" />
                            </Trigger>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter Property="Foreground" Value="#F8FAFC" />
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <!-- Standard Accent Button -->
        <Style x:Key="AccentButton" TargetType="Button">
            <Setter Property="Background" Value="#3B82F6" />
            <Setter Property="Foreground" Value="#FFFFFF" />
            <Setter Property="FontSize" Value="14" />
            <Setter Property="FontWeight" Value="SemiBold" />
            <Setter Property="Padding" Value="20,10" />
            <Setter Property="BorderThickness" Value="0" />
            <Setter Property="Cursor" Value="Hand" />
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Button">
                        <Border x:Name="border"
                                Background="{TemplateBinding Background}"
                                CornerRadius="8"
                                Padding="{TemplateBinding Padding}">
                            <ContentPresenter HorizontalAlignment="Center"
                                              VerticalAlignment="Center" />
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter TargetName="border" Property="Background" Value="#2563EB" />
                            </Trigger>
                            <Trigger Property="IsPressed" Value="True">
                                <Setter TargetName="border" Property="Background" Value="#1D4ED8" />
                            </Trigger>
                            <Trigger Property="IsEnabled" Value="False">
                                <Setter TargetName="border" Property="Background" Value="#334155" />
                                <Setter Property="Foreground" Value="#64748B" />
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <!-- Secondary Button Style -->
        <Style x:Key="SecondaryButton" TargetType="Button" BasedOn="{StaticResource AccentButton}">
            <Setter Property="Background" Value="#1E293B" />
            <Setter Property="Foreground" Value="#F8FAFC" />
            <Setter Property="BorderBrush" Value="#334155" />
            <Setter Property="BorderThickness" Value="1" />
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Button">
                        <Border x:Name="border"
                                Background="{TemplateBinding Background}"
                                BorderBrush="{TemplateBinding BorderBrush}"
                                BorderThickness="{TemplateBinding BorderThickness}"
                                CornerRadius="8"
                                Padding="{TemplateBinding Padding}">
                            <ContentPresenter HorizontalAlignment="Center"
                                              VerticalAlignment="Center" />
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter TargetName="border" Property="Background" Value="#334155" />
                            </Trigger>
                            <Trigger Property="IsPressed" Value="True">
                                <Setter TargetName="border" Property="Background" Value="#475569" />
                            </Trigger>
                            <Trigger Property="IsEnabled" Value="False">
                                <Setter TargetName="border" Property="Foreground" Value="#475569" />
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <!-- Modern TextBox -->
        <Style x:Key="ModernTextBox" TargetType="TextBox">
            <Setter Property="Background" Value="#1E293B" />
            <Setter Property="Foreground" Value="#F8FAFC" />
            <Setter Property="CaretBrush" Value="#F8FAFC" />
            <Setter Property="BorderBrush" Value="#334155" />
            <Setter Property="BorderThickness" Value="1" />
            <Setter Property="Padding" Value="12,10" />
            <Setter Property="FontSize" Value="14" />
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="TextBox">
                        <Border Background="{TemplateBinding Background}"
                                BorderBrush="{TemplateBinding BorderBrush}"
                                BorderThickness="{TemplateBinding BorderThickness}"
                                CornerRadius="8"
                                Padding="{TemplateBinding Padding}">
                            <ScrollViewer x:Name="PART_ContentHost" />
                        </Border>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
    </Window.Resources>

    <Grid Margin="24">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="16" />
            <RowDefinition Height="*" />
            <RowDefinition Height="Auto" />
        </Grid.RowDefinitions>

        <!-- App Header Banner -->
        <Grid Grid.Row="0">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*" />
                <ColumnDefinition Width="Auto" />
            </Grid.ColumnDefinitions>
            <StackPanel Grid.Column="0">
                <TextBlock Text="🛡️ ShareGuard"
                           FontSize="26"
                           FontWeight="Bold"
                           Foreground="#F8FAFC" />
                <TextBlock Text="Stripping personal metadata and tracking parameters"
                           FontSize="13"
                           Foreground="#64748B"
                           Margin="0,2,0,0" />
            </StackPanel>
        </Grid>

        <!-- Main Tabbed Panel Layout -->
        <TabControl Grid.Row="2"
                    Background="Transparent"
                    BorderThickness="0"
                    Margin="0,8,0,0">
            
            <!-- CLEAN FILES & URLS TAB -->
            <TabItem Header="Clean Files &amp; URLs" Style="{StaticResource ModernTabItem}">
                <Grid Margin="0,16,0,0">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="340" />
                        <ColumnDefinition Width="24" />
                        <ColumnDefinition Width="*" />
                    </Grid.ColumnDefinitions>

                    <!-- Left: URL Cleaner Card Controls -->
                    <StackPanel Grid.Column="0">
                        <!-- Auto Clipboard Monitor Section -->
                        <Border Background="#1E293B"
                                CornerRadius="12"
                                Padding="16"
                                Margin="0,0,0,16">
                            <Grid>
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="*" />
                                    <ColumnDefinition Width="Auto" />
                                </Grid.ColumnDefinitions>
                                <StackPanel Grid.Column="0" VerticalAlignment="Center">
                                    <TextBlock Text="Auto-Clean Clipboard"
                                               FontSize="15"
                                               FontWeight="SemiBold"
                                               Foreground="#F8FAFC" />
                                    <TextBlock Text="Strips track parameters on copy"
                                               FontSize="11"
                                               Foreground="#94A3B8"
                                               Margin="0,2,0,0" />
                                </StackPanel>
                                <CheckBox Grid.Column="1"
                                          IsChecked="{Binding IsMonitoring}"
                                          VerticalAlignment="Center"
                                          Content="Active"
                                          Foreground="#94A3B8"
                                          FontSize="12" />
                            </Grid>
                        </Border>

                        <!-- Manual URL Ingestion Card -->
                        <Border Background="#1E293B"
                                CornerRadius="12"
                                Padding="16">
                            <StackPanel>
                                <TextBlock Text="Manual URL Stripper"
                                           FontSize="15"
                                           FontWeight="SemiBold"
                                           Foreground="#F8FAFC"
                                           Margin="0,0,0,8" />
                                <TextBox Style="{StaticResource ModernTextBox}"
                                         Text="{Binding ManualUrlInput, UpdateSourceTrigger=PropertyChanged}"
                                         Margin="0,0,0,12" />
                                <Button Style="{StaticResource AccentButton}"
                                        Content="Clean URL"
                                        HorizontalAlignment="Right"
                                        Command="{Binding CleanManualUrlCommand}" />

                                <!-- Result Area -->
                                <Border Visibility="{Binding ShowResults, Converter={StaticResource BoolToVis}}"
                                        Background="#0F172A"
                                        CornerRadius="8"
                                        Padding="12"
                                        Margin="0,16,0,0">
                                    <StackPanel>
                                        <TextBlock Text="Original URL" FontSize="11" Foreground="#64748B" />
                                        <TextBox Text="{Binding BeforeUrl, Mode=OneWay}"
                                                 IsReadOnly="True" Background="Transparent" Foreground="#EF4444"
                                                 BorderThickness="0" FontSize="12" TextWrapping="Wrap" Margin="0,2,0,8" />

                                        <TextBlock Text="Cleaned URL" FontSize="11" Foreground="#64748B" />
                                        <TextBox Text="{Binding AfterUrl, Mode=OneWay}"
                                                 IsReadOnly="True" Background="Transparent" Foreground="#22C55E"
                                                 BorderThickness="0" FontSize="12" TextWrapping="Wrap" Margin="0,2,0,8" />

                                        <TextBlock Foreground="#94A3B8" FontSize="11">
                                            <Run Text="Parameters stripped: " />
                                            <Run Text="{Binding RemovedCount, Mode=OneWay}" FontWeight="Bold" Foreground="#F59E0B" />
                                        </TextBlock>
                                    </StackPanel>
                                </Border>
                            </StackPanel>
                        </Border>
                    </StackPanel>

                    <!-- Right Column: Parallel File Stripper Panel -->
                    <Grid Grid.Column="2">
                        
                        <!-- 1. DRAG AND DROP ZONE -->
                        <Border Style="{StaticResource InverseVisStyle}"
                                BorderBrush="#334155"
                                BorderThickness="2"
                                StrokeDashArray="4 3"
                                CornerRadius="12"
                                Background="#1E293B"
                                AllowDrop="True"
                                x:Name="FileDropBorder">
                            <Grid VerticalAlignment="Center" HorizontalAlignment="Center">
                                <StackPanel HorizontalAlignment="Center">
                                    <TextBlock Text="📂" FontSize="44" HorizontalAlignment="Center" />
                                    <TextBlock Text="Drag and Drop Files Here"
                                               FontSize="18"
                                               FontWeight="SemiBold"
                                               Foreground="#F8FAFC"
                                               Margin="0,12,0,0"
                                               HorizontalAlignment="Center" />
                                    <TextBlock Text="Supports JPEG, PNG, WEBP images (batch processing enabled)"
                                               FontSize="12"
                                               Foreground="#94A3B8"
                                               Margin="0,4,0,16"
                                               HorizontalAlignment="Center" />
                                    <Button Style="{StaticResource AccentButton}"
                                            Content="Browse Local Files"
                                            Command="{Binding BrowseFilesCommand}"
                                            HorizontalAlignment="Center" />
                                </StackPanel>
                            </Grid>
                        </Border>

                        <!-- 2. BATCH PROCESSING PROGRESS RING / BAR -->
                        <Border Visibility="{Binding IsBatchProcessing, Converter={StaticResource BoolToVis}}"
                                Background="#1E293B"
                                CornerRadius="12"
                                Padding="24">
                            <Grid VerticalAlignment="Center" HorizontalAlignment="Center" Width="400">
                                <StackPanel>
                                    <TextBlock Text="Processing File Batch..."
                                               FontSize="18"
                                               FontWeight="SemiBold"
                                               Foreground="#F8FAFC"
                                               HorizontalAlignment="Center"
                                               Margin="0,0,0,16" />
                                    
                                    <ProgressBar Height="10"
                                                 Value="{Binding BatchProgressPercentage, Mode=OneWay}"
                                                 Background="#0F172A"
                                                 Foreground="#3B82F6"
                                                 BorderThickness="0"
                                                 Margin="0,0,0,8" />
                                    
                                    <Grid Margin="0,0,0,16">
                                        <TextBlock Foreground="#94A3B8" FontSize="12" HorizontalAlignment="Left">
                                            <Run Text="Progress: " />
                                            <Run Text="{Binding CurrentFilesProcessed, Mode=OneWay}" Foreground="#F8FAFC" FontWeight="SemiBold" />
                                            <Run Text=" of " />
                                            <Run Text="{Binding TotalFilesToProcess, Mode=OneWay}" Foreground="#F8FAFC" FontWeight="SemiBold" />
                                        </TextBlock>
                                        <TextBlock Text="{Binding BatchProgressPercentage, StringFormat={}{0:0}%, Mode=OneWay}"
                                                   Foreground="#3B82F6"
                                                   FontSize="12"
                                                   FontWeight="SemiBold"
                                                   HorizontalAlignment="Right" />
                                    </Grid>

                                    <TextBlock Text="{Binding CurrentFileName}"
                                               FontSize="13"
                                               Foreground="#94A3B8"
                                               TextTrimming="CharacterEllipsis"
                                               HorizontalAlignment="Center" />
                                </StackPanel>
                            </Grid>
                        </Border>

                        <!-- 3. BATCH RESULTS GRID -->
                        <Border Visibility="{Binding ShowBatchResults, Converter={StaticResource BoolToVis}}"
                                Background="#1E293B"
                                CornerRadius="12"
                                Padding="16">
                            <Grid>
                                <Grid.RowDefinitions>
                                    <RowDefinition Height="Auto" />
                                    <RowDefinition Height="12" />
                                    <RowDefinition Height="*" />
                                    <RowDefinition Height="12" />
                                    <RowDefinition Height="Auto" />
                                </Grid.RowDefinitions>

                                <Grid Grid.Row="0">
                                    <TextBlock Text="Batch Clean Results" FontSize="16" FontWeight="SemiBold" Foreground="#F8FAFC" />
                                    <Button Style="{StaticResource SecondaryButton}"
                                            Content="Dismiss"
                                            Command="{Binding ClearBatchResultsCommand}"
                                            HorizontalAlignment="Right"
                                            Padding="10,4"
                                            FontSize="12" />
                                </Grid>

                                <!-- Grid detailing stripped values -->
                                <ListView Grid.Row="2"
                                          ItemsSource="{Binding CurrentBatchResults}"
                                          Background="Transparent"
                                          BorderThickness="0"
                                          Foreground="#F8FAFC">
                                    <!-- Enable Virtualization -->
                                    <ListView.ItemsPanel>
                                        <ItemsPanelTemplate>
                                            <VirtualizingStackPanel IsVirtualizing="True" VirtualizationMode="Recycling" />
                                        </ItemsPanelTemplate>
                                    </ListView.ItemsPanel>
                                    <ListView.View>
                                        <GridView>
                                            <GridViewColumn Header="File Name" Width="150">
                                                <GridViewColumn.CellTemplate>
                                                    <DataTemplate>
                                                        <TextBlock Text="{Binding FileName}" Foreground="#F8FAFC" TextTrimming="CharacterEllipsis" />
                                                    </DataTemplate>
                                                </GridViewColumn.CellTemplate>
                                            </GridViewColumn>
                                            <GridViewColumn Header="Stripped Count" Width="90">
                                                <GridViewColumn.CellTemplate>
                                                    <DataTemplate>
                                                        <TextBlock Text="{Binding FindingsCount}" Foreground="#F59E0B" FontWeight="Bold" HorizontalAlignment="Center" />
                                                    </DataTemplate>
                                                </GridViewColumn.CellTemplate>
                                            </GridViewColumn>
                                            <GridViewColumn Header="Status" Width="80">
                                                <GridViewColumn.CellTemplate>
                                                    <DataTemplate>
                                                        <TextBlock Text="{Binding StatusText}" Foreground="{Binding StatusColor}" FontWeight="SemiBold" />
                                                    </DataTemplate>
                                                </GridViewColumn.CellTemplate>
                                            </GridViewColumn>
                                            <GridViewColumn Header="Actions" Width="120">
                                                <GridViewColumn.CellTemplate>
                                                    <DataTemplate>
                                                        <Button Content="Show Folder"
                                                                FontSize="11"
                                                                Padding="8,2"
                                                                Style="{StaticResource SecondaryButton}"
                                                                Command="{Binding DataContext.OpenFileFolderCommand, RelativeSource={RelativeSource AncestorType=Window}}"
                                                                CommandParameter="{Binding CleanPath}"
                                                                IsEnabled="{Binding Success}" />
                                                    </DataTemplate>
                                                </GridViewColumn.CellTemplate>
                                            </GridViewColumn>
                                        </GridView>
                                    </ListView.View>
                                </ListView>

                                <TextBlock Grid.Row="4" Foreground="#94A3B8" FontSize="11" Text="Click 'Show Folder' to open output directory and highlight the cleaned file." />
                            </Grid>
                        </Border>
                    </Grid>
                </Grid>
            </TabItem>

            <!-- SQL DATABASE HISTORY VIEWER TAB -->
            <TabItem Header="Operation History" Style="{StaticResource ModernTabItem}">
                <Grid Margin="0,16,0,0">
                    <Grid.RowDefinitions>
                        <RowDefinition Height="Auto" />
                        <RowDefinition Height="12" />
                        <RowDefinition Height="*" />
                        <RowDefinition Height="12" />
                        <RowDefinition Height="Auto" />
                    </Grid.RowDefinitions>

                    <!-- Header with wipe button -->
                    <Grid Grid.Row="0">
                        <StackPanel Orientation="Horizontal" VerticalAlignment="Center">
                            <TextBlock Text="Local Operations History log" FontSize="16" FontWeight="SemiBold" Foreground="#F8FAFC" />
                            <TextBlock Text=" (Stored in Local SQLite database)" FontSize="12" Foreground="#64748B" VerticalAlignment="Bottom" Margin="4,0,0,1" />
                        </StackPanel>
                        <Button Style="{StaticResource SecondaryButton}"
                                Content="Clear History Log"
                                Command="{Binding ClearHistoryCommand}"
                                HorizontalAlignment="Right"
                                Padding="12,5"
                                FontSize="12" />
                    </Grid>

                    <!-- Virtualized History Log List -->
                    <ListView Grid.Row="2"
                              ItemsSource="{Binding HistoryEvents}"
                              Background="#1E293B"
                              BorderThickness="1"
                              BorderBrush="#334155"
                              Foreground="#F8FAFC">
                        <!-- Enable UI Virtualization explicitly -->
                        <ListView.ItemsPanel>
                            <ItemsPanelTemplate>
                                <VirtualizingStackPanel IsVirtualizing="True" VirtualizationMode="Recycling" />
                            </ItemsPanelTemplate>
                        </ListView.ItemsPanel>
                        <ListView.View>
                            <GridView>
                                <GridViewColumn Header="Source" Width="180">
                                    <GridViewColumn.CellTemplate>
                                        <DataTemplate>
                                            <TextBlock Text="{Binding FileName}" Foreground="#F8FAFC" TextTrimming="CharacterEllipsis" FontWeight="SemiBold" />
                                        </DataTemplate>
                                    </GridViewColumn.CellTemplate>
                                </GridViewColumn>
                                <GridViewColumn Header="Processed Date" Width="130">
                                    <GridViewColumn.CellTemplate>
                                        <DataTemplate>
                                            <TextBlock Text="{Binding ProcessedAt}" Foreground="#94A3B8" />
                                        </DataTemplate>
                                    </GridViewColumn.CellTemplate>
                                </GridViewColumn>
                                <GridViewColumn Header="Findings" Width="70">
                                    <GridViewColumn.CellTemplate>
                                        <DataTemplate>
                                            <TextBlock Text="{Binding FindingsCount}" Foreground="#F59E0B" FontWeight="Bold" />
                                        </DataTemplate>
                                    </GridViewColumn.CellTemplate>
                                </GridViewColumn>
                                <GridViewColumn Header="Status" Width="70">
                                    <GridViewColumn.CellTemplate>
                                        <DataTemplate>
                                            <TextBlock Text="{Binding StatusText}" Foreground="{Binding StatusColor}" FontWeight="SemiBold" />
                                        </DataTemplate>
                                    </GridViewColumn.CellTemplate>
                                </GridViewColumn>
                                <GridViewColumn Header="Original Path" Width="200">
                                    <GridViewColumn.CellTemplate>
                                        <DataTemplate>
                                            <TextBlock Text="{Binding OriginalPath}" Foreground="#64748B" TextTrimming="CharacterEllipsis" ToolTip="{Binding OriginalPath}" />
                                        </DataTemplate>
                                    </GridViewColumn.CellTemplate>
                                </GridViewColumn>
                                <GridViewColumn Header="Actions" Width="120">
                                    <GridViewColumn.CellTemplate>
                                        <DataTemplate>
                                            <Button Content="Show Cleaned"
                                                    FontSize="11"
                                                    Padding="8,2"
                                                    Style="{StaticResource SecondaryButton}"
                                                    Command="{Binding DataContext.OpenFileFolderCommand, RelativeSource={RelativeSource AncestorType=Window}}"
                                                    CommandParameter="{Binding CleanPath}"
                                                    IsEnabled="{Binding IsSuccess}" />
                                        </DataTemplate>
                                    </GridViewColumn.CellTemplate>
                                </GridViewColumn>
                            </GridView>
                        </ListView.View>
                    </ListView>

                    <!-- Pager navigation panel -->
                    <Grid Grid.Row="4">
                        <StackPanel Orientation="Horizontal" HorizontalAlignment="Left" VerticalAlignment="Center">
                            <TextBlock Text="Page " Foreground="#94A3B8" FontSize="13" />
                            <TextBlock Text="{Binding CurrentHistoryPage}" Foreground="#F8FAFC" FontWeight="Bold" FontSize="13" />
                            <TextBlock Text=" of " Foreground="#94A3B8" FontSize="13" />
                            <TextBlock Text="{Binding TotalHistoryPages}" Foreground="#F8FAFC" FontWeight="Bold" FontSize="13" />
                        </StackPanel>

                        <StackPanel Orientation="Horizontal" HorizontalAlignment="Right">
                            <Button Style="{StaticResource SecondaryButton}"
                                    Content="◀ Prev"
                                    Command="{Binding PrevHistoryPageCommand}"
                                    Margin="0,0,8,0"
                                    Padding="12,4"
                                    FontSize="12" />
                            <Button Style="{StaticResource SecondaryButton}"
                                    Content="Next ▶"
                                    Command="{Binding NextHistoryPageCommand}"
                                    Padding="12,4"
                                    FontSize="12" />
                        </StackPanel>
                    </Grid>
                </Grid>
            </TabItem>
        </TabControl>

        <!-- Status Bar -->
        <TextBlock Grid.Row="3"
                   Text="{Binding StatusMessage}"
                   FontSize="12"
                   Foreground="#64748B"
                   Margin="0,16,0,0" />
    </Grid>
</Window>
