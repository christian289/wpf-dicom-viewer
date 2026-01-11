namespace DicomViewer.Domain.Entities;

/// <summary>
/// DICOM 환자 정보 엔티티
/// DICOM patient information entity
/// </summary>
public sealed class DicomPatient
{
    public required string PatientId { get; init; }
    public required string PatientName { get; init; }
    public DateOnly? BirthDate { get; init; }
    public string? Sex { get; init; }
}
