using DicomViewer.OpenSilverServices.Models;

namespace DicomViewer.OpenSilverServices.Interfaces;

/// <summary>
/// TCIA (The Cancer Imaging Archive) 서비스 인터페이스
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
    /// 컬렉션 내 환자 목록 조회
    /// Get patient list in collection
    /// </summary>
    Task<IReadOnlyList<TciaPatientDto>> GetPatientsAsync(
        string collection,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 환자의 Study 목록 조회
    /// Get patient studies
    /// </summary>
    Task<IReadOnlyList<TciaStudyDto>> GetPatientStudiesAsync(
        string collection,
        string patientId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Study의 Series 목록 조회
    /// Get series list in study
    /// </summary>
    Task<IReadOnlyList<TciaSeriesDto>> GetSeriesAsync(
        string studyInstanceUid,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Series 다운로드 (ZIP 형식)
    /// Download series (ZIP format)
    /// </summary>
    Task<byte[]> DownloadSeriesAsync(
        string seriesInstanceUid,
        IProgress<DownloadProgressInfo>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 연결 테스트
    /// Test connection
    /// </summary>
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
}
