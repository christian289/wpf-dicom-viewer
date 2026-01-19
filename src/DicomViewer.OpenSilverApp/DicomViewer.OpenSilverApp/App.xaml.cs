using DicomViewer.OpenSilverApp.ViewModels;
using DicomViewer.OpenSilverServices.Interfaces;
using DicomViewer.OpenSilverServices.Services;
using System.Net.Http;
using System.Windows;

namespace DicomViewer.OpenSilverApp;

public sealed partial class App : Application
{
    // 서비스 인스턴스 (간단한 DI)
    // Service instances (simple DI)
    public static HttpClient HttpClient { get; } = new HttpClient();
    public static ITciaService TciaService { get; } = new TciaService(HttpClient);
    public static IDialogService DialogService { get; } = new WebDialogService();
    public static IImageRenderService ImageRenderService { get; } = new OpenSilverImageRenderService();

    // 메인 ViewModel
    // Main ViewModel
    public static MainWindowViewModel MainViewModel { get; } = new MainWindowViewModel(TciaService, DialogService);

    public App()
    {
        this.InitializeComponent();

        // 메인 페이지로 이동
        // Navigate to main page
        var mainPage = new MainPage();
        Window.Current.Content = mainPage;
    }
}
