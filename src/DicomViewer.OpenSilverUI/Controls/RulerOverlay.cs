using System.Windows.Input;

namespace DicomViewer.OpenSilverUI.Controls;

/// <summary>
/// 거리 측정 오버레이 Custom Control (Shape 기반)
/// Distance measurement overlay custom control (Shape-based)
/// </summary>
public class RulerOverlay : Control
{
    private Canvas? _canvas;
    private readonly List<MeasurementVisual> _measurements = new List<MeasurementVisual>();

    private bool _isDrawing;
    private Point _startPoint;
    private Line? _currentLine;

    public RulerOverlay()
    {
        DefaultStyleKey = typeof(RulerOverlay);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _canvas = GetTemplateChild("PART_Canvas") as Canvas;
    }

    #region Dependency Properties

    // 픽셀 간격 X (mm)
    // Pixel spacing X (mm)
    public static readonly DependencyProperty PixelSpacingXProperty =
        DependencyProperty.Register(
            nameof(PixelSpacingX),
            typeof(double),
            typeof(RulerOverlay),
            new PropertyMetadata(1.0));

    public double PixelSpacingX
    {
        get => (double)GetValue(PixelSpacingXProperty);
        set => SetValue(PixelSpacingXProperty, value);
    }

    // 픽셀 간격 Y (mm)
    // Pixel spacing Y (mm)
    public static readonly DependencyProperty PixelSpacingYProperty =
        DependencyProperty.Register(
            nameof(PixelSpacingY),
            typeof(double),
            typeof(RulerOverlay),
            new PropertyMetadata(1.0));

    public double PixelSpacingY
    {
        get => (double)GetValue(PixelSpacingYProperty);
        set => SetValue(PixelSpacingYProperty, value);
    }

    // 측정 활성화
    // Measurement enabled
    public static readonly DependencyProperty IsMeasuringProperty =
        DependencyProperty.Register(
            nameof(IsMeasuring),
            typeof(bool),
            typeof(RulerOverlay),
            new PropertyMetadata(false));

    public bool IsMeasuring
    {
        get => (bool)GetValue(IsMeasuringProperty);
        set => SetValue(IsMeasuringProperty, value);
    }

    // 선 색상
    // Line color
    public static readonly DependencyProperty LineBrushProperty =
        DependencyProperty.Register(
            nameof(LineBrush),
            typeof(Brush),
            typeof(RulerOverlay),
            new PropertyMetadata(new SolidColorBrush(Colors.Yellow)));

    public Brush LineBrush
    {
        get => (Brush)GetValue(LineBrushProperty);
        set => SetValue(LineBrushProperty, value);
    }

    #endregion

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        if (!IsMeasuring || _canvas == null) return;

        _isDrawing = true;
        _startPoint = e.GetPosition(_canvas);

        // 현재 그리는 선 생성
        // Create current drawing line
        _currentLine = new Line
        {
            X1 = _startPoint.X,
            Y1 = _startPoint.Y,
            X2 = _startPoint.X,
            Y2 = _startPoint.Y,
            Stroke = LineBrush,
            StrokeThickness = 2
        };
        _canvas.Children.Add(_currentLine);

        CaptureMouse();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (!_isDrawing || _currentLine == null || _canvas == null) return;

        var currentPoint = e.GetPosition(_canvas);
        _currentLine.X2 = currentPoint.X;
        _currentLine.Y2 = currentPoint.Y;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);

        if (!_isDrawing || _currentLine == null || _canvas == null)
        {
            _isDrawing = false;
            ReleaseMouseCapture();
            return;
        }

        var endPoint = e.GetPosition(_canvas);

        // 거리 계산 (mm)
        // Calculate distance (mm)
        var dx = (endPoint.X - _startPoint.X) * PixelSpacingX;
        var dy = (endPoint.Y - _startPoint.Y) * PixelSpacingY;
        var distanceMm = Math.Sqrt(dx * dx + dy * dy);

        // 레이블 생성
        // Create label
        var midPoint = new Point(
            (_startPoint.X + endPoint.X) / 2,
            (_startPoint.Y + endPoint.Y) / 2 - 15);

        var label = new TextBlock
        {
            Text = $"{distanceMm:F1} mm",
            Foreground = LineBrush,
            FontSize = 12,
            FontFamily = new FontFamily("Segoe UI")
        };
        Canvas.SetLeft(label, midPoint.X);
        Canvas.SetTop(label, midPoint.Y);
        _canvas.Children.Add(label);

        // 핸들 생성
        // Create handles
        AddHandle(_startPoint);
        AddHandle(endPoint);

        // 측정 저장
        // Save measurement
        _measurements.Add(new MeasurementVisual
        {
            Line = _currentLine,
            Label = label,
            StartPoint = _startPoint,
            EndPoint = endPoint,
            DistanceMm = distanceMm
        });

        _currentLine = null;
        _isDrawing = false;
        ReleaseMouseCapture();
    }

    private void AddHandle(Point position)
    {
        if (_canvas == null) return;

        var handle = new Ellipse
        {
            Width = 8,
            Height = 8,
            Fill = LineBrush,
            Stroke = new SolidColorBrush(Colors.White),
            StrokeThickness = 1
        };
        Canvas.SetLeft(handle, position.X - 4);
        Canvas.SetTop(handle, position.Y - 4);
        _canvas.Children.Add(handle);
    }

    /// <summary>
    /// 모든 측정 지우기
    /// Clear all measurements
    /// </summary>
    public void ClearMeasurements()
    {
        if (_canvas == null) return;

        _canvas.Children.Clear();
        _measurements.Clear();
    }

    private class MeasurementVisual
    {
        public Line? Line { get; set; }
        public TextBlock? Label { get; set; }
        public Point StartPoint { get; set; }
        public Point EndPoint { get; set; }
        public double DistanceMm { get; set; }
    }
}
