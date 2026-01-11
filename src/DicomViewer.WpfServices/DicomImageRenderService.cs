namespace DicomViewer.WpfServices;

/// <summary>
/// DICOM 이미지 렌더링 서비스 (WriteableBitmap 변환)
/// DICOM image rendering service (WriteableBitmap conversion)
/// </summary>
public sealed class DicomImageRenderService
{
    /// <summary>
    /// 8비트 그레이스케일 픽셀 데이터를 WriteableBitmap으로 변환
    /// Convert 8-bit grayscale pixel data to WriteableBitmap
    /// </summary>
    public WriteableBitmap CreateGrayscaleBitmap(byte[] pixels, int width, int height)
    {
        var bitmap = new WriteableBitmap(
            width,
            height,
            96,  // DPI X
            96,  // DPI Y
            PixelFormats.Gray8,
            null);

        bitmap.Lock();
        try
        {
            var backBuffer = bitmap.BackBuffer;
            var stride = bitmap.BackBufferStride;

            // 픽셀 데이터 복사
            // Copy pixel data
            for (int y = 0; y < height; y++)
            {
                var srcOffset = y * width;
                var dstOffset = y * stride;

                Marshal.Copy(
                    pixels,
                    srcOffset,
                    backBuffer + dstOffset,
                    width);
            }

            bitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
        }
        finally
        {
            bitmap.Unlock();
        }

        bitmap.Freeze(); // 성능 최적화 / Performance optimization
        return bitmap;
    }

    /// <summary>
    /// 변환(확대/축소, 팬) 적용된 TransformGroup 생성
    /// Create TransformGroup with transforms (zoom, pan)
    /// </summary>
    public TransformGroup CreateTransformGroup(
        double zoomFactor,
        double panX,
        double panY,
        double centerX,
        double centerY)
    {
        var transformGroup = new TransformGroup();

        // 중심점 기준 확대/축소
        // Scale around center point
        var scaleTransform = new ScaleTransform(zoomFactor, zoomFactor, centerX, centerY);
        transformGroup.Children.Add(scaleTransform);

        // 팬 (이동)
        // Pan (translate)
        var translateTransform = new TranslateTransform(panX, panY);
        transformGroup.Children.Add(translateTransform);

        return transformGroup;
    }

    /// <summary>
    /// 회전 및 반전 변환 생성
    /// Create rotation and flip transforms
    /// </summary>
    public TransformGroup CreateFullTransformGroup(
        double zoomFactor,
        double panX,
        double panY,
        double rotation,
        bool flipHorizontal,
        bool flipVertical,
        double centerX,
        double centerY)
    {
        var transformGroup = new TransformGroup();

        // 1. 반전 (Flip)
        if (flipHorizontal || flipVertical)
        {
            var scaleX = flipHorizontal ? -1 : 1;
            var scaleY = flipVertical ? -1 : 1;
            transformGroup.Children.Add(new ScaleTransform(scaleX, scaleY, centerX, centerY));
        }

        // 2. 회전 (Rotation)
        if (rotation != 0)
        {
            transformGroup.Children.Add(new RotateTransform(rotation, centerX, centerY));
        }

        // 3. 확대/축소 (Zoom)
        transformGroup.Children.Add(new ScaleTransform(zoomFactor, zoomFactor, centerX, centerY));

        // 4. 팬 (Pan)
        transformGroup.Children.Add(new TranslateTransform(panX, panY));

        return transformGroup;
    }
}
