namespace DicomViewer.Domain.ValueObjects;

/// <summary>
/// 이미지 변환 상태를 나타내는 불변 레코드
/// Immutable record representing image transformation state
/// </summary>
/// <remarks>
/// 변환 순서: 플립·확대 → 회전 → 이동. 회전은 화면(y 아래 방향) 기준 시계 방향이 양수이다.
/// Transform order: flip and zoom, then rotate, then pan. Positive rotation is clockwise on screen (y grows downward).
/// </remarks>
public sealed record ImageTransform(
    double ZoomFactor = 1.0,
    double PanX = 0.0,
    double PanY = 0.0,
    double Rotation = 0.0,
    bool FlipHorizontal = false,
    bool FlipVertical = false)
{
    public static ImageTransform Default => new();

    public ImageTransform WithZoom(double zoomFactor) =>
        this with { ZoomFactor = Math.Clamp(zoomFactor, 0.1, 10.0) };

    public ImageTransform WithPan(double panX, double panY) =>
        this with { PanX = panX, PanY = panY };

    public ImageTransform WithRotation(double rotation) =>
        this with { Rotation = rotation % 360.0 };

    public ImageTransform Reset() => Default;

    /// <summary>
    /// 1 영상 픽셀당 DIP 길이 (맞춤 배율 × 확대율)
    /// DIP length of one image pixel (fit scale × zoom factor)
    /// </summary>
    public double DipPerImagePixel(ImageViewport viewport)
    {
        ValidateZoom();
        return viewport.FitScaleDipPerPixel * ZoomFactor;
    }

    /// <summary>
    /// 영상 픽셀 좌표를 뷰포트 DIP 좌표로 변환
    /// Map an image pixel coordinate to viewport DIP coordinates
    /// </summary>
    public Point2D ImageToViewport(Point2D imagePixel, ImageViewport viewport)
    {
        ValidateZoom();
        var scale = viewport.FitScaleDipPerPixel * ZoomFactor;

        // 영상 중심 기준 좌표 (픽셀)
        // Coordinates relative to the image center (pixels)
        var u = imagePixel.X - viewport.ImageWidthPx / 2.0;
        var v = imagePixel.Y - viewport.ImageHeightPx / 2.0;

        if (FlipHorizontal) u = -u;
        if (FlipVertical) v = -v;

        var x = u * scale;
        var y = v * scale;

        var (sin, cos) = RotationSinCos();
        var rx = x * cos - y * sin;
        var ry = x * sin + y * cos;

        return new Point2D(
            rx + viewport.ViewportWidthDip / 2.0 + PanX,
            ry + viewport.ViewportHeightDip / 2.0 + PanY);
    }

    /// <summary>
    /// 뷰포트 DIP 좌표를 영상 픽셀 좌표로 역변환 (범위 제한 없음)
    /// Inverse-map viewport DIP coordinates to image pixel coordinates (no bounds applied)
    /// </summary>
    public Point2D ViewportToImage(Point2D viewportDip, ImageViewport viewport)
    {
        ValidateZoom();
        var scale = viewport.FitScaleDipPerPixel * ZoomFactor;

        var dx = viewportDip.X - viewport.ViewportWidthDip / 2.0 - PanX;
        var dy = viewportDip.Y - viewport.ViewportHeightDip / 2.0 - PanY;

        // 회전 역변환 (-θ)
        // Inverse rotation (-θ)
        var (sin, cos) = RotationSinCos();
        var x = dx * cos + dy * sin;
        var y = -dx * sin + dy * cos;

        var u = x / scale;
        var v = y / scale;

        if (FlipHorizontal) u = -u;
        if (FlipVertical) v = -v;

        return new Point2D(u + viewport.ImageWidthPx / 2.0, v + viewport.ImageHeightPx / 2.0);
    }

    /// <summary>
    /// 역변환 결과가 영상 범위 [0,W]×[0,H] 안이면 true
    /// Returns true when the inverse-mapped point lies inside the image bounds [0,W]×[0,H]
    /// </summary>
    public bool TryViewportToImage(Point2D viewportDip, ImageViewport viewport, out Point2D imagePixel)
    {
        imagePixel = ViewportToImage(viewportDip, viewport);
        return imagePixel.X >= 0 && imagePixel.X <= viewport.ImageWidthPx &&
               imagePixel.Y >= 0 && imagePixel.Y <= viewport.ImageHeightPx;
    }

    /// <summary>
    /// 역변환 후 영상 범위 [0,W]×[0,H]로 고정
    /// Inverse-map and clamp the result to the image bounds [0,W]×[0,H]
    /// </summary>
    public Point2D ViewportToImageClamped(Point2D viewportDip, ImageViewport viewport)
    {
        var p = ViewportToImage(viewportDip, viewport);
        return new Point2D(
            Math.Clamp(p.X, 0.0, viewport.ImageWidthPx),
            Math.Clamp(p.Y, 0.0, viewport.ImageHeightPx));
    }

    private (double Sin, double Cos) RotationSinCos()
    {
        if (!double.IsFinite(Rotation))
        {
            throw new ArgumentOutOfRangeException(nameof(Rotation), Rotation, "Rotation must be finite.");
        }

        var radians = Rotation * Math.PI / 180.0;
        return (Math.Sin(radians), Math.Cos(radians));
    }

    private void ValidateZoom()
    {
        if (!double.IsFinite(ZoomFactor) || ZoomFactor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ZoomFactor), ZoomFactor, "ZoomFactor must be finite and positive.");
        }
    }
}

/// <summary>
/// 뷰포트(DIP)와 영상(픽셀) 크기. 영상 좌표는 좌상단 모서리가 원점이다.
/// Viewport size in DIP and image size in pixels. Image origin is the top-left corner.
/// </summary>
public readonly record struct ImageViewport(
    double ViewportWidthDip,
    double ViewportHeightDip,
    double ImageWidthPx,
    double ImageHeightPx)
{
    /// <summary>
    /// 전체 영상이 뷰포트에 맞는 1 픽셀당 DIP: min(뷰포트 폭/영상 폭, 뷰포트 높이/영상 높이)
    /// DIP per image pixel that fits the whole image: min(viewport width / image width, viewport height / image height)
    /// </summary>
    public double FitScaleDipPerPixel
    {
        get
        {
            Validate();
            return Math.Min(ViewportWidthDip / ImageWidthPx, ViewportHeightDip / ImageHeightPx);
        }
    }

    private void Validate()
    {
        RequirePositiveFinite(ViewportWidthDip, nameof(ViewportWidthDip));
        RequirePositiveFinite(ViewportHeightDip, nameof(ViewportHeightDip));
        RequirePositiveFinite(ImageWidthPx, nameof(ImageWidthPx));
        RequirePositiveFinite(ImageHeightPx, nameof(ImageHeightPx));
    }

    private static void RequirePositiveFinite(double value, string name)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            throw new ArgumentOutOfRangeException(name, value, $"{name} must be finite and positive.");
        }
    }
}