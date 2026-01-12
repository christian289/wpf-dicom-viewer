using System.Windows;
using Microsoft.Extensions.Configuration;

namespace DicomViewer.WpfApp;

/// <summary>
/// Application 진입점
/// Application entry point
/// </summary>
public partial class App : System.Windows.Application
{
    private readonly IHost _host;

    public App()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((context, config) =>
            {
                config.SetBasePath(AppContext.BaseDirectory);
                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            })
            .ConfigureServices((context, services) =>
            {
                ConfigureServices(context.Configuration, services);
            })
            .Build();
    }

    private static void ConfigureServices(IConfiguration configuration, IServiceCollection services)
    {
        // === HTTP Client Factory ===
        services.AddHttpClient();

        // === Infrastructure Layer ===
        services.AddSingleton<IDicomImageService, FoDicomImageService>();
        services.AddSingleton<ISeriesNavigationService, SeriesNavigationService>();

        // TCIA 서비스
        // TCIA service
        services.AddSingleton<ITciaService, DicomViewer.Infrastructure.Tcia.TciaService>();

        // Rubo Medical 데이터 소스
        // Rubo Medical data source
        services.AddSingleton<IDicomDataSource, DicomViewer.Infrastructure.DataSources.RuboMedicalDataSource>();

        // PACS 서비스 (설정에 따라 선택)
        // PACS service (selected by configuration)
        services.AddSingleton<IPacsConnectionService>(sp =>
        {
            var pacsType = configuration.GetValue<string>("Pacs:Type") ?? "LocalFolder";
            var connectionString = pacsType switch
            {
                "Orthanc" => configuration.GetValue<string>("Pacs:OrthancUrl") ?? "http://localhost:8042",
                _ => configuration.GetValue<string>("Pacs:LocalPath") ?? @"C:\DICOM"
            };

            var factory = sp.GetRequiredService<IPacsConnectionFactory>();
            var type = pacsType == "Orthanc" ? PacsConnectionType.Orthanc : PacsConnectionType.LocalFolder;
            return factory.CreateService(type, connectionString);
        });

        services.AddSingleton<IPacsConnectionFactory, PacsConnectionFactory>();

        // === Application Layer ===
        services.AddSingleton<DicomStudyService>();
        services.AddSingleton<HistogramService>();
        services.AddSingleton<ThumbnailService>();

        // === WPF Services ===
        services.AddSingleton<DicomImageRenderService>();
        services.AddSingleton<IDialogService, DialogService>();

        // === ViewModels ===
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<StudyListViewModel>();
        services.AddSingleton<ViewerViewModel>();
        services.AddSingleton<TciaExplorerViewModel>();

        // === Views ===
        services.AddSingleton<MainWindow>();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await _host.StartAsync();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        using (_host)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(5));
        }

        base.OnExit(e);
    }
}
