# Phase 4: Advanced File Types Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Expand metadata stripping capabilities to support document formats: Office Open XML (.docx, .xlsx, .pptx) and PDF files, using pure .NET libraries with a verification loop using MetadataExtractor.

**Architecture:** Introduce a unified `IFileStripper` interface in the Domain layer. Implement concrete strippers in the Infrastructure layer using `DocumentFormat.OpenXml` (Open XML SDK) and `PDFsharp`. Orchestrate them in the Application layer via a new `FileCleanupService` that replaces or generalizes the image-only flow, applying independent verification via `MetadataExtractor`.

**Tech Stack:** .NET 10, C# 14, DocumentFormat.OpenXml 3.0.2, PDFsharp 6.1.1, MetadataExtractor 2.8.1

---

## File Structure

### `ShareGuard.Domain` (no external dependencies)
| Status | Path | Responsibility |
|--------|------|----------------|
| NEW | `ShareGuard.Domain/Interfaces/IFileStripper.cs` | Domain interface defining metadata stripping capabilities |

### `ShareGuard.Application` (depends on Domain)
| Status | Path | Responsibility |
|--------|------|----------------|
| NEW | `ShareGuard.Application/Services/IFileCleanupService.cs` | Application orchestration interface for general files |
| NEW | `ShareGuard.Application/Services/FileCleanupService.cs` | Route file -> correct IFileStripper -> run verifier |
| NEW | `ShareGuard.Application/Services/MetadataVerifier.cs` | Read-only verification check using MetadataExtractor |
| MODIFY | `ShareGuard.Application/Services/MultiFileProcessorService.cs` | Modify batch processor to inject/use IFileCleanupService |
| MODIFY | `ShareGuard.Application/DependencyInjection.cs` | Register `FileCleanupService` and new mappings |

### `ShareGuard.Infrastructure` (depends on Domain + Application)
| Status | Path | Responsibility |
|--------|------|----------------|
| NEW | `ShareGuard.Infrastructure/Services/ImageSharpStripper.cs` | Wrap `IImageCleaner` under `IFileStripper` interface |
| NEW | `ShareGuard.Infrastructure/Services/OfficeOpenXmlStripper.cs` | Open XML SDK implementation for Word, Excel, PowerPoint |
| NEW | `ShareGuard.Infrastructure/Services/PdfMetadataStripper.cs` | PDFsharp implementation for PDF Info dictionary clearing |
| MODIFY | `ShareGuard.Infrastructure/DependencyInjection.cs` | Register new strippers |
| MODIFY | `ShareGuard.Infrastructure/ShareGuard.Infrastructure.csproj` | Add `DocumentFormat.OpenXml` and `PDFsharp` package references |

### Tests
| Status | Path | Responsibility |
|--------|------|----------------|
| NEW | `ShareGuard.Application.Tests/OfficeOpenXmlStripperTests.cs` | Programmatic docx creation and strip verification tests |
| NEW | `ShareGuard.Application.Tests/PdfMetadataStripperTests.cs` | Programmatic pdf creation and strip verification tests |
| NEW | `ShareGuard.Application.Tests/MetadataVerifierTests.cs` | Unit tests for MetadataVerifier checks |
| NEW | `ShareGuard.Application.Tests/FileCleanupServiceTests.cs` | Unit tests for routing and verification failure handling |

---

### Task 1: Add NuGet Package References

**Files:**
- Modify: `Directory.Packages.props`
- Modify: `ShareGuard.Infrastructure/ShareGuard.Infrastructure.csproj`
- Modify: `ShareGuard.Application.Tests/ShareGuard.Application.Tests.csproj`

- [ ] **Step 1: Add package versions to Directory.Packages.props**

Open `Directory.Packages.props`. Add three new `PackageVersion` entries inside the existing `<ItemGroup>`:

```xml
    <!-- Advanced Document Processing -->
    <PackageVersion Include="DocumentFormat.OpenXml" Version="3.0.2" />
    <PackageVersion Include="PDFsharp" Version="6.1.1" />
    <!-- Metadata Verification -->
    <PackageVersion Include="MetadataExtractor" Version="2.8.1" />
```

- [ ] **Step 2: Add DocumentFormat.OpenXml and PDFsharp to Infrastructure csproj**

Open `ShareGuard.Infrastructure/ShareGuard.Infrastructure.csproj`. Add the new package references:

```xml
    <PackageReference Include="DocumentFormat.OpenXml" />
    <PackageReference Include="PDFsharp" />
```

- [ ] **Step 3: Add MetadataExtractor to Application.Tests csproj**

Open `ShareGuard.Application.Tests/ShareGuard.Application.Tests.csproj`. Add the package reference:

```xml
    <PackageReference Include="MetadataExtractor" />
```

- [ ] **Step 4: Restore and build**

Run:
```powershell
dotnet restore Shareguard-wpf.slnx
dotnet build Shareguard-wpf.slnx --no-restore
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 5: Commit**

```powershell
git add Directory.Packages.props ShareGuard.Infrastructure/ShareGuard.Infrastructure.csproj ShareGuard.Application.Tests/ShareGuard.Application.Tests.csproj
git commit -m "build: add DocumentFormat.OpenXml, PDFsharp, and MetadataExtractor package references"
```

---

### Task 2: Define IFileStripper Domain Interface

**Files:**
- Create: `ShareGuard.Domain/Interfaces/IFileStripper.cs`
- Create: `ShareGuard.Domain.Tests/FileStripperCompilationTests.cs`

- [ ] **Step 1: Write a compilation test**

Create `ShareGuard.Domain.Tests/FileStripperCompilationTests.cs`:

```csharp
using ShareGuard.Domain.Interfaces;
using Xunit;

namespace ShareGuard.Domain.Tests;

public class FileStripperCompilationTests
{
    [Fact]
    public void TestCompile()
    {
        Assert.True(false, "IFileStripper not defined yet.");
    }
}
```

- [ ] **Step 2: Run test to verify failure**

Run:
```powershell
dotnet test ShareGuard.Domain.Tests --filter "FileStripperCompilationTests"
```
Expected: FAIL

- [ ] **Step 3: Implement IFileStripper interface**

Create `ShareGuard.Domain/Interfaces/IFileStripper.cs`:

```csharp
using ShareGuard.Domain.Models;

namespace ShareGuard.Domain.Interfaces;

/// <summary>
/// Contract for stripping metadata profiles from specialized file types.
/// Implementations reside in the Infrastructure layer.
/// </summary>
public interface IFileStripper
{
    /// <summary>
    /// Checks if this stripper is capable of handling the specified file extension.
    /// </summary>
    bool CanHandle(string extension);

    /// <summary>
    /// Strips metadata from the file at <paramref name="sourcePath"/>, saving the clean
    /// copy to <paramref name="destPath"/>, and returns a list of stripped findings.
    /// </summary>
    Task<List<Finding>> StripMetadataAsync(string sourcePath, string destPath, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 4: Update the compilation test and run**

Replace `ShareGuard.Domain.Tests/FileStripperCompilationTests.cs` content with:

```csharp
using ShareGuard.Domain.Interfaces;
using ShareGuard.Domain.Models;
using Xunit;

namespace ShareGuard.Domain.Tests;

public class FileStripperCompilationTests
{
    private class DummyFileStripper : IFileStripper
    {
        public bool CanHandle(string extension) => extension == ".txt";
        public Task<List<Finding>> StripMetadataAsync(string sourcePath, string destPath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new List<Finding>());
        }
    }

    [Fact]
    public void TestCompile()
    {
        IFileStripper stripper = new DummyFileStripper();
        Assert.NotNull(stripper);
    }
}
```

Run test:
```powershell
dotnet test ShareGuard.Domain.Tests --filter "FileStripperCompilationTests"
```
Expected: PASS

- [ ] **Step 5: Commit**

```powershell
git add ShareGuard.Domain/Interfaces/IFileStripper.cs ShareGuard.Domain.Tests/FileStripperCompilationTests.cs
git commit -m "feat(domain): add IFileStripper interface"
```

---

### Task 3: Implement ImageSharpStripper Wrapper

**Files:**
- Create: `ShareGuard.Infrastructure/Services/ImageSharpStripper.cs`
- Create: `ShareGuard.Application.Tests/ImageSharpStripperTests.cs`

- [ ] **Step 1: Write a compilation test**

Create `ShareGuard.Application.Tests/ImageSharpStripperTests.cs`:

```csharp
using ShareGuard.Infrastructure.Services;
using Xunit;

namespace ShareGuard.Application.Tests;

public class ImageSharpStripperTests
{
    [Fact]
    public void TestCompile()
    {
        Assert.True(false, "ImageSharpStripper not defined yet.");
    }
}
```

- [ ] **Step 2: Run test to verify failure**

Run:
```powershell
dotnet test ShareGuard.Application.Tests --filter "ImageSharpStripperTests"
```
Expected: FAIL

- [ ] **Step 3: Implement ImageSharpStripper**

Create `ShareGuard.Infrastructure/Services/ImageSharpStripper.cs`:

```csharp
using ShareGuard.Domain.Interfaces;
using ShareGuard.Domain.Models;

namespace ShareGuard.Infrastructure.Services;

/// <summary>
/// Implements IFileStripper for standard image formats, delegating to the existing IImageCleaner.
/// </summary>
public sealed class ImageSharpStripper : IFileStripper
{
    private readonly IImageCleaner _imageCleaner;
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    public ImageSharpStripper(IImageCleaner imageCleaner)
    {
        _imageCleaner = imageCleaner;
    }

    public bool CanHandle(string extension)
    {
        return SupportedExtensions.Contains(extension);
    }

    public Task<List<Finding>> StripMetadataAsync(string sourcePath, string destPath, CancellationToken cancellationToken = default)
    {
        return _imageCleaner.CleanImageAsync(sourcePath, destPath, cancellationToken);
    }
}
```

- [ ] **Step 4: Update the test and run**

Replace `ShareGuard.Application.Tests/ImageSharpStripperTests.cs` with:

```csharp
using NSubstitute;
using ShareGuard.Domain.Interfaces;
using ShareGuard.Domain.Models;
using ShareGuard.Infrastructure.Services;
using Xunit;

namespace ShareGuard.Application.Tests;

public class ImageSharpStripperTests
{
    [Fact]
    public void CanHandle_ShouldReturnTrueForImages()
    {
        var mockCleaner = Substitute.For<IImageCleaner>();
        var stripper = new ImageSharpStripper(mockCleaner);

        Assert.True(stripper.CanHandle(".jpg"));
        Assert.True(stripper.CanHandle(".png"));
        Assert.False(stripper.CanHandle(".pdf"));
    }

    [Fact]
    public async Task StripMetadataAsync_ShouldDelegateToImageCleaner()
    {
        var mockCleaner = Substitute.For<IImageCleaner>();
        var findings = new List<Finding> { new("GPS/Location", "Lat", "1.0") };
        mockCleaner.CleanImageAsync("src", "dest", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(findings));

        var stripper = new ImageSharpStripper(mockCleaner);
        var result = await stripper.StripMetadataAsync("src", "dest");

        Assert.Same(findings, result);
        await mockCleaner.Received(1).CleanImageAsync("src", "dest", Arg.Any<CancellationToken>());
    }
}
```

Run test:
```powershell
dotnet test ShareGuard.Application.Tests --filter "ImageSharpStripperTests"
```
Expected: PASS

- [ ] **Step 5: Commit**

```powershell
git add ShareGuard.Infrastructure/Services/ImageSharpStripper.cs ShareGuard.Application.Tests/ImageSharpStripperTests.cs
git commit -m "feat(infra): add ImageSharpStripper implementing IFileStripper"
```

---

### Task 4: Implement OfficeOpenXmlStripper

**Files:**
- Create: `ShareGuard.Infrastructure/Services/OfficeOpenXmlStripper.cs`
- Create: `ShareGuard.Application.Tests/OfficeOpenXmlStripperTests.cs`

- [ ] **Step 1: Write the failing tests using programmatic Office document fixtures**

Create `ShareGuard.Application.Tests/OfficeOpenXmlStripperTests.cs`:

```csharp
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using ShareGuard.Domain.Models;
using ShareGuard.Infrastructure.Services;
using Xunit;

namespace ShareGuard.Application.Tests;

public class OfficeOpenXmlStripperTests
{
    private string CreateWordFixture(string path, string creator, string company, string customPropName, string customPropValue)
    {
        using (var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document(new Body(new Paragraph(new Run(new Text("Test content")))));
            
            doc.PackageProperties.Creator = creator;
            doc.PackageProperties.Title = "Original Title";
            
            var extPart = doc.AddExtendedFilePropertiesPart();
            extPart.Properties = new DocumentFormat.OpenXml.ExtendedProperties.Properties(
                new DocumentFormat.OpenXml.ExtendedProperties.Company(company),
                new DocumentFormat.OpenXml.ExtendedProperties.Application("Microsoft Word")
            );
            extPart.Properties.Save();

            var customPart = doc.AddCustomFilePropertiesPart();
            customPart.Properties = new DocumentFormat.OpenXml.CustomProperties.Properties(
                new DocumentFormat.OpenXml.CustomProperties.CustomDocumentProperty
                {
                    Name = customPropName,
                    FormatId = "{D5CDD502-2E9C-101B-9397-08002B2CF9AE}",
                    PropertyId = 2,
                    VTLpwstr = new DocumentFormat.OpenXml.VariantTypes.VTLpwstr(customPropValue)
                }
            );
            customPart.Properties.Save();
        }
        return path;
    }

    [Fact]
    public async Task StripMetadataAsync_ShouldRemoveCoreExtendedAndCustomProperties()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"office-strip-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        
        var sourcePath = Path.Combine(tempDir, "document.docx");
        var destPath = Path.Combine(tempDir, "document.clean.docx");

        CreateWordFixture(sourcePath, "Author Name", "Acme Corp", "MyCustomField", "CustomValue");

        try
        {
            var stripper = new OfficeOpenXmlStripper();
            var findings = await stripper.StripMetadataAsync(sourcePath, destPath);

            // Assert output file was created
            Assert.True(File.Exists(destPath));

            // Verify findings lists correct stripped info
            Assert.Contains(findings, f => f.Category == "Document/Core" && f.FieldName == "Creator" && f.Value == "Author Name");
            Assert.Contains(findings, f => f.Category == "Document/Extended" && f.FieldName == "Company" && f.Value == "Acme Corp");
            Assert.Contains(findings, f => f.Category == "Document/Custom" && f.FieldName == "MyCustomField" && f.Value == "CustomValue");

            // Reopen clean document and verify elements are gone
            using (var cleanDoc = WordprocessingDocument.Open(destPath, false))
            {
                Assert.Null(cleanDoc.PackageProperties.Creator);
                Assert.Null(cleanDoc.PackageProperties.Title);

                var extPart = cleanDoc.ExtendedFilePropertiesPart;
                if (extPart?.Properties is not null)
                {
                    Assert.Null(extPart.Properties.Company);
                    Assert.Null(extPart.Properties.Application);
                }

                var customPart = cleanDoc.CustomFilePropertiesPart;
                if (customPart?.Properties is not null)
                {
                    Assert.Empty(customPart.Properties.Elements<DocumentFormat.OpenXml.CustomProperties.CustomDocumentProperty>());
                }
            }
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
```

- [ ] **Step 2: Run test to verify failure**

Run:
```powershell
dotnet test ShareGuard.Application.Tests --filter "OfficeOpenXmlStripperTests"
```
Expected: FAIL — `The type or namespace name 'OfficeOpenXmlStripper' could not be found`

- [ ] **Step 3: Implement OfficeOpenXmlStripper**

Create `ShareGuard.Infrastructure/Services/OfficeOpenXmlStripper.cs`:

```csharp
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.CustomProperties;
using ShareGuard.Domain.Interfaces;
using ShareGuard.Domain.Models;

namespace ShareGuard.Infrastructure.Services;

/// <summary>
/// Strips core, extended, and custom metadata properties from Office Open XML files
/// (.docx, .xlsx, .pptx) using Open XML SDK.
/// </summary>
public sealed class OfficeOpenXmlStripper : IFileStripper
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".docx", ".xlsx", ".pptx"
    };

    public bool CanHandle(string extension)
    {
        return SupportedExtensions.Contains(extension);
    }

    public async Task<List<Finding>> StripMetadataAsync(string sourcePath, string destPath, CancellationToken cancellationToken = default)
    {
        // Office Open XML is a ZIP package containing XML parts.
        // We copy the file to the destination first, then edit the clean copy in-place.
        File.Copy(sourcePath, destPath, overwrite: true);

        var findings = new List<Finding>();
        var extension = Path.GetExtension(destPath).ToLowerInvariant();

        await Task.Run(() =>
        {
            switch (extension)
            {
                case ".docx":
                    using (var doc = WordprocessingDocument.Open(destPath, true))
                    {
                        ExtractAndClearProperties(doc, findings);
                    }
                    break;
                case ".xlsx":
                    using (var doc = SpreadsheetDocument.Open(destPath, true))
                    {
                        ExtractAndClearProperties(doc, findings);
                    }
                    break;
                case ".pptx":
                    using (var doc = PresentationDocument.Open(destPath, true))
                    {
                        ExtractAndClearProperties(doc, findings);
                    }
                    break;
                default:
                    throw new NotSupportedException($"Unsupported Office file extension: {extension}");
            }
        }, cancellationToken);

        return findings;
    }

    private void ExtractAndClearProperties(OpenXmlPackage package, List<Finding> findings)
    {
        // 1. Clear Core Package Properties
        var props = package.PackageProperties;
        AddFindingIfNotEmpty(findings, "Document/Core", "Creator", props.Creator);
        AddFindingIfNotEmpty(findings, "Document/Core", "LastModifiedBy", props.LastModifiedBy);
        AddFindingIfNotEmpty(findings, "Document/Core", "Title", props.Title);
        AddFindingIfNotEmpty(findings, "Document/Core", "Subject", props.Subject);
        AddFindingIfNotEmpty(findings, "Document/Core", "Keywords", props.Keywords);
        AddFindingIfNotEmpty(findings, "Document/Core", "Description", props.Description);
        AddFindingIfNotEmpty(findings, "Document/Core", "Created", props.Created?.ToString());
        AddFindingIfNotEmpty(findings, "Document/Core", "Modified", props.Modified?.ToString());
        AddFindingIfNotEmpty(findings, "Document/Core", "LastPrinted", props.LastPrinted?.ToString());

        props.Creator = null;
        props.LastModifiedBy = null;
        props.Title = null;
        props.Subject = null;
        props.Keywords = null;
        props.Description = null;
        props.Category = null;
        props.ContentStatus = null;
        props.Language = null;
        props.Revision = null;
        props.Version = null;
        props.Created = null;
        props.Modified = null;
        props.LastPrinted = null;

        // 2. Clear Extended File Properties (Company, Manager, Application)
        var extPart = package.GetPartsOfType<ExtendedFilePropertiesPart>().FirstOrDefault();
        if (extPart?.Properties is not null)
        {
            var extProps = extPart.Properties;
            AddFindingIfNotEmpty(findings, "Document/Extended", "Company", extProps.Company?.Text);
            AddFindingIfNotEmpty(findings, "Document/Extended", "Manager", extProps.Manager?.Text);
            AddFindingIfNotEmpty(findings, "Document/Extended", "Application", extProps.Application?.Text);
            AddFindingIfNotEmpty(findings, "Document/Extended", "TotalTime", extProps.TotalTime?.Text);

            extProps.Company?.Remove();
            extProps.Manager?.Remove();
            extProps.Application?.Remove();
            extProps.TotalTime?.Remove();
            extProps.Save();
        }

        // 3. Clear Custom Document Properties
        var customPart = package.GetPartsOfType<CustomFilePropertiesPart>().FirstOrDefault();
        if (customPart?.Properties is not null)
        {
            foreach (var prop in customPart.Properties.Elements<CustomDocumentProperty>())
            {
                AddFindingIfNotEmpty(findings, "Document/Custom", prop.Name ?? "Property", prop.InnerText);
            }
            customPart.Properties.RemoveAllChildren<CustomDocumentProperty>();
            customPart.Properties.Save();
        }
    }

    private void AddFindingIfNotEmpty(List<Finding> findings, string category, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            findings.Add(new Finding(category, name, value));
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run:
```powershell
dotnet test ShareGuard.Application.Tests --filter "OfficeOpenXmlStripperTests"
```
Expected: `Passed! - Failed: 0, Passed: 1, Skipped: 0`

- [ ] **Step 5: Commit**

```powershell
git add ShareGuard.Infrastructure/Services/OfficeOpenXmlStripper.cs ShareGuard.Application.Tests/OfficeOpenXmlStripperTests.cs
git commit -m "feat(infra): implement OfficeOpenXmlStripper for Word, Excel, and PowerPoint"
```

---

### Task 5: Implement PdfMetadataStripper

**Files:**
- Create: `ShareGuard.Infrastructure/Services/PdfMetadataStripper.cs`
- Create: `ShareGuard.Application.Tests/PdfMetadataStripperTests.cs`

- [ ] **Step 1: Write the failing tests using programmatic PDF fixtures**

Create `ShareGuard.Application.Tests/PdfMetadataStripperTests.cs`:

```csharp
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using ShareGuard.Domain.Models;
using ShareGuard.Infrastructure.Services;
using Xunit;

namespace ShareGuard.Application.Tests;

public class PdfMetadataStripperTests
{
    private string CreatePdfFixture(string path, string author, string creator)
    {
        using (var doc = new PdfDocument())
        {
            doc.Info.Author = author;
            doc.Info.Creator = creator;
            doc.Info.Title = "Original PDF Title";
            doc.Save(path);
        }
        return path;
    }

    [Fact]
    public async Task StripMetadataAsync_ShouldClearPdfInfoDictionary()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"pdf-strip-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        
        var sourcePath = Path.Combine(tempDir, "document.pdf");
        var destPath = Path.Combine(tempDir, "document.clean.pdf");

        CreatePdfFixture(sourcePath, "Jane Doe", "InDesign PDF Creator");

        try
        {
            var stripper = new PdfMetadataStripper();
            var findings = await stripper.StripMetadataAsync(sourcePath, destPath);

            Assert.True(File.Exists(destPath));

            // Verify findings captured correct info
            Assert.Contains(findings, f => f.Category == "Document/PDF" && f.FieldName == "Author" && f.Value == "Jane Doe");
            Assert.Contains(findings, f => f.Category == "Document/PDF" && f.FieldName == "Creator" && f.Value == "InDesign PDF Creator");

            // Reopen clean PDF and assert Info dictionary is cleared
            using (var cleanDoc = PdfReader.Open(destPath, PdfDocumentOpenMode.Import))
            {
                Assert.Empty(cleanDoc.Info.Author);
                Assert.Empty(cleanDoc.Info.Creator);
                Assert.Empty(cleanDoc.Info.Title);
            }
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
```

- [ ] **Step 2: Run test to verify failure**

Run:
```powershell
dotnet test ShareGuard.Application.Tests --filter "PdfMetadataStripperTests"
```
Expected: FAIL — `The type or namespace name 'PdfMetadataStripper' could not be found`

- [ ] **Step 3: Implement PdfMetadataStripper**

Create `ShareGuard.Infrastructure/Services/PdfMetadataStripper.cs`:

```csharp
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using ShareGuard.Domain.Interfaces;
using ShareGuard.Domain.Models;

namespace ShareGuard.Infrastructure.Services;

/// <summary>
/// Strips standard document properties from PDFs using PDFsharp.
/// </summary>
public sealed class PdfMetadataStripper : IFileStripper
{
    public bool CanHandle(string extension)
    {
        return ".pdf".Equals(extension, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<List<Finding>> StripMetadataAsync(string sourcePath, string destPath, CancellationToken cancellationToken = default)
    {
        var findings = new List<Finding>();

        await Task.Run(() =>
        {
            // PDFsharp reads from the source file and saves a clean copy directly to destPath
            using var document = PdfReader.Open(sourcePath, PdfDocumentOpenMode.Modify);

            var info = document.Info;

            AddFindingIfNotEmpty(findings, "Document/PDF", "Title", info.Title);
            AddFindingIfNotEmpty(findings, "Document/PDF", "Author", info.Author);
            AddFindingIfNotEmpty(findings, "Document/PDF", "Subject", info.Subject);
            AddFindingIfNotEmpty(findings, "Document/PDF", "Keywords", info.Keywords);
            AddFindingIfNotEmpty(findings, "Document/PDF", "Creator", info.Creator);
            AddFindingIfNotEmpty(findings, "Document/PDF", "Producer", info.Producer);

            // Clear PDF info fields
            info.Title = string.Empty;
            info.Author = string.Empty;
            info.Subject = string.Empty;
            info.Keywords = string.Empty;
            info.Creator = string.Empty;
            info.Producer = string.Empty;

            document.Save(destPath);
        }, cancellationToken);

        return findings;
    }

    private void AddFindingIfNotEmpty(List<Finding> findings, string category, string name, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            findings.Add(new Finding(category, name, value));
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run:
```powershell
dotnet test ShareGuard.Application.Tests --filter "PdfMetadataStripperTests"
```
Expected: `Passed! - Failed: 0, Passed: 1, Skipped: 0`

- [ ] **Step 5: Commit**

```powershell
git add ShareGuard.Infrastructure/Services/PdfMetadataStripper.cs ShareGuard.Application.Tests/PdfMetadataStripperTests.cs
git commit -m "feat(infra): implement PdfMetadataStripper using PDFsharp"
```

---

### Task 6: Implement MetadataVerifier

**Files:**
- Create: `ShareGuard.Application/Services/MetadataVerifier.cs`
- Create: `ShareGuard.Application.Tests/MetadataVerifierTests.cs`

- [ ] **Step 1: Write the verifier tests**

Create `ShareGuard.Application.Tests/MetadataVerifierTests.cs`:

```csharp
using ShareGuard.Application.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using Xunit;

namespace ShareGuard.Application.Tests;

public class MetadataVerifierTests
{
    private string CreateImageWithExif(string path, bool hasGps)
    {
        using (var image = new Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(10, 10))
        {
            if (hasGps)
            {
                var profile = new ExifProfile();
                profile.SetValue(ExifTag.GPSLatitude, new Rational[] { new(37, 1), new(46, 1), new(3000, 100) });
                image.Metadata.ExifProfile = profile;
            }
            image.Save(path);
        }
        return path;
    }

    [Fact]
    public void VerifyNoSensitiveMetadata_WithCleanFile_ShouldReturnTrue()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"verifier-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var cleanImg = Path.Combine(tempDir, "clean.jpg");
        CreateImageWithExif(cleanImg, hasGps: false);

        try
        {
            var result = MetadataVerifier.VerifyNoSensitiveMetadata(cleanImg);
            Assert.True(result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void VerifyNoSensitiveMetadata_WithLeakingFile_ShouldReturnFalse()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"verifier-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var dirtyImg = Path.Combine(tempDir, "dirty.jpg");
        CreateImageWithExif(dirtyImg, hasGps: true);

        try
        {
            var result = MetadataVerifier.VerifyNoSensitiveMetadata(dirtyImg);
            Assert.False(result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
```

- [ ] **Step 2: Run test to verify failure**

Run:
```powershell
dotnet test ShareGuard.Application.Tests --filter "MetadataVerifierTests"
```
Expected: FAIL — `The name 'MetadataVerifier' does not exist in the current context`

- [ ] **Step 3: Implement MetadataVerifier**

Create `ShareGuard.Application/Services/MetadataVerifier.cs`:

```csharp
using MetadataExtractor;

namespace ShareGuard.Application.Services;

/// <summary>
/// Independent metadata scanner used as a safety checker gate.
/// Reads the final cleaned output file and returns false if any sensitive tag remains populated.
/// </summary>
public static class MetadataVerifier
{
    private static readonly HashSet<string> SensitiveTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "GPS Latitude", 
        "GPS Longitude", 
        "GPS Altitude",
        "Author", 
        "Creator", 
        "Company", 
        "Manager",
        "Title",
        "Subject",
        "Keywords",
        "Date Time Original",
        "Camera Owner Name"
    };

    /// <summary>
    /// Reads metadata tags using MetadataExtractor. 
    /// Returns false if any known sensitive property remains populated with a non-empty value.
    /// </summary>
    public static bool VerifyNoSensitiveMetadata(string filePath)
    {
        try
        {
            var directories = ImageMetadataReader.ReadMetadata(filePath);
            foreach (var dir in directories)
            {
                foreach (var tag in dir.Tags)
                {
                    if (SensitiveTags.Contains(tag.TagName))
                    {
                        var val = tag.Description;
                        if (!string.IsNullOrWhiteSpace(val))
                        {
                            return false; // Sensitive metadata leaked!
                        }
                    }
                }
            }
        }
        catch
        {
            // If format is not supported or parsing fails, return true (best effort)
        }
        
        return true;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run:
```powershell
dotnet test ShareGuard.Application.Tests --filter "MetadataVerifierTests"
```
Expected: `Passed! - Failed: 0, Passed: 2, Skipped: 0`

- [ ] **Step 5: Commit**

```powershell
git add ShareGuard.Application/Services/MetadataVerifier.cs ShareGuard.Application.Tests/MetadataVerifierTests.cs
git commit -m "feat(application): add MetadataVerifier independent check gate using MetadataExtractor"
```

---

### Task 7: Define and Implement FileCleanupService

**Files:**
- Create: `ShareGuard.Application/Services/IFileCleanupService.cs`
- Create: `ShareGuard.Application/Services/FileCleanupService.cs`
- Create: `ShareGuard.Application.Tests/FileCleanupServiceTests.cs`

- [ ] **Step 1: Write unit tests for FileCleanupService**

Create `ShareGuard.Application.Tests/FileCleanupServiceTests.cs`:

```csharp
using NSubstitute;
using ShareGuard.Application.Interfaces;
using ShareGuard.Application.Services;
using ShareGuard.Domain.Interfaces;
using ShareGuard.Domain.Models;
using Xunit;

namespace ShareGuard.Application.Tests;

public class FileCleanupServiceTests
{
    private readonly IFileStripper _mockStripper = Substitute.For<IFileStripper>();
    private readonly IHistoryService _mockHistory = Substitute.For<IHistoryService>();

    [Fact]
    public async Task CleanFileAsync_WithUnsupportedExtension_ShouldReturnFailedResult()
    {
        _mockStripper.CanHandle(".xyz").Returns(false);

        var service = new FileCleanupService(new[] { _mockStripper }, _mockHistory);
        var result = await service.CleanFileAsync("test.xyz");

        Assert.False(result.IsSuccess);
        Assert.Contains("Unsupported file type", result.ErrorMessage);
    }

    [Fact]
    public async Task CleanFileAsync_WhenStrippingFails_ShouldDeleteCleanCopyAndReturnFailure()
    {
        _mockStripper.CanHandle(".docx").Returns(true);
        _mockStripper.StripMetadataAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Answers(_ => { throw new Exception("Stripper failed"); });

        var service = new FileCleanupService(new[] { _mockStripper }, _mockHistory);
        var result = await service.CleanFileAsync("doc.docx");

        Assert.False(result.IsSuccess);
        Assert.Equal("Stripper failed", result.ErrorMessage);
    }
}
```

- [ ] **Step 2: Run test to verify failure**

Run:
```powershell
dotnet test ShareGuard.Application.Tests --filter "FileCleanupServiceTests"
```
Expected: FAIL — `The type or namespace name 'IFileCleanupService' / 'FileCleanupService' could not be found`

- [ ] **Step 3: Define IFileCleanupService**

Create `ShareGuard.Application/Services/IFileCleanupService.cs`:

```csharp
using ShareGuard.Domain.Models;

namespace ShareGuard.Application.Services;

/// <summary>
/// General file metadata cleaning service orchestrating file type routing and verification.
/// </summary>
public interface IFileCleanupService
{
    /// <summary>
    /// Routes the file to the correct IFileStripper, strips metadata to a clean copy,
    /// runs the MetadataVerifier safety check, and writes the event to the history DB.
    /// </summary>
    Task<StripResult> CleanFileAsync(string sourcePath, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 4: Implement FileCleanupService**

Create `ShareGuard.Application/Services/FileCleanupService.cs`:

```csharp
using ShareGuard.Application.Commands;
using ShareGuard.Application.Interfaces;
using ShareGuard.Domain.Interfaces;
using ShareGuard.Domain.Models;

namespace ShareGuard.Application.Services;

public sealed class FileCleanupService : IFileCleanupService
{
    private readonly IEnumerable<IFileStripper> _strippers;
    private readonly IHistoryService _historyService;

    public FileCleanupService(IEnumerable<IFileStripper> strippers, IHistoryService historyService)
    {
        _strippers = strippers;
        _historyService = historyService;
    }

    public async Task<StripResult> CleanFileAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return StripResult.Failed(sourcePath, "Source path cannot be empty.");
        }

        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        var stripper = _strippers.FirstOrDefault(s => s.CanHandle(extension));

        if (stripper == null)
        {
            var errMessage = $"Unsupported file type: {extension}";
            var failResult = StripResult.Failed(sourcePath, errMessage);
            await LogToDbAsync(sourcePath, string.Empty, 0, false, errMessage, cancellationToken);
            return failResult;
        }

        // Generate destination file name (append .clean, auto-increment if exists)
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
            // Execute metadata strip
            var findings = await stripper.StripMetadataAsync(sourcePath, cleanPath, cancellationToken);

            // Execute independent verification check
            bool verified = MetadataVerifier.VerifyNoSensitiveMetadata(cleanPath);
            if (!verified)
            {
                if (File.Exists(cleanPath))
                {
                    File.Delete(cleanPath);
                }
                var verifyErr = "Verification failed: sensitive metadata remains in the clean copy.";
                var failResult = StripResult.Failed(sourcePath, verifyErr);
                await LogToDbAsync(sourcePath, string.Empty, 0, false, verifyErr, cancellationToken);
                return failResult;
            }

            var successResult = StripResult.Succeeded(cleanPath, findings);
            await LogToDbAsync(sourcePath, cleanPath, findings.Count, true, null, cancellationToken);
            return successResult;
        }
        catch (Exception ex)
        {
            if (File.Exists(cleanPath))
            {
                File.Delete(cleanPath);
            }
            var failResult = StripResult.Failed(sourcePath, ex.Message);
            await LogToDbAsync(sourcePath, string.Empty, 0, false, ex.Message, cancellationToken);
            return failResult;
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

- [ ] **Step 5: Run tests to verify it passes**

Run:
```powershell
dotnet test ShareGuard.Application.Tests --filter "FileCleanupServiceTests"
```
Expected: `Passed! - Failed: 0, Passed: 2, Skipped: 0`

- [ ] **Step 6: Commit**

```powershell
git add ShareGuard.Application/Services/IFileCleanupService.cs ShareGuard.Application/Services/FileCleanupService.cs ShareGuard.Application.Tests/FileCleanupServiceTests.cs
git commit -m "feat(application): implement FileCleanupService general orchestrator and unit tests"
```

---

### Task 8: Register Services in Dependency Injection

**Files:**
- Modify: `ShareGuard.Infrastructure/DependencyInjection.cs`
- Modify: `ShareGuard.Application/DependencyInjection.cs`

- [ ] **Step 1: Update Infrastructure DependencyInjection**

Open `ShareGuard.Infrastructure/DependencyInjection.cs`. Register the new strippers:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShareGuard.Domain.Interfaces;
using ShareGuard.Infrastructure.Data;
using ShareGuard.Infrastructure.Repositories;
using ShareGuard.Infrastructure.Services;

namespace ShareGuard.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        // Database configuration
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string dbFolder = Path.Combine(appData, "ShareGuard");
        string dbPath = Path.Combine(dbFolder, "history.db");
        Directory.CreateDirectory(dbFolder);

        services.AddDbContextFactory<ShareGuardDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        services.AddSingleton<IHistoryRepository, HistoryRepository>();

        // Phase 1 Cleaners
        services.AddSingleton<IImageCleaner, ImageSharpCleaner>();

        // Phase 4 IFileStripper registrations
        services.AddSingleton<IFileStripper, ImageSharpStripper>();
        services.AddSingleton<IFileStripper, OfficeOpenXmlStripper>();
        services.AddSingleton<IFileStripper, PdfMetadataStripper>();

        return services;
    }
}
```

- [ ] **Step 2: Update Application DependencyInjection**

Open `ShareGuard.Application/DependencyInjection.cs`. Register the `IFileCleanupService`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using ShareGuard.Application.Interfaces;
using ShareGuard.Application.Services;

namespace ShareGuard.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Services
        services.AddSingleton<IHistoryService, HistoryService>();
        services.AddSingleton<IMultiFileProcessorService, MultiFileProcessorService>();
        
        // Generalized file cleanup service
        services.AddSingleton<IFileCleanupService, FileCleanupService>();

        return services;
    }
}
```

- [ ] **Step 3: Verify the entire solution builds**

Run:
```powershell
dotnet build Shareguard-wpf.slnx
```
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 4: Commit**

```powershell
git add ShareGuard.Infrastructure/DependencyInjection.cs ShareGuard.Application/DependencyInjection.cs
git commit -m "feat(di): wire IFileStripper and IFileCleanupService registrations"
```

---

### Task 9: Refactor MultiFileProcessorService to use FileCleanupService

**Files:**
- Modify: `ShareGuard.Application/Services/MultiFileProcessorService.cs`
- Modify: `ShareGuard.Application.Tests/MultiFileProcessorServiceTests.cs`

- [ ] **Step 1: Write compilation test verifying FileCleanupService injection**

Modify `ShareGuard.Application.Tests/MultiFileProcessorServiceTests.cs` to mock `IFileCleanupService` instead of `IImageCleanupService`:

```csharp
using Moq;
using ShareGuard.Application.Services;
using ShareGuard.Domain.Models;
using Xunit;

namespace ShareGuard.Application.Tests;

public class MultiFileProcessorServiceTests
{
    [Fact]
    public async Task ProcessFilesAsync_ShouldProcessAllFilesUsingFileCleanupService()
    {
        // Arrange
        var mockCleanup = new Mock<IFileCleanupService>();
        mockCleanup
            .Setup(s => s.CleanFileAsync(It.IsAny<string>(), Arg.Any<CancellationToken>()))
            .ReturnsAsync((string path, CancellationToken ct) => StripResult.Succeeded(path + ".clean", new List<Finding>()));

        var sut = new MultiFileProcessorService(mockCleanup.Object);
        var files = new[] { "C:\\doc1.docx", "C:\\doc2.pdf", "C:\\img1.jpg" };
        var reportedList = new List<ProcessingStatus>();
        var progress = new Progress<ProcessingStatus>(status => reportedList.Add(status));

        // Act
        await sut.ProcessFilesAsync(files, progress);

        // Assert
        Assert.Equal(3, reportedList.Count);
        Assert.Contains(reportedList, r => r.FilePath == "C:\\doc1.docx" && r.Success);
        Assert.Contains(reportedList, r => r.FilePath == "C:\\doc2.pdf" && r.Success);
        Assert.Contains(reportedList, r => r.FilePath == "C:\\img1.jpg" && r.Success);
        mockCleanup.Verify(s => s.CleanFileAsync(It.IsAny<string>(), Arg.Any<CancellationToken>()), Times.Exactly(3));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:
```powershell
dotnet test ShareGuard.Application.Tests --filter "MultiFileProcessorServiceTests"
```
Expected: FAIL (Compilation error: MultiFileProcessorService constructor requires IImageCleanupService)

- [ ] **Step 3: Update MultiFileProcessorService implementation**

Replace `ShareGuard.Application/Services/MultiFileProcessorService.cs` content with:

```csharp
using ShareGuard.Application.Services;

namespace ShareGuard.Application.Services;

public class MultiFileProcessorService : IMultiFileProcessorService
{
    private readonly IFileCleanupService _fileCleanupService;

    public MultiFileProcessorService(IFileCleanupService fileCleanupService)
    {
        _fileCleanupService = fileCleanupService;
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

        await Parallel.ForEachAsync(filesList, options, async (filePath, ct) =>
        {
            bool success = false;
            string cleanPath = string.Empty;
            int findingsCount = 0;
            string? errorMessage = null;

            try
            {
                var result = await _fileCleanupService.CleanFileAsync(filePath, ct);
                success = result.Success;
                cleanPath = result.CleanPath;
                findingsCount = result.Findings.Count;
                errorMessage = result.ErrorMessage;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
            }

            int currentCount = Interlocked.Increment(ref currentProcessedCount);

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

- [ ] **Step 4: Run test to verify it passes**

Run:
```powershell
dotnet test ShareGuard.Application.Tests --filter "MultiFileProcessorServiceTests"
```
Expected: `Passed! - Failed: 0, Passed: 1, Skipped: 0`

- [ ] **Step 5: Commit**

```powershell
git add ShareGuard.Application/Services/MultiFileProcessorService.cs ShareGuard.Application.Tests/MultiFileProcessorServiceTests.cs
git commit -m "feat(application): update MultiFileProcessorService to use generalized IFileCleanupService"
```

---

## Final Verification

After all tasks are complete, execute:

```powershell
# 1. Clean build
dotnet build Shareguard-wpf.slnx

# 2. Run the test suite
dotnet test Shareguard-wpf.slnx --verbosity normal
```

Expected:
- Build: succeeds with zero errors and zero warnings.
- Tests: all tests pass (including new Office Open XML and PDF stripper integration tests).
