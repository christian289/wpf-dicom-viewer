---
name: wpf-adorner
description: WPF Adorner를 사용한 장식 레이어 구현. AdornerLayer, AdornerDecorator, 커스텀 Adorner 패턴. 드래그 핸들, 유효성 검사 표시, 워터마크, 선택 표시, 리사이즈 그립 구현 시 이 스킬 적용.
---

# WPF Adorner 패턴

Adorner는 UIElement 위에 장식적인 시각 요소를 오버레이하는 메커니즘입니다.

## 1. Adorner 개념

### 1.1 특징

- **AdornerLayer**: Adorner를 담는 별도의 렌더링 레이어
- **Z-Order**: 장식 대상 요소 위에 항상 렌더링
- **레이아웃 독립**: 대상 요소의 레이아웃에 영향 없음
- **이벤트 지원**: 마우스/키보드 이벤트 수신 가능

### 1.2 사용 시나리오

| 시나리오 | 설명 |
|---------|------|
| **유효성 표시** | 입력 필드 오류 표시 |
| **드래그 핸들** | 요소 이동/리사이즈 핸들 |
| **워터마크** | 빈 TextBox 힌트 텍스트 |
| **선택 표시** | 선택된 요소 하이라이트 |
| **툴팁/배지** | 요소 위 추가 정보 표시 |
| **드래그 앤 드롭** | 드래그 중 미리보기 |

---

## 2. 기본 Adorner 구현

### 2.1 간단한 Adorner

```csharp
namespace MyApp.Adorners;

using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

/// <summary>
/// 요소 주변에 테두리를 그리는 Adorner
/// Adorner that draws border around element
/// </summary>
public sealed class BorderAdorner : Adorner
{
    private readonly Pen _borderPen;

    public BorderAdorner(UIElement adornedElement) : base(adornedElement)
    {
        _borderPen = new Pen(Brushes.Red, 2)
        {
            DashStyle = DashStyles.Dash
        };
        _borderPen.Freeze();
        
        // 마우스 이벤트 비활성화 (장식만)
        // Disable mouse events (decoration only)
        IsHitTestVisible = false;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var rect = new Rect(AdornedElement.RenderSize);
        
        // 테두리 그리기
        // Draw border
        drawingContext.DrawRectangle(null, _borderPen, rect);
    }
}
```

### 2.2 Adorner 적용

```csharp
// AdornerLayer 가져오기
// Get AdornerLayer
var adornerLayer = AdornerLayer.GetAdornerLayer(targetElement);

if (adornerLayer is not null)
{
    // Adorner 추가
    // Add Adorner
    var adorner = new BorderAdorner(targetElement);
    adornerLayer.Add(adorner);
}
```

### 2.3 Adorner 제거

```csharp
// 특정 요소의 모든 Adorner 제거
// Remove all Adorners from specific element
var adornerLayer = AdornerLayer.GetAdornerLayer(targetElement);
var adorners = adornerLayer?.GetAdorners(targetElement);

if (adorners is not null)
{
    foreach (var adorner in adorners)
    {
        adornerLayer!.Remove(adorner);
    }
}
```

---

## 3. AdornerDecorator

### 3.1 기본 위치

```xml
<!-- Window 기본 템플릿에 AdornerDecorator 포함 -->
<!-- Window default template includes AdornerDecorator -->
<Window>
    <!-- AdornerDecorator는 자동으로 포함됨 -->
    <!-- AdornerDecorator is automatically included -->
    <Grid>
        <TextBox x:Name="MyTextBox"/>
    </Grid>
</Window>
```

### 3.2 명시적 AdornerDecorator

```xml
<!-- ControlTemplate에서 명시적 AdornerDecorator -->
<!-- Explicit AdornerDecorator in ControlTemplate -->
<ControlTemplate TargetType="{x:Type ContentControl}">
    <AdornerDecorator>
        <ContentPresenter/>
    </AdornerDecorator>
</ControlTemplate>

<!-- Popup이나 특수 컨테이너에서 -->
<!-- In Popup or special containers -->
<Popup>
    <AdornerDecorator>
        <Border>
            <StackPanel>
                <TextBox/>
                <Button Content="OK"/>
            </StackPanel>
        </Border>
    </AdornerDecorator>
</Popup>
```

