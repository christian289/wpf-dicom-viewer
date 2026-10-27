namespace DicomViewer.UI.Controls;

/// <summary>
/// 히스토그램 시각화 CustomControl
/// Histogram visualization CustomControl
/// </summary>
public sealed class HistogramViewer : FrameworkElement
{
    // 기본(폴백) 브러시: 호스트가 테마를 제공하지 않을 때 사용 (정적 필드 초기화로 DP 기본값에 안전하게 사용)
    // Default (fallback) brushes used when the host provides no theme (static field initializers so they are safe as DP defaults)
    private static readonly Brush DefaultBarBrush = BrushFactory.Frozen(100, 180, 255);
    private static readonly Brush DefaultWindowRangeBrush = BrushFactory.Frozen(255, 200, 60);
    private static readonly Brush DefaultPlotBackground = BrushFactory.Frozen(30, 30, 30, 200);
    private static readonly Brush DefaultPlotBorderBrush = BrushFactory.Frozen(128, 128, 128);

    // 막대/윈도우 범위는 테마 색상을 반투명으로 그린다
    // Bars and window range are drawn with the theme color at partial opacity
    private const double BarOpacity = 0.7;
    private const double WindowRangeOpacity = 0.35;

    static HistogramViewer()
    {
        // FrameworkElement 기반이므로 기본 스타일(테마 브러시)을 쓰려면 DefaultStyleKey를 직접 지정해야 한다
        // Being FrameworkElement-based, DefaultStyleKey must be set explicitly for the default style (theme brushes) to apply
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(HistogramViewer),
            new FrameworkPropertyMetadata(typeof(HistogramViewer)));
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new HistogramViewerAutomationPeer(this);

    public HistogramViewer()
    {
        RebuildBorderPen();
    }

    // 브러시가 바뀔 때만 다시 만드는 캐시된 테두리 펜
    // Cached border pen, rebuilt only when its brush changes
    private Pen _borderPen = null!;

    #region Dependency Properties

    // 히스토그램 데이터 (256개 빈)
    // Histogram data (256 bins)
    public static readonly DependencyProperty HistogramDataProperty =
        DependencyProperty.Register(
            nameof(HistogramData),
            typeof(int[]),
            typeof(HistogramViewer),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsRender));

    public int[]? HistogramData
    {
        get => (int[]?)GetValue(HistogramDataProperty);
        set => SetValue(HistogramDataProperty, value);
    }

    // Window Width (Window/Level 표시용)
    // Window Width (for Window/Level display)
    public static readonly DependencyProperty WindowWidthProperty =
        DependencyProperty.Register(
            nameof(WindowWidth),
            typeof(double),
            typeof(HistogramViewer),
            new FrameworkPropertyMetadata(
                400.0,
                FrameworkPropertyMetadataOptions.AffectsRender));

    public double WindowWidth
    {
        get => (double)GetValue(WindowWidthProperty);
        set => SetValue(WindowWidthProperty, value);
    }

    // Window Center (Window/Level 표시용)
    // Window Center (for Window/Level display)
    public static readonly DependencyProperty WindowCenterProperty =
        DependencyProperty.Register(
            nameof(WindowCenter),
            typeof(double),
            typeof(HistogramViewer),
            new FrameworkPropertyMetadata(
                40.0,
                FrameworkPropertyMetadataOptions.AffectsRender));

    public double WindowCenter
    {
        get => (double)GetValue(WindowCenterProperty);
        set => SetValue(WindowCenterProperty, value);
    }

    // Window/Level 오버레이 표시 여부
    // Show Window/Level overlay
    public static readonly DependencyProperty ShowWindowLevelOverlayProperty =
        DependencyProperty.Register(
            nameof(ShowWindowLevelOverlay),
            typeof(bool),
            typeof(HistogramViewer),
            new FrameworkPropertyMetadata(
                true,
                FrameworkPropertyMetadataOptions.AffectsRender));

    public bool ShowWindowLevelOverlay
    {
        get => (bool)GetValue(ShowWindowLevelOverlayProperty);
        set => SetValue(ShowWindowLevelOverlayProperty, value);
    }

    // 히스토그램 최대값 (0이면 자동 계산)
    // Histogram max value (auto-calculate if 0)
    public static readonly DependencyProperty MaxValueProperty =
        DependencyProperty.Register(
            nameof(MaxValue),
            typeof(int),
            typeof(HistogramViewer),
            new FrameworkPropertyMetadata(
                0,
                FrameworkPropertyMetadataOptions.AffectsRender));

    public int MaxValue
    {
        get => (int)GetValue(MaxValueProperty);
        set => SetValue(MaxValueProperty, value);
    }

    // 막대 브러시
    // Bar brush
    public static readonly DependencyProperty BarBrushProperty =
        DependencyProperty.Register(
            nameof(BarBrush),
            typeof(Brush),
            typeof(HistogramViewer),
            new FrameworkPropertyMetadata(DefaultBarBrush, FrameworkPropertyMetadataOptions.AffectsRender));

    public Brush BarBrush
    {
        get => (Brush)GetValue(BarBrushProperty);
        set => SetValue(BarBrushProperty, value);
    }

    // Window/Level 범위 브러시
    // Window/Level range brush
    public static readonly DependencyProperty WindowRangeBrushProperty =
        DependencyProperty.Register(
            nameof(WindowRangeBrush),
            typeof(Brush),
            typeof(HistogramViewer),
            new FrameworkPropertyMetadata(DefaultWindowRangeBrush, FrameworkPropertyMetadataOptions.AffectsRender));

    public Brush WindowRangeBrush
    {
        get => (Brush)GetValue(WindowRangeBrushProperty);
        set => SetValue(WindowRangeBrushProperty, value);
    }

    // 플롯 배경 브러시
    // Plot background brush
    public static readonly DependencyProperty PlotBackgroundProperty =
        DependencyProperty.Register(
            nameof(PlotBackground),
            typeof(Brush),
            typeof(HistogramViewer),
            new FrameworkPropertyMetadata(DefaultPlotBackground, FrameworkPropertyMetadataOptions.AffectsRender));

    public Brush PlotBackground
    {
        get => (Brush)GetValue(PlotBackgroundProperty);
        set => SetValue(PlotBackgroundProperty, value);
    }

    // 플롯 테두리 브러시
    // Plot border brush
    public static readonly DependencyProperty PlotBorderBrushProperty =
        DependencyProperty.Register(
            nameof(PlotBorderBrush),
            typeof(Brush),
            typeof(HistogramViewer),
            new FrameworkPropertyMetadata(
                DefaultPlotBorderBrush,
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnPlotBorderBrushChanged));

    public Brush PlotBorderBrush
    {
        get => (Brush)GetValue(PlotBorderBrushProperty);
        set => SetValue(PlotBorderBrushProperty, value);
    }

    private static void OnPlotBorderBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is HistogramViewer viewer)
        {
            viewer.RebuildBorderPen();
        }
    }

    private void RebuildBorderPen()
    {
        var pen = new Pen(PlotBorderBrush, 1);
        if (PlotBorderBrush.IsFrozen)
        {
            pen.Freeze();
        }

        _borderPen = pen;
    }

    #endregion

    /// <summary>
    /// DrawingContext를 사용한 고성능 렌더링
    /// High-performance rendering using DrawingContext
    /// </summary>
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var width = ActualWidth;
        var height = ActualHeight;

        if (width <= 0 || height <= 0) return;

        // 배경 그리기
        // Draw background
        dc.DrawRectangle(PlotBackground, _borderPen, new Rect(0, 0, width, height));

        // 히스토그램 그리기
        // Draw histogram
        if (HistogramData is not null && HistogramData.Length > 0)
        {
            DrawHistogram(dc, width, height);
        }

        // Window/Level 오버레이 그리기
        // Draw Window/Level overlay
        if (ShowWindowLevelOverlay)
        {
            DrawWindowLevelOverlay(dc, width, height);
        }
    }

    private void DrawHistogram(DrawingContext dc, double width, double height)
    {
        var data = HistogramData!;
        var binCount = data.Length;
        var barWidth = width / binCount;

        // 최대값 계산
        // Calculate max value
        var maxVal = MaxValue;
        if (maxVal <= 0)
        {
            foreach (var val in data)
            {
                if (val > maxVal) maxVal = val;
            }
        }

        if (maxVal <= 0) return;

        // 히스토그램 바 그리기 (StreamGeometry 사용)
        // Draw histogram bars (using StreamGeometry)
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(0, height), true, true);

            for (int i = 0; i < binCount; i++)
            {
                var barHeight = (data[i] / (double)maxVal) * (height - 4);
                var x = i * barWidth;
                var y = height - barHeight;

                ctx.LineTo(new Point(x, y), false, false);
                ctx.LineTo(new Point(x + barWidth, y), false, false);
            }

            ctx.LineTo(new Point(width, height), false, false);
        }

        geometry.Freeze();
        dc.PushOpacity(BarOpacity);
        dc.DrawGeometry(BarBrush, null, geometry);
        dc.Pop();
    }

    private void DrawWindowLevelOverlay(DrawingContext dc, double width, double height)
    {
        // Window/Level 범위를 히스토그램 위에 표시
        // Display Window/Level range over histogram
        var windowMin = WindowCenter - WindowWidth / 2.0;
        var windowMax = WindowCenter + WindowWidth / 2.0;

        // -1024 ~ 3071 범위를 0 ~ 255로 매핑 (CT용)
        // Map -1024 ~ 3071 range to 0 ~ 255 (for CT)
        // 간단한 예시로 0-255 범위 가정
        // Simple example assuming 0-255 range
        var startX = Math.Max(0, windowMin / 256.0 * width);
        var endX = Math.Min(width, windowMax / 256.0 * width);

        if (endX > startX)
        {
            var rect = new Rect(startX, 0, endX - startX, height);
            dc.PushOpacity(WindowRangeOpacity);
            dc.DrawRectangle(WindowRangeBrush, null, rect);
            dc.Pop();
        }
    }
}