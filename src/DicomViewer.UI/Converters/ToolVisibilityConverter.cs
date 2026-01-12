namespace DicomViewer.UI.Converters;

/// <summary>
/// ViewerTool enum을 Visibility로 변환
/// Convert ViewerTool enum to Visibility
/// </summary>
public sealed class ToolVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not Enum enumValue || parameter is not string paramString)
            return Visibility.Collapsed;

        var valueName = enumValue.ToString();
        return valueName.Equals(paramString, StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