---

## 4. 실용적인 Adorner 예제

### 4.1 워터마크 Adorner

```csharp
namespace MyApp.Adorners;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

/// <summary>
/// TextBox에 워터마크(힌트 텍스트) 표시
/// Display watermark (hint text) on TextBox
/// </summary>
public sealed class WatermarkAdorner : Adorner
{
    private readonly TextBlock _watermarkText;

    public WatermarkAdorner(UIElement adornedElement, string watermark) 
        : base(adornedElement)
    {
        _watermarkText = new TextBlock
        {
            Text = watermark,
            Foreground = Brushes.Gray,
            FontStyle = FontStyles.Italic,
            Margin = new Thickness(4, 2, 0, 0),
            IsHitTestVisible = false
        };
        
        AddVisualChild(_watermarkText);
        
        IsHitTestVisible = false;
    }

    protected override int VisualChildrenCount => 1;

    protected override Visual GetVisualChild(int index) => _watermarkText;

    protected override Size MeasureOverride(Size constraint)
    {
        _watermarkText.Measure(constraint);
        return _watermarkText.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _watermarkText.Arrange(new Rect(finalSize));
        return finalSize;
    }
}
```

### 4.2 워터마크 Attached Property

```csharp
namespace MyApp.Behaviors;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using MyApp.Adorners;

public static class Watermark
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.RegisterAttached(
            "Text",
            typeof(string),
            typeof(Watermark),
            new PropertyMetadata(null, OnTextChanged));

    public static string GetText(DependencyObject obj) =>
        (string)obj.GetValue(TextProperty);

    public static void SetText(DependencyObject obj, string value) =>
        obj.SetValue(TextProperty, value);

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox textBox)
        {
            return;
        }

        textBox.Loaded -= OnTextBoxLoaded;
        textBox.Loaded += OnTextBoxLoaded;
        textBox.TextChanged -= OnTextBoxTextChanged;
        textBox.TextChanged += OnTextBoxTextChanged;
    }

    private static void OnTextBoxLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            UpdateWatermark(textBox);
        }
    }

    private static void OnTextBoxTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            UpdateWatermark(textBox);
        }
    }

    private static void UpdateWatermark(TextBox textBox)
    {
        var adornerLayer = AdornerLayer.GetAdornerLayer(textBox);
        if (adornerLayer is null)
        {
            return;
        }

        // 기존 워터마크 제거
        // Remove existing watermark
        RemoveWatermark(textBox, adornerLayer);

        // 텍스트가 비어있으면 워터마크 추가
        // Add watermark if text is empty
        if (string.IsNullOrEmpty(textBox.Text))
        {
            var watermark = GetText(textBox);
            if (!string.IsNullOrEmpty(watermark))
            {
                adornerLayer.Add(new WatermarkAdorner(textBox, watermark));
            }
        }
    }

    private static void RemoveWatermark(TextBox textBox, AdornerLayer adornerLayer)
    {
        var adorners = adornerLayer.GetAdorners(textBox);
        if (adorners is null)
        {
            return;
        }

        foreach (var adorner in adorners)
        {
            if (adorner is WatermarkAdorner)
            {
                adornerLayer.Remove(adorner);
            }
        }
    }
}
```

### 4.3 XAML에서 워터마크 사용

```xml
<TextBox local:Watermark.Text="이메일을 입력하세요"/>
<!-- <TextBox local:Watermark.Text="Enter email address"/> -->
```

---

## 5. 리사이즈 핸들 Adorner

### 5.1 구현

```csharp
namespace MyApp.Adorners;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

/// <summary>
/// 요소 리사이즈를 위한 핸들 Adorner
/// Handle Adorner for element resizing
/// </summary>
public sealed class ResizeAdorner : Adorner
{
    private readonly VisualCollection _visuals;
    private readonly Thumb _topLeft, _topRight, _bottomLeft, _bottomRight;
    private readonly Thumb _top, _bottom, _left, _right;

    private const double ThumbSize = 8;

    public ResizeAdorner(UIElement adornedElement) : base(adornedElement)
    {
        _visuals = new VisualCollection(this);

        // 코너 핸들
        // Corner handles
        _topLeft = CreateThumb(Cursors.SizeNWSE);
        _topRight = CreateThumb(Cursors.SizeNESW);
        _bottomLeft = CreateThumb(Cursors.SizeNESW);
        _bottomRight = CreateThumb(Cursors.SizeNWSE);

        // 가장자리 핸들
        // Edge handles
        _top = CreateThumb(Cursors.SizeNS);
        _bottom = CreateThumb(Cursors.SizeNS);
        _left = CreateThumb(Cursors.SizeWE);
        _right = CreateThumb(Cursors.SizeWE);

        // 드래그 이벤트 연결
        // Connect drag events
        _bottomRight.DragDelta += OnBottomRightDrag;
        _topLeft.DragDelta += OnTopLeftDrag;
        _topRight.DragDelta += OnTopRightDrag;
        _bottomLeft.DragDelta += OnBottomLeftDrag;
        _top.DragDelta += OnTopDrag;
        _bottom.DragDelta += OnBottomDrag;
        _left.DragDelta += OnLeftDrag;
        _right.DragDelta += OnRightDrag;
    }

    private Thumb CreateThumb(Cursor cursor)
    {
        var thumb = new Thumb
        {
            Width = ThumbSize,
            Height = ThumbSize,
            Cursor = cursor,
            Background = Brushes.White,
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(1)
        };
        _visuals.Add(thumb);
        return thumb;
    }

    protected override int VisualChildrenCount => _visuals.Count;

    protected override Visual GetVisualChild(int index) => _visuals[index];

    protected override Size ArrangeOverride(Size finalSize)
    {
        var halfThumb = ThumbSize / 2;
        var width = AdornedElement.RenderSize.Width;
        var height = AdornedElement.RenderSize.Height;

        // 코너 핸들 배치
        // Arrange corner handles
        _topLeft.Arrange(new Rect(-halfThumb, -halfThumb, ThumbSize, ThumbSize));
        _topRight.Arrange(new Rect(width - halfThumb, -halfThumb, ThumbSize, ThumbSize));
        _bottomLeft.Arrange(new Rect(-halfThumb, height - halfThumb, ThumbSize, ThumbSize));
        _bottomRight.Arrange(new Rect(width - halfThumb, height - halfThumb, ThumbSize, ThumbSize));

        // 가장자리 핸들 배치
        // Arrange edge handles
        _top.Arrange(new Rect(width / 2 - halfThumb, -halfThumb, ThumbSize, ThumbSize));
        _bottom.Arrange(new Rect(width / 2 - halfThumb, height - halfThumb, ThumbSize, ThumbSize));
        _left.Arrange(new Rect(-halfThumb, height / 2 - halfThumb, ThumbSize, ThumbSize));
        _right.Arrange(new Rect(width - halfThumb, height / 2 - halfThumb, ThumbSize, ThumbSize));

        return finalSize;
    }

    private void OnBottomRightDrag(object sender, DragDeltaEventArgs e)
    {
        if (AdornedElement is FrameworkElement element)
        {
            element.Width = Math.Max(element.MinWidth, element.Width + e.HorizontalChange);
            element.Height = Math.Max(element.MinHeight, element.Height + e.VerticalChange);
        }
    }

    private void OnTopLeftDrag(object sender, DragDeltaEventArgs e)
    {
        if (AdornedElement is FrameworkElement element)
        {
            var newWidth = Math.Max(element.MinWidth, element.Width - e.HorizontalChange);
            var newHeight = Math.Max(element.MinHeight, element.Height - e.VerticalChange);
            
            if (element.Width != newWidth)
            {
                Canvas.SetLeft(element, Canvas.GetLeft(element) + e.HorizontalChange);
                element.Width = newWidth;
            }
            if (element.Height != newHeight)
            {
                Canvas.SetTop(element, Canvas.GetTop(element) + e.VerticalChange);
                element.Height = newHeight;
            }
        }
    }

    private void OnTopRightDrag(object sender, DragDeltaEventArgs e)
    {
        if (AdornedElement is FrameworkElement element)
        {
            element.Width = Math.Max(element.MinWidth, element.Width + e.HorizontalChange);
            
            var newHeight = Math.Max(element.MinHeight, element.Height - e.VerticalChange);
            if (element.Height != newHeight)
            {
                Canvas.SetTop(element, Canvas.GetTop(element) + e.VerticalChange);
                element.Height = newHeight;
            }
        }
    }

    private void OnBottomLeftDrag(object sender, DragDeltaEventArgs e)
    {
        if (AdornedElement is FrameworkElement element)
        {
            var newWidth = Math.Max(element.MinWidth, element.Width - e.HorizontalChange);
            if (element.Width != newWidth)
            {
                Canvas.SetLeft(element, Canvas.GetLeft(element) + e.HorizontalChange);
                element.Width = newWidth;
            }
            element.Height = Math.Max(element.MinHeight, element.Height + e.VerticalChange);
        }
    }

    private void OnTopDrag(object sender, DragDeltaEventArgs e)
    {
        if (AdornedElement is FrameworkElement element)
        {
            var newHeight = Math.Max(element.MinHeight, element.Height - e.VerticalChange);
            if (element.Height != newHeight)
            {
                Canvas.SetTop(element, Canvas.GetTop(element) + e.VerticalChange);
                element.Height = newHeight;
            }
        }
    }

    private void OnBottomDrag(object sender, DragDeltaEventArgs e)
    {
        if (AdornedElement is FrameworkElement element)
        {
            element.Height = Math.Max(element.MinHeight, element.Height + e.VerticalChange);
        }
    }

    private void OnLeftDrag(object sender, DragDeltaEventArgs e)
    {
        if (AdornedElement is FrameworkElement element)
        {
            var newWidth = Math.Max(element.MinWidth, element.Width - e.HorizontalChange);
            if (element.Width != newWidth)
            {
                Canvas.SetLeft(element, Canvas.GetLeft(element) + e.HorizontalChange);
                element.Width = newWidth;
            }
        }
    }

    private void OnRightDrag(object sender, DragDeltaEventArgs e)
    {
        if (AdornedElement is FrameworkElement element)
        {
            element.Width = Math.Max(element.MinWidth, element.Width + e.HorizontalChange);
        }
    }
}
```

---

## 6. 유효성 검사 Adorner

### 6.1 ValidationErrorAdorner

```csharp
namespace MyApp.Adorners;

using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

/// <summary>
/// 유효성 검사 오류 표시 Adorner
/// Validation error display Adorner
/// </summary>
public sealed class ValidationErrorAdorner : Adorner
{
    private readonly string _errorMessage;
    private readonly Pen _borderPen;
    private readonly Brush _errorBrush;

    public ValidationErrorAdorner(UIElement adornedElement, string errorMessage) 
        : base(adornedElement)
    {
        _errorMessage = errorMessage;
        
        _borderPen = new Pen(Brushes.Red, 2);
        _borderPen.Freeze();
        
        _errorBrush = new SolidColorBrush(Color.FromArgb(50, 255, 0, 0));
        _errorBrush.Freeze();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var rect = new Rect(AdornedElement.RenderSize);
        
        // 오류 배경
        // Error background
        drawingContext.DrawRectangle(_errorBrush, _borderPen, rect);
        
        // 오류 아이콘 (우측 상단)
        // Error icon (top right)
        var iconSize = 16.0;
        var iconRect = new Rect(rect.Right - iconSize - 2, rect.Top + 2, iconSize, iconSize);
        
        drawingContext.DrawEllipse(Brushes.Red, null, 
            new Point(iconRect.Left + iconSize / 2, iconRect.Top + iconSize / 2),
            iconSize / 2, iconSize / 2);
        
        // 느낌표
        // Exclamation mark
        var formattedText = new FormattedText(
            "!",
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            12,
            Brushes.White,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        
        drawingContext.DrawText(formattedText, 
            new Point(iconRect.Left + iconSize / 2 - formattedText.Width / 2, 
                      iconRect.Top + iconSize / 2 - formattedText.Height / 2));
    }
}
```

