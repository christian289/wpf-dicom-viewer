namespace DicomViewer.Application.Interfaces;

/// <summary>
/// 앱의 기본 입력인 고정 로컬 DICOM 데이터셋 소스
/// Fixed local DICOM dataset source used as the application's default input
/// </summary>
public interface IDicomDatasetSource
{
    /// <summary>
    /// 무결성을 검증한 뒤 헤더를 한 번 읽어 데이터셋을 연다. 원 출처 다운로드나 합성 대체는 하지 않는다.
    /// Verifies integrity, then opens the dataset with one header pass. Never downloads from the source or substitutes synthetic data.
    /// </summary>
    /// <exception cref="InvalidDataException">매니페스트 누락·LFS 포인터 미복원·누락·손상 / Manifest missing, LFS pointer unrestored, file missing or corrupt</exception>
    Task<DicomDatasetDto> OpenAsync(CancellationToken cancellationToken = default);
}