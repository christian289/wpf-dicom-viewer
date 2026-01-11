namespace DicomViewer.Application.DTOs;

/// <summary>
/// TCIA Study 전송 객체
/// TCIA Study data transfer object
/// </summary>
public sealed record TciaStudyDto(
    string StudyInstanceUID,
    string PatientId,
    string? PatientName = null,
    string? StudyDate = null,
    string? StudyDescription = null,
    int SeriesCount = 0);
