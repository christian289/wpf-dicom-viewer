using System.Windows.Controls.Primitives;

namespace DicomViewer.UI.Controls;

/// <summary>
/// 아이콘과 텍스트를 조합한 토글 버튼 Custom Control
/// Icon and text combined toggle button custom control
/// </summary>
public sealed class IconToggleButton : ToggleButton
{
    static IconToggleButton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(IconToggleButton),
            new FrameworkPropertyMetadata(typeof(IconToggleButton)));
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

    // 텍스트 표시 여부
    // Show text or not
    public static readonly DependencyProperty ShowTextProperty =
        DependencyProperty.Register(
            nameof(ShowText),
            typeof(bool),
            typeof(IconToggleButton),
            new PropertyMetadata(false));

    public bool ShowText
    {
        get => (bool)GetValue(ShowTextProperty);
        set => SetValue(ShowTextProperty, value);
    }

    // 버튼 텍스트
    // Button text
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text),
            typeof(string),
            typeof(IconToggleButton),
            new PropertyMetadata(string.Empty));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    // 아이콘과 텍스트 방향
    // Icon and text orientation
    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.Register(
            nameof(Orientation),
            typeof(Orientation),
            typeof(IconToggleButton),
            new PropertyMetadata(Orientation.Vertical));

    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
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

    // Hover 배경색
    // Hover background color
    public static readonly DependencyProperty HoverBackgroundProperty =
        DependencyProperty.Register(
            nameof(HoverBackground),
            typeof(Brush),
            typeof(IconToggleButton),
            new PropertyMetadata(new SolidColorBrush(Color.FromRgb(62, 62, 66))));

    public Brush HoverBackground
    {
        get => (Brush)GetValue(HoverBackgroundProperty);
        set => SetValue(HoverBackgroundProperty, value);
    }

    // Checked 배경색
    // Checked background color
    public static readonly DependencyProperty CheckedBackgroundProperty =
        DependencyProperty.Register(
            nameof(CheckedBackground),
            typeof(Brush),
            typeof(IconToggleButton),
            new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0, 120, 212))));

    public Brush CheckedBackground
    {
        get => (Brush)GetValue(CheckedBackgroundProperty);
        set => SetValue(CheckedBackgroundProperty, value);
    }

    // Checked 전경색
    // Checked foreground color
    public static readonly DependencyProperty CheckedForegroundProperty =
        DependencyProperty.Register(
            nameof(CheckedForeground),
            typeof(Brush),
            typeof(IconToggleButton),
            new PropertyMetadata(Brushes.White));

    public Brush CheckedForeground
    {
        get => (Brush)GetValue(CheckedForegroundProperty);
        set => SetValue(CheckedForegroundProperty, value);
    }

    #endregion
}
