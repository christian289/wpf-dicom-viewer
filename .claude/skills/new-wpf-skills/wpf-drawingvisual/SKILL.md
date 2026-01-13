---
name: wpf-drawingvisual
description: WPF DrawingVisual을 사용한 경량 렌더링. ContainerVisual, VisualCollection, DrawingContext 활용. 대량 그래픽 요소, 차트, 게임 그래픽, 커스텀 렌더링 구현 시 이 스킬 적용.
---

# WPF DrawingVisual 패턴

DrawingVisual은 Shape보다 가볍고 빠른 시각적 요소로, 대량 렌더링에 적합합니다.

## 1. Visual 계층 구조

```
Visual (추상)
├── UIElement
│   └── FrameworkElement
│       └── Shape (무거움, 이벤트 지원)
│
├── DrawingVisual       ← 경량, 이벤트 없음, 직접 렌더링
├── ContainerVisual     ← 여러 Visual 그룹화
└── HostVisual          ← 크로스 스레드 Visual
```

---

## 2. DrawingVisual vs Shape

| 구분 | DrawingVisual | Shape |
|------|--------------|-------|
| **오버헤드** | 낮음 | 높음 |
| **레이아웃** | 미참여 | 참여 |
| **이벤트** | 직접 구현 | 기본 지원 |
| **데이터 바인딩** | 불가 | 가능 |
| **용도** | 대량 요소, 성능 중시 | 인터랙티브 UI |

---

## 3. 기본 DrawingVisual 호스트

### 3.1 FrameworkElement 호스트

```csharp
namespace MyApp.Controls;

using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

/// <summary>
/// DrawingVisual을 호스팅하는 컨트롤
/// Control that hosts DrawingVisual
/// </summary>
public sealed class DrawingVisualHost : FrameworkElement
{
    private readonly List<Visual> _visuals = [];

    protected override int VisualChildrenCount => _visuals.Count;

    protected override Visual GetVisualChild(int index)
    {
        return _visuals[index];
    }

    /// <summary>
    /// Visual 추가
    /// Add Visual
    /// </summary>
    public void AddVisual(Visual visual)
    {
        _visuals.Add(visual);
        AddVisualChild(visual);
        AddLogicalChild(visual);
    }

    /// <summary>
    /// Visual 제거
    /// Remove Visual
    /// </summary>
    public void RemoveVisual(Visual visual)
    {
        _visuals.Remove(visual);
        RemoveVisualChild(visual);
        RemoveLogicalChild(visual);
    }

    /// <summary>
    /// 모든 Visual 제거
    /// Remove all Visuals
    /// </summary>
    public void ClearVisuals()
    {
        foreach (var visual in _visuals)
        {
            RemoveVisualChild(visual);
            RemoveLogicalChild(visual);
        }
        _visuals.Clear();
    }

    /// <summary>
    /// 좌표에서 Visual 검색 (Hit Testing)
    /// Find Visual at coordinate (Hit Testing)
    /// </summary>
    public Visual? GetVisualAt(Point point)
    {
        var hitResult = VisualTreeHelper.HitTest(this, point);
        return hitResult?.VisualHit;
    }
}
```

### 3.2 DrawingVisual 생성

