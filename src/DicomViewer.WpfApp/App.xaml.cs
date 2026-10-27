using System.IO;
using System.Windows;

using DicomViewer.Infrastructure.Datasets;

using Microsoft.Extensions.Configuration;

namespace DicomViewer.WpfApp;

/// <summary>
/// Application 진입점. 시작은 호스트를 시작한 뒤 창을 띄우고 고정 데이터셋을 연다. 종료는 창이 닫힌 뒤 호스트를 비동기로 멈추고 나서 Shutdown을 호출한다.
/// Application entry point. Startup starts the host, shows the window and opens the fixed dataset. Shutdown stops the host asynchronously
/// after the window has closed and only then calls Shutdown (ShutdownMode is OnExplicitShutdown), so no sync-over-async is needed.
/// </summary>
public partial class App : WpfApplication
{
    private static readonly TimeSpan HostStopTimeout = TimeSpan.FromSeconds(10);

    private readonly IHost _host;

    public App()
    {
        // 설정·콘텐츠 루트는 현재 작업 디렉터리가 아니라 실행 파일 위치 기준이다.
        // Configuration and content root are relative to the executable, not the current working directory.
        _host = Host.CreateDefaultBuilder()
            .UseContentRoot(AppContext.BaseDirectory)
            .ConfigureServices((context, services) => ConfigureServices(context.Configuration, services))
            .Build();
    }

    private static void ConfigureServices(IConfiguration configuration, IServiceCollection services)
    {
        // === 고정 로컬 데이터셋 위치 (AppContext.BaseDirectory 기준 상대 경로) ===
        // === Fixed local dataset location (relative to AppContext.BaseDirectory) ===
        var configuredPath = configuration["Dataset:ManifestPath"];
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            throw new InvalidOperationException("Configuration value 'Dataset:ManifestPath' is missing from appsettings.json.");
        }

        var manifestPath = Path.GetFullPath(configuredPath, AppContext.BaseDirectory);

        // === Infrastructure / Application ===
        services.AddSingleton<IDicomImageService, FoDicomImageService>();
        services.AddSingleton<ISeriesNavigationService, SeriesNavigationService>();
        services.AddSingleton<HistogramService>();
        services.AddSingleton<IDicomDatasetSource>(provider =>
            new ManifestDicomDatasetSource(provider.GetRequiredService<IDicomImageService>(), manifestPath));

        // === ViewModels (창 DataContext는 합성 루트인 OnStartup에서 연결한다) ===
        // === ViewModels (the window DataContext is attached in the composition root, OnStartup) ===
        services.AddSingleton<ViewerViewModel>();
        services.AddSingleton<MainWindowViewModel>();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            await _host.StartAsync();

            var viewModel = _host.Services.GetRequiredService<MainWindowViewModel>();
            var window = new MainWindow { DataContext = viewModel };
            window.Closed += OnMainWindowClosed;
            MainWindow = window;
            window.Show();

            // 열기·검증 오류는 ViewModel 상태(Error)로 표시되며 예외로 올라오지 않는다.
            // Open and verification failures surface as ViewModel state (Error), not as exceptions.
            await viewModel.StartAsync();
        }
        catch (Exception ex)
        {
            // OnExplicitShutdown에서는 창 없이 시작이 실패하면 프로세스가 남는다. 실패를 알리고 명시적으로 종료한다.
            // With OnExplicitShutdown a startup failure without a window would leave the process running: report it and shut down explicitly.
            MessageBox.Show(
                $"앱을 시작할 수 없습니다. / The application could not start.{Environment.NewLine}{ex.Message}",
                "DICOM Viewer",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private async void OnMainWindowClosed(object? sender, EventArgs e)
    {
        try
        {
            using var timeout = new CancellationTokenSource(HostStopTimeout);
            await _host.StopAsync(timeout.Token);

            // ViewerViewModel은 IAsyncDisposable만 구현하므로 동기 Dispose가 아니라 DisposeAsync로 컨테이너를 정리한다.
            // ViewerViewModel implements only IAsyncDisposable, so the container is disposed with DisposeAsync rather than Dispose.
            if (_host is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // 호스트 정지가 제한 시간을 넘겼다: 그래도 종료한다.
            // Host stop exceeded its time limit: shut down anyway.
        }
        finally
        {
            Shutdown();
        }
    }
}