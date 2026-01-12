namespace DicomViewer.UI.Converters;

/// <summary>
/// 0-based 인덱스를 1-based 표시 값으로 변환
/// Convert 0-based index to 1-based display value
/// </summary>
public sealed class IndexToDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int index)
            return index + 1;

        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int displayValue)
            return displayValue - 1;

        return value;
    }
}
