namespace DicomViewer.Application.Interfaces;

/// <summary>
/// DICOM 이미지 처리 서비스 인터페이스
/// DICOM image processing service interface
/// </summary>
public interface IDicomImageService
{
    /// <summary>
    /// 파일에서 픽셀 데이터 로드
    /// Load pixel data from file
    /// </summary>
    Task<PixelData> LoadPixelDataAsync(
        string filePath,
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
    WindowLevel GetDefaultWindowLevel(string filePath);

    /// <summary>
    /// Window/Level 적용하여 8비트 그레이스케일로 변환
    /// Apply Window/Level and convert to 8-bit grayscale
    /// </summary>
    byte[] ApplyWindowLevel(PixelData pixelData, WindowLevel windowLevel);

    /// <summary>
    /// 파일이 유효한 DICOM 이미지인지 확인 (픽셀 데이터 포함 여부)
    /// Check if file is a valid DICOM image (contains pixel data)
    /// </summary>
    bool IsValidDicomImage(string filePath);
}
