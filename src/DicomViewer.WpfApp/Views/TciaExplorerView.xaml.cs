using System.Windows;
using System.Windows.Controls;

namespace DicomViewer.WpfApp.Views;

/// <summary>
/// TciaExplorerView 코드 비하인드
/// TciaExplorerView code-behind
/// </summary>
public partial class TciaExplorerView : UserControl
{
    public TciaExplorerView()
    {
        InitializeComponent();
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        // 컬렉션 자동 로드
        // Auto-load collections
        if (DataContext is DicomViewer.ViewModels.TciaExplorerViewModel viewModel)
        {
            viewModel.LoadCollectionsCommand.Execute(null);
        }
    }
}
