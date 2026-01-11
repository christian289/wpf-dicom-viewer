namespace DicomViewer.Application.DTOs;

/// <summary>
/// TCIA Collection 전송 객체
/// TCIA Collection data transfer object
/// </summary>
public sealed record TciaCollectionDto(
    string Collection,
    string? Description = null);
