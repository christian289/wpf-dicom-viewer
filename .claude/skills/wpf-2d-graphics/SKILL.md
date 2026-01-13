---
name: wpf-2d-graphics
description: WPF 2D 그래픽스 시스템. Shape (Ellipse, Rectangle, Path), Geometry, Brush, Pen 활용 패턴. 벡터 그래픽 UI, 아이콘, 차트, 다이어그램 구현 시 이 스킬 적용.
---

# WPF 2D Graphics 패턴

WPF의 2D 그래픽스 시스템으로 벡터 기반 시각적 요소를 구현합니다.

## 1. 그래픽스 계층 구조

```
UIElement
└── Shape (FrameworkElement)        ← 레이아웃 참여, 이벤트 지원
    ├── Ellipse
    ├── Rectangle
    ├── Line
    ├── Polyline
    ├── Polygon
    └── Path

Drawing                             ← 경량, 이벤트 없음
├── GeometryDrawing
├── ImageDrawing
├── VideoDrawing
└── GlyphRunDrawing
```

---

## 2. Shape 기본

### 2.1 기본 도형

```xml
<!-- 타원 -->
<!-- Ellipse -->
<Ellipse Width="100" Height="100" 
         Fill="Blue" 
         Stroke="Black" 
         StrokeThickness="2"/>

<!-- 사각형 -->
<!-- Rectangle -->
<Rectangle Width="100" Height="50" 
           Fill="Red" 
           Stroke="Black" 
           StrokeThickness="1"
           RadiusX="10" RadiusY="10"/>

<!-- 선 -->
<!-- Line -->
<Line X1="0" Y1="0" X2="100" Y2="100" 
      Stroke="Green" 
      StrokeThickness="3"/>

<!-- 폴리라인 (연결된 선) -->
<!-- Polyline (connected lines) -->
<Polyline Points="0,0 50,50 100,0 150,50" 
          Stroke="Purple" 
          StrokeThickness="2"
          Fill="Transparent"/>

<!-- 폴리곤 (닫힌 다각형) -->
<!-- Polygon (closed shape) -->
<Polygon Points="50,0 100,100 0,100" 
         Fill="Yellow" 
         Stroke="Orange" 
         StrokeThickness="2"/>
```

### 2.2 Path와 Geometry

```xml
<!-- Path: 복잡한 도형 -->
<!-- Path: complex shapes -->
<Path Fill="LightBlue" Stroke="DarkBlue" StrokeThickness="2">
    <Path.Data>
        <PathGeometry>
            <PathFigure StartPoint="10,10" IsClosed="True">
                <LineSegment Point="100,10"/>
                <ArcSegment Point="100,100" Size="50,50" 
                            SweepDirection="Clockwise"/>
                <LineSegment Point="10,100"/>
            </PathFigure>
        </PathGeometry>
    </Path.Data>
</Path>

<!-- Mini-Language 문법 -->
<!-- Mini-Language syntax -->
<Path Data="M 10,10 L 100,10 A 50,50 0 0 1 100,100 L 10,100 Z"
      Fill="LightGreen" Stroke="DarkGreen"/>
```

### 2.3 Path Mini-Language

| 명령 | 설명 | 예시 |
|------|------|------|
| **M** | MoveTo (시작점) | M 10,10 |
| **L** | LineTo (직선) | L 100,100 |
| **H** | Horizontal LineTo | H 100 |
| **V** | Vertical LineTo | V 100 |
| **A** | ArcTo (호) | A 50,50 0 0 1 100,100 |
| **C** | Cubic Bezier | C 20,20 40,60 100,100 |
| **Q** | Quadratic Bezier | Q 50,50 100,100 |
| **Z** | ClosePath | Z |

소문자 = 상대 좌표, 대문자 = 절대 좌표

---

## 3. Geometry

### 3.1 기본 Geometry

