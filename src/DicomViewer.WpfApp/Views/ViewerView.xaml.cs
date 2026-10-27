using System.Windows.Controls;

namespace DicomViewer.WpfApp.Views;

/// <summary>
/// 뷰어 업무 화면. 여러 컨트롤을 조합해 ViewerViewModel에 바인딩하며 코드 비하인드 로직이 없다.
/// Viewer business screen. Composes several controls bound to ViewerViewModel and has no code-behind logic.
/// </summary>
public partial class ViewerView : UserControl
{
    public ViewerView()
    {
        InitializeComponent();
    }
}