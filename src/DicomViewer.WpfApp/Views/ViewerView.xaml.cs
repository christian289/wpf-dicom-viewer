using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DicomViewer.UI.Controls;
using DicomViewer.ViewModels;

namespace DicomViewer.WpfApp.Views;

/// <summary>
/// ViewerView 코드 비하인드
/// ViewerView code-behind
/// </summary>
public partial class ViewerView : UserControl
{
    private Point _measureStartPoint;
    private bool _isDrawingMeasurement;

    public ViewerView()
    {
        InitializeComponent();
    }

    private void DicomImageViewer_SliceChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewerViewModel viewModel && sender is DicomImageViewer viewer)
        {
            // 슬라이스 변경 시 ViewModel에 알림
            // Notify ViewModel when slice changes
            if (viewModel.CurrentSliceIndex != viewer.CurrentSlice)
            {
                viewModel.NavigateToSliceCommand.Execute(viewer.CurrentSlice);
            }
        }
    }

    private void RulerOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is RulerOverlay overlay)
        {
            var clickPoint = e.GetPosition(overlay);

            // 이미지 영역 내에서만 측정 시작
            // Only start measurement within image area
            if (!ImageViewer.IsPointWithinImage(clickPoint))
                return;

            _measureStartPoint = clickPoint;
            _isDrawingMeasurement = true;

            overlay.IsDrawing = true;
            overlay.CurrentStartPoint = _measureStartPoint;
            overlay.CurrentEndPoint = _measureStartPoint;

            overlay.CaptureMouse();
        }
    }

    private void RulerOverlay_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDrawingMeasurement && sender is RulerOverlay overlay)
        {
            var currentPoint = e.GetPosition(overlay);

            // 끝점을 이미지 영역 내로 제한
            // Clamp end point to image area
            var clampedPoint = ImageViewer.ClampPointToImage(currentPoint);
            overlay.CurrentEndPoint = clampedPoint;
        }
    }

    private void RulerOverlay_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDrawingMeasurement && sender is RulerOverlay overlay)
        {
            var endPoint = e.GetPosition(overlay);

            // 끝점을 이미지 영역 내로 제한
            // Clamp end point to image area
            endPoint = ImageViewer.ClampPointToImage(endPoint);

            // 최소 거리 확인 (5픽셀 이상)
            // Check minimum distance (at least 5 pixels)
            var distance = Math.Sqrt(
                Math.Pow(endPoint.X - _measureStartPoint.X, 2) +
                Math.Pow(endPoint.Y - _measureStartPoint.Y, 2));

            if (distance >= 5)
            {
                overlay.AddMeasurement(_measureStartPoint, endPoint);
            }

            overlay.IsDrawing = false;
            _isDrawingMeasurement = false;
            overlay.ReleaseMouseCapture();
        }
    }

    private void ClearMeasurements_Click(object sender, RoutedEventArgs e)
    {
        // 모든 측정선 삭제
        // Clear all measurement lines
        RulerOverlay.ClearMeasurements();
    }
}
