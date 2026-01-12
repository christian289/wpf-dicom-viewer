using DicomViewer.Application.DTOs;

namespace DicomViewer.Application.Interfaces;

/// <summary>
/// DICOM 데이터 소스 통합 인터페이스
/// Unified DICOM data source interface
/// </summary>
public interface IDicomDataSource
{
    /// <summary>
    /// 데이터 소스 이름
    /// Data source name
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 데이터 소스 설명
    /// Data source description
    /// </summary>
    string Description { get; }

    /// <summary>
    /// 사용 가능한 샘플 목록 조회
    /// Get available sample list
    /// </summary>
    Task<IReadOnlyList<DicomSampleDto>> GetSamplesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 샘플 다운로드
    /// Download sample
    /// </summary>
    Task<string> DownloadSampleAsync(
        DicomSampleDto sample,
        string destinationFolder,
        IProgress<DownloadProgressInfo>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 연결 테스트
    /// Test connection
    /// </summary>
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// DICOM 샘플 데이터 DTO
/// DICOM sample data DTO
/// </summary>
public sealed record DicomSampleDto(
    string Id,
    string Name,
    string Description,
    string Modality,
    string? BodyPart,
    string DownloadUrl,
    long? FileSize,
    int? ImageCount);
