namespace DicomViewer.Application.DTOs;

/// <summary>
/// 단위가 확인된 픽셀 간격. X는 열(수평), Y는 행(수직) 방향 간격이며 DICOM PixelSpacing(0028,0030)의 mm 값이다.
/// Pixel spacing with confirmed units. X is column (horizontal) spacing and Y is row (vertical) spacing, in mm from DICOM PixelSpacing (0028,0030).
/// </summary>
public readonly record struct PixelSpacing(double X, double Y)
{
    /// <summary>
    /// 확인되지 않은 간격 (거리는 픽셀 단위로만 계산)
    /// Unknown spacing (distances are computed in pixels only)
    /// </summary>
    public static PixelSpacing Unknown => default;

    /// <summary>
    /// 두 축 모두 유한한 양수일 때만 유효
    /// Valid only when both axes are finite positive numbers
    /// </summary>
    public bool IsValid => double.IsFinite(X) && double.IsFinite(Y) && X > 0 && Y > 0;

    /// <summary>
    /// 유효한 간격은 mm, 그 밖은 px
    /// mm when valid, otherwise px
    /// </summary>
    public PixelSpacingUnit Unit => IsValid ? PixelSpacingUnit.Millimeter : PixelSpacingUnit.Pixel;

    /// <summary>
    /// 픽셀 차이로부터 거리 계산. 간격이 유효하면 축별 mm, 아니면 픽셀 유클리드 거리
    /// Distance from a pixel delta. Per-axis millimeters when valid, otherwise Euclidean pixels
    /// </summary>
    public double CalculateDistance(double pixelDeltaX, double pixelDeltaY)
    {
        if (!IsValid)
        {
            return Math.Sqrt(pixelDeltaX * pixelDeltaX + pixelDeltaY * pixelDeltaY);
        }

        var mmX = pixelDeltaX * X;
        var mmY = pixelDeltaY * Y;
        return Math.Sqrt(mmX * mmX + mmY * mmY);
    }
}

public enum PixelSpacingUnit
{
    Pixel,
    Millimeter,
}