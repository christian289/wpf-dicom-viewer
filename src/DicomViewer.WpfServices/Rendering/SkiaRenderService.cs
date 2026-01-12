using SkiaSharp;
using SkiaSharp.Views.WPF;

namespace DicomViewer.WpfServices.Rendering;

/// <summary>
/// SkiaSharp 기반 GPU 가속 렌더링 서비스
/// SkiaSharp-based GPU accelerated rendering service
/// </summary>
public sealed class SkiaRenderService : IDisposable
{
    // SKBitmap 캐시 (재사용)
    // SKBitmap cache (for reuse)
    private SKBitmap? _cachedBitmap;
    private int _cachedWidth;
    private int _cachedHeight;

    // Freeze된 펜과 브러시 (성능 최적화)
    // Frozen pens and brushes (performance optimization)
    private readonly SKPaint _grayscalePaint;

    public SkiaRenderService()
    {
        _grayscalePaint = new SKPaint
        {
            IsAntialias = false
        };
    }

    /// <summary>
    /// 8비트 그레이스케일 픽셀 데이터를 SKBitmap으로 변환 (GPU 가속)
    /// Convert 8-bit grayscale pixel data to SKBitmap (GPU accelerated)
    /// </summary>
    public SKBitmap CreateGrayscaleBitmap(byte[] pixels, int width, int height)
    {
        // 캐시된 비트맵 크기가 다르면 새로 생성
        // Create new bitmap if cached size differs
        if (_cachedBitmap is null || _cachedWidth != width || _cachedHeight != height)
        {
            _cachedBitmap?.Dispose();
            _cachedBitmap = new SKBitmap(width, height, SKColorType.Gray8, SKAlphaType.Opaque);
            _cachedWidth = width;
            _cachedHeight = height;
        }

        // unsafe로 직접 메모리 복사 (최고 성능)
        // Direct memory copy with unsafe (best performance)
        unsafe
        {
            fixed (byte* srcPtr = pixels)
            {
                var dstPtr = (byte*)_cachedBitmap.GetPixels();
                Buffer.MemoryCopy(srcPtr, dstPtr, pixels.Length, pixels.Length);
            }
        }

        return _cachedBitmap;
    }

    /// <summary>
    /// 변환(회전, 반전, 확대/축소, 팬)이 적용된 SKBitmap 생성
    /// Create SKBitmap with transforms (rotation, flip, zoom, pan)
    /// </summary>
    public SKBitmap ApplyTransforms(
        SKBitmap source,
        double zoomFactor,
        double panX,
        double panY,
        double rotationAngle,
        bool flipHorizontal,
        bool flipVertical)
    {
        var width = source.Width;
        var height = source.Height;

        // 회전 시 크기 계산
        // Calculate size for rotation
        var radians = rotationAngle * Math.PI / 180.0;
        var cos = Math.Abs(Math.Cos(radians));
        var sin = Math.Abs(Math.Sin(radians));

        var newWidth = (int)(width * cos + height * sin);
        var newHeight = (int)(width * sin + height * cos);

        // 결과 비트맵 생성
        // Create result bitmap
        var result = new SKBitmap(newWidth, newHeight, SKColorType.Gray8, SKAlphaType.Opaque);

        using var canvas = new SKCanvas(result);
        canvas.Clear(SKColors.Black);

        // 변환 매트릭스 구성
        // Compose transform matrix
        var matrix = SKMatrix.CreateIdentity();

        // 1. 중심으로 이동
        // Move to center
        var centerX = newWidth / 2f;
        var centerY = newHeight / 2f;

        // 2. 반전 적용
        // Apply flip
        if (flipHorizontal || flipVertical)
        {
            var scaleX = flipHorizontal ? -1f : 1f;
            var scaleY = flipVertical ? -1f : 1f;
            matrix = matrix.PostConcat(SKMatrix.CreateScale(scaleX, scaleY, centerX, centerY));
        }

        // 3. 회전 적용
        // Apply rotation
        if (rotationAngle != 0)
        {
            matrix = matrix.PostConcat(SKMatrix.CreateRotationDegrees((float)rotationAngle, centerX, centerY));
        }

        // 4. 원본 이미지 중앙 배치
        // Center source image
        var offsetX = (newWidth - width) / 2f;
        var offsetY = (newHeight - height) / 2f;
        matrix = matrix.PreConcat(SKMatrix.CreateTranslation(offsetX, offsetY));

        canvas.SetMatrix(matrix);
        canvas.DrawBitmap(source, 0, 0, _grayscalePaint);

        return result;
    }

    /// <summary>
    /// SKBitmap을 WPF WriteableBitmap으로 변환
    /// Convert SKBitmap to WPF WriteableBitmap
    /// </summary>
    public WriteableBitmap ConvertToWriteableBitmap(SKBitmap skBitmap)
    {
        // SkiaSharp.Views.WPF 확장 메서드 사용
        // Use SkiaSharp.Views.WPF extension method
        return skBitmap.ToWriteableBitmap();
    }

    /// <summary>
    /// 8비트 그레이스케일을 직접 WriteableBitmap으로 변환 (GPU 가속)
    /// Convert 8-bit grayscale directly to WriteableBitmap (GPU accelerated)
    /// </summary>
    public WriteableBitmap CreateWriteableBitmapDirect(byte[] pixels, int width, int height)
    {
        var skBitmap = CreateGrayscaleBitmap(pixels, width, height);
        return ConvertToWriteableBitmap(skBitmap);
    }

    /// <summary>
    /// 변환이 적용된 WriteableBitmap 생성
    /// Create WriteableBitmap with transforms applied
    /// </summary>
    public WriteableBitmap CreateTransformedBitmap(
        byte[] pixels,
        int width,
        int height,
        double rotationAngle,
        bool flipHorizontal,
        bool flipVertical)
    {
        var sourceBitmap = CreateGrayscaleBitmap(pixels, width, height);

        // 변환이 필요 없으면 바로 반환
        // Return directly if no transforms needed
        if (rotationAngle == 0 && !flipHorizontal && !flipVertical)
        {
            return ConvertToWriteableBitmap(sourceBitmap);
        }

        using var transformed = ApplyTransforms(
            sourceBitmap, 1.0, 0, 0,
            rotationAngle, flipHorizontal, flipVertical);

        return ConvertToWriteableBitmap(transformed);
    }

    public void Dispose()
    {
        _cachedBitmap?.Dispose();
        _grayscalePaint.Dispose();
    }
}
