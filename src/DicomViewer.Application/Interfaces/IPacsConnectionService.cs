using DicomViewer.Application.DTOs;

namespace DicomViewer.Application.Interfaces;

/// <summary>
/// PACS 연결 서비스 인터페이스
/// PACS connection service interface
/// </summary>
public interface IPacsConnectionService
{
    /// <summary>
    /// Study 목록 조회
    /// Query study list
    /// </summary>
    Task<IReadOnlyList<DicomStudyDto>> QueryStudiesAsync(
        string? patientId = null,
        string? patientName = null,
        DateTime? studyDateFrom = null,
        DateTime? studyDateTo = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Study 상세 정보 조회
    /// Get study details
    /// </summary>
    Task<DicomStudyDto?> GetStudyAsync(
        string studyInstanceUid,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Instance 파일 스트림 가져오기
    /// Get instance file stream
    /// </summary>
    Task<Stream?> GetInstanceStreamAsync(
        string sopInstanceUid,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 연결 테스트
    /// Test connection
    /// </summary>
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
}
