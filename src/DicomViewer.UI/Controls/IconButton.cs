namespace DicomViewer.UI.Controls;

/// <summary>
/// 아이콘과 텍스트를 조합한 버튼 Custom Control
/// Icon and text combined button custom control
/// </summary>
public sealed class IconButton : Button
{
    // Freeze된 기본 브러시 (성능 최적화)
    // Frozen default brushes (performance optimization)
    private static readonly Brush DefaultHoverBackground;
    private static readonly Brush DefaultPressedBackground;

    static IconButton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(IconButton),
            new FrameworkPropertyMetadata(typeof(IconButton)));

        // Frozen 브러시 생성
        // Create frozen brushes
        DefaultHoverBackground = new SolidColorBrush(Color.FromRgb(62, 62, 66));
        DefaultHoverBackground.Freeze();
        DefaultPressedBackground = new SolidColorBrush(Color.FromRgb(0, 120, 212));
        DefaultPressedBackground.Freeze();
    }

    #region Dependency Properties

    // 아이콘 문자 (Segoe Fluent Icons)
    // Icon character (Segoe Fluent Icons)
    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(
            nameof(Icon),
            typeof(string),
            typeof(IconButton),
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
            typeof(IconButton),
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
            typeof(IconButton),
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
            typeof(IconButton),
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
            typeof(IconButton),
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
            typeof(IconButton),
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
            typeof(IconButton),
            new PropertyMetadata(DefaultHoverBackground));

    public Brush HoverBackground
    {
        get => (Brush)GetValue(HoverBackgroundProperty);
        set => SetValue(HoverBackgroundProperty, value);
    }

    // Pressed 배경색
    // Pressed background color
    public static readonly DependencyProperty PressedBackgroundProperty =
        DependencyProperty.Register(
            nameof(PressedBackground),
            typeof(Brush),
            typeof(IconButton),
            new PropertyMetadata(DefaultPressedBackground));

    public Brush PressedBackground
    {
        get => (Brush)GetValue(PressedBackgroundProperty);
        set => SetValue(PressedBackgroundProperty, value);
    }

    #endregion
}
