namespace DicomViewer.UI.Converters;

/// <summary>
/// ViewerTool enum을 bool로 변환하는 컨버터 (라디오 버튼용)
/// Converter that transforms ViewerTool enum to bool (for radio buttons)
/// </summary>
public sealed class ToolEnumConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null || parameter is null) return false;

        var enumValue = value.ToString();
        var parameterValue = parameter.ToString();

        return string.Equals(enumValue, parameterValue, StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue && boolValue && parameter is not null)
        {
            return parameter.ToString();
        }

        return Binding.DoNothing;
    }
}
