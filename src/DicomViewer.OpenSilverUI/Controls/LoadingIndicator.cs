namespace DicomViewer.OpenSilverUI.Controls;

/// <summary>
/// 로딩 인디케이터 Custom Control
/// Loading indicator custom control
/// </summary>
public class LoadingIndicator : Control
{
    public LoadingIndicator()
    {
        DefaultStyleKey = typeof(LoadingIndicator);
    }

    #region Dependency Properties

    // 로딩 중 여부
    // Is loading
    public static readonly DependencyProperty IsLoadingProperty =
        DependencyProperty.Register(
            nameof(IsLoading),
            typeof(bool),
            typeof(LoadingIndicator),
            new PropertyMetadata(false));

    public bool IsLoading
    {
        get => (bool)GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    // 로딩 메시지
    // Loading message
    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register(
            nameof(Message),
            typeof(string),
            typeof(LoadingIndicator),
            new PropertyMetadata("Loading..."));

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    // 인디케이터 크기
    // Indicator size
    public static readonly DependencyProperty IndicatorSizeProperty =
        DependencyProperty.Register(
            nameof(IndicatorSize),
            typeof(double),
            typeof(LoadingIndicator),
            new PropertyMetadata(32.0));

    public double IndicatorSize
    {
        get => (double)GetValue(IndicatorSizeProperty);
        set => SetValue(IndicatorSizeProperty, value);
    }

    #endregion
}
