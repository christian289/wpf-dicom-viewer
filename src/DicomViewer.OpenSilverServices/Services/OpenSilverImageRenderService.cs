using System.Windows.Media.Imaging;
using DicomViewer.OpenSilverServices.Interfaces;

namespace DicomViewer.OpenSilverServices.Services;

/// <summary>
/// OpenSilver용 이미지 렌더링 서비스
/// Image rendering service for OpenSilver
/// </summary>
public sealed class OpenSilverImageRenderService : IImageRenderService
{
    public object CreateGrayscaleBitmap(byte[] pixels, int width, int height)
    {
        if (pixels == null || pixels.Length == 0 || width <= 0 || height <= 0)
        {
            return null!;
        }

        try
        {
            // PNG 인코딩
            // PNG encoding
            var pngBytes = PngEncoder.EncodeGrayscale(pixels, width, height);

            // Base64 Data URI 생성
            // Create Base64 Data URI
            var base64 = Convert.ToBase64String(pngBytes);
            var dataUri = $"data:image/png;base64,{base64}";

            // BitmapImage 생성
            // Create BitmapImage
            var bitmap = new BitmapImage(new Uri(dataUri, UriKind.Absolute));
            return bitmap;
        }
        catch
        {
            return null!;
        }
    }

    public object CreateTransformedBitmap(
        byte[] pixels,
        int width,
        int height,
        double rotationAngle,
        bool flipHorizontal,
        bool flipVertical)
    {
        // 변환은 XAML의 RenderTransform으로 처리
        // Transforms are handled by XAML RenderTransform
        return CreateGrayscaleBitmap(pixels, width, height);
    }
}
