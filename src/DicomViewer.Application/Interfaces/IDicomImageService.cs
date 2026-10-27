namespace DicomViewer.Application.Interfaces;

/// <summary>
/// DICOM 이미지 처리 서비스 인터페이스
/// DICOM image processing service interface
/// </summary>
public interface IDicomImageService
{
    /// <summary>
    /// 파일에서 단일 프레임 영상과 간격·기본 Window/Level을 로드
    /// Load a single-frame image with its spacing and default Window/Level from a file
    /// </summary>
    /// <exception cref="DicomViewer.Application.Exceptions.UnsupportedDicomImageException">지원하지 않는 형식 / Unsupported format</exception>
    Task<LoadedDicomImage> LoadImageAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 스트림에서 단일 프레임 영상과 간격·기본 Window/Level을 로드
    /// Load a single-frame image with its spacing and default Window/Level from a stream
    /// </summary>
    /// <exception cref="DicomViewer.Application.Exceptions.UnsupportedDicomImageException">지원하지 않는 형식 / Unsupported format</exception>
    Task<LoadedDicomImage> LoadImageFromStreamAsync(
        Stream dicomStream,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 픽셀 데이터를 읽지 않고 헤더 메타데이터만 읽기
    /// Read header metadata without loading pixel data
    /// </summary>
    Task<DicomInstanceDto> ReadMetadataAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Window/Level을 적용해 8비트 그레이스케일을 호출자 버퍼에 기록
    /// Apply Window/Level and write 8-bit grayscale into the caller-owned buffer
    /// </summary>
    /// <remarks>
    /// destination 길이는 PixelCount 이상이어야 하며 WindowWidth는 1 이상이어야 한다.
    /// destination.Length must be at least PixelCount and WindowWidth must be at least 1.
    /// </remarks>
    void ApplyWindowLevel(
        PixelData pixelData,
        WindowLevel windowLevel,
        Span<byte> destination,
        CancellationToken cancellationToken = default);
}