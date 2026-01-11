namespace DicomViewer.Domain.ValueObjects;

/// <summary>
/// DICOM 이미지 픽셀 데이터를 나타내는 레코드
/// Record representing DICOM image pixel data
/// </summary>
public sealed record PixelData(
    int Width,
    int Height,
    int BitsAllocated,
    int BitsStored,
    bool IsSigned,
    double RescaleSlope,
    double RescaleIntercept,
    byte[] RawPixels)
{
    public int PixelCount => Width * Height;

    public static PixelData Empty => new(0, 0, 8, 8, false, 1.0, 0.0, []);
}