```csharp
namespace MyApp.Graphics;

using System.Windows;
using System.Windows.Media;

public static class DrawingVisualFactory
{
    /// <summary>
    /// 원형 DrawingVisual 생성
    /// Create circular DrawingVisual
    /// </summary>
    public static DrawingVisual CreateCircle(
        Point center, 
        double radius, 
        Brush fill, 
        Pen? stroke = null)
    {
        var visual = new DrawingVisual();
        
        using (var dc = visual.RenderOpen())
        {
            dc.DrawEllipse(fill, stroke, center, radius, radius);
        }
        
        return visual;
    }

    /// <summary>
    /// 사각형 DrawingVisual 생성
    /// Create rectangle DrawingVisual
    /// </summary>
    public static DrawingVisual CreateRectangle(
        Rect rect, 
        Brush fill, 
        Pen? stroke = null)
    {
        var visual = new DrawingVisual();
        
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(fill, stroke, rect);
        }
        
        return visual;
    }

    /// <summary>
    /// 텍스트 DrawingVisual 생성
    /// Create text DrawingVisual
    /// </summary>
    public static DrawingVisual CreateText(
        string text, 
        Point origin, 
        Brush foreground,
        double fontSize = 12)
    {
        var visual = new DrawingVisual();
        
        var formattedText = new FormattedText(
            text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            fontSize,
            foreground,
            VisualTreeHelper.GetDpi(visual).PixelsPerDip);
        
        using (var dc = visual.RenderOpen())
        {
            dc.DrawText(formattedText, origin);
        }
        
        return visual;
    }

    /// <summary>
    /// 이미지 DrawingVisual 생성
    /// Create image DrawingVisual
    /// </summary>
    public static DrawingVisual CreateImage(
        ImageSource image, 
        Rect rect)
    {
        var visual = new DrawingVisual();
        
        using (var dc = visual.RenderOpen())
        {
            dc.DrawImage(image, rect);
        }
        
        return visual;
    }
}
```

---

## 4. ContainerVisual (그룹화)

### 4.1 ContainerVisual 사용

```csharp
namespace MyApp.Graphics;

using System.Windows;
using System.Windows.Media;

public sealed class VisualGroup
{
    public ContainerVisual Container { get; } = new();

    /// <summary>
    /// 자식 Visual 추가
    /// Add child Visual
    /// </summary>
    public void Add(Visual visual)
    {
        Container.Children.Add(visual);
    }

    /// <summary>
    /// 자식 Visual 제거
    /// Remove child Visual
    /// </summary>
    public void Remove(Visual visual)
    {
        Container.Children.Remove(visual);
    }

    /// <summary>
    /// 그룹 전체 이동
    /// Move entire group
    /// </summary>
    public void SetOffset(double x, double y)
    {
        Container.Offset = new Vector(x, y);
    }

    /// <summary>
    /// 그룹 전체 변환
    /// Transform entire group
    /// </summary>
    public void SetTransform(Transform transform)
    {
        Container.Transform = transform;
    }

    /// <summary>
    /// 그룹 전체 투명도
    /// Set opacity for entire group
    /// </summary>
    public void SetOpacity(double opacity)
    {
        Container.Opacity = opacity;
    }
}
```

### 4.2 계층적 Visual 구조

```csharp
// 계층 구조 예시
// Hierarchical structure example
//
// ContainerVisual (루트)
// ├── ContainerVisual (레이어 1 - 배경)
// │   ├── DrawingVisual (그리드)
// │   └── DrawingVisual (배경 이미지)
// ├── ContainerVisual (레이어 2 - 콘텐츠)
// │   ├── DrawingVisual (노드 1)
// │   ├── DrawingVisual (노드 2)
// │   └── DrawingVisual (연결선)
// └── ContainerVisual (레이어 3 - 오버레이)
//     └── DrawingVisual (선택 영역)

public sealed class LayeredCanvas : FrameworkElement
{
    private readonly ContainerVisual _rootVisual = new();
    private readonly ContainerVisual _backgroundLayer = new();
    private readonly ContainerVisual _contentLayer = new();
    private readonly ContainerVisual _overlayLayer = new();

    public LayeredCanvas()
    {
        _rootVisual.Children.Add(_backgroundLayer);
        _rootVisual.Children.Add(_contentLayer);
        _rootVisual.Children.Add(_overlayLayer);
        
        AddVisualChild(_rootVisual);
    }

    protected override int VisualChildrenCount => 1;

    protected override Visual GetVisualChild(int index) => _rootVisual;

    public void AddToBackground(DrawingVisual visual)
    {
        _backgroundLayer.Children.Add(visual);
    }

    public void AddToContent(DrawingVisual visual)
    {
        _contentLayer.Children.Add(visual);
    }

    public void AddToOverlay(DrawingVisual visual)
    {
        _overlayLayer.Children.Add(visual);
    }
}
```

---

## 5. Hit Testing

### 5.1 기본 Hit Testing

