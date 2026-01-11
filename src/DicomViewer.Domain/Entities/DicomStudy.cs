namespace DicomViewer.Domain.Entities;

/// <summary>
/// DICOM Study 엔티티
/// DICOM Study entity
/// </summary>
public sealed class DicomStudy
{
    public required string StudyInstanceUid { get; init; }
    public string? StudyDescription { get; init; }
    public DateTime? StudyDate { get; init; }
    public string? AccessionNumber { get; init; }
    public string? ReferringPhysicianName { get; init; }
    public required DicomPatient Patient { get; init; }
    public IReadOnlyList<DicomSeries> Series { get; init; } = [];
}
