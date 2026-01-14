namespace DicomViewer.OpenSilverUI.Controls;

/// <summary>
/// 히스토그램 뷰어 Custom Control (Shape 기반)
/// Histogram viewer custom control (Shape-based)
/// </summary>
public class HistogramViewer : Control
{
    private Canvas? _canvas;
    private Polygon? _histogramPolygon;
    private Rectangle? _windowLevelRect;

    public HistogramViewer()
    {
        DefaultStyleKey = typeof(HistogramViewer);
        SizeChanged += OnSizeChanged;
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _canvas = GetTemplateChild("PART_Canvas") as Canvas;
        _histogramPolygon = GetTemplateChild("PART_HistogramPolygon") as Polygon;
        _windowLevelRect = GetTemplateChild("PART_WindowLevelRect") as Rectangle;

        UpdateHistogramVisual();
        UpdateWindowLevelOverlay();
    }

    #region Dependency Properties

    // 히스토그램 데이터 (256 bins)
    // Histogram data (256 bins)
    public static readonly DependencyProperty HistogramDataProperty =
        DependencyProperty.Register(
            nameof(HistogramData),
            typeof(int[]),
            typeof(HistogramViewer),
            new PropertyMetadata(null, OnHistogramDataChanged));

    public int[]? HistogramData
    {
        get => (int[]?)GetValue(HistogramDataProperty);
        set => SetValue(HistogramDataProperty, value);
    }

    // Window Width
    public static readonly DependencyProperty WindowWidthProperty =
        DependencyProperty.Register(
            nameof(WindowWidth),
            typeof(double),
            typeof(HistogramViewer),
            new PropertyMetadata(400.0, OnWindowLevelChanged));

    public double WindowWidth
    {
        get => (double)GetValue(WindowWidthProperty);
        set => SetValue(WindowWidthProperty, value);
    }

    // Window Center
    public static readonly DependencyProperty WindowCenterProperty =
        DependencyProperty.Register(
            nameof(WindowCenter),
            typeof(double),
            typeof(HistogramViewer),
            new PropertyMetadata(40.0, OnWindowLevelChanged));

    public double WindowCenter
    {
        get => (double)GetValue(WindowCenterProperty);
        set => SetValue(WindowCenterProperty, value);
    }

    // 히스토그램 색상
    // Histogram color
    public static readonly DependencyProperty HistogramBrushProperty =
        DependencyProperty.Register(
            nameof(HistogramBrush),
            typeof(Brush),
            typeof(HistogramViewer),
            new PropertyMetadata(new SolidColorBrush(Color.FromArgb(180, 100, 180, 255))));

    public Brush HistogramBrush
    {
        get => (Brush)GetValue(HistogramBrushProperty);
        set => SetValue(HistogramBrushProperty, value);
    }

    // Window/Level 오버레이 색상
    // Window/Level overlay color
    public static readonly DependencyProperty WindowLevelBrushProperty =
        DependencyProperty.Register(
            nameof(WindowLevelBrush),
            typeof(Brush),
            typeof(HistogramViewer),
            new PropertyMetadata(new SolidColorBrush(Color.FromArgb(100, 255, 255, 0))));

    public Brush WindowLevelBrush
    {
        get => (Brush)GetValue(WindowLevelBrushProperty);
        set => SetValue(WindowLevelBrushProperty, value);
    }

    #endregion

    private static void OnHistogramDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is HistogramViewer viewer)
        {
            viewer.UpdateHistogramVisual();
        }
    }

    private static void OnWindowLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is HistogramViewer viewer)
        {
            viewer.UpdateWindowLevelOverlay();
        }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateHistogramVisual();
        UpdateWindowLevelOverlay();
    }

    private void UpdateHistogramVisual()
    {
        if (_histogramPolygon == null || HistogramData == null || HistogramData.Length == 0)
            return;

        var width = ActualWidth;
        var height = ActualHeight;

        if (width <= 0 || height <= 0)
            return;

        var points = new PointCollection();
        var data = HistogramData;
        var binCount = data.Length;
        var barWidth = width / binCount;

        // 최대값 계산 (상위 1% 클리핑)
        // Calculate max value (clip top 1%)
        var sortedData = data.OrderByDescending(x => x).ToArray();
        var clipIndex = Math.Max(1, binCount / 100);
        var maxVal = sortedData[clipIndex];

        if (maxVal <= 0)
        {
            _histogramPolygon.Points = new PointCollection();
            return;
        }

        // 첫 번째 점 (좌하단)
        // First point (bottom left)
        points.Add(new Point(0, height));

        // 히스토그램 점들
        // Histogram points
        for (int i = 0; i < binCount; i++)
        {
            var normalizedValue = Math.Min(1.0, (double)data[i] / maxVal);
            var barHeight = normalizedValue * (height - 4);
            points.Add(new Point(i * barWidth, height - barHeight));
        }

        // 마지막 점 (우하단)
        // Last point (bottom right)
        points.Add(new Point(width, height));

        _histogramPolygon.Points = points;
    }

    private void UpdateWindowLevelOverlay()
    {
        if (_windowLevelRect == null || _canvas == null)
            return;

        var width = ActualWidth;
        var height = ActualHeight;

        if (width <= 0 || height <= 0)
            return;

        // Window/Level을 0-255 범위로 정규화
        // Normalize Window/Level to 0-255 range
        var minValue = WindowCenter - WindowWidth / 2;
        var maxValue = WindowCenter + WindowWidth / 2;

        // -1024 ~ 3071 (CT Hounsfield) 범위를 0-255로 매핑
        // Map -1024 ~ 3071 (CT Hounsfield) range to 0-255
        const double minHU = -1024;
        const double maxHU = 3071;
        const double range = maxHU - minHU;

        var startX = ((minValue - minHU) / range) * width;
        var endX = ((maxValue - minHU) / range) * width;

        startX = Math.Max(0, Math.Min(width, startX));
        endX = Math.Max(0, Math.Min(width, endX));

        Canvas.SetLeft(_windowLevelRect, startX);
        _windowLevelRect.Width = Math.Max(0, endX - startX);
        _windowLevelRect.Height = height;
        _windowLevelRect.Visibility = Visibility.Visible;
    }
}
