---
name: wpf-frameworkelement-hittest
description: 'WPF FrameworkElement에서 DrawingContext 사용 시 마우스 이벤트 수신을 위한 Hit Testing 패턴'
---

# WPF FrameworkElement Hit Testing

WPF에서 `FrameworkElement`를 상속하여 `OnRender(DrawingContext)`로 직접 렌더링할 때, 마우스 이벤트를 수신하기 위한 필수 패턴입니다.

## 1. 문제 상황

### 증상
- `FrameworkElement` 상속 컨트롤에서 `MouseLeftButtonDown`, `MouseMove` 등 이벤트가 발생하지 않음
- 클릭해도 아무 반응이 없음

### 원인
WPF의 Hit Testing은 렌더링된 픽셀을 기준으로 수행됩니다. `OnRender()`에서 아무것도 그리지 않거나, 배경이 없으면 해당 영역이 "비어있음"으로 판단되어 마우스 이벤트가 전달되지 않습니다.

---

## 2. 해결 방법

### 2.1 투명 배경 그리기 (필수)

```csharp
namespace MyApp.Controls;

using System.Windows;
using System.Windows.Media;

public sealed class MyOverlay : FrameworkElement
{
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        // ⚠️ 필수: 투명 배경 그리기 (마우스 이벤트 수신을 위해)
        // ⚠️ Required: Draw transparent background (for mouse event reception)
        dc.DrawRectangle(
            Brushes.Transparent,
            null,
            new Rect(0, 0, ActualWidth, ActualHeight));

        // 이후 실제 렌더링 로직
        // Actual rendering logic follows
        DrawContent(dc);
    }

    private void DrawContent(DrawingContext dc)
    {
        // 실제 콘텐츠 그리기
        // Draw actual content
    }
}
```

---

## 3. 왜 Transparent인가?

### Transparent vs null

| 설정 | Hit Test 결과 | 시각적 결과 |
|------|---------------|-------------|
| `Brushes.Transparent` | ✅ 성공 | 보이지 않음 |
| `null` | ❌ 실패 | 보이지 않음 |
| `new SolidColorBrush(Color.FromArgb(0, 0, 0, 0))` | ✅ 성공 | 보이지 않음 |

`Transparent`는 Alpha 채널이 0인 "존재하는" 브러시입니다. WPF Hit Testing은 브러시가 **존재하는지**를 확인하므로, `null`과는 다르게 동작합니다.

---

## 4. 실제 적용 예제

### 4.1 측정 도구 오버레이

```csharp
namespace MyApp.Controls;

using System.Windows;
using System.Windows.Media;

public sealed class RulerOverlay : FrameworkElement
{
    private static readonly Pen LinePen;
    private static readonly Brush TextBrush;

    static RulerOverlay()
    {
        // Freeze된 리소스 (성능 최적화)
        // Frozen resources (performance optimization)
        LinePen = new Pen(Brushes.Yellow, 2);
        LinePen.Freeze();
        TextBrush = Brushes.Yellow;
    }

    public Point StartPoint { get; set; }
    public Point EndPoint { get; set; }
    public bool IsDrawing { get; set; }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        // 1. 투명 배경 (Hit Testing 필수)
        // 1. Transparent background (required for hit testing)
        dc.DrawRectangle(
            Brushes.Transparent,
            null,
            new Rect(0, 0, ActualWidth, ActualHeight));

        // 2. 실제 측정선 그리기
        // 2. Draw actual measurement line
        if (IsDrawing)
        {
            dc.DrawLine(LinePen, StartPoint, EndPoint);
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        // 이제 이벤트가 정상적으로 수신됨
        // Now events are received normally
        StartPoint = e.GetPosition(this);
        IsDrawing = true;
        CaptureMouse();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (IsDrawing)
        {
            EndPoint = e.GetPosition(this);
            InvalidateVisual();  // 다시 그리기
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);

        if (IsDrawing)
        {
            IsDrawing = false;
            ReleaseMouseCapture();
        }
    }
}
```

---

## 5. 코드 비하인드에서 이벤트 연결

XAML에서 이벤트를 연결할 때도 동일한 원칙이 적용됩니다:

```xml
<controls:RulerOverlay x:Name="RulerOverlay"
                       MouseLeftButtonDown="RulerOverlay_MouseLeftButtonDown"
                       MouseMove="RulerOverlay_MouseMove"
                       MouseLeftButtonUp="RulerOverlay_MouseLeftButtonUp" />
```

```csharp
// 코드 비하인드
// Code-behind
private void RulerOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
{
    if (sender is RulerOverlay overlay)
    {
        // 투명 배경이 없으면 이 이벤트가 발생하지 않음!
        // Without transparent background, this event won't fire!
        var point = e.GetPosition(overlay);
        // ...
    }
}
```

---

## 6. IsHitTestVisible 속성과의 관계

### 주의사항

```xml
<!-- IsHitTestVisible="False"는 투명 배경과 무관하게 이벤트 차단 -->
<!-- IsHitTestVisible="False" blocks events regardless of transparent background -->
<controls:MyOverlay IsHitTestVisible="False" />
```

| 설정 | 투명 배경 | Hit Test 결과 |
|------|----------|---------------|
| `IsHitTestVisible="True"` (기본값) | 있음 | ✅ 성공 |
| `IsHitTestVisible="True"` | 없음 | ❌ 실패 |
| `IsHitTestVisible="False"` | 있음 | ❌ 실패 |
| `IsHitTestVisible="False"` | 없음 | ❌ 실패 |

---

## 7. 체크리스트

- [ ] `OnRender()`에서 `Brushes.Transparent`로 전체 영역 배경 그리기
- [ ] 배경은 다른 콘텐츠보다 **먼저** 그리기
- [ ] `IsHitTestVisible`이 `True`인지 확인 (기본값)
- [ ] Pen, Brush에 `Freeze()` 적용 (성능 최적화)

---

## 8. 흔한 실수

### ❌ 잘못된 예: 배경 없음

```csharp
protected override void OnRender(DrawingContext dc)
{
    base.OnRender(dc);

    // 배경 없이 바로 콘텐츠만 그림
    // Draw content without background
    dc.DrawLine(LinePen, StartPoint, EndPoint);  // 선 위만 Hit Test 성공
}
```

### ✅ 올바른 예: 투명 배경 포함

```csharp
protected override void OnRender(DrawingContext dc)
{
    base.OnRender(dc);

    // 1. 먼저 투명 배경
    // 1. Transparent background first
    dc.DrawRectangle(Brushes.Transparent, null,
        new Rect(0, 0, ActualWidth, ActualHeight));

    // 2. 그 다음 콘텐츠
    // 2. Then content
    dc.DrawLine(LinePen, StartPoint, EndPoint);
}
```

---

## 9. 참고 문서

- [Hit Testing in the Visual Layer - Microsoft Docs](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/hit-testing-in-the-visual-layer)
- [FrameworkElement.OnRender - Microsoft Docs](https://learn.microsoft.com/en-us/dotnet/api/system.windows.frameworkelement.onrender)
