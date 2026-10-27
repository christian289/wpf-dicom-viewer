using DicomViewer.Domain.Enums;

namespace DicomViewer.UI.Converters;

/// <summary>
/// ViewerTool을 bool로 변환하는 컨버터 (라디오 방식 토글 버튼용).
/// ConverterParameter는 ViewerTool 값(x:Static 권장) 또는 ViewerTool 이름 문자열이다.
/// Converter that maps a ViewerTool to a bool (for radio-style toggle buttons).
/// ConverterParameter is a ViewerTool value (x:Static recommended) or a ViewerTool name string.
/// </summary>
public sealed class ToolEnumConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is ViewerTool tool &&
               TryGetTool(parameter, out var expected) &&
               tool == expected;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // 선택된(true) 경우에만 도구를 돌려주고, 해제는 무시한다
        // Return the tool only when checked (true); unchecking is ignored
        if (value is true && TryGetTool(parameter, out var tool))
        {
            return tool;
        }

        return Binding.DoNothing;
    }

    private static bool TryGetTool(object? parameter, out ViewerTool tool)
    {
        switch (parameter)
        {
            case ViewerTool typed:
                tool = typed;
                return true;
            case string name when Enum.TryParse(name, ignoreCase: true, out ViewerTool parsed):
                tool = parsed;
                return true;
            default:
                tool = ViewerTool.None;
                return false;
        }
    }
}