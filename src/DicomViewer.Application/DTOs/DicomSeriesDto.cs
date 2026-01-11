namespace DicomViewer.Application.DTOs;

/// <summary>
/// DICOM Series 전송 객체
/// DICOM Series data transfer object
/// </summary>
public sealed record DicomSeriesDto(
    string SeriesInstanceUid,
    string? SeriesDescription,
    int SeriesNumber,
    string Modality,
    int NumberOfInstances,
    IReadOnlyList<DicomInstanceDto> Instances);