---

## 7. 드래그 미리보기 Adorner

### 7.1 DragPreviewAdorner

```csharp
namespace MyApp.Adorners;

using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

/// <summary>
/// 드래그 중 미리보기 표시
/// Display preview during drag
/// </summary>
public sealed class DragPreviewAdorner : Adorner
{
    private readonly VisualBrush _visualBrush;
    private Point _offset;

    public DragPreviewAdorner(UIElement adornedElement, Point offset) 
        : base(adornedElement)
    {
        _offset = offset;
        
        _visualBrush = new VisualBrush(adornedElement)
        {
            Opacity = 0.7
        };
        
        IsHitTestVisible = false;
    }

    public void UpdatePosition(Point position)
    {
        _offset = position;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var size = AdornedElement.RenderSize;
        var rect = new Rect(
            _offset.X - size.Width / 2,
            _offset.Y - size.Height / 2,
            size.Width,
            size.Height);
        
        drawingContext.DrawRectangle(_visualBrush, null, rect);
    }
}
```

---

## 8. Adorner 관리 서비스

```csharp
namespace MyApp.Services;

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Documents;

/// <summary>
/// Adorner 생명주기 관리
/// Adorner lifecycle management
/// </summary>
public sealed class AdornerService
{
    private readonly Dictionary<UIElement, List<Adorner>> _adornerMap = [];

    /// <summary>
    /// Adorner 추가
    /// Add Adorner
    /// </summary>
    public bool AddAdorner(UIElement element, Adorner adorner)
    {
        var layer = AdornerLayer.GetAdornerLayer(element);
        if (layer is null)
        {
            return false;
        }

        layer.Add(adorner);

        if (!_adornerMap.TryGetValue(element, out var list))
        {
            list = [];
            _adornerMap[element] = list;
        }
        list.Add(adorner);

        return true;
    }

    /// <summary>
    /// 특정 타입 Adorner 제거
    /// Remove specific type Adorner
    /// </summary>
    public void RemoveAdorner<T>(UIElement element) where T : Adorner
    {
        var layer = AdornerLayer.GetAdornerLayer(element);
        if (layer is null || !_adornerMap.TryGetValue(element, out var list))
        {
            return;
        }

        for (var i = list.Count - 1; i >= 0; i--)
        {
            if (list[i] is T adorner)
            {
                layer.Remove(adorner);
                list.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// 모든 Adorner 제거
    /// Remove all Adorners
    /// </summary>
    public void ClearAdorners(UIElement element)
    {
        var layer = AdornerLayer.GetAdornerLayer(element);
        if (layer is null || !_adornerMap.TryGetValue(element, out var list))
        {
            return;
        }

        foreach (var adorner in list)
        {
            layer.Remove(adorner);
        }
        list.Clear();
    }

    /// <summary>
    /// 특정 타입 Adorner 존재 여부
    /// Check if specific type Adorner exists
    /// </summary>
    public bool HasAdorner<T>(UIElement element) where T : Adorner
    {
        if (!_adornerMap.TryGetValue(element, out var list))
        {
            return false;
        }

        foreach (var adorner in list)
        {
            if (adorner is T)
            {
                return true;
            }
        }
        return false;
    }
}
```

---

## 9. 체크리스트

- [ ] AdornerLayer 존재 확인 후 Adorner 추가
- [ ] 장식 전용이면 `IsHitTestVisible = false` 설정
- [ ] VisualChildrenCount와 GetVisualChild 올바르게 구현
- [ ] MeasureOverride와 ArrangeOverride로 자식 배치
- [ ] 불필요한 Adorner 제거 (메모리 누수 방지)
- [ ] Popup 등에서는 AdornerDecorator 명시적 추가

---

## 10. 참고 문서

- [Adorners Overview - Microsoft Docs](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/adorners-overview)
- [Adorner Class - Microsoft Docs](https://learn.microsoft.com/en-us/dotnet/api/system.windows.documents.adorner)