```xml
<Path Stroke="Black" StrokeThickness="2">
    <Path.Data>
        <!-- 사각형 Geometry -->
        <!-- Rectangle Geometry -->
        <RectangleGeometry Rect="10,10,80,60" RadiusX="5" RadiusY="5"/>
    </Path.Data>
</Path>

<Path Stroke="Black" Fill="Yellow">
    <Path.Data>
        <!-- 타원 Geometry -->
        <!-- Ellipse Geometry -->
        <EllipseGeometry Center="50,50" RadiusX="40" RadiusY="30"/>
    </Path.Data>
</Path>

<Path Stroke="Black">
    <Path.Data>
        <!-- 선 Geometry -->
        <!-- Line Geometry -->
        <LineGeometry StartPoint="10,10" EndPoint="90,90"/>
    </Path.Data>
</Path>
```

### 3.2 CombinedGeometry (도형 결합)

```xml
<Path Fill="LightBlue" Stroke="DarkBlue" StrokeThickness="2">
    <Path.Data>
        <CombinedGeometry GeometryCombineMode="Union">
            <CombinedGeometry.Geometry1>
                <EllipseGeometry Center="50,50" RadiusX="40" RadiusY="40"/>
            </CombinedGeometry.Geometry1>
            <CombinedGeometry.Geometry2>
                <EllipseGeometry Center="80,50" RadiusX="40" RadiusY="40"/>
            </CombinedGeometry.Geometry2>
        </CombinedGeometry>
    </Path.Data>
</Path>
```

**GeometryCombineMode:**
- **Union**: 합집합
- **Intersect**: 교집합
- **Exclude**: 차집합 (Geometry1 - Geometry2)
- **Xor**: 배타적 합집합

### 3.3 GeometryGroup (다중 Geometry)

```xml
<Path Fill="Coral" Stroke="DarkRed" StrokeThickness="1">
    <Path.Data>
        <GeometryGroup FillRule="EvenOdd">
            <EllipseGeometry Center="50,50" RadiusX="45" RadiusY="45"/>
            <EllipseGeometry Center="50,50" RadiusX="30" RadiusY="30"/>
        </GeometryGroup>
    </Path.Data>
</Path>
```

**FillRule:**
- **EvenOdd**: 홀수 규칙 (도넛 모양)
- **Nonzero**: 0이 아닌 규칙 (채움)

---

## 4. Brush (브러시)

### 4.1 SolidColorBrush

```xml
<Rectangle Fill="Blue"/>
<Rectangle Fill="#FF2196F3"/>
<Rectangle>
    <Rectangle.Fill>
        <SolidColorBrush Color="Blue" Opacity="0.5"/>
    </Rectangle.Fill>
</Rectangle>
```

### 4.2 LinearGradientBrush

```xml
<Rectangle Width="200" Height="100">
    <Rectangle.Fill>
        <LinearGradientBrush StartPoint="0,0" EndPoint="1,1">
            <GradientStop Color="#2196F3" Offset="0"/>
            <GradientStop Color="#4CAF50" Offset="0.5"/>
            <GradientStop Color="#FF9800" Offset="1"/>
        </LinearGradientBrush>
    </Rectangle.Fill>
</Rectangle>
```

### 4.3 RadialGradientBrush

```xml
<Ellipse Width="200" Height="200">
    <Ellipse.Fill>
        <RadialGradientBrush GradientOrigin="0.3,0.3">
            <GradientStop Color="White" Offset="0"/>
            <GradientStop Color="Blue" Offset="1"/>
        </RadialGradientBrush>
    </Ellipse.Fill>
</Ellipse>
```

### 4.4 ImageBrush

```xml
<Rectangle Width="200" Height="200">
    <Rectangle.Fill>
        <ImageBrush ImageSource="/Assets/pattern.png"
                    TileMode="Tile"
                    Viewport="0,0,0.25,0.25"
                    ViewportUnits="RelativeToBoundingBox"/>
    </Rectangle.Fill>
</Rectangle>
```

### 4.5 VisualBrush

