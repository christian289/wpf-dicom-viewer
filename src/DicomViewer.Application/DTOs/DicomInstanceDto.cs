namespace DicomViewer.Application.DTOs;

/// <summary>
/// DICOM Instance 전송 객체
/// DICOM Instance data transfer object
/// </summary>
public sealed record DicomInstanceDto(
    string SopInstanceUid,
    int InstanceNumber,
    string FilePath,
    int Rows,
    int Columns,
    double? SliceLocation);
