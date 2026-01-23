namespace DicomViewer.UI.Controls;

/// <summary>
/// 세련된 트랙/썸 스타일의 슬라이더 Custom Control
/// Modern styled slider with refined track and thumb
/// </summary>
public sealed class ModernSlider : Slider
{
    // Freeze된 기본 브러시 (성능 최적화)
    // Frozen default brushes (performance optimization)
    private static readonly Brush DefaultTrackBackground;
    private static readonly Brush DefaultProgressFill;
    private static readonly Brush DefaultThumbBorderBrush;

    static ModernSlider()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(ModernSlider),
            new FrameworkPropertyMetadata(typeof(ModernSlider)));

        // Frozen 브러시 생성
        // Create frozen brushes
        DefaultTrackBackground = new SolidColorBrush(Color.FromRgb(62, 62, 66));
        DefaultTrackBackground.Freeze();
        DefaultProgressFill = new SolidColorBrush(Color.FromRgb(0, 120, 212));
        DefaultProgressFill.Freeze();
        DefaultThumbBorderBrush = new SolidColorBrush(Color.FromRgb(0, 120, 212));
        DefaultThumbBorderBrush.Freeze();
    }

    #region Dependency Properties

    // 트랙 높이
    // Track height
    public static readonly DependencyProperty TrackHeightProperty =
        DependencyProperty.Register(
            nameof(TrackHeight),
            typeof(double),
            typeof(ModernSlider),
            new PropertyMetadata(4.0));

    public double TrackHeight
    {
        get => (double)GetValue(TrackHeightProperty);
        set => SetValue(TrackHeightProperty, value);
    }

    // 트랙 배경색
    // Track background
    public static readonly DependencyProperty TrackBackgroundProperty =
        DependencyProperty.Register(
            nameof(TrackBackground),
            typeof(Brush),
            typeof(ModernSlider),
            new PropertyMetadata(DefaultTrackBackground));

    public Brush TrackBackground
    {
        get => (Brush)GetValue(TrackBackgroundProperty);
        set => SetValue(TrackBackgroundProperty, value);
    }

    // 진행 트랙 색상
    // Progress track color
    public static readonly DependencyProperty ProgressFillProperty =
        DependencyProperty.Register(
            nameof(ProgressFill),
            typeof(Brush),
            typeof(ModernSlider),
            new PropertyMetadata(DefaultProgressFill));

    public Brush ProgressFill
    {
        get => (Brush)GetValue(ProgressFillProperty);
        set => SetValue(ProgressFillProperty, value);
    }

    // 썸 크기
    // Thumb size
    public static readonly DependencyProperty ThumbSizeProperty =
        DependencyProperty.Register(
            nameof(ThumbSize),
            typeof(double),
            typeof(ModernSlider),
            new PropertyMetadata(16.0));

    public double ThumbSize
    {
        get => (double)GetValue(ThumbSizeProperty);
        set => SetValue(ThumbSizeProperty, value);
    }

    // 썸 배경색
    // Thumb background
    public static readonly DependencyProperty ThumbBackgroundProperty =
        DependencyProperty.Register(
            nameof(ThumbBackground),
            typeof(Brush),
            typeof(ModernSlider),
            new PropertyMetadata(Brushes.White));

    public Brush ThumbBackground
    {
        get => (Brush)GetValue(ThumbBackgroundProperty);
        set => SetValue(ThumbBackgroundProperty, value);
    }

    // 썸 테두리 색상
    // Thumb border color
    public static readonly DependencyProperty ThumbBorderBrushProperty =
        DependencyProperty.Register(
            nameof(ThumbBorderBrush),
            typeof(Brush),
            typeof(ModernSlider),
            new PropertyMetadata(DefaultThumbBorderBrush));

    public Brush ThumbBorderBrush
    {
        get => (Brush)GetValue(ThumbBorderBrushProperty);
        set => SetValue(ThumbBorderBrushProperty, value);
    }

    // 썸 테두리 두께
    // Thumb border thickness
    public static readonly DependencyProperty ThumbBorderThicknessProperty =
        DependencyProperty.Register(
            nameof(ThumbBorderThickness),
            typeof(double),
            typeof(ModernSlider),
            new PropertyMetadata(2.0));

    public double ThumbBorderThickness
    {
        get => (double)GetValue(ThumbBorderThicknessProperty);
        set => SetValue(ThumbBorderThicknessProperty, value);
    }

    // Hover 시 썸 스케일
    // Thumb scale on hover
    public static readonly DependencyProperty ThumbHoverScaleProperty =
        DependencyProperty.Register(
            nameof(ThumbHoverScale),
            typeof(double),
            typeof(ModernSlider),
            new PropertyMetadata(1.2));

    public double ThumbHoverScale
    {
        get => (double)GetValue(ThumbHoverScaleProperty);
        set => SetValue(ThumbHoverScaleProperty, value);
    }

    #endregion
}