```xml
<Rectangle Width="200" Height="200">
    <Rectangle.Fill>
        <VisualBrush TileMode="Tile" Viewport="0,0,0.5,0.5">
            <VisualBrush.Visual>
                <StackPanel>
                    <Ellipse Width="20" Height="20" Fill="Red"/>
                    <Ellipse Width="20" Height="20" Fill="Blue"/>
                </StackPanel>
            </VisualBrush.Visual>
        </VisualBrush>
    </Rectangle.Fill>
</Rectangle>
```

---

## 5. Stroke 스타일링

### 5.1 StrokeDashArray

```xml
<!-- 점선 패턴 -->
<!-- Dashed line patterns -->
<Line X1="0" Y1="10" X2="200" Y2="10" 
      Stroke="Black" StrokeThickness="2"
      StrokeDashArray="4,2"/>

<!-- 점-대시 패턴 -->
<!-- Dot-dash pattern -->
<Line X1="0" Y1="30" X2="200" Y2="30" 
      Stroke="Black" StrokeThickness="2"
      StrokeDashArray="4,2,1,2"/>
```

### 5.2 StrokeLineCap / StrokeLineJoin

```xml
<Polyline Points="10,50 50,10 90,50" 
          Stroke="Blue" 
          StrokeThickness="10"
          StrokeStartLineCap="Round"
          StrokeEndLineCap="Triangle"
          StrokeLineJoin="Round"/>
```

**LineCap:** Flat, Round, Square, Triangle
**LineJoin:** Miter, Bevel, Round

---

## 6. 코드에서 그래픽스 생성

### 6.1 동적 도형 생성

```csharp
namespace MyApp.Graphics;

using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

public static class ShapeFactory
{
    /// <summary>
    /// 원형 마커 생성
    /// Create circular marker
    /// </summary>
    public static Ellipse CreateCircleMarker(double size, Brush fill)
    {
        return new Ellipse
        {
            Width = size,
            Height = size,
            Fill = fill,
            Stroke = Brushes.Black,
            StrokeThickness = 1
        };
    }

    /// <summary>
    /// 화살표 Path 생성
    /// Create arrow Path
    /// </summary>
    public static Path CreateArrow(Point start, Point end, Brush stroke)
    {
        var geometry = new PathGeometry();
        
        // 화살표 본체
        // Arrow body
        var bodyFigure = new PathFigure { StartPoint = start };
        bodyFigure.Segments.Add(new LineSegment(end, isStroked: true));
        geometry.Figures.Add(bodyFigure);
        
        // 화살표 머리 계산
        // Calculate arrow head
        var direction = end - start;
        direction.Normalize();
        var perpendicular = new Vector(-direction.Y, direction.X);
        
        const double headLength = 10;
        const double headWidth = 5;
        
        var headBase = end - direction * headLength;
        var headLeft = headBase + perpendicular * headWidth;
        var headRight = headBase - perpendicular * headWidth;
        
        var headFigure = new PathFigure { StartPoint = end };
        headFigure.Segments.Add(new LineSegment(headLeft, isStroked: true));
        headFigure.Segments.Add(new LineSegment(headRight, isStroked: true));
        headFigure.IsClosed = true;
        geometry.Figures.Add(headFigure);
        
        return new Path
        {
            Data = geometry,
            Stroke = stroke,
            StrokeThickness = 2,
            Fill = stroke
        };
    }
}
```

### 6.2 PathGeometry 동적 생성

```csharp
namespace MyApp.Graphics;

using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

public static class GeometryBuilder
{
    /// <summary>
    /// 점 목록으로 다각형 Geometry 생성
    /// Create polygon Geometry from point list
    /// </summary>
    public static PathGeometry CreatePolygon(IReadOnlyList<Point> points)
    {
        if (points.Count < 3)
        {
            return new PathGeometry();
        }
        
        var figure = new PathFigure
        {
            StartPoint = points[0],
            IsClosed = true,
            IsFilled = true
        };
        
        for (var i = 1; i < points.Count; i++)
        {
            figure.Segments.Add(new LineSegment(points[i], isStroked: true));
        }
        
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        
        return geometry;
    }

    /// <summary>
    /// 베지어 곡선 Geometry 생성
    /// Create Bezier curve Geometry
    /// </summary>
    public static PathGeometry CreateBezierCurve(
        Point start, 
        Point control1, 
        Point control2, 
        Point end)
    {
        var figure = new PathFigure { StartPoint = start };
        figure.Segments.Add(new BezierSegment(control1, control2, end, isStroked: true));
        
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        
        return geometry;
    }
}
```

