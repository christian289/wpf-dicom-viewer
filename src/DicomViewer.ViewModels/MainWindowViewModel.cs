using DicomViewer.ViewModels.Messages;

namespace DicomViewer.ViewModels;

/// <summary>
/// 메인 윈도우 ViewModel
/// Main window ViewModel
/// </summary>
public sealed partial class MainWindowViewModel : ObservableRecipient
{
    private readonly ViewerViewModel _viewerViewModel;
    private readonly TciaExplorerViewModel _tciaExplorerViewModel;

    [ObservableProperty]
    private ObservableObject? _currentViewModel;

    [ObservableProperty]
    private string _statusMessage = "준비됨 / Ready";

    [ObservableProperty]
    private bool _isStatusError;

    public MainWindowViewModel(
        ViewerViewModel viewerViewModel,
        TciaExplorerViewModel tciaExplorerViewModel)
    {
        _viewerViewModel = viewerViewModel;
        _tciaExplorerViewModel = tciaExplorerViewModel;

        CurrentViewModel = _tciaExplorerViewModel;

        IsActive = true;
    }

    protected override void OnActivated()
    {
        Messenger.Register<MainWindowViewModel, NavigationMessage>(this, (r, m) =>
        {
            r.HandleNavigation(m);
        });

        Messenger.Register<MainWindowViewModel, StatusMessage>(this, (r, m) =>
        {
            r.StatusMessage = m.Message;
            r.IsStatusError = m.IsError;
        });

        Messenger.Register<MainWindowViewModel, OpenFolderMessage>(this, (r, m) =>
        {
            r._viewerViewModel.LoadFromFolder(m.FolderPath);
            r.CurrentViewModel = r._viewerViewModel;
            r.StatusMessage = $"폴더 로드 완료: {m.FolderPath} / Folder loaded: {m.FolderPath}";
        });
    }

    private void HandleNavigation(NavigationMessage message)
    {
        CurrentViewModel = message.ViewName switch
        {
            "Viewer" => _viewerViewModel,
            "TciaExplorer" => _tciaExplorerViewModel,
            _ => CurrentViewModel
        };
    }

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
}
