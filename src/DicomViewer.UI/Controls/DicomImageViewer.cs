using System.Diagnostics;

using DicomViewer.Domain.Enums;
using DicomViewer.Domain.ValueObjects;

namespace DicomViewer.UI.Controls;

/// <summary>
/// DICOM 이미지 뷰어 커스텀 컨트롤
/// DICOM image viewer custom control
/// </summary>
/// <remarks>
/// 그리기(이미지/오버레이)와 마우스 히트 테스트는 모두 도메인 <see cref="ImageTransform"/>/<see cref="ImageViewport"/> 하나를 기준으로 한다.
/// Image drawing, overlay drawing and mouse hit-testing all share the single domain <see cref="ImageTransform"/>/<see cref="ImageViewport"/>.
/// </remarks>
[TemplatePart(Name = PartContent, Type = typeof(FrameworkElement))]
[TemplatePart(Name = PartImage, Type = typeof(Image))]
[TemplatePart(Name = PartRuler, Type = typeof(RulerOverlay))]
public class DicomImageViewer : Control
{
    private const string PartContent = "PART_Content";
    private const string PartImage = "PART_Image";
    private const string PartRuler = "PART_Ruler";

    static DicomImageViewer()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(DicomImageViewer),
            new FrameworkPropertyMetadata(typeof(DicomImageViewer)));
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new DicomImageViewerAutomationPeer(this);

    #region Dependency Properties

    // 표시할 프레임 (불변 Gray8 스냅샷)
    // Frame to display (immutable Gray8 snapshot)
    public static readonly DependencyProperty FrameProperty =
        DependencyProperty.Register(
            nameof(Frame),
            typeof(RenderedFrame),
            typeof(DicomImageViewer),
            new FrameworkPropertyMetadata(null, OnFrameChanged));

    public RenderedFrame? Frame
    {
        get => (RenderedFrame?)GetValue(FrameProperty);
        set => SetValue(FrameProperty, value);
    }

    // 프레임 → WriteableBitmap 복사 시간 (ms)
    // Frame → WriteableBitmap copy time (ms)
    private static readonly DependencyPropertyKey FrameCopyMillisecondsPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(FrameCopyMilliseconds),
            typeof(double),
            typeof(DicomImageViewer),
            new FrameworkPropertyMetadata(0.0));

    public static readonly DependencyProperty FrameCopyMillisecondsProperty =
        FrameCopyMillisecondsPropertyKey.DependencyProperty;

    public double FrameCopyMilliseconds => (double)GetValue(FrameCopyMillisecondsProperty);

    // 영상 폭 (픽셀, 프레임에서 유도)
    // Image width in pixels (derived from the frame)
    private static readonly DependencyPropertyKey ImagePixelWidthPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(ImagePixelWidth),
            typeof(int),
            typeof(DicomImageViewer),
            new FrameworkPropertyMetadata(0));

    public static readonly DependencyProperty ImagePixelWidthProperty =
        ImagePixelWidthPropertyKey.DependencyProperty;

    public int ImagePixelWidth => (int)GetValue(ImagePixelWidthProperty);

    // 영상 높이 (픽셀, 프레임에서 유도)
    // Image height in pixels (derived from the frame)
    private static readonly DependencyPropertyKey ImagePixelHeightPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(ImagePixelHeight),
            typeof(int),
            typeof(DicomImageViewer),
            new FrameworkPropertyMetadata(0));

    public static readonly DependencyProperty ImagePixelHeightProperty =
        ImagePixelHeightPropertyKey.DependencyProperty;

    public int ImagePixelHeight => (int)GetValue(ImagePixelHeightProperty);

    // 표시 중인 영상 식별자
    // Identity of the displayed image
    private static readonly DependencyPropertyKey FrameSourceIdPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(FrameSourceId),
            typeof(string),
            typeof(DicomImageViewer),
            new FrameworkPropertyMetadata(null));

    public static readonly DependencyProperty FrameSourceIdProperty =
        FrameSourceIdPropertyKey.DependencyProperty;

    public string? FrameSourceId => (string?)GetValue(FrameSourceIdProperty);

    // 현재 도구가 측정인지 여부 (오버레이 입력 활성화용)
    // Whether the current tool is Measure (enables overlay input)
    private static readonly DependencyPropertyKey IsMeasuringPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(IsMeasuring),
            typeof(bool),
            typeof(DicomImageViewer),
            new FrameworkPropertyMetadata(false));

    public static readonly DependencyProperty IsMeasuringProperty =
        IsMeasuringPropertyKey.DependencyProperty;

    public bool IsMeasuring => (bool)GetValue(IsMeasuringProperty);

    // 확대/축소
    // Zoom
    public static readonly DependencyProperty ZoomFactorProperty =
        DependencyProperty.Register(
            nameof(ZoomFactor),
            typeof(double),
            typeof(DicomImageViewer),
            new FrameworkPropertyMetadata(
                1.0,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnTransformChanged));

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
            typeof(DicomImageViewer),
            new FrameworkPropertyMetadata(
                0.0,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnTransformChanged));

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
            typeof(DicomImageViewer),
            new FrameworkPropertyMetadata(
                0.0,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnTransformChanged));

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
            typeof(DicomImageViewer),
            new FrameworkPropertyMetadata(
                0.0,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnTransformChanged));

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
            typeof(DicomImageViewer),
            new FrameworkPropertyMetadata(
                false,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnTransformChanged));

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
            typeof(DicomImageViewer),
            new FrameworkPropertyMetadata(
                false,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnTransformChanged));

    public bool FlipVertical
    {
        get => (bool)GetValue(FlipVerticalProperty);
        set => SetValue(FlipVerticalProperty, value);
    }

    // Window Width
    public static readonly DependencyProperty WindowWidthProperty =
        DependencyProperty.Register(
            nameof(WindowWidth),
            typeof(double),
            typeof(DicomImageViewer),
            new FrameworkPropertyMetadata(
                400.0,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

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
            typeof(DicomImageViewer),
            new FrameworkPropertyMetadata(
                40.0,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public double WindowCenter
    {
        get => (double)GetValue(WindowCenterProperty);
        set => SetValue(WindowCenterProperty, value);
    }

    // 현재 도구
    // Current tool
    public static readonly DependencyProperty CurrentToolProperty =
        DependencyProperty.Register(
            nameof(CurrentTool),
            typeof(ViewerTool),
            typeof(DicomImageViewer),
            new FrameworkPropertyMetadata(ViewerTool.None, OnCurrentToolChanged));

    public ViewerTool CurrentTool
    {
        get => (ViewerTool)GetValue(CurrentToolProperty);
        set => SetValue(CurrentToolProperty, value);
    }

    // 현재 슬라이스 (0-based)
    // Current slice (0-based)
    public static readonly DependencyProperty CurrentSliceProperty =
        DependencyProperty.Register(
            nameof(CurrentSlice),
            typeof(int),
            typeof(DicomImageViewer),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public int CurrentSlice
    {
        get => (int)GetValue(CurrentSliceProperty);
        set => SetValue(CurrentSliceProperty, value);
    }

    // 전체 슬라이스 수
    // Total slice count
    public static readonly DependencyProperty TotalSlicesProperty =
        DependencyProperty.Register(
            nameof(TotalSlices),
            typeof(int),
            typeof(DicomImageViewer),
            new PropertyMetadata(0));

    public int TotalSlices
    {
        get => (int)GetValue(TotalSlicesProperty);
        set => SetValue(TotalSlicesProperty, value);
    }

    // 측정 목록 (영상 픽셀 좌표). 컬렉션 변경 구독은 오버레이가 약한 이벤트로 처리한다.
    // Measurement list (image pixel coordinates). The overlay handles collection-changed subscription weakly.
    public static readonly DependencyProperty MeasurementsProperty =
        DependencyProperty.Register(
            nameof(Measurements),
            typeof(IEnumerable<MeasurementLine>),
            typeof(DicomImageViewer),
            new FrameworkPropertyMetadata(null));

    public IEnumerable<MeasurementLine>? Measurements
    {
        get => (IEnumerable<MeasurementLine>?)GetValue(MeasurementsProperty);
        set => SetValue(MeasurementsProperty, value);
    }

    // 측정 추가 명령 (MeasurementRequest를 매개변수로 실행)
    // Add-measurement command (executed with a MeasurementRequest parameter)
    public static readonly DependencyProperty AddMeasurementCommandProperty =
        DependencyProperty.Register(
            nameof(AddMeasurementCommand),
            typeof(ICommand),
            typeof(DicomImageViewer),
            new FrameworkPropertyMetadata(null));

    public ICommand? AddMeasurementCommand
    {
        get => (ICommand?)GetValue(AddMeasurementCommandProperty);
        set => SetValue(AddMeasurementCommandProperty, value);
    }

    #endregion

    #region Routed Events

    public static readonly RoutedEvent WindowLevelChangedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(WindowLevelChanged),
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(DicomImageViewer));

    public event RoutedEventHandler WindowLevelChanged
    {
        add => AddHandler(WindowLevelChangedEvent, value);
        remove => RemoveHandler(WindowLevelChangedEvent, value);
    }

    #endregion

    private FrameworkElement? _content;
    private Image? _image;
    private Point _lastMousePosition;
    private bool _isDragging;

    // 단일 재사용 비트맵 (프레임 크기가 바뀔 때만 새로 만든다. 절대 Freeze하지 않는다)
    // Single reusable bitmap (recreated only when the frame size changes; never frozen)
    private WriteableBitmap? _bitmap;

    // 캐시된 Transform 객체: 행렬 값만 제자리에서 갱신
    // Cached transform object: only the matrix value is updated in place
    private readonly MatrixTransform _imageMatrixTransform = new();

    private static void OnTransformChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DicomImageViewer viewer)
        {
            viewer.UpdateImageTransform();
        }
    }

    private static void OnFrameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DicomImageViewer viewer)
        {
            viewer.ApplyFrame((RenderedFrame?)e.NewValue);
        }
    }

    private static void OnCurrentToolChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DicomImageViewer viewer)
        {
            viewer.SetValue(IsMeasuringPropertyKey, (ViewerTool)e.NewValue == ViewerTool.Measure);
        }
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        // 기존 핸들러 해제 (메모리 누수 방지)
        // Unsubscribe existing handlers (prevent memory leak)
        if (_content is not null)
        {
            _content.SizeChanged -= OnContentSizeChanged;
        }

        _content = GetTemplateChild(PartContent) as FrameworkElement;
        _image = GetTemplateChild(PartImage) as Image;

        if (_content is not null)
        {
            _content.SizeChanged += OnContentSizeChanged;
        }

        SyncImageElement();
    }

    private void OnContentSizeChanged(object sender, SizeChangedEventArgs e) => UpdateImageTransform();

    /// <summary>
    /// 프레임 픽셀을 단일 WriteableBitmap에 UI 스레드에서 동기 복사한다. Frame.Pixels는 보관/수정하지 않는다.
    /// Synchronously copy the frame pixels into the single WriteableBitmap on the UI thread. Frame.Pixels is never retained or mutated.
    /// </summary>
    private void ApplyFrame(RenderedFrame? frame)
    {
        if (frame is null)
        {
            _bitmap = null;
            SetValue(ImagePixelWidthPropertyKey, 0);
            SetValue(ImagePixelHeightPropertyKey, 0);
            SetValue(FrameSourceIdPropertyKey, null);
            SetValue(FrameCopyMillisecondsPropertyKey, 0.0);
            SyncImageElement();
            return;
        }

        var start = Stopwatch.GetTimestamp();

        var width = frame.Width;
        var height = frame.Height;
        var recreated = false;

        if (_bitmap is null || _bitmap.PixelWidth != width || _bitmap.PixelHeight != height)
        {
            _bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Gray8, null);
            recreated = true;
        }

        // Gray8: 픽셀당 1바이트이므로 stride == width
        // Gray8: one byte per pixel so stride == width
        _bitmap.WritePixels(new Int32Rect(0, 0, width, height), frame.Pixels, width, 0);

        var elapsedMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;

        SetValue(ImagePixelWidthPropertyKey, width);
        SetValue(ImagePixelHeightPropertyKey, height);
        SetValue(FrameSourceIdPropertyKey, frame.SourceId);
        SetValue(FrameCopyMillisecondsPropertyKey, elapsedMs);

        if (recreated)
        {
            SyncImageElement();
        }
    }

    /// <summary>
    /// 템플릿의 Image 요소에 비트맵/크기/변환을 연결한다.
    /// Wire the bitmap, size and transform to the template Image element.
    /// </summary>
    private void SyncImageElement()
    {
        if (_image is null) return;

        _image.Source = _bitmap;
        _image.RenderTransform = _imageMatrixTransform;

        // 영상 픽셀 크기를 레이아웃 크기로 사용 (DIP가 아닌 픽셀 기준, 변환은 행렬이 담당)
        // Use the pixel size as layout size (pixels, not DIPs; the matrix carries the transform)
        var width = ImagePixelWidth;
        var height = ImagePixelHeight;
        _image.Width = width > 0 ? width : double.NaN;
        _image.Height = height > 0 ? height : double.NaN;

        UpdateImageTransform();
    }

    /// <summary>
    /// ImageTransform.ImageToViewport 세 점 표본으로 이미지의 아핀 행렬을 계산해 캐시된 MatrixTransform을 갱신한다.
    /// Derive the image affine matrix from three ImageToViewport samples and update the cached MatrixTransform in place.
    /// </summary>
    private void UpdateImageTransform()
    {
        if (_image is null || _content is null) return;

        if (!ViewerGeometry.TryCreate(
                _content.ActualWidth,
                _content.ActualHeight,
                ImagePixelWidth,
                ImagePixelHeight,
                ZoomFactor,
                PanX,
                PanY,
                RotationAngle,
                FlipHorizontal,
                FlipVertical,
                out var transform,
                out var viewport))
        {
            return;
        }

        var p00 = transform.ImageToViewport(new Point2D(0, 0), viewport);
        var p10 = transform.ImageToViewport(new Point2D(1, 0), viewport);
        var p01 = transform.ImageToViewport(new Point2D(0, 1), viewport);

        _imageMatrixTransform.Matrix = new Matrix(
            p10.X - p00.X,
            p10.Y - p00.Y,
            p01.X - p00.X,
            p01.Y - p00.Y,
            p00.X,
            p00.Y);
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);

        // 측정은 오버레이가 처리하며 None은 아무 동작도 하지 않는다
        // Measure is handled by the overlay and None does nothing
        if (CurrentTool is ViewerTool.None or ViewerTool.Measure) return;

        _isDragging = true;
        _lastMousePosition = e.GetPosition(this);
        CaptureMouse();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (!_isDragging) return;

        var currentPosition = e.GetPosition(this);
        var delta = currentPosition - _lastMousePosition;

        switch (CurrentTool)
        {
            case ViewerTool.Pan:
                PanX += delta.X;
                PanY += delta.Y;
                break;

            case ViewerTool.WindowLevel:
                WindowWidth = Math.Max(1, WindowWidth + delta.X * 2);
                WindowCenter += delta.Y * 2;
                RaiseEvent(new RoutedEventArgs(WindowLevelChangedEvent, this));
                break;

            case ViewerTool.Zoom:
                ZoomFactor = Math.Clamp(ZoomFactor * (1.0 + delta.Y / 100.0), 0.1, 10.0);
                break;

            case ViewerTool.Rotate:
                // 마우스 X 이동으로 회전
                // Rotate with mouse X movement
                RotationAngle = (RotationAngle + delta.X) % 360;
                break;
        }

        _lastMousePosition = currentPosition;
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);

        if (!_isDragging) return;

        _isDragging = false;
        ReleaseMouseCapture();
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);

        _isDragging = false;
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);

        // 마우스 휠로 슬라이스 탐색: 위로 = 이전, 아래로 = 다음
        // Mouse wheel for slice navigation: up = previous, down = next
        var total = TotalSlices;
        if (total <= 0 || e.Delta == 0) return;

        CurrentSlice = e.Delta > 0
            ? Math.Clamp(CurrentSlice - 1, 0, total - 1)
            : Math.Clamp(CurrentSlice + 1, 0, total - 1);

        e.Handled = true;
    }
}