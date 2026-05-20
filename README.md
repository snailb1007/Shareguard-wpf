# ShareGuard WPF

> Privacy-first, local-only metadata stripper for Windows Desktop.

ShareGuard WPF is a desktop utility that ports the privacy-centric concepts of the ShareGuard Android application to the Windows Desktop environment. It strips sensitive metadata (such as GPS/location data, camera details, timestamps, and software signatures) from files and tracks/affiliates from URLs, ensuring your shared content does not leak personal information.

> [!IMPORTANT]
> **Privacy by Design:** All operations are strictly local. ShareGuard does not upload files or URLs to any remote cloud services, and it never modifies your original files.

---

## Features

- **Metadata Stripping**: Automatically removes EXIF, GPS, IPTC, and XMP metadata from common image formats (JPEG, PNG, WEBP), with planned support for videos, PDFs, and Office documents.
- **URL Tracker Removal**: Cleans tracking parameters and affiliate identifiers from shared links.
- **Immutable Source Files**: Never modifies original source files. Output is always saved as a clean copy (e.g., `filename.clean.jpg`), with automatic collision handling (e.g., `filename.clean (1).jpg`).
- **Drag & Drop Interface**: Process files instantly by dragging them onto the window.
- **Detailed Findings UI**: Displays a categorized summary of what metadata was stripped (Location, Device, Software, etc.) directly in the application.

---

## Architecture & Project Structure

The project is designed using **Clean Architecture** combined with the **Model-View-ViewModel (MVVM)** pattern to enforce a strict separation of concerns.

```text
Input (Dropped File / Clipboard URL)
 └──> ShareItem Created
       └──> StripperRouter determines content type
             └──> Specific Stripper (e.g., ImageStripper)
                   ├──> 1. Pre-flight Check
                   ├──> 2. Strip Sensitive Data
                   ├──> 3. Privacy Leak Verification
                   └──> StripResult Generated
                         └──> Saved as Clean Copy & Logged to Local History
```

### Module Directory Breakdown

- **`ShareGuard.Domain`**: Pure, platform-agnostic class library containing the domain model, core entities (`ShareItem`, `StripResult`, `Finding`), stripper interfaces (`IStripper`), and domain exceptions.
- **`ShareGuard.Application`**: Handles service registration, application commands, and core business rules.
- **`ShareGuard.Infrastructure`**: Concrete implementations of media processing and external integrations (e.g., metadata cleaning via `SixLabors.ImageSharp`).
- **`Shareguard-wpf`**: The presentation layer. A modern WPF application utilizing a Fluent theme, built-in DI via the .NET Generic Host, and MVVM via `CommunityToolkit.Mvvm`.
- **`ShareGuard.Domain.Tests` & `ShareGuard.Application.Tests`**: Unit and architectural rule verification tests using `xunit.v3`.

---

## Technology Stack

- **Runtime**: .NET 10.0 (C# 14)
- **UI Framework**: WPF (fluent layout)
- **MVVM Toolkit**: `CommunityToolkit.Mvvm` 8.4
- **DI & Host Lifecycle**: `Microsoft.Extensions.Hosting` & `Microsoft.Extensions.DependencyInjection` 10.0
- **Image Processing**: `SixLabors.ImageSharp` 3.x
- **Unit Testing**: `xunit.v3` 1.1

---

## Getting Started

### Prerequisites

- **.NET 10.0 SDK** (preview or later)
- **Windows 10 / 11**

### Build

To restore packages and build the solution:

```bash
dotnet build
```

### Run the App

To run the WPF application:

```bash
dotnet run --project Shareguard-wpf
```

### Run Tests

To run the architectural and unit tests:

```bash
dotnet test
```

> [!NOTE]
> Architecture tests enforce that the domain layer (`ShareGuard.Domain`) remains entirely platform-agnostic and never references UI components, WPF, or infrastructure assemblies.