```csharp
namespace MyApp.Controls;

using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

public sealed class InteractiveDrawingHost : FrameworkElement
{
    private readonly List<DrawingVisual> _visuals = [];
    private DrawingVisual? _hoveredVisual;
    private DrawingVisual? _selectedVisual;

    public InteractiveDrawingHost()
    {
        MouseMove += OnMouseMove;
        MouseLeftButtonDown += OnMouseLeftButtonDown;
    }

    // ... VisualChildrenCount, GetVisualChild 구현 생략 ...

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        var position = e.GetPosition(this);
        var hitVisual = HitTestVisual(position);
        
        if (hitVisual != _hoveredVisual)
        {
            // 호버 상태 변경
            // Hover state changed
            _hoveredVisual = hitVisual;
            Cursor = hitVisual is not null ? Cursors.Hand : Cursors.Arrow;
        }
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var position = e.GetPosition(this);
        _selectedVisual = HitTestVisual(position);
        
        if (_selectedVisual is not null)
        {
            // 선택된 Visual 처리
            // Handle selected Visual
            OnVisualSelected(_selectedVisual);
        }
    }

    private DrawingVisual? HitTestVisual(Point point)
    {
        DrawingVisual? result = null;
        
        VisualTreeHelper.HitTest(
            this,
            null,
            hitResult =>
            {
                if (hitResult.VisualHit is DrawingVisual visual)
                {
                    result = visual;
                    return HitTestResultBehavior.Stop;
                }
                return HitTestResultBehavior.Continue;
            },
            new PointHitTestParameters(point));
        
        return result;
    }

    private void OnVisualSelected(DrawingVisual visual)
    {
        // 선택 이벤트 발생
        // Raise selection event
    }
}
```

### 5.2 Geometry 기반 Hit Testing

```csharp
/// <summary>
/// 특정 영역 내 모든 Visual 검색
/// Find all Visuals within specific area
/// </summary>
public List<DrawingVisual> HitTestArea(Rect area)
{
    var results = new List<DrawingVisual>();
    var geometry = new RectangleGeometry(area);
    
    VisualTreeHelper.HitTest(
        this,
        null,
        hitResult =>
        {
            if (hitResult.VisualHit is DrawingVisual visual)
            {
                results.Add(visual);
            }
            return HitTestResultBehavior.Continue;
        },
        new GeometryHitTestParameters(geometry));
    
    return results;
}
```

---

## 6. 대량 렌더링 예제 (산점도)

### 6.1 ScatterPlot 컨트롤

```csharp
namespace MyApp.Controls;

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

public sealed class ScatterPlot : FrameworkElement
{
    private readonly DrawingVisual _plotVisual = new();
    private readonly List<Point> _dataPoints = [];

    public ScatterPlot()
    {
        AddVisualChild(_plotVisual);
    }

    protected override int VisualChildrenCount => 1;
    protected override Visual GetVisualChild(int index) => _plotVisual;

    /// <summary>
    /// 데이터 설정 및 렌더링
    /// Set data and render
    /// </summary>
    public void SetData(IEnumerable<Point> points)
    {
        _dataPoints.Clear();
        _dataPoints.AddRange(points);
        Render();
    }

    private void Render()
    {
        var width = ActualWidth;
        var height = ActualHeight;
        
        if (width <= 0 || height <= 0 || _dataPoints.Count is 0)
        {
            return;
        }
        
        // 데이터 범위 계산
        // Calculate data range
        var minX = double.MaxValue;
        var maxX = double.MinValue;
        var minY = double.MaxValue;
        var maxY = double.MinValue;
        
        foreach (var p in _dataPoints)
        {
            minX = Math.Min(minX, p.X);
            maxX = Math.Max(maxX, p.X);
            minY = Math.Min(minY, p.Y);
            maxY = Math.Max(maxY, p.Y);
        }
        
        var rangeX = maxX - minX;
        var rangeY = maxY - minY;
        
        // 렌더링
        // Rendering
        using var dc = _plotVisual.RenderOpen();
        
        // 배경
        // Background
        dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
        
        // 축
        // Axes
        var axisPen = new Pen(Brushes.Black, 1);
        dc.DrawLine(axisPen, new Point(40, height - 30), new Point(width - 10, height - 30));
        dc.DrawLine(axisPen, new Point(40, 10), new Point(40, height - 30));
        
        // 데이터 포인트
        // Data points
        var plotArea = new Rect(50, 20, width - 70, height - 60);
        var pointBrush = new SolidColorBrush(Color.FromArgb(180, 33, 150, 243));
        pointBrush.Freeze();
        
        foreach (var dataPoint in _dataPoints)
        {
            var x = plotArea.Left + (dataPoint.X - minX) / rangeX * plotArea.Width;
            var y = plotArea.Bottom - (dataPoint.Y - minY) / rangeY * plotArea.Height;
            
            dc.DrawEllipse(pointBrush, null, new Point(x, y), 3, 3);
        }
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        Render();
    }
}
```

