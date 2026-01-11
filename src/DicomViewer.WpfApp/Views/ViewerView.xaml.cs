using System.Windows.Controls;
using DicomViewer.UI.Controls;

namespace DicomViewer.WpfApp.Views;

/// <summary>
/// ViewerView 코드 비하인드
/// ViewerView code-behind
/// </summary>
public partial class ViewerView : UserControl
{
    public ViewerView()
    {
        InitializeComponent();
    }

    private void DicomImageViewer_SliceChanged(object sender, System.Windows.RoutedEventArgs e)
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
}
