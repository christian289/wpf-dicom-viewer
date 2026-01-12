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

    // 회전 각도 (도)
    // Rotation angle (degrees)
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
            typeof(object),
            typeof(DicomImageViewer),
            new PropertyMetadata(null));

    public object? CurrentTool
    {
        get => GetValue(CurrentToolProperty);
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

        // CurrentTool을 문자열로 변환하여 비교 (enum.ToString())
        // Convert CurrentTool to string for comparison (enum.ToString())
        var toolName = CurrentTool?.ToString() ?? string.Empty;

        switch (toolName)
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

            case "Rotate":
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

        // RenderTransformOrigin="0.5,0.5"가 설정되어 있으므로 중심점 지정 불필요
        // No need to specify center point since RenderTransformOrigin="0.5,0.5" is set

        // 1. 반전 (Flip) - 먼저 적용
        // 1. Flip - applied first
        if (FlipHorizontal || FlipVertical)
        {
            var scaleX = FlipHorizontal ? -1 : 1;
            var scaleY = FlipVertical ? -1 : 1;
            transformGroup.Children.Add(new ScaleTransform(scaleX, scaleY));
        }

        // 2. 회전 (Rotation)
        // 2. Rotation
        if (RotationAngle != 0)
        {
            transformGroup.Children.Add(new RotateTransform(RotationAngle));
        }

        // 3. 확대/축소 (Zoom)
        // 3. Zoom
        transformGroup.Children.Add(new ScaleTransform(ZoomFactor, ZoomFactor));

        // 4. 팬 (Pan) - 마지막에 적용
        // 4. Pan - applied last
        transformGroup.Children.Add(new TranslateTransform(PanX, PanY));

        _imageElement.RenderTransform = transformGroup;
    }

    /// <summary>
    /// 주어진 점이 이미지 영역 내에 있는지 확인합니다.
    /// Check if the given point is within the image area.
    /// </summary>
    /// <param name="point">부모 컨테이너 기준 좌표 / Point in parent container coordinates</param>
    /// <returns>이미지 영역 내이면 true / True if within image area</returns>
    public bool IsPointWithinImage(Point point)
    {
        if (_imageElement is null || ImageSource is null)
            return false;

        // 이미지 원본 크기
        // Original image size
        var imageWidth = ImageSource.Width;
        var imageHeight = ImageSource.Height;

        // 변환된 이미지 크기 (줌 적용)
        // Transformed image size (with zoom)
        var transformedWidth = imageWidth * ZoomFactor;
        var transformedHeight = imageHeight * ZoomFactor;

        // 뷰어 중심
        // Viewer center
        var viewerCenterX = ActualWidth / 2;
        var viewerCenterY = ActualHeight / 2;

        // 이미지 중심 (팬 적용)
        // Image center (with pan)
        var imageCenterX = viewerCenterX + PanX;
        var imageCenterY = viewerCenterY + PanY;

        // 이미지 경계 (회전 미적용 기준)
        // Image bounds (without rotation)
        var left = imageCenterX - transformedWidth / 2;
        var top = imageCenterY - transformedHeight / 2;
        var right = imageCenterX + transformedWidth / 2;
        var bottom = imageCenterY + transformedHeight / 2;

        // 회전이 있는 경우, 점을 역회전하여 확인
        // If rotated, check by inverse-rotating the point
        if (RotationAngle != 0)
        {
            // 점을 이미지 중심 기준으로 역회전
            // Inverse-rotate point around image center
            var radians = -RotationAngle * Math.PI / 180;
            var cos = Math.Cos(radians);
            var sin = Math.Sin(radians);

            var dx = point.X - imageCenterX;
            var dy = point.Y - imageCenterY;

            var rotatedX = dx * cos - dy * sin + imageCenterX;
            var rotatedY = dx * sin + dy * cos + imageCenterY;

            return rotatedX >= left && rotatedX <= right &&
                   rotatedY >= top && rotatedY <= bottom;
        }

        return point.X >= left && point.X <= right &&
               point.Y >= top && point.Y <= bottom;
    }

    /// <summary>
    /// 주어진 점을 이미지 경계 내로 제한합니다.
    /// Clamp the given point to be within the image bounds.
    /// </summary>
    /// <param name="point">부모 컨테이너 기준 좌표 / Point in parent container coordinates</param>
    /// <returns>이미지 경계 내로 제한된 점 / Point clamped to image bounds</returns>
    public Point ClampPointToImage(Point point)
    {
        if (_imageElement is null || ImageSource is null)
            return point;

        // 이미지 원본 크기
        // Original image size
        var imageWidth = ImageSource.Width;
        var imageHeight = ImageSource.Height;

        // 변환된 이미지 크기 (줌 적용)
        // Transformed image size (with zoom)
        var transformedWidth = imageWidth * ZoomFactor;
        var transformedHeight = imageHeight * ZoomFactor;

        // 뷰어 중심
        // Viewer center
        var viewerCenterX = ActualWidth / 2;
        var viewerCenterY = ActualHeight / 2;

        // 이미지 중심 (팬 적용)
        // Image center (with pan)
        var imageCenterX = viewerCenterX + PanX;
        var imageCenterY = viewerCenterY + PanY;

        // 이미지 경계
        // Image bounds
        var left = imageCenterX - transformedWidth / 2;
        var top = imageCenterY - transformedHeight / 2;
        var right = imageCenterX + transformedWidth / 2;
        var bottom = imageCenterY + transformedHeight / 2;

        // 점을 경계 내로 제한
        // Clamp point within bounds
        var clampedX = Math.Clamp(point.X, left, right);
        var clampedY = Math.Clamp(point.Y, top, bottom);

        return new Point(clampedX, clampedY);
    }
}
