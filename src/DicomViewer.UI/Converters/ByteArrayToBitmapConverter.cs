using System.Runtime.InteropServices;

namespace DicomViewer.UI.Converters;

/// <summary>
/// byte[] 픽셀 데이터를 WriteableBitmap으로 변환하는 컨버터
/// Converter that transforms byte[] pixel data to WriteableBitmap
/// </summary>
public sealed class ByteArrayToBitmapConverter : IMultiValueConverter
{
    public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 3) return null;

        if (values[0] is not byte[] pixels) return null;
        if (values[1] is not int width || width <= 0) return null;
        if (values[2] is not int height || height <= 0) return null;

        if (pixels.Length == 0) return null;

        try
        {
            var bitmap = new WriteableBitmap(
                width,
                height,
                96,
                96,
                PixelFormats.Gray8,
                null);

            bitmap.Lock();
            try
            {
                var backBuffer = bitmap.BackBuffer;
                var stride = bitmap.BackBufferStride;

                for (int y = 0; y < height; y++)
                {
                    var srcOffset = y * width;
                    var dstOffset = y * stride;

                    Marshal.Copy(
                        pixels,
                        srcOffset,
                        backBuffer + dstOffset,
                        Math.Min(width, pixels.Length - srcOffset));
                }

                bitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
            }
            finally
            {
                bitmap.Unlock();
            }

            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
