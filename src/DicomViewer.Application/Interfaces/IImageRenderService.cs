namespace DicomViewer.Application.Interfaces;

/// <summary>
/// 이미지 렌더링 서비스 인터페이스
/// Image rendering service interface
/// </summary>
public interface IImageRenderService
{
    /// <summary>
    /// 8비트 그레이스케일 픽셀 데이터를 렌더링 가능한 비트맵으로 변환
    /// Convert 8-bit grayscale pixel data to renderable bitmap
    /// </summary>
    object CreateGrayscaleBitmap(byte[] pixels, int width, int height);

    /// <summary>
    /// 변환(회전, 반전)이 적용된 비트맵 생성
    /// Create bitmap with transforms (rotation, flip) applied
    /// </summary>
    object CreateTransformedBitmap(
        byte[] pixels,
        int width,
        int height,
        double rotationAngle,
        bool flipHorizontal,
        bool flipVertical);
}

/// <summary>
/// 이미지 변환 파라미터
/// Image transform parameters
/// </summary>
public readonly record struct ImageTransformParams(
    double ZoomFactor,
    double PanX,
    double PanY,
    double RotationAngle,
    bool FlipHorizontal,
    bool FlipVertical)
{
    public static ImageTransformParams Default => new(1.0, 0, 0, 0, false, false);
}
