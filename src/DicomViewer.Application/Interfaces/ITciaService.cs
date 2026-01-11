using DicomViewer.Application.DTOs;

namespace DicomViewer.Application.Interfaces;

/// <summary>
/// TCIA(The Cancer Imaging Archive) 서비스 인터페이스
/// TCIA (The Cancer Imaging Archive) service interface
/// </summary>
public interface ITciaService
{
    /// <summary>
    /// 컬렉션 목록 조회
    /// Get collection list
    /// </summary>
    Task<IReadOnlyList<TciaCollectionDto>> GetCollectionsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 컬렉션의 환자 목록 조회
    /// Get patient list for a collection
    /// </summary>
    Task<IReadOnlyList<TciaPatientDto>> GetPatientsAsync(
        string collection,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 환자의 Study 목록 조회
    /// Get study list for a patient
    /// </summary>
    Task<IReadOnlyList<TciaStudyDto>> GetPatientStudiesAsync(
        string collection,
        string patientId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Study의 Series 목록 조회
    /// Get series list for a study
    /// </summary>
    Task<IReadOnlyList<TciaSeriesDto>> GetSeriesAsync(
        string studyInstanceUid,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Series 이미지 다운로드 (ZIP)
    /// Download series images (ZIP)
    /// </summary>
    Task<string> DownloadSeriesAsync(
        string seriesInstanceUid,
        string destinationFolder,
        IProgress<DownloadProgressInfo>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 모달리티 목록 조회
    /// Get modality list
    /// </summary>
    Task<IReadOnlyList<string>> GetModalitiesAsync(
        string? collection = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Body Part 목록 조회
    /// Get body part list
    /// </summary>
    Task<IReadOnlyList<string>> GetBodyPartsAsync(
        string? collection = null,
        string? modality = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 연결 테스트
    /// Test connection
    /// </summary>
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
}
