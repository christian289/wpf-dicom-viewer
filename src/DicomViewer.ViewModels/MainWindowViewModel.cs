namespace DicomViewer.ViewModels;

/// <summary>
/// 메인 윈도우 ViewModel. 뷰어 단일 화면을 ViewModel-first(Mappings.xaml의 암시적 DataTemplate)로 호스팅하고 시작·종료 수명을 뷰어에 위임한다.
/// Main window ViewModel. Hosts the single viewer screen ViewModel-first (implicit DataTemplate in Mappings.xaml) and delegates the
/// start and close lifecycle to the viewer.
/// </summary>
public sealed class MainWindowViewModel : ObservableObject
{
    private readonly ViewerViewModel _viewer;

    public MainWindowViewModel(ViewerViewModel viewer)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        _viewer = viewer;
    }

    public string Title => "DICOM Viewer";

    /// <summary>
    /// 창이 호스팅하는 화면의 ViewModel
    /// ViewModel of the screen hosted by the window
    /// </summary>
    public ObservableObject CurrentViewModel => _viewer;

    /// <summary>
    /// 앱 시작 시 고정 데이터셋을 연다.
    /// Opens the fixed dataset at application start.
    /// </summary>
    public Task StartAsync() => _viewer.OpenDatasetAsync();

    /// <summary>
    /// 창을 닫기 전에 진행 중인 모든 작업을 취소하고 멈출 때까지 기다린다.
    /// Cancels all in-flight work and waits for it to stop before the window closes.
    /// </summary>
    public Task CloseAsync() => _viewer.CloseAsync();
}