namespace DicomViewer.Application.DTOs;

/// <summary>
/// DICOM 이미지의 픽셀 간격 (mm 단위)
/// Pixel spacing of DICOM image (in mm)
/// </summary>
public readonly record struct PixelSpacing(double X, double Y)
{
    /// <summary>
    /// 기본값 (1mm x 1mm)
    /// Default value (1mm x 1mm)
    /// </summary>
    public static PixelSpacing Default => new(1.0, 1.0);

    /// <summary>
    /// 유효한 PixelSpacing인지 확인
    /// Check if PixelSpacing is valid
    /// </summary>
    public bool IsValid => X > 0 && Y > 0;

    /// <summary>
    /// 두 점 사이의 실제 거리 계산 (mm)
    /// Calculate actual distance between two points (in mm)
    /// </summary>
    public double CalculateDistance(double pixelDeltaX, double pixelDeltaY)
    {
        var mmX = pixelDeltaX * X;
        var mmY = pixelDeltaY * Y;
        return Math.Sqrt(mmX * mmX + mmY * mmY);
    }
}
