namespace DicomViewer.Domain.ValueObjects;

/// <summary>
/// 측정 선분 데이터
/// Measurement line data
/// </summary>
public sealed record MeasurementLine(
    Point2D StartPoint,
    Point2D EndPoint,
    double LengthMm,
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
