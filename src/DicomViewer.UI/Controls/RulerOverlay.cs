using System.Collections.ObjectModel;
using System.Collections.Specialized;
using DicomViewer.Domain.ValueObjects;

namespace DicomViewer.UI.Controls;

/// <summary>
/// 거리 측정 도구 오버레이 CustomControl
/// Distance measurement tool overlay CustomControl
/// </summary>
public sealed class RulerOverlay : FrameworkElement
{
    // Freeze된 렌더링 리소스 (성능 최적화)
    // Frozen rendering resources (performance optimization)
    private static readonly Pen LinePen;
    private static readonly Pen HandlePen;
    private static readonly Brush TextBrush;
    private static readonly Brush HandleBrush;
    private static readonly Typeface TextTypeface;

    static RulerOverlay()
    {
        // 펜과 브러시 생성 및 Freeze
        // Create and freeze pens and brushes
        LinePen = new Pen(Brushes.Yellow, 2);
        LinePen.Freeze();

        HandlePen = new Pen(Brushes.White, 1);
        HandlePen.Freeze();

        TextBrush = Brushes.Yellow;
        HandleBrush = Brushes.Yellow;

        TextTypeface = new Typeface("Segoe UI");
    }

    #region Dependency Properties

    // 측정 목록
    // Measurements list
    public static readonly DependencyProperty MeasurementsProperty =
        DependencyProperty.Register(
            nameof(Measurements),
            typeof(ObservableCollection<MeasurementLine>),
            typeof(RulerOverlay),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnMeasurementsChanged));

    public ObservableCollection<MeasurementLine>? Measurements
    {
        get => (ObservableCollection<MeasurementLine>?)GetValue(MeasurementsProperty);
        set => SetValue(MeasurementsProperty, value);
    }

    // 그리기 모드
    // Drawing mode
    public static readonly DependencyProperty IsDrawingProperty =
        DependencyProperty.Register(
            nameof(IsDrawing),
            typeof(bool),
            typeof(RulerOverlay),
            new PropertyMetadata(false));

    public bool IsDrawing
    {
        get => (bool)GetValue(IsDrawingProperty);
        set => SetValue(IsDrawingProperty, value);
    }

    // 현재 그리는 중인 선분 시작점
    // Start point of currently drawing line
    public static readonly DependencyProperty CurrentStartPointProperty =
        DependencyProperty.Register(
            nameof(CurrentStartPoint),
            typeof(Point),
            typeof(RulerOverlay),
            new FrameworkPropertyMetadata(
                new Point(),
                FrameworkPropertyMetadataOptions.AffectsRender));

    public Point CurrentStartPoint
    {
        get => (Point)GetValue(CurrentStartPointProperty);
        set => SetValue(CurrentStartPointProperty, value);
    }

    // 현재 그리는 중인 선분 끝점
    // End point of currently drawing line
    public static readonly DependencyProperty CurrentEndPointProperty =
        DependencyProperty.Register(
            nameof(CurrentEndPoint),
            typeof(Point),
            typeof(RulerOverlay),
            new FrameworkPropertyMetadata(
                new Point(),
                FrameworkPropertyMetadataOptions.AffectsRender));

    public Point CurrentEndPoint
    {
        get => (Point)GetValue(CurrentEndPointProperty);
        set => SetValue(CurrentEndPointProperty, value);
    }

    // PixelSpacing X (mm/pixel)
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

    // PixelSpacing Y (mm/pixel)
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

    // 확대/축소 배율 (좌표 변환용)
    // Zoom factor (for coordinate transformation)
    public static readonly DependencyProperty ZoomFactorProperty =
        DependencyProperty.Register(
            nameof(ZoomFactor),
            typeof(double),
            typeof(RulerOverlay),
            new FrameworkPropertyMetadata(
                1.0,
                FrameworkPropertyMetadataOptions.AffectsRender));

    public double ZoomFactor
    {
        get => (double)GetValue(ZoomFactorProperty);
        set => SetValue(ZoomFactorProperty, value);
    }

    #endregion

    #region Routed Events

    // 측정 완료 이벤트
    // Measurement completed event
    public static readonly RoutedEvent MeasurementCompletedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(MeasurementCompleted),
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(RulerOverlay));

    public event RoutedEventHandler MeasurementCompleted
    {
        add => AddHandler(MeasurementCompletedEvent, value);
        remove => RemoveHandler(MeasurementCompletedEvent, value);
    }

    #endregion

    private static void OnMeasurementsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RulerOverlay overlay)
        {
            // WeakEventManager 사용으로 메모리 누수 방지
            // Use WeakEventManager to prevent memory leak
            if (e.OldValue is INotifyCollectionChanged oldCollection)
            {
                CollectionChangedEventManager.RemoveHandler(oldCollection, overlay.OnMeasurementsCollectionChanged);
            }

            if (e.NewValue is INotifyCollectionChanged newCollection)
            {
                CollectionChangedEventManager.AddHandler(newCollection, overlay.OnMeasurementsCollectionChanged);
            }

            overlay.InvalidateVisual();
        }
    }

    private void OnMeasurementsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        InvalidateVisual();
    }

    /// <summary>
    /// DrawingContext를 사용한 고성능 렌더링
    /// High-performance rendering using DrawingContext
    /// </summary>
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        // 투명 배경 그리기 (마우스 이벤트 수신을 위해 필수)
        // Draw transparent background (required for mouse event reception)
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));

        // 기존 측정 선분 그리기
        // Draw existing measurement lines
        if (Measurements is not null)
        {
            foreach (var measurement in Measurements)
            {
                DrawMeasurement(dc, measurement);
            }
        }

        // 현재 그리는 중인 선분 그리기
        // Draw currently drawing line
        if (IsDrawing)
        {
            DrawCurrentLine(dc);
        }
    }

    private void DrawMeasurement(DrawingContext dc, MeasurementLine measurement)
    {
        var start = new Point(measurement.StartPoint.X, measurement.StartPoint.Y);
        var end = new Point(measurement.EndPoint.X, measurement.EndPoint.Y);

        // 선분 그리기
        // Draw line
        dc.DrawLine(LinePen, start, end);

        // 핸들 그리기 (원형)
        // Draw handles (circles)
        dc.DrawEllipse(HandleBrush, HandlePen, start, 4, 4);
        dc.DrawEllipse(HandleBrush, HandlePen, end, 4, 4);

        // 레이블 그리기 (거리)
        // Draw label (distance)
        var midPoint = new Point((start.X + end.X) / 2, (start.Y + end.Y) / 2 - 10);
        var formattedText = new FormattedText(
            measurement.Label,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            TextTypeface,
            12,
            TextBrush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        dc.DrawText(formattedText, midPoint);
    }

    private void DrawCurrentLine(DrawingContext dc)
    {
        // 현재 그리는 선분
        // Currently drawing line
        dc.DrawLine(LinePen, CurrentStartPoint, CurrentEndPoint);

        // 핸들
        // Handles
        dc.DrawEllipse(HandleBrush, HandlePen, CurrentStartPoint, 4, 4);
        dc.DrawEllipse(HandleBrush, HandlePen, CurrentEndPoint, 4, 4);

        // 실시간 거리 표시
        // Real-time distance display
        var dx = (CurrentEndPoint.X - CurrentStartPoint.X) * PixelSpacingX;
        var dy = (CurrentEndPoint.Y - CurrentStartPoint.Y) * PixelSpacingY;
        var distanceMm = Math.Sqrt(dx * dx + dy * dy);

        var midPoint = new Point(
            (CurrentStartPoint.X + CurrentEndPoint.X) / 2,
            (CurrentStartPoint.Y + CurrentEndPoint.Y) / 2 - 10);

        var formattedText = new FormattedText(
            $"{distanceMm:F2} mm",
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            TextTypeface,
            12,
            TextBrush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        dc.DrawText(formattedText, midPoint);
    }

    /// <summary>
    /// 실제 거리 계산 (mm)
    /// Calculate actual distance (mm)
    /// </summary>
    public double CalculateDistanceMm(Point start, Point end)
    {
        var dx = (end.X - start.X) * PixelSpacingX;
        var dy = (end.Y - start.Y) * PixelSpacingY;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// 측정 선분 추가
    /// Add measurement line
    /// </summary>
    public void AddMeasurement(Point start, Point end)
    {
        var distanceMm = CalculateDistanceMm(start, end);
        var measurement = new MeasurementLine(
            new Point2D(start.X, start.Y),
            new Point2D(end.X, end.Y),
            distanceMm,
            $"{distanceMm:F2} mm");

        Measurements ??= [];
        Measurements.Add(measurement);

        RaiseEvent(new RoutedEventArgs(MeasurementCompletedEvent, this));
    }

    /// <summary>
    /// 모든 측정 지우기
    /// Clear all measurements
    /// </summary>
    public void ClearMeasurements()
    {
        Measurements?.Clear();
        InvalidateVisual();
    }
}
