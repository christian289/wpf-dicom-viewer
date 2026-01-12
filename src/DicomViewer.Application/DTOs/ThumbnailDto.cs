namespace DicomViewer.Application.DTOs;

/// <summary>
/// 썸네일 데이터 DTO
/// Thumbnail data DTO
/// </summary>
public sealed record ThumbnailDto(
    int Index,
    byte[] Pixels,
    int Width,
    int Height,
    string? Label = null)
{
    /// <summary>
    /// 표시용 번호 (1-based)
    /// Display number (1-based)
    /// </summary>
    public int DisplayNumber => Index + 1;

    /// <summary>
    /// 빈 썸네일 생성
    /// Create empty thumbnail
    /// </summary>
    public static ThumbnailDto Empty(int index, int width = 64, int height = 64) =>
        new(index, new byte[width * height], width, height);
}
