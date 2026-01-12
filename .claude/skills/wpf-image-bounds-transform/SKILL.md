---
name: wpf-image-bounds-transform
description: 'WPF에서 Pan/Zoom/Rotate 변환이 적용된 이미지의 경계 확인 및 좌표 제한 패턴'
---

# WPF 이미지 경계 확인 (변환 적용)

Pan, Zoom, Rotate 변환이 적용된 이미지에서 마우스 좌표가 이미지 영역 내에 있는지 확인하고, 좌표를 이미지 경계 내로 제한하는 패턴입니다.

## 1. 문제 상황

### 요구사항
- 이미지 뷰어에서 측정 도구, 주석 도구 등은 **이미지 영역 내에서만** 동작해야 함
- 이미지가 확대/축소, 이동, 회전된 상태에서도 정확하게 경계 판단 필요

### 복잡성
- `Image` 컨트롤은 `Stretch="None"`, `HorizontalAlignment="Center"` 등 설정에 따라 위치가 변함
- `RenderTransform`으로 Pan, Zoom, Rotate가 적용되면 실제 이미지 위치가 계산 복잡

---

## 2. 해결 패턴

### 2.1 이미지 경계 확인 메서드

```csharp
namespace MyApp.Controls;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

public class ImageViewer : Control
{
    // DependencyProperties (생략)
    public ImageSource? ImageSource { get; set; }
    public double ZoomFactor { get; set; } = 1.0;
    public double PanX { get; set; }
    public double PanY { get; set; }
    public double RotationAngle { get; set; }

    /// <summary>
    /// 주어진 점이 이미지 영역 내에 있는지 확인합니다.
    /// Check if the given point is within the image area.
    /// </summary>
    /// <param name="point">부모 컨테이너 기준 좌표 / Point in parent container coordinates</param>
    /// <returns>이미지 영역 내이면 true / True if within image area</returns>
    public bool IsPointWithinImage(Point point)
    {
        if (ImageSource is null)
            return false;

        // 1. 이미지 원본 크기
        // 1. Original image size
        var imageWidth = ImageSource.Width;
        var imageHeight = ImageSource.Height;

        // 2. 변환된 이미지 크기 (줌 적용)
        // 2. Transformed image size (with zoom)
        var transformedWidth = imageWidth * ZoomFactor;
        var transformedHeight = imageHeight * ZoomFactor;

        // 3. 뷰어 중심 (이미지가 중앙 정렬된 경우)
        // 3. Viewer center (if image is center-aligned)
        var viewerCenterX = ActualWidth / 2;
        var viewerCenterY = ActualHeight / 2;

        // 4. 이미지 중심 (팬 적용)
        // 4. Image center (with pan)
        var imageCenterX = viewerCenterX + PanX;
        var imageCenterY = viewerCenterY + PanY;

        // 5. 이미지 경계 (회전 미적용 기준)
        // 5. Image bounds (without rotation)
        var left = imageCenterX - transformedWidth / 2;
        var top = imageCenterY - transformedHeight / 2;
        var right = imageCenterX + transformedWidth / 2;
        var bottom = imageCenterY + transformedHeight / 2;

        // 6. 회전이 있는 경우, 점을 역회전하여 확인
        // 6. If rotated, check by inverse-rotating the point
        if (RotationAngle != 0)
        {
            // 점을 이미지 중심 기준으로 역회전
            // Inverse-rotate point around image center
            var radians = -RotationAngle * Math.PI / 180;
            var cos = Math.Cos(radians);
            var sin = Math.Sin(radians);

            var dx = point.X - imageCenterX;
            var dy = point.Y - imageCenterY;

            var rotatedX = dx * cos - dy * sin + imageCenterX;
            var rotatedY = dx * sin + dy * cos + imageCenterY;

            return rotatedX >= left && rotatedX <= right &&
                   rotatedY >= top && rotatedY <= bottom;
        }

        return point.X >= left && point.X <= right &&
               point.Y >= top && point.Y <= bottom;
    }
}
```

---

### 2.2 좌표 제한 메서드

