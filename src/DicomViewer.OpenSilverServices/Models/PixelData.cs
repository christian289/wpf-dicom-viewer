namespace DicomViewer.OpenSilverServices.Models;

/// <summary>
/// DICOM 이미지 픽셀 데이터
/// DICOM image pixel data
/// </summary>
public sealed class PixelData
{
    public int Width { get; }
    public int Height { get; }
    public int BitsAllocated { get; }
    public int BitsStored { get; }
    public bool IsSigned { get; }
    public double RescaleSlope { get; }
    public double RescaleIntercept { get; }
    public byte[] RawPixels { get; }

    public int PixelCount => Width * Height;

    public PixelData(
        int width,
        int height,
        int bitsAllocated,
        int bitsStored,
        bool isSigned,
        double rescaleSlope,
        double rescaleIntercept,
        byte[] rawPixels)
    {
        Width = width;
        Height = height;
        BitsAllocated = bitsAllocated;
        BitsStored = bitsStored;
        IsSigned = isSigned;
        RescaleSlope = rescaleSlope;
        RescaleIntercept = rescaleIntercept;
        RawPixels = rawPixels;
    }

    public static PixelData Empty { get; } = new(0, 0, 8, 8, false, 1.0, 0.0, Array.Empty<byte>());
}
