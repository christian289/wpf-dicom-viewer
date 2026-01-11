# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Test Commands

```bash
# Build the main application
dotnet build src\DicomViewer.WpfApp\DicomViewer.WpfApp.csproj

# Run all tests
dotnet test src\DicomViewer.Tests\DicomViewer.Tests.csproj

# Run specific test
dotnet test src\DicomViewer.Tests\DicomViewer.Tests.csproj --filter "FullyQualifiedName~TciaServiceTests"

# Run application
dotnet run --project src\DicomViewer.WpfApp\DicomViewer.WpfApp.csproj
```

## Architecture Overview

WPF DICOM Viewer using **Clean Architecture** with 7 projects:

```
┌─────────────────────────────────────────────────────────────────┐
│                      DicomViewer.WpfApp                         │
│               (Entry point, DI configuration)                   │
├─────────────────┬─────────────────┬─────────────────────────────┤
│ DicomViewer.UI  │DicomViewer.     │ DicomViewer.ViewModels      │
│ (CustomControls)│WpfServices      │ (MVVM ViewModels)           │
├─────────────────┴─────────────────┴─────────────────────────────┤
│                   DicomViewer.Infrastructure                    │
│              (fo-dicom, TCIA REST API, PACS)                    │
├─────────────────────────────────────────────────────────────────┤
│                   DicomViewer.Application                       │
│              (DTOs, Interfaces, Services)                       │
├─────────────────────────────────────────────────────────────────┤
│                     DicomViewer.Domain                          │
│              (Entities, ValueObjects, Enums)                    │
└─────────────────────────────────────────────────────────────────┘
```

### Layer Dependencies (Top to Bottom Only)
- **WpfApp**: References all layers, configures DI via `Microsoft.Extensions.Hosting`
- **ViewModels**: Uses `CommunityToolkit.Mvvm`, must NOT reference WPF assemblies
- **Infrastructure**: Implements interfaces from Application layer
- **Application**: Defines interfaces and DTOs (no UI references)
- **Domain**: Pure domain models (no external dependencies)

### Key Technologies
- **.NET 10.0-windows** (defined in `Directory.Build.props`)
- **fo-dicom**: DICOM file parsing and pixel data extraction
- **TCIA REST API**: Download public DICOM datasets from The Cancer Imaging Archive
- **CommunityToolkit.Mvvm**: MVVM framework with source generators

### MVVM Rules
- ViewModels use `[ObservableProperty]` and `[RelayCommand]` attributes
- ViewModels must NOT reference `System.Windows.*` or WPF assemblies
- Use `WeakReferenceMessenger` for cross-ViewModel communication
- Custom Controls in `DicomViewer.UI` project with standalone ResourceDictionary styles

### TCIA Cache Location
Downloaded DICOM files are cached at: `%LocalAppData%\DicomViewer\TciaCache\`

## Common WPF Patterns Used

### Custom Control Icon Fonts
When using Segoe Fluent Icons in CustomControls, always set explicit `FontFamily="Segoe UI"` on text elements to prevent icon font inheritance.

### Command Parameter Types
When binding enum types to `CommandParameter`, use `x:Static` instead of string:
```xml
<!-- Correct -->
CommandParameter="{x:Static viewmodels:ViewerTool.Pan}"

<!-- Wrong - causes ArgumentException -->
CommandParameter="Pan"
```

### Slider Index Display
For 0-based collection indices displayed to users, provide 1-based computed properties:
```csharp
[ObservableProperty]
[NotifyPropertyChangedFor(nameof(SliceDisplayNumber))]
private int _currentSliceIndex;

public int SliceDisplayNumber => CurrentSliceIndex + 1;
public int MaxSliceIndex => Math.Max(0, TotalSliceCount - 1);
```
