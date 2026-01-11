namespace DicomViewer.UI.Controls;

/// <summary>
/// DICOM 이미지 뷰어 커스텀 컨트롤
/// DICOM image viewer custom control
/// </summary>
public class DicomImageViewer : Control
{
    static DicomImageViewer()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(DicomImageViewer),
            new FrameworkPropertyMetadata(typeof(DicomImageViewer)));
    }

    #region Dependency Properties

    // 이미지 소스
    // Image source
    public static readonly DependencyProperty ImageSourceProperty =
        DependencyProperty.Register(
            nameof(ImageSource),
            typeof(ImageSource),
            typeof(DicomImageViewer),
            new PropertyMetadata(null));

    public ImageSource? ImageSource
    {
        get => (ImageSource?)GetValue(ImageSourceProperty);
        set => SetValue(ImageSourceProperty, value);
    }

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

    // 팬 X
    // Pan X
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

    // 팬 Y
    // Pan Y
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
            typeof(string),
            typeof(DicomImageViewer),
            new PropertyMetadata("WindowLevel"));

    public string CurrentTool
    {
        get => (string)GetValue(CurrentToolProperty);
        set => SetValue(CurrentToolProperty, value);
    }

    // 환자 정보 표시
    // Patient info display
    public static readonly DependencyProperty PatientNameProperty =
        DependencyProperty.Register(
            nameof(PatientName),
            typeof(string),
            typeof(DicomImageViewer),
            new PropertyMetadata(string.Empty));

    public string PatientName
    {
        get => (string)GetValue(PatientNameProperty);
        set => SetValue(PatientNameProperty, value);
    }

    // 슬라이스 정보
    // Slice info
    public static readonly DependencyProperty CurrentSliceProperty =
        DependencyProperty.Register(
            nameof(CurrentSlice),
            typeof(int),
            typeof(DicomImageViewer),
            new PropertyMetadata(0));

    public int CurrentSlice
    {
        get => (int)GetValue(CurrentSliceProperty);
        set => SetValue(CurrentSliceProperty, value);
    }

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

    public static readonly RoutedEvent SliceChangedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(SliceChanged),
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(DicomImageViewer));

    public event RoutedEventHandler SliceChanged
    {
        add => AddHandler(SliceChangedEvent, value);
        remove => RemoveHandler(SliceChangedEvent, value);
    }

    #endregion

    private Image? _imageElement;
    private Point _lastMousePosition;
    private bool _isDragging;

    private static void OnTransformChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DicomImageViewer viewer)
        {
            viewer.UpdateTransform();
        }
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _imageElement = GetTemplateChild("PART_Image") as Image;

        if (_imageElement is not null)
        {
            UpdateTransform();
        }
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);

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
            case "Pan":
                PanX += delta.X;
                PanY += delta.Y;
                break;

            case "WindowLevel":
                WindowWidth = Math.Max(1, WindowWidth + delta.X * 2);
                WindowCenter += delta.Y * 2;
                RaiseEvent(new RoutedEventArgs(WindowLevelChangedEvent, this));
                break;

            case "Zoom":
                var zoomDelta = 1.0 + (delta.Y / 100.0);
                ZoomFactor = Math.Clamp(ZoomFactor * zoomDelta, 0.1, 10.0);
                break;
        }

        _lastMousePosition = currentPosition;
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);

        _isDragging = false;
        ReleaseMouseCapture();
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);

        // 마우스 휠로 슬라이스 네비게이션
        // Mouse wheel for slice navigation
        if (e.Delta > 0)
        {
            CurrentSlice = Math.Max(0, CurrentSlice - 1);
        }
        else
        {
            CurrentSlice = Math.Min(TotalSlices - 1, CurrentSlice + 1);
        }

        RaiseEvent(new RoutedEventArgs(SliceChangedEvent, this));
    }

    private void UpdateTransform()
    {
        if (_imageElement is null) return;

        var transformGroup = new TransformGroup();

        // 확대/축소
        // Scale
        transformGroup.Children.Add(
            new ScaleTransform(
                ZoomFactor,
                ZoomFactor,
                ActualWidth / 2,
                ActualHeight / 2));

        // 팬
        // Pan
        transformGroup.Children.Add(
            new TranslateTransform(PanX, PanY));

        _imageElement.RenderTransform = transformGroup;
    }
}
