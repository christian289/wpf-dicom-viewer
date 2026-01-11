namespace DicomViewer.Application.DTOs;

/// <summary>
/// TCIA Patient 전송 객체
/// TCIA Patient data transfer object
/// </summary>
public sealed record TciaPatientDto(
    string PatientId,
    string Collection,
    string? PatientName = null);
