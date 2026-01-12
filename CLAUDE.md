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

### DrawingContext Hit Testing
When using `FrameworkElement` with `OnRender()` for custom drawing, you MUST draw a transparent background to receive mouse events:
```csharp
protected override void OnRender(DrawingContext dc)
{
    // 투명 배경 그리기 (마우스 이벤트 수신을 위해 필수)
    // Draw transparent background (required for mouse event reception)
    dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));

    // ... rest of rendering
}
```

### Image Bounds Checking with Transforms
When implementing tools that should only work within transformed image bounds (zoom, pan, rotate):
```csharp
// 이미지 영역 내 클릭 확인
// Check if click is within image area
public bool IsPointWithinImage(Point point)
{
    var imageWidth = ImageSource.Width * ZoomFactor;
    var imageHeight = ImageSource.Height * ZoomFactor;
    var imageCenterX = ActualWidth / 2 + PanX;
    var imageCenterY = ActualHeight / 2 + PanY;

    // 회전 적용 시 역회전으로 점 변환
    // If rotated, inverse-rotate the point
    if (RotationAngle != 0)
    {
        var radians = -RotationAngle * Math.PI / 180;
        // Transform point...
    }

    return /* bounds check */;
}
```

## Performance Patterns

### SIMD Pixel Processing
For 16-bit DICOM pixel data, use `Vector256<short>` for 16-pixel parallel processing:
```csharp
if (Vector256.IsHardwareAccelerated && pixelData.BitsAllocated == 16)
{
    // Process 16 pixels at once with AVX2
    var scaleVector = Vector256.Create(scale);
    // ...
}
```

### Parallel Histogram Calculation
Use `ThreadLocal<T>` for thread-safe parallel aggregation:
```csharp
var localBins = new ThreadLocal<int[]>(() => new int[256], trackAllValues: true);
Parallel.For(0, pixelCount, i => localBins.Value![pixels[i]]++);

// Merge results
foreach (var local in localBins.Values)
    for (int i = 0; i < 256; i++)
        bins[i] += local[i];
```

### ArrayPool Memory Reuse
For temporary large arrays, use `ArrayPool<T>` to reduce GC pressure:
```csharp
var buffer = ArrayPool<byte>.Shared.Rent(size);
try
{
    // Use buffer...
}
finally
{
    ArrayPool<byte>.Shared.Return(buffer);
}
```

## Custom Controls

### Available Controls
| Control | Purpose | Key Features |
|---------|---------|--------------|
| `DicomImageViewer` | Main image display | Pan, Zoom, Rotate, Flip, W/L adjustment |
| `RulerOverlay` | Distance measurement | DrawingContext rendering, PixelSpacing-based mm calculation |
| `HistogramViewer` | Brightness distribution | Parallel calculation, W/L range overlay |
| `SliceNavigator` | Thumbnail navigation | VirtualizingPanel, async loading |

### Control Themes Location
All control themes are in `src/DicomViewer.UI/Themes/` and merged via `Generic.xaml`.
