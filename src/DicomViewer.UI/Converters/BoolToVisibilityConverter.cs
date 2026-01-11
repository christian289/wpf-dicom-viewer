namespace DicomViewer.UI.Converters;

/// <summary>
/// bool을 Visibility로 변환하는 컨버터
/// Converter that transforms bool to Visibility
/// </summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var boolValue = value is bool b && b;

        // parameter가 "Inverse"면 반전
        // Invert if parameter is "Inverse"
        if (parameter is string s && s.Equals("Inverse", StringComparison.OrdinalIgnoreCase))
        {
            boolValue = !boolValue;
        }

        return boolValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            var result = visibility == Visibility.Visible;

            if (parameter is string s && s.Equals("Inverse", StringComparison.OrdinalIgnoreCase))
            {
                result = !result;
            }

            return result;
        }

        return false;
    }
}