### 6.2 사용 예시

```csharp
// 10,000개 포인트 생성
// Generate 10,000 points
var random = new Random();
var points = Enumerable.Range(0, 10000)
    .Select(_ => new Point(
        random.NextDouble() * 100,
        random.NextDouble() * 100))
    .ToList();

scatterPlot.SetData(points);
```

---

## 7. RenderTargetBitmap (오프스크린 렌더링)

### 7.1 Visual을 이미지로 변환

```csharp
namespace MyApp.Graphics;

using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

public static class VisualRenderer
{
    /// <summary>
    /// Visual을 BitmapSource로 렌더링
    /// Render Visual to BitmapSource
    /// </summary>
    public static BitmapSource RenderToBitmap(
        Visual visual, 
        int width, 
        int height, 
        double dpi = 96)
    {
        var renderTarget = new RenderTargetBitmap(
            width, 
            height, 
            dpi, 
            dpi, 
            PixelFormats.Pbgra32);
        
        renderTarget.Render(visual);
        renderTarget.Freeze();
        
        return renderTarget;
    }

    /// <summary>
    /// FrameworkElement를 PNG로 저장
    /// Save FrameworkElement as PNG
    /// </summary>
    public static void SaveAsPng(FrameworkElement element, string filePath)
    {
        var width = (int)element.ActualWidth;
        var height = (int)element.ActualHeight;
        
        if (width <= 0 || height <= 0)
        {
            element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            element.Arrange(new Rect(element.DesiredSize));
            
            width = (int)element.ActualWidth;
            height = (int)element.ActualHeight;
        }
        
        var bitmap = RenderToBitmap(element, width, height);
        
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        
        using var stream = System.IO.File.Create(filePath);
        encoder.Save(stream);
    }
}
```

---

## 8. 성능 최적화 팁

```csharp
// 1. Brush/Pen 재사용 및 Freeze
// Reuse and Freeze Brush/Pen
var brush = new SolidColorBrush(Colors.Blue);
brush.Freeze();

var pen = new Pen(Brushes.Black, 1);
pen.Freeze();

// 2. StreamGeometry 사용 (불변, 최적화됨)
// Use StreamGeometry (immutable, optimized)
var geometry = new StreamGeometry();
using (var ctx = geometry.Open())
{
    ctx.BeginFigure(new Point(0, 0), true, true);
    ctx.LineTo(new Point(100, 0), true, false);
    ctx.LineTo(new Point(100, 100), true, false);
}
geometry.Freeze();

// 3. DrawingGroup으로 일괄 렌더링
// Batch rendering with DrawingGroup
var drawingGroup = new DrawingGroup();
using (var dc = drawingGroup.Open())
{
    // 여러 요소를 한 번에 그리기
    // Draw multiple elements at once
}

// 4. Dirty 영역만 다시 그리기
// Redraw only dirty region
dc.PushClip(new RectangleGeometry(dirtyRect));
```

---

## 9. 참고 문서

- [DrawingVisual Class - Microsoft Docs](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.drawingvisual)
- [Using DrawingVisual Objects - Microsoft Docs](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/using-drawingvisual-objects)
- [Hit Testing in the Visual Layer - Microsoft Docs](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/hit-testing-in-the-visual-layer)
