namespace DicomViewer.Application.DTOs;

/// <summary>
/// DICOM Instance 전송 객체. 기하 값은 파일에 실제로 있을 때만 채운다.
/// DICOM Instance data transfer object. Geometry values are set only when present in the file.
/// </summary>
public sealed record DicomInstanceDto(
    string SopInstanceUid,
    int InstanceNumber,
    string FilePath,
    int Rows,
    int Columns,
    string? SeriesInstanceUid = null,
    string? FrameOfReferenceUid = null,
    double[]? ImagePositionPatient = null,
    double[]? ImageOrientationPatient = null);