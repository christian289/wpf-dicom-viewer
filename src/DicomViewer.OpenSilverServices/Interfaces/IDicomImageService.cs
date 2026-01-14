using DicomViewer.OpenSilverServices.Models;

namespace DicomViewer.OpenSilverServices.Interfaces;

/// <summary>
/// DICOM 이미지 처리 서비스 인터페이스
/// DICOM image processing service interface
/// </summary>
public interface IDicomImageService
{
    /// <summary>
    /// 바이트 배열에서 픽셀 데이터 로드
    /// Load pixel data from byte array
    /// </summary>
    Task<PixelData> LoadPixelDataFromBytesAsync(
        byte[] dicomBytes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 스트림에서 픽셀 데이터 로드
    /// Load pixel data from stream
    /// </summary>
    Task<PixelData> LoadPixelDataFromStreamAsync(
        Stream dicomStream,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 기본 Window/Level 값 가져오기
    /// Get default Window/Level values
    /// </summary>
    WindowLevel GetDefaultWindowLevel(byte[] dicomBytes);

    /// <summary>
    /// Window/Level 적용하여 8비트 그레이스케일로 변환
    /// Apply Window/Level and convert to 8-bit grayscale
    /// </summary>
    byte[] ApplyWindowLevel(PixelData pixelData, WindowLevel windowLevel);

    /// <summary>
    /// DICOM 이미지의 PixelSpacing 값 가져오기 (mm 단위)
    /// Get PixelSpacing values from DICOM image (in mm)
    /// </summary>
    PixelSpacing GetPixelSpacing(byte[] dicomBytes);
}
