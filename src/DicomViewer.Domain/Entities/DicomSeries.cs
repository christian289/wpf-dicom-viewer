using DicomViewer.Domain.Enums;

namespace DicomViewer.Domain.Entities;

/// <summary>
/// DICOM Series 엔티티
/// DICOM Series entity
/// </summary>
public sealed class DicomSeries
{
    public required string SeriesInstanceUid { get; init; }
    public string? SeriesDescription { get; init; }
    public int SeriesNumber { get; init; }
    public Modality Modality { get; init; }
    public int NumberOfInstances { get; init; }
    public string? BodyPartExamined { get; init; }
    public IReadOnlyList<DicomInstance> Instances { get; init; } = [];
}
