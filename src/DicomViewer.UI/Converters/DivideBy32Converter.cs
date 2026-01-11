namespace DicomViewer.UI.Converters;

/// <summary>
/// 값을 32로 나누는 Converter (스피너 스케일 조정용)
/// Converter to divide value by 32 (for spinner scaling)
/// </summary>
public sealed class DivideBy32Converter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double d)
        {
            return d / 32.0;
        }
        return 1.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
