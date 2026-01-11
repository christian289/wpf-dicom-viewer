using DicomViewer.Application.DTOs;
using DicomViewer.Application.Interfaces;

namespace DicomViewer.Application.Services;

/// <summary>
/// DICOM Study 서비스
/// DICOM Study service
/// </summary>
public sealed class DicomStudyService(IPacsConnectionService pacsService)
{
    private readonly IPacsConnectionService _pacsService = pacsService;

    /// <summary>
    /// Study 검색
    /// Search studies
    /// </summary>
    public async Task<IReadOnlyList<DicomStudyDto>> SearchStudiesAsync(
        string? patientId = null,
        string? patientName = null,
        DateTime? studyDateFrom = null,
        DateTime? studyDateTo = null,
        CancellationToken cancellationToken = default)
    {
        return await _pacsService.QueryStudiesAsync(
            patientId,
            patientName,
            studyDateFrom,
            studyDateTo,
            cancellationToken);
    }

    /// <summary>
    /// Study 로드
    /// Load study
    /// </summary>
    public async Task<DicomStudyDto?> LoadStudyAsync(
        string studyInstanceUid,
        CancellationToken cancellationToken = default)
    {
        return await _pacsService.GetStudyAsync(studyInstanceUid, cancellationToken);
    }

    /// <summary>
    /// 연결 테스트
    /// Test connection
    /// </summary>
    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        return await _pacsService.TestConnectionAsync(cancellationToken);
    }
}
