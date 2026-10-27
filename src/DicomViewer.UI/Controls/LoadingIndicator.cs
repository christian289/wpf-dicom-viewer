namespace DicomViewer.UI.Controls;

/// <summary>
/// 로딩 스피너 애니메이션 Custom Control
/// Loading spinner animation custom control
/// </summary>
public sealed class LoadingIndicator : Control
{
    // Freeze된 기본(폴백) 브러시: 호스트가 테마를 제공하지 않을 때 사용. 정적 필드 초기화로 DP 기본값에 안전하게 사용된다
    // Frozen default (fallback) brushes used when the host provides no theme; static field initializers make them safe as DP defaults
    private static readonly Brush DefaultSpinnerColor = BrushFactory.Frozen(0, 120, 212);
    private static readonly Brush DefaultTrackColor = BrushFactory.Frozen(62, 62, 66);

    static LoadingIndicator()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(LoadingIndicator),
            new FrameworkPropertyMetadata(typeof(LoadingIndicator)));
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new LoadingIndicatorAutomationPeer(this);

    #region Dependency Properties

    // 로딩 중 여부
    // Is loading
    public static readonly DependencyProperty IsActiveProperty =
        DependencyProperty.Register(
            nameof(IsActive),
            typeof(bool),
            typeof(LoadingIndicator),
            new PropertyMetadata(true));

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    // 스피너 크기
    // Spinner size
    public static readonly DependencyProperty SpinnerSizeProperty =
        DependencyProperty.Register(
            nameof(SpinnerSize),
            typeof(double),
            typeof(LoadingIndicator),
            new PropertyMetadata(32.0));

    public double SpinnerSize
    {
        get => (double)GetValue(SpinnerSizeProperty);
        set => SetValue(SpinnerSizeProperty, value);
    }

    // 스피너 두께
    // Spinner thickness
    public static readonly DependencyProperty SpinnerThicknessProperty =
        DependencyProperty.Register(
            nameof(SpinnerThickness),
            typeof(double),
            typeof(LoadingIndicator),
            new PropertyMetadata(3.0));

    public double SpinnerThickness
    {
        get => (double)GetValue(SpinnerThicknessProperty);
        set => SetValue(SpinnerThicknessProperty, value);
    }

    // 스피너 색상
    // Spinner color
    public static readonly DependencyProperty SpinnerColorProperty =
        DependencyProperty.Register(
            nameof(SpinnerColor),
            typeof(Brush),
            typeof(LoadingIndicator),
            new PropertyMetadata(DefaultSpinnerColor));

    public Brush SpinnerColor
    {
        get => (Brush)GetValue(SpinnerColorProperty);
        set => SetValue(SpinnerColorProperty, value);
    }

    // 배경 트랙 색상
    // Background track color
    public static readonly DependencyProperty TrackColorProperty =
        DependencyProperty.Register(
            nameof(TrackColor),
            typeof(Brush),
            typeof(LoadingIndicator),
            new PropertyMetadata(DefaultTrackColor));

    public Brush TrackColor
    {
        get => (Brush)GetValue(TrackColorProperty);
        set => SetValue(TrackColorProperty, value);
    }

    // 로딩 텍스트
    // Loading text
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text),
            typeof(string),
            typeof(LoadingIndicator),
            new PropertyMetadata(string.Empty));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    // 텍스트 표시 여부
    // Show text
    public static readonly DependencyProperty ShowTextProperty =
        DependencyProperty.Register(
            nameof(ShowText),
            typeof(bool),
            typeof(LoadingIndicator),
            new PropertyMetadata(false));

    public bool ShowText
    {
        get => (bool)GetValue(ShowTextProperty);
        set => SetValue(ShowTextProperty, value);
    }

    #endregion
}