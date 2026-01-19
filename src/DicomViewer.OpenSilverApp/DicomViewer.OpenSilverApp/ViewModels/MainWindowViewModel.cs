namespace DicomViewer.OpenSilverApp.ViewModels;

/// <summary>
/// 메인 윈도우 ViewModel
/// Main window ViewModel
/// </summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly TciaExplorerViewModel _tciaExplorerViewModel;
    private readonly ViewerViewModel _viewerViewModel;

    [ObservableProperty]
    private ObservableObject? _currentViewModel;

    [ObservableProperty]
    private string _statusMessage = "준비됨 / Ready";

    [ObservableProperty]
    private bool _isStatusError;

    public MainWindowViewModel(
        ITciaService tciaService,
        IDialogService dialogService)
    {
        _tciaExplorerViewModel = new TciaExplorerViewModel(tciaService, dialogService, this);
        _viewerViewModel = new ViewerViewModel(dialogService, this);

        // 초기 화면: TCIA Explorer
        // Initial view: TCIA Explorer
        CurrentViewModel = _tciaExplorerViewModel;
    }

    public TciaExplorerViewModel TciaExplorerViewModel => _tciaExplorerViewModel;
    public ViewerViewModel ViewerViewModel => _viewerViewModel;

    [RelayCommand]
    private void NavigateToViewer()
    {
        CurrentViewModel = _viewerViewModel;
    }

    [RelayCommand]
    private void NavigateToTciaExplorer()
    {
        CurrentViewModel = _tciaExplorerViewModel;
    }

    public void SetStatus(string message, bool isError = false)
    {
        StatusMessage = message;
        IsStatusError = isError;
    }

    public void OpenInViewer(byte[] dicomZipData)
    {
        _viewerViewModel.LoadFromZipData(dicomZipData);
        CurrentViewModel = _viewerViewModel;
    }
}
