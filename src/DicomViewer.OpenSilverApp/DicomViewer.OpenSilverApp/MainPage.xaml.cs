using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using DicomViewer.OpenSilverApp.ViewModels;
using DicomViewer.OpenSilverApp.Views;

namespace DicomViewer.OpenSilverApp;

public partial class MainPage : Page
{
    private readonly MainWindowViewModel _viewModel;

    public MainPage()
    {
        this.InitializeComponent();

        // DataContext를 MainWindowViewModel로 설정
        // Set DataContext to MainWindowViewModel
        _viewModel = App.MainViewModel!;
        this.DataContext = _viewModel;

        // ViewModel의 PropertyChanged 이벤트 구독
        // Subscribe to ViewModel's PropertyChanged event
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        // 초기 뷰 설정
        // Set initial view
        UpdateContent();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.CurrentViewModel))
        {
            UpdateContent();
        }
    }

    /// <summary>
    /// CurrentViewModel에 따라 ContentControl의 Content를 업데이트
    /// Update ContentControl's Content based on CurrentViewModel
    /// </summary>
    private void UpdateContent()
    {
        var currentVm = _viewModel.CurrentViewModel;

        if (currentVm is TciaExplorerViewModel)
        {
            var template = (DataTemplate)Resources["TciaExplorerViewTemplate"];
            MainContent.ContentTemplate = template;
            MainContent.Content = currentVm;
        }
        else if (currentVm is ViewerViewModel)
        {
            var template = (DataTemplate)Resources["ViewerViewTemplate"];
            MainContent.ContentTemplate = template;
            MainContent.Content = currentVm;
        }
        else
        {
            MainContent.Content = null;
            MainContent.ContentTemplate = null;
        }
    }
}
