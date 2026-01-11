namespace DicomViewer.Application.DTOs;

/// <summary>
/// TCIA Series 전송 객체
/// TCIA Series data transfer object
/// </summary>
public sealed record TciaSeriesDto(
    string SeriesInstanceUID,
    string? SeriesDescription = null,
    string? Modality = null,
    string? BodyPartExamined = null,
    string? Manufacturer = null,
    int ImageCount = 0,
    long FileSize = 0);
