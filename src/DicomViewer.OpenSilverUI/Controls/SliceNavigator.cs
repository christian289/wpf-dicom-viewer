namespace DicomViewer.OpenSilverUI.Controls;

/// <summary>
/// 슬라이스 네비게이터 Custom Control
/// Slice navigator custom control
/// </summary>
public class SliceNavigator : Control
{
    public SliceNavigator()
    {
        DefaultStyleKey = typeof(SliceNavigator);
    }

    #region Dependency Properties

    // 현재 슬라이스 인덱스 (0-based)
    // Current slice index (0-based)
    public static readonly DependencyProperty CurrentIndexProperty =
        DependencyProperty.Register(
            nameof(CurrentIndex),
            typeof(int),
            typeof(SliceNavigator),
            new PropertyMetadata(0, OnCurrentIndexChanged));

    public int CurrentIndex
    {
        get => (int)GetValue(CurrentIndexProperty);
        set => SetValue(CurrentIndexProperty, value);
    }

    // 총 슬라이스 수
    // Total slice count
    public static readonly DependencyProperty TotalCountProperty =
        DependencyProperty.Register(
            nameof(TotalCount),
            typeof(int),
            typeof(SliceNavigator),
            new PropertyMetadata(0));

    public int TotalCount
    {
        get => (int)GetValue(TotalCountProperty);
        set => SetValue(TotalCountProperty, value);
    }

    // 표시용 번호 (1-based)
    // Display number (1-based)
    public int DisplayNumber => CurrentIndex + 1;

    // 최대 인덱스 (슬라이더용)
    // Maximum index (for slider)
    public int MaxIndex => Math.Max(0, TotalCount - 1);

    #endregion

    #region Events

    public event EventHandler<SliceChangedEventArgs>? SliceChanged;

    #endregion

    private static void OnCurrentIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SliceNavigator navigator)
        {
            navigator.SliceChanged?.Invoke(navigator, new SliceChangedEventArgs((int)e.NewValue));
        }
    }

    /// <summary>
    /// 다음 슬라이스로 이동
    /// Navigate to next slice
    /// </summary>
    public void NavigateNext()
    {
        if (CurrentIndex < MaxIndex)
        {
            CurrentIndex++;
        }
    }

    /// <summary>
    /// 이전 슬라이스로 이동
    /// Navigate to previous slice
    /// </summary>
    public void NavigatePrevious()
    {
        if (CurrentIndex > 0)
        {
            CurrentIndex--;
        }
    }

    /// <summary>
    /// 첫 번째 슬라이스로 이동
    /// Navigate to first slice
    /// </summary>
    public void NavigateFirst()
    {
        CurrentIndex = 0;
    }

    /// <summary>
    /// 마지막 슬라이스로 이동
    /// Navigate to last slice
    /// </summary>
    public void NavigateLast()
    {
        CurrentIndex = MaxIndex;
    }
}

/// <summary>
/// 슬라이스 변경 이벤트 인자
/// Slice changed event arguments
/// </summary>
public class SliceChangedEventArgs : EventArgs
{
    public int NewIndex { get; }

    public SliceChangedEventArgs(int newIndex)
    {
        NewIndex = newIndex;
    }
}
