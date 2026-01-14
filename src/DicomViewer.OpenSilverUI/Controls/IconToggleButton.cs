using System.Windows.Controls.Primitives;

namespace DicomViewer.OpenSilverUI.Controls;

/// <summary>
/// 아이콘 토글 버튼 Custom Control
/// Icon toggle button custom control
/// </summary>
public class IconToggleButton : ToggleButton
{
    public IconToggleButton()
    {
        DefaultStyleKey = typeof(IconToggleButton);
    }

    #region Dependency Properties

    // 아이콘 문자 (Segoe Fluent Icons)
    // Icon character (Segoe Fluent Icons)
    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(
            nameof(Icon),
            typeof(string),
            typeof(IconToggleButton),
            new PropertyMetadata(string.Empty));

    public string Icon
    {
        get => (string)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    // 체크됨 상태 아이콘
    // Checked state icon
    public static readonly DependencyProperty CheckedIconProperty =
        DependencyProperty.Register(
            nameof(CheckedIcon),
            typeof(string),
            typeof(IconToggleButton),
            new PropertyMetadata(string.Empty));

    public string CheckedIcon
    {
        get => (string)GetValue(CheckedIconProperty);
        set => SetValue(CheckedIconProperty, value);
    }

    // 아이콘 크기
    // Icon size
    public static readonly DependencyProperty IconSizeProperty =
        DependencyProperty.Register(
            nameof(IconSize),
            typeof(double),
            typeof(IconToggleButton),
            new PropertyMetadata(16.0));

    public double IconSize
    {
        get => (double)GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    // 코너 반경
    // Corner radius
    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.Register(
            nameof(CornerRadius),
            typeof(CornerRadius),
            typeof(IconToggleButton),
            new PropertyMetadata(new CornerRadius(4)));

    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    #endregion
}
