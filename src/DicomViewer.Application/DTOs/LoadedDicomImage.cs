namespace DicomViewer.Application.DTOs;

/// <summary>
/// 단일 프레임 DICOM 로드 결과
/// Result of loading a single-frame DICOM image
/// </summary>
public sealed record LoadedDicomImage(
    PixelData Pixels,
    PixelSpacing Spacing,
    WindowLevel DefaultWindowLevel);