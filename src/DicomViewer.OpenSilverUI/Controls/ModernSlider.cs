namespace DicomViewer.OpenSilverUI.Controls;

/// <summary>
/// 모던 스타일 슬라이더 Custom Control
/// Modern style slider custom control
/// </summary>
public class ModernSlider : Slider
{
    public ModernSlider()
    {
        DefaultStyleKey = typeof(ModernSlider);
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

    // 트랙 배경색
    // Track background color
    public static readonly DependencyProperty TrackBackgroundProperty =
        DependencyProperty.Register(
            nameof(TrackBackground),
            typeof(Brush),
            typeof(ModernSlider),
            new PropertyMetadata(new SolidColorBrush(Color.FromRgb(60, 60, 60))));

    public Brush TrackBackground
    {
        get => (Brush)GetValue(TrackBackgroundProperty);
        set => SetValue(TrackBackgroundProperty, value);
    }

    // 진행 색상
    // Progress color
    public static readonly DependencyProperty ProgressBrushProperty =
        DependencyProperty.Register(
            nameof(ProgressBrush),
            typeof(Brush),
            typeof(ModernSlider),
            new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0, 120, 212))));

    public Brush ProgressBrush
    {
        get => (Brush)GetValue(ProgressBrushProperty);
        set => SetValue(ProgressBrushProperty, value);
    }

    #endregion
}
