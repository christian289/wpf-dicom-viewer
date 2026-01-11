namespace DicomViewer.Domain.ValueObjects;

/// <summary>
/// 이미지 변환 상태를 나타내는 불변 레코드
/// Immutable record representing image transformation state
/// </summary>
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
}
