namespace DicomViewer.Application.DTOs;

/// <summary>
/// DICOM Study 전송 객체
/// DICOM Study data transfer object
/// </summary>
public sealed record DicomStudyDto(
    string StudyInstanceUid,
    string PatientId,
    string PatientName,
    string? StudyDescription,
    DateTime? StudyDate,
    string? AccessionNumber,
    IReadOnlyList<DicomSeriesDto> Series);