```csharp
/// <summary>
/// 주어진 점을 이미지 경계 내로 제한합니다.
/// Clamp the given point to be within the image bounds.
/// </summary>
/// <param name="point">부모 컨테이너 기준 좌표 / Point in parent container coordinates</param>
/// <returns>이미지 경계 내로 제한된 점 / Point clamped to image bounds</returns>
public Point ClampPointToImage(Point point)
{
    if (ImageSource is null)
        return point;

    // 이미지 원본 크기
    // Original image size
    var imageWidth = ImageSource.Width;
    var imageHeight = ImageSource.Height;

    // 변환된 이미지 크기 (줌 적용)
    // Transformed image size (with zoom)
    var transformedWidth = imageWidth * ZoomFactor;
    var transformedHeight = imageHeight * ZoomFactor;

    // 뷰어 중심
    // Viewer center
    var viewerCenterX = ActualWidth / 2;
    var viewerCenterY = ActualHeight / 2;

    // 이미지 중심 (팬 적용)
    // Image center (with pan)
    var imageCenterX = viewerCenterX + PanX;
    var imageCenterY = viewerCenterY + PanY;

    // 이미지 경계
    // Image bounds
    var left = imageCenterX - transformedWidth / 2;
    var top = imageCenterY - transformedHeight / 2;
    var right = imageCenterX + transformedWidth / 2;
    var bottom = imageCenterY + transformedHeight / 2;

    // 점을 경계 내로 제한
    // Clamp point within bounds
    var clampedX = Math.Clamp(point.X, left, right);
    var clampedY = Math.Clamp(point.Y, top, bottom);

    return new Point(clampedX, clampedY);
}
```

---

## 3. 사용 예제

### 3.1 측정 도구에서 활용

```csharp
// 코드 비하인드
// Code-behind
private Point _measureStartPoint;
private bool _isDrawingMeasurement;

private void RulerOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
{
    if (sender is RulerOverlay overlay)
    {
        var clickPoint = e.GetPosition(overlay);

        // 이미지 영역 내에서만 측정 시작
        // Only start measurement within image area
        if (!ImageViewer.IsPointWithinImage(clickPoint))
            return;

        _measureStartPoint = clickPoint;
        _isDrawingMeasurement = true;

        overlay.IsDrawing = true;
        overlay.CurrentStartPoint = _measureStartPoint;
        overlay.CurrentEndPoint = _measureStartPoint;

        overlay.CaptureMouse();
    }
}

private void RulerOverlay_MouseMove(object sender, MouseEventArgs e)
{
    if (_isDrawingMeasurement && sender is RulerOverlay overlay)
    {
        var currentPoint = e.GetPosition(overlay);

        // 끝점을 이미지 영역 내로 제한
        // Clamp end point to image area
        var clampedPoint = ImageViewer.ClampPointToImage(currentPoint);
        overlay.CurrentEndPoint = clampedPoint;
    }
}

private void RulerOverlay_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
{
    if (_isDrawingMeasurement && sender is RulerOverlay overlay)
    {
        var endPoint = e.GetPosition(overlay);

        // 끝점을 이미지 영역 내로 제한
        // Clamp end point to image area
        endPoint = ImageViewer.ClampPointToImage(endPoint);

        // 측정 완료 처리
        // Complete measurement
        overlay.AddMeasurement(_measureStartPoint, endPoint);

        overlay.IsDrawing = false;
        _isDrawingMeasurement = false;
        overlay.ReleaseMouseCapture();
    }
}
```

---

## 4. 회전 변환 처리 원리

### 4.1 역회전 (Inverse Rotation)

이미지가 회전된 경우, 클릭 좌표를 이미지의 원래 좌표계로 변환해야 합니다:

```
실제 클릭 위치 → 역회전 → 회전 전 좌표계에서 경계 확인
Actual click position → Inverse rotate → Check bounds in pre-rotation coordinate system
```

```csharp
// 45도 회전된 이미지에서 클릭 위치 확인
// Check click position on 45-degree rotated image

// 이미지 중심 기준 상대 좌표
// Relative coordinates from image center
var dx = point.X - imageCenterX;
var dy = point.Y - imageCenterY;

// 역회전 (-45도)
// Inverse rotation (-45 degrees)
var radians = -RotationAngle * Math.PI / 180;  // -45 → -0.785 rad
var cos = Math.Cos(radians);  // 약 0.707
var sin = Math.Sin(radians);  // 약 -0.707

// 회전 행렬 적용
// Apply rotation matrix
var rotatedX = dx * cos - dy * sin + imageCenterX;
var rotatedY = dx * sin + dy * cos + imageCenterY;
```

### 4.2 시각적 설명

```
        회전된 이미지          역회전 후 좌표
       Rotated Image        After Inverse Rotation

          ◇                      □
         /  \                    |  |
        /    \                   |  |
        \    /       →           |  |
         \  /                    |  |
          ◇                      □

    클릭 좌표 변환            직사각형 경계로 확인
    Transform click coord    Check against rectangle
```

---

## 5. 다른 변환과의 조합

### 5.1 Flip (수평/수직 반전)

Flip은 좌표 확인에 영향을 주지 않습니다 (경계는 동일):

```csharp
// Flip은 이미지 내용만 뒤집고, 경계는 동일
// Flip only mirrors content, bounds remain the same
public bool FlipHorizontal { get; set; }
public bool FlipVertical { get; set; }

// 경계 계산에서 Flip은 무시
// Flip is ignored in bounds calculation
```

### 5.2 Scale (확대/축소)

ZoomFactor로 이미 반영됨:

```csharp
var transformedWidth = imageWidth * ZoomFactor;
var transformedHeight = imageHeight * ZoomFactor;
```

### 5.3 Pan (이동)

이미지 중심 좌표에 반영:

```csharp
var imageCenterX = viewerCenterX + PanX;
var imageCenterY = viewerCenterY + PanY;
```

---

## 6. RenderTransform과의 일관성

### 6.1 XAML에서의 변환 순서

```xml
<Image x:Name="PART_Image"
       RenderTransformOrigin="0.5,0.5">
    <Image.RenderTransform>
        <TransformGroup>
            <!-- 1. Flip -->
            <ScaleTransform ScaleX="{Binding FlipHorizontal, ...}"
                            ScaleY="{Binding FlipVertical, ...}" />
            <!-- 2. Rotate -->
            <RotateTransform Angle="{Binding RotationAngle, ...}" />
            <!-- 3. Zoom -->
            <ScaleTransform ScaleX="{Binding ZoomFactor, ...}"
                            ScaleY="{Binding ZoomFactor, ...}" />
            <!-- 4. Pan -->
            <TranslateTransform X="{Binding PanX, ...}"
                                Y="{Binding PanY, ...}" />
        </TransformGroup>
    </Image.RenderTransform>
</Image>
```

### 6.2 경계 계산에서의 순서

코드에서도 동일한 순서로 변환 적용:

```csharp
// 1. 원본 크기에 Zoom 적용
var transformedWidth = imageWidth * ZoomFactor;
var transformedHeight = imageHeight * ZoomFactor;

// 2. 중심에 Pan 적용
var imageCenterX = viewerCenterX + PanX;
var imageCenterY = viewerCenterY + PanY;

// 3. 경계 계산
var left = imageCenterX - transformedWidth / 2;
// ...

// 4. Rotate 적용 (역변환으로 좌표 확인)
if (RotationAngle != 0)
{
    // 역회전...
}
```

---

## 7. 체크리스트

- [ ] `IsPointWithinImage()`: 클릭 시작 전 이미지 영역 확인
- [ ] `ClampPointToImage()`: 드래그 중/완료 시 좌표 제한
- [ ] 회전 처리: 역회전으로 좌표 변환 후 경계 확인
- [ ] 변환 순서 일관성: XAML과 코드에서 동일 순서 유지
- [ ] `RenderTransformOrigin="0.5,0.5"` 설정 확인

---

## 8. 참고 문서

- [RenderTransform - Microsoft Docs](https://learn.microsoft.com/en-us/dotnet/api/system.windows.uielement.rendertransform)
- [RotateTransform - Microsoft Docs](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.rotatetransform)
- [Math.Clamp - Microsoft Docs](https://learn.microsoft.com/en-us/dotnet/api/system.math.clamp)