---

## 7. 아이콘 구현

### 7.1 XAML 벡터 아이콘

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <!-- 체크마크 아이콘 -->
    <!-- Check mark icon -->
    <PathGeometry x:Key="CheckIconGeometry">
        M 2,7 L 5,10 L 10,3
    </PathGeometry>

    <!-- 닫기 아이콘 -->
    <!-- Close icon -->
    <PathGeometry x:Key="CloseIconGeometry">
        M 2,2 L 10,10 M 10,2 L 2,10
    </PathGeometry>

    <!-- 메뉴 아이콘 (햄버거) -->
    <!-- Menu icon (hamburger) -->
    <GeometryGroup x:Key="MenuIconGeometry">
        <RectangleGeometry Rect="0,0,16,2"/>
        <RectangleGeometry Rect="0,6,16,2"/>
        <RectangleGeometry Rect="0,12,16,2"/>
    </GeometryGroup>

</ResourceDictionary>
```

### 7.2 아이콘 버튼 스타일

```xml
<Style x:Key="IconButtonStyle" TargetType="{x:Type Button}">
    <Setter Property="Width" Value="32"/>
    <Setter Property="Height" Value="32"/>
    <Setter Property="Background" Value="Transparent"/>
    <Setter Property="Template">
        <Setter.Value>
            <ControlTemplate TargetType="{x:Type Button}">
                <Border Background="{TemplateBinding Background}">
                    <Path x:Name="IconPath"
                          Data="{TemplateBinding Content}"
                          Fill="{TemplateBinding Foreground}"
                          Stretch="Uniform"
                          HorizontalAlignment="Center"
                          VerticalAlignment="Center"
                          Width="16" Height="16"/>
                </Border>
                <ControlTemplate.Triggers>
                    <Trigger Property="IsMouseOver" Value="True">
                        <Setter TargetName="IconPath" Property="Fill" Value="#2196F3"/>
                    </Trigger>
                </ControlTemplate.Triggers>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>

<!-- 사용 -->
<!-- Usage -->
<Button Style="{StaticResource IconButtonStyle}" 
        Content="{StaticResource CloseIconGeometry}"/>
```

---

## 8. 성능 고려사항

| 요소 | 복잡도 | 권장 용도 |
|------|--------|----------|
| **Shape** | 높음 | 인터랙티브 요소 (클릭, 드래그) |
| **DrawingVisual** | 낮음 | 대량 정적 그래픽 |
| **StreamGeometry** | 최저 | 고정된 복잡한 경로 |

```csharp
// StreamGeometry: 불변, 최적화됨
// StreamGeometry: immutable, optimized
var streamGeometry = new StreamGeometry();
using (var context = streamGeometry.Open())
{
    context.BeginFigure(new Point(0, 0), isFilled: true, isClosed: true);
    context.LineTo(new Point(100, 0), isStroked: true, isSmoothJoin: false);
    context.LineTo(new Point(100, 100), isStroked: true, isSmoothJoin: false);
}
streamGeometry.Freeze(); // 불변으로 설정하여 성능 향상
                          // Set immutable for performance improvement
```

---

## 9. 참고 문서

- [Shapes and Basic Drawing - Microsoft Docs](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/shapes-and-basic-drawing-in-wpf-overview)
- [Geometry Overview - Microsoft Docs](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/geometry-overview)
- [Painting with Brushes - Microsoft Docs](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/painting-with-solid-colors-and-gradients-overview)
