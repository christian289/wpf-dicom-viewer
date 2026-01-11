using System.Windows;

namespace DicomViewer.WpfApp;

/// <summary>
/// MainWindow 코드 비하인드
/// MainWindow code-behind
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
