namespace DicomViewer.Domain.ValueObjects;

/// <summary>
/// 표시용으로 게시되는 불변 8비트 그레이스케일 프레임 스냅샷. 크기·픽셀·출처·히스토그램이 한 객체로 원자적으로 게시된다.
/// Immutable 8-bit grayscale frame snapshot published for display. Size, pixels, source and histogram are published atomically as one object.
/// </summary>
/// <remarks>
/// <see cref="Pixels"/>는 게시 때마다 새로 할당되며 게시 후 어떤 쪽도 수정하지 않는다. 소비자는 읽기만 하고 변경하지 않는다.
/// <see cref="Pixels"/> is freshly allocated for every publication and is never written after publication. Consumers only read it.
/// </remarks>
/// <param name="SourceId">표시 중인 영상의 식별자(SOP Instance UID). 같은 영상의 Window/Level 재렌더는 같은 값을 갖는다 / Identity of the displayed image (SOP Instance UID); re-renders of the same image keep the value</param>
/// <param name="Sequence">게시 순번. 모든 게시마다 증가하므로 프레임은 서로 같지 않다 / Publication sequence; strictly increases so frames never compare equal</param>
/// <param name="Width">영상 폭(픽셀) / Image width in pixels</param>
/// <param name="Height">영상 높이(픽셀) / Image height in pixels</param>
/// <param name="Pixels">Width*Height 바이트 / Width*Height bytes</param>
/// <param name="Histogram">256-빈 히스토그램, 표시하지 않으면 null / 256-bin histogram, or null when not displayed</param>
public sealed record RenderedFrame(
    string SourceId,
    long Sequence,
    int Width,
    int Height,
    byte[] Pixels,
    int[]? Histogram)
{
    public int Width { get; } = Width > 0
        ? Width
        : throw new ArgumentOutOfRangeException(nameof(Width), Width, "Width must be positive.");

    public int Height { get; } = Height > 0
        ? Height
        : throw new ArgumentOutOfRangeException(nameof(Height), Height, "Height must be positive.");

    public byte[] Pixels { get; } = Pixels is not null && (long)Pixels.Length == (long)Width * Height
        ? Pixels
        : throw new ArgumentException("Pixels must contain exactly Width*Height bytes.", nameof(Pixels));
}