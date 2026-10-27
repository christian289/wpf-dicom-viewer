namespace DicomViewer.Domain.ValueObjects;

/// <summary>
/// 측정 선분 데이터. LengthMm은 보정 간격이 확인된 경우에만 값이 있으며 미확인이면 null이다(px 값을 mm로 저장하지 않는다).
/// Measurement line data. LengthMm has a value only when calibration is known; null when unknown (pixel values are never stored as mm).
/// </summary>
public sealed record MeasurementLine(
    Point2D StartPoint,
    Point2D EndPoint,
    double? LengthMm,
    string Label)
{
    /// <summary>
    /// 픽셀 단위 거리 계산
    /// Calculate distance in pixels
    /// </summary>
    public double LengthPixels => Math.Sqrt(
        Math.Pow(EndPoint.X - StartPoint.X, 2) +
        Math.Pow(EndPoint.Y - StartPoint.Y, 2));
}

/// <summary>
/// 2D 점 좌표
/// 2D point coordinate
/// </summary>
public readonly record struct Point2D(double X, double Y)
{
    public static Point2D Zero => new(0, 0);

    public double DistanceTo(Point2D other) => Math.Sqrt(
        Math.Pow(other.X - X, 2) +
        Math.Pow(other.Y - Y, 2));
}