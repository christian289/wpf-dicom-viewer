namespace DicomViewer.UI.Controls;

/// <summary>
/// 히스토그램 시각화 CustomControl
/// Histogram visualization CustomControl
/// </summary>
public sealed class HistogramViewer : FrameworkElement
{
    // Freeze된 렌더링 리소스 (성능 최적화)
    // Frozen rendering resources (performance optimization)
    private static readonly Brush HistogramBrush;
    private static readonly Brush WindowLevelBrush;
    private static readonly Pen BorderPen;
    private static readonly Brush BackgroundBrush;

    static HistogramViewer()
    {
        HistogramBrush = new SolidColorBrush(Color.FromArgb(180, 100, 180, 255));
        HistogramBrush.Freeze();

        WindowLevelBrush = new SolidColorBrush(Color.FromArgb(100, 255, 255, 0));
        WindowLevelBrush.Freeze();

        BorderPen = new Pen(Brushes.Gray, 1);
        BorderPen.Freeze();

        BackgroundBrush = new SolidColorBrush(Color.FromArgb(200, 30, 30, 30));
        BackgroundBrush.Freeze();
    }

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
        dc.DrawRectangle(BackgroundBrush, BorderPen, new Rect(0, 0, width, height));

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
        dc.DrawGeometry(HistogramBrush, null, geometry);
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
            dc.DrawRectangle(WindowLevelBrush, null, rect);
        }
    }
}
