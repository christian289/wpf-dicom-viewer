namespace DicomViewer.OpenSilverServices.Models;

/// <summary>
/// DICOM 픽셀 간격 (mm 단위)
/// DICOM pixel spacing (in mm)
/// </summary>
public sealed class PixelSpacing
{
    public double RowSpacing { get; }
    public double ColumnSpacing { get; }

    public PixelSpacing(double rowSpacing, double columnSpacing)
    {
        RowSpacing = rowSpacing;
        ColumnSpacing = columnSpacing;
    }

    /// <summary>
    /// 기본값 (1mm x 1mm)
    /// Default value (1mm x 1mm)
    /// </summary>
    public static PixelSpacing Default { get; } = new(1.0, 1.0);
}
