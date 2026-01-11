namespace DicomViewer.Domain.Entities;

/// <summary>
/// DICOM Instance(이미지) 엔티티
/// DICOM Instance (image) entity
/// </summary>
public sealed class DicomInstance
{
    public required string SopInstanceUid { get; init; }
    public int InstanceNumber { get; init; }
    public required string FilePath { get; init; }
    public int Rows { get; init; }
    public int Columns { get; init; }
    public double? SliceLocation { get; init; }
    public double? SliceThickness { get; init; }
}
