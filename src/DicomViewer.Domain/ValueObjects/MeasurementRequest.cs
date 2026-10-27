namespace DicomViewer.Domain.ValueObjects;

/// <summary>
/// 표시 영상 위에서 그린 측정 선분. 좌표는 영상 픽셀(좌상단 원점)이며 SourceId는 그린 시점에 표시 중이던 영상이다.
/// A measurement segment drawn over the displayed image. Coordinates are image pixels (top-left origin); SourceId is the image displayed when it was drawn.
/// </summary>
public readonly record struct MeasurementRequest(string SourceId, Point2D Start, Point2D End);