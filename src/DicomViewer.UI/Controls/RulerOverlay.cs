using System.Collections.Specialized;

using DicomViewer.Domain.ValueObjects;

namespace DicomViewer.UI.Controls;

/// <summary>
/// 거리 측정 도구 오버레이 CustomControl. 측정 좌표는 영상 픽셀이며 그릴 때마다 도메인 변환으로 뷰포트에 투영한다.
/// Distance measurement tool overlay CustomControl. Measurement coordinates are image pixels and are projected to the viewport through the domain transform on every render.
/// </summary>
public sealed class RulerOverlay : FrameworkElement
{
    // 드래그를 측정으로 인정하는 최소 뷰포트 거리 (DIP)
    // Minimum viewport distance (DIP) for a drag to count as a measurement
    private const double MinimumDragDistanceDip = 5.0;

    private const double HandleRadius = 4.0;
    private const double LabelFontSize = 12.0;

    // 기본(폴백) 브러시: 호스트가 테마를 제공하지 않을 때 사용
    // Default (fallback) brushes used when the host provides no theme
    private static readonly Brush DefaultLineBrush = BrushFactory.Frozen(255, 200, 60);
    private static readonly Brush DefaultHandleOutlineBrush = BrushFactory.Frozen(32, 32, 32);

    // 영상 위 가독성을 위한 반투명 어두운 라벨 칩 (테마와 무관)
    // Semi-transparent dark label chip for legibility over any image (theme independent)
    private static readonly Brush LabelChipBrush = BrushFactory.Frozen(0, 0, 0, 160);

    private static readonly Typeface TextTypeface = new("Segoe UI");

    static RulerOverlay()
    {
        // FrameworkElement 기반이므로 기본 스타일(테마 브러시)을 쓰려면 DefaultStyleKey를 직접 지정해야 한다
        // Being FrameworkElement-based, DefaultStyleKey must be set explicitly for the default style (theme brushes) to apply
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(RulerOverlay),
            new FrameworkPropertyMetadata(typeof(RulerOverlay)));
    }

    public RulerOverlay()
    {
        // 측정 중이 아닐 때는 입력을 받지 않는다 (그리기는 계속)
        // Receives no input unless measuring (drawing continues)
        SetCurrentValue(IsHitTestVisibleProperty, false);
        Focusable = true;

        RebuildLinePen();
        RebuildHandlePen();
    }

    #region Dependency Properties

    // 측정 목록 (영상 픽셀 좌표)
    // Measurements list (image pixel coordinates)
    public static readonly DependencyProperty MeasurementsProperty =
        DependencyProperty.Register(
            nameof(Measurements),
            typeof(IEnumerable<MeasurementLine>),
            typeof(RulerOverlay),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnMeasurementsChanged));

    public IEnumerable<MeasurementLine>? Measurements
    {
        get => (IEnumerable<MeasurementLine>?)GetValue(MeasurementsProperty);
        set => SetValue(MeasurementsProperty, value);
    }

    // 영상 폭 (픽셀)
    // Image width (pixels)
    public static readonly DependencyProperty ImageWidthPxProperty =
        DependencyProperty.Register(
            nameof(ImageWidthPx),
            typeof(int),
            typeof(RulerOverlay),
            new FrameworkPropertyMetadata(
                0,
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnDragContextChanged));

    public int ImageWidthPx
    {
        get => (int)GetValue(ImageWidthPxProperty);
        set => SetValue(ImageWidthPxProperty, value);
    }

    // 영상 높이 (픽셀)
    // Image height (pixels)
    public static readonly DependencyProperty ImageHeightPxProperty =
        DependencyProperty.Register(
            nameof(ImageHeightPx),
            typeof(int),
            typeof(RulerOverlay),
            new FrameworkPropertyMetadata(
                0,
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnDragContextChanged));

    public int ImageHeightPx
    {
        get => (int)GetValue(ImageHeightPxProperty);
        set => SetValue(ImageHeightPxProperty, value);
    }

    // 확대율
    // Zoom factor
    public static readonly DependencyProperty ZoomFactorProperty =
        DependencyProperty.Register(
            nameof(ZoomFactor),
            typeof(double),
            typeof(RulerOverlay),
            new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public double ZoomFactor
    {
        get => (double)GetValue(ZoomFactorProperty);
        set => SetValue(ZoomFactorProperty, value);
    }

    // 팬 X (DIP)
    // Pan X (DIP)
    public static readonly DependencyProperty PanXProperty =
        DependencyProperty.Register(
            nameof(PanX),
            typeof(double),
            typeof(RulerOverlay),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public double PanX
    {
        get => (double)GetValue(PanXProperty);
        set => SetValue(PanXProperty, value);
    }

    // 팬 Y (DIP)
    // Pan Y (DIP)
    public static readonly DependencyProperty PanYProperty =
        DependencyProperty.Register(
            nameof(PanY),
            typeof(double),
            typeof(RulerOverlay),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public double PanY
    {
        get => (double)GetValue(PanYProperty);
        set => SetValue(PanYProperty, value);
    }

    // 회전 각도 (도, 시계 방향)
    // Rotation angle (degrees, clockwise)
    public static readonly DependencyProperty RotationAngleProperty =
        DependencyProperty.Register(
            nameof(RotationAngle),
            typeof(double),
            typeof(RulerOverlay),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public double RotationAngle
    {
        get => (double)GetValue(RotationAngleProperty);
        set => SetValue(RotationAngleProperty, value);
    }

    // 수평 반전
    // Horizontal flip
    public static readonly DependencyProperty FlipHorizontalProperty =
        DependencyProperty.Register(
            nameof(FlipHorizontal),
            typeof(bool),
            typeof(RulerOverlay),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public bool FlipHorizontal
    {
        get => (bool)GetValue(FlipHorizontalProperty);
        set => SetValue(FlipHorizontalProperty, value);
    }

    // 수직 반전
    // Vertical flip
    public static readonly DependencyProperty FlipVerticalProperty =
        DependencyProperty.Register(
            nameof(FlipVertical),
            typeof(bool),
            typeof(RulerOverlay),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public bool FlipVertical
    {
        get => (bool)GetValue(FlipVerticalProperty);
        set => SetValue(FlipVerticalProperty, value);
    }

    // 표시 중인 영상 식별자 (측정 요청에 실린다)
    // Identity of the displayed image (carried by measurement requests)
    public static readonly DependencyProperty FrameSourceIdProperty =
        DependencyProperty.Register(
            nameof(FrameSourceId),
            typeof(string),
            typeof(RulerOverlay),
            new FrameworkPropertyMetadata(null, OnDragContextChanged));

    public string? FrameSourceId
    {
        get => (string?)GetValue(FrameSourceIdProperty);
        set => SetValue(FrameSourceIdProperty, value);
    }

    // 측정 입력 활성화 여부 (false면 입력을 받지 않는다)
    // Whether measuring input is enabled (no input is received when false)
    public static readonly DependencyProperty IsMeasuringProperty =
        DependencyProperty.Register(
            nameof(IsMeasuring),
            typeof(bool),
            typeof(RulerOverlay),
            new FrameworkPropertyMetadata(false, OnIsMeasuringChanged));

    public bool IsMeasuring
    {
        get => (bool)GetValue(IsMeasuringProperty);
        set => SetValue(IsMeasuringProperty, value);
    }

    // 측정 추가 명령 (MeasurementRequest 매개변수)
    // Add-measurement command (MeasurementRequest parameter)
    public static readonly DependencyProperty AddMeasurementCommandProperty =
        DependencyProperty.Register(
            nameof(AddMeasurementCommand),
            typeof(ICommand),
            typeof(RulerOverlay),
            new FrameworkPropertyMetadata(null));

    public ICommand? AddMeasurementCommand
    {
        get => (ICommand?)GetValue(AddMeasurementCommandProperty);
        set => SetValue(AddMeasurementCommandProperty, value);
    }

    // 선 브러시
    // Line brush
    public static readonly DependencyProperty LineBrushProperty =
        DependencyProperty.Register(
            nameof(LineBrush),
            typeof(Brush),
            typeof(RulerOverlay),
            new FrameworkPropertyMetadata(
                DefaultLineBrush,
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnLineBrushChanged));

    public Brush LineBrush
    {
        get => (Brush)GetValue(LineBrushProperty);
        set => SetValue(LineBrushProperty, value);
    }

    // 핸들 채움 브러시
    // Handle fill brush
    public static readonly DependencyProperty HandleBrushProperty =
        DependencyProperty.Register(
            nameof(HandleBrush),
            typeof(Brush),
            typeof(RulerOverlay),
            new FrameworkPropertyMetadata(DefaultLineBrush, FrameworkPropertyMetadataOptions.AffectsRender));

    public Brush HandleBrush
    {
        get => (Brush)GetValue(HandleBrushProperty);
        set => SetValue(HandleBrushProperty, value);
    }

    // 핸들 외곽선 브러시
    // Handle outline brush
    public static readonly DependencyProperty HandleOutlineBrushProperty =
        DependencyProperty.Register(
            nameof(HandleOutlineBrush),
            typeof(Brush),
            typeof(RulerOverlay),
            new FrameworkPropertyMetadata(
                DefaultHandleOutlineBrush,
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnHandleOutlineBrushChanged));

    public Brush HandleOutlineBrush
    {
        get => (Brush)GetValue(HandleOutlineBrushProperty);
        set => SetValue(HandleOutlineBrushProperty, value);
    }

    // 라벨 글자 브러시
    // Label text brush
    public static readonly DependencyProperty LabelBrushProperty =
        DependencyProperty.Register(
            nameof(LabelBrush),
            typeof(Brush),
            typeof(RulerOverlay),
            new FrameworkPropertyMetadata(DefaultLineBrush, FrameworkPropertyMetadataOptions.AffectsRender));

    public Brush LabelBrush
    {
        get => (Brush)GetValue(LabelBrushProperty);
        set => SetValue(LabelBrushProperty, value);
    }

    #endregion

    // 드래그 상태 (영상 픽셀 좌표, 비공개)
    // Drag state (image pixel coordinates, private)
    private bool _isDragging;
    private Point2D _dragStart;
    private Point2D _dragEnd;

    // 브러시가 바뀔 때만 다시 만드는 캐시된 펜
    // Cached pens, rebuilt only when their brush changes
    private Pen _linePen = null!;
    private Pen _handlePen = null!;

    #region Property callbacks

    private static void OnMeasurementsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not RulerOverlay overlay) return;

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
    }

    private void OnMeasurementsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => InvalidateVisual();

    // 영상/식별자가 바뀌면 진행 중인 드래그를 취소한다
    // Abort the in-progress drag when the image or its identity changes
    private static void OnDragContextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RulerOverlay overlay)
        {
            overlay.CancelDrag();
        }
    }

    private static void OnIsMeasuringChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not RulerOverlay overlay) return;

        overlay.CancelDrag();
        overlay.SetCurrentValue(IsHitTestVisibleProperty, (bool)e.NewValue);
    }

    private static void OnLineBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RulerOverlay overlay)
        {
            overlay.RebuildLinePen();
        }
    }

    private static void OnHandleOutlineBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RulerOverlay overlay)
        {
            overlay.RebuildHandlePen();
        }
    }

    private void RebuildLinePen()
    {
        _linePen = CreatePen(LineBrush, 2);
    }

    private void RebuildHandlePen()
    {
        _handlePen = CreatePen(HandleOutlineBrush, 1);
    }

    private static Pen CreatePen(Brush brush, double thickness)
    {
        var pen = new Pen(brush, thickness);
        if (brush.IsFrozen)
        {
            pen.Freeze();
        }

        return pen;
    }

    #endregion

    #region Geometry

    private bool TryGetGeometry(out ImageTransform transform, out ImageViewport viewport) =>
        ViewerGeometry.TryCreate(
            ActualWidth,
            ActualHeight,
            ImageWidthPx,
            ImageHeightPx,
            ZoomFactor,
            PanX,
            PanY,
            RotationAngle,
            FlipHorizontal,
            FlipVertical,
            out transform,
            out viewport);

    private static Point2D ToPoint2D(Point point) => new(point.X, point.Y);

    private static Point ToPoint(Point2D point) => new(point.X, point.Y);

    #endregion

    #region Input

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        if (!IsMeasuring || e.Handled) return;
        if (!TryGetGeometry(out var transform, out var viewport)) return;

        // 영상 안에서만 시작한다
        // Start only inside the image
        if (!transform.TryViewportToImage(ToPoint2D(e.GetPosition(this)), viewport, out var imagePoint)) return;

        _dragStart = imagePoint;
        _dragEnd = imagePoint;
        _isDragging = true;

        Focus();
        CaptureMouse();
        e.Handled = true;
        InvalidateVisual();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (!_isDragging) return;

        if (!TryGetGeometry(out var transform, out var viewport))
        {
            CancelDrag();
            return;
        }

        // 끝점은 영상 경계로 고정 (픽셀 공간)
        // The end point is clamped to the image bounds (pixel space)
        _dragEnd = transform.ViewportToImageClamped(ToPoint2D(e.GetPosition(this)), viewport);
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);

        if (!_isDragging) return;

        var start = _dragStart;
        var end = _dragEnd;
        var hasGeometry = TryGetGeometry(out var transform, out var viewport);

        if (hasGeometry)
        {
            end = transform.ViewportToImageClamped(ToPoint2D(e.GetPosition(this)), viewport);
        }

        _isDragging = false;
        ReleaseMouseCapture();
        e.Handled = true;
        InvalidateVisual();

        if (!hasGeometry) return;

        // 뷰포트 DIP 거리가 충분할 때만 측정으로 인정
        // Count as a measurement only when the viewport DIP distance is large enough
        var distanceDip = transform.ImageToViewport(start, viewport)
            .DistanceTo(transform.ImageToViewport(end, viewport));
        if (distanceDip < MinimumDragDistanceDip) return;

        var command = AddMeasurementCommand;
        var sourceId = FrameSourceId;
        if (command is null || sourceId is null) return;

        var request = new MeasurementRequest(sourceId, start, end);
        if (command.CanExecute(request))
        {
            command.Execute(request);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.Escape && _isDragging)
        {
            CancelDrag();
            e.Handled = true;
        }
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);

        if (_isDragging)
        {
            _isDragging = false;
            InvalidateVisual();
        }
    }

    /// <summary>
    /// 진행 중인 드래그를 명령 실행 없이 취소한다.
    /// Abort the in-progress drag without executing any command.
    /// </summary>
    private void CancelDrag()
    {
        if (!_isDragging) return;

        _isDragging = false;
        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }

        InvalidateVisual();
    }

    #endregion

    #region Rendering

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

        if (!TryGetGeometry(out var transform, out var viewport)) return;

        var pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        // 기존 측정 선분: 영상 픽셀 → 뷰포트 DIP 투영
        // Existing measurement lines: project image pixels to viewport DIP
        var measurements = Measurements;
        if (measurements is not null)
        {
            foreach (var measurement in measurements)
            {
                DrawSegment(dc, transform, viewport, measurement.StartPoint, measurement.EndPoint, measurement.Label, pixelsPerDip);
            }
        }

        // 현재 그리는 중인 선분 (픽셀 공간의 점을 다시 투영하므로 끝점이 영상 경계에 놓인다)
        // Line being drawn (pixel-space points are projected again so a clamped end lies on the image edge)
        if (_isDragging)
        {
            var label = string.Create(CultureInfo.CurrentCulture, $"{_dragStart.DistanceTo(_dragEnd):F1} px");
            DrawSegment(dc, transform, viewport, _dragStart, _dragEnd, label, pixelsPerDip);
        }
    }

    private void DrawSegment(
        DrawingContext dc,
        ImageTransform transform,
        ImageViewport viewport,
        Point2D startImage,
        Point2D endImage,
        string label,
        double pixelsPerDip)
    {
        var start = ToPoint(transform.ImageToViewport(startImage, viewport));
        var end = ToPoint(transform.ImageToViewport(endImage, viewport));

        dc.DrawLine(_linePen, start, end);
        dc.DrawEllipse(HandleBrush, _handlePen, start, HandleRadius, HandleRadius);
        dc.DrawEllipse(HandleBrush, _handlePen, end, HandleRadius, HandleRadius);

        if (string.IsNullOrEmpty(label)) return;

        var text = new FormattedText(
            label,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            TextTypeface,
            LabelFontSize,
            LabelBrush,
            pixelsPerDip);

        var origin = new Point(
            (start.X + end.X) / 2 - text.Width / 2,
            (start.Y + end.Y) / 2 - text.Height - 6);

        dc.DrawRoundedRectangle(
            LabelChipBrush,
            null,
            new Rect(origin.X - 3, origin.Y - 1, text.Width + 6, text.Height + 2),
            3,
            3);
        dc.DrawText(text, origin);
    }

    #endregion
}