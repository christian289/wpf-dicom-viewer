using System.Windows.Input;

namespace DicomViewer.OpenSilverUI.Controls;

/// <summary>
/// DICOM 이미지 뷰어 Custom Control
/// DICOM image viewer custom control
/// </summary>
public class DicomImageViewer : Control
{
    private Image? _imageElement;
    private bool _isDragging;
    private Point _lastMousePosition;

    public DicomImageViewer()
    {
        DefaultStyleKey = typeof(DicomImageViewer);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _imageElement = GetTemplateChild("PART_Image") as Image;
        UpdateTransform();
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

    // 줌 팩터
    // Zoom factor
    public static readonly DependencyProperty ZoomFactorProperty =
        DependencyProperty.Register(
            nameof(ZoomFactor),
            typeof(double),
            typeof(DicomImageViewer),
            new PropertyMetadata(1.0, OnTransformChanged));

    public double ZoomFactor
    {
        get => (double)GetValue(ZoomFactorProperty);
        set => SetValue(ZoomFactorProperty, value);
    }

    // Pan X
    public static readonly DependencyProperty PanXProperty =
        DependencyProperty.Register(
            nameof(PanX),
            typeof(double),
            typeof(DicomImageViewer),
            new PropertyMetadata(0.0, OnTransformChanged));

    public double PanX
    {
        get => (double)GetValue(PanXProperty);
        set => SetValue(PanXProperty, value);
    }

    // Pan Y
    public static readonly DependencyProperty PanYProperty =
        DependencyProperty.Register(
            nameof(PanY),
            typeof(double),
            typeof(DicomImageViewer),
            new PropertyMetadata(0.0, OnTransformChanged));

    public double PanY
    {
        get => (double)GetValue(PanYProperty);
        set => SetValue(PanYProperty, value);
    }

    // 회전 각도
    // Rotation angle
    public static readonly DependencyProperty RotationAngleProperty =
        DependencyProperty.Register(
            nameof(RotationAngle),
            typeof(double),
            typeof(DicomImageViewer),
            new PropertyMetadata(0.0, OnTransformChanged));

    public double RotationAngle
    {
        get => (double)GetValue(RotationAngleProperty);
        set => SetValue(RotationAngleProperty, value);
    }

    // 수평 반전
    // Flip horizontal
    public static readonly DependencyProperty FlipHorizontalProperty =
        DependencyProperty.Register(
            nameof(FlipHorizontal),
            typeof(bool),
            typeof(DicomImageViewer),
            new PropertyMetadata(false, OnTransformChanged));

    public bool FlipHorizontal
    {
        get => (bool)GetValue(FlipHorizontalProperty);
        set => SetValue(FlipHorizontalProperty, value);
    }

    // 수직 반전
    // Flip vertical
    public static readonly DependencyProperty FlipVerticalProperty =
        DependencyProperty.Register(
            nameof(FlipVertical),
            typeof(bool),
            typeof(DicomImageViewer),
            new PropertyMetadata(false, OnTransformChanged));

    public bool FlipVertical
    {
        get => (bool)GetValue(FlipVerticalProperty);
        set => SetValue(FlipVerticalProperty, value);
    }

    // 현재 도구
    // Current tool
    public static readonly DependencyProperty CurrentToolProperty =
        DependencyProperty.Register(
            nameof(CurrentTool),
            typeof(ViewerTool),
            typeof(DicomImageViewer),
            new PropertyMetadata(ViewerTool.None));

    public ViewerTool CurrentTool
    {
        get => (ViewerTool)GetValue(CurrentToolProperty);
        set => SetValue(CurrentToolProperty, value);
    }

    #endregion

    #region Events

    public event EventHandler<WindowLevelChangedEventArgs>? WindowLevelChanged;

    #endregion

    private static void OnTransformChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DicomImageViewer viewer)
        {
            viewer.UpdateTransform();
        }
    }

    private void UpdateTransform()
    {
        if (_imageElement == null) return;

        var transformGroup = new TransformGroup();

        // 반전
        // Flip
        var scaleX = FlipHorizontal ? -1 : 1;
        var scaleY = FlipVertical ? -1 : 1;
        transformGroup.Children.Add(new ScaleTransform { ScaleX = scaleX, ScaleY = scaleY });

        // 회전
        // Rotation
        transformGroup.Children.Add(new RotateTransform { Angle = RotationAngle });

        // 줌
        // Zoom
        transformGroup.Children.Add(new ScaleTransform { ScaleX = ZoomFactor, ScaleY = ZoomFactor });

        // 팬
        // Pan
        transformGroup.Children.Add(new TranslateTransform { X = PanX, Y = PanY });

        _imageElement.RenderTransform = transformGroup;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        _isDragging = true;
        _lastMousePosition = e.GetPosition(this);
        CaptureMouse();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (!_isDragging) return;

        var currentPosition = e.GetPosition(this);
        var deltaX = currentPosition.X - _lastMousePosition.X;
        var deltaY = currentPosition.Y - _lastMousePosition.Y;

        switch (CurrentTool)
        {
            case ViewerTool.Pan:
                PanX += deltaX;
                PanY += deltaY;
                break;

            case ViewerTool.WindowLevel:
                // Window/Level 조절 (우클릭 드래그)
                // Window/Level adjustment (right-click drag)
                WindowLevelChanged?.Invoke(this, new WindowLevelChangedEventArgs(deltaX, deltaY));
                break;

            case ViewerTool.Zoom:
                // 줌 조절
                // Zoom adjustment
                var zoomDelta = 1 + (deltaY * -0.01);
                ZoomFactor = Math.Max(0.1, Math.Min(10.0, ZoomFactor * zoomDelta));
                break;
        }

        _lastMousePosition = currentPosition;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        _isDragging = false;
        ReleaseMouseCapture();
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);

        // 마우스 휠로 줌 조절
        // Zoom with mouse wheel
        var zoomDelta = e.Delta > 0 ? 1.1 : 0.9;
        ZoomFactor = Math.Max(0.1, Math.Min(10.0, ZoomFactor * zoomDelta));
    }
}

/// <summary>
/// 뷰어 도구 열거형
/// Viewer tool enumeration
/// </summary>
public enum ViewerTool
{
    None,
    Pan,
    Zoom,
    WindowLevel,
    Rotate,
    Measure
}

/// <summary>
/// Window/Level 변경 이벤트 인자
/// Window/Level changed event arguments
/// </summary>
public class WindowLevelChangedEventArgs : EventArgs
{
    public double DeltaX { get; }
    public double DeltaY { get; }

    public WindowLevelChangedEventArgs(double deltaX, double deltaY)
    {
        DeltaX = deltaX;
        DeltaY = deltaY;
    }
}
