namespace DicomViewer.OpenSilverApp.ViewModels;

/// <summary>
/// 메인 뷰모델
/// Main view model
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly ITciaService _tciaService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private ObservableCollection<TciaCollectionDto> _collections = new();

    [ObservableProperty]
    private TciaCollectionDto? _selectedCollection;

    [ObservableProperty]
    private ObservableCollection<TciaPatientDto> _patients = new();

    [ObservableProperty]
    private TciaPatientDto? _selectedPatient;

    [ObservableProperty]
    private ObservableCollection<TciaStudyDto> _studies = new();

    [ObservableProperty]
    private TciaStudyDto? _selectedStudy;

    [ObservableProperty]
    private ObservableCollection<TciaSeriesDto> _seriesList = new();

    [ObservableProperty]
    private TciaSeriesDto? _selectedSeries;

    [ObservableProperty]
    private double _downloadProgress;

    [ObservableProperty]
    private string _downloadStatus = "";

    // 뷰어 상태
    // Viewer state
    [ObservableProperty]
    private byte[]? _currentImagePixels;

    [ObservableProperty]
    private int _imageWidth;

    [ObservableProperty]
    private int _imageHeight;

    [ObservableProperty]
    private double _windowWidth = 400;

    [ObservableProperty]
    private double _windowCenter = 40;

    [ObservableProperty]
    private double _zoomFactor = 1.0;

    [ObservableProperty]
    private double _panX;

    [ObservableProperty]
    private double _panY;

    [ObservableProperty]
    private double _rotationAngle;

    [ObservableProperty]
    private bool _flipHorizontal;

    [ObservableProperty]
    private bool _flipVertical;

    public MainViewModel(ITciaService tciaService, IDialogService dialogService)
    {
        _tciaService = tciaService;
        _dialogService = dialogService;
    }

    [RelayCommand]
    private async Task LoadCollectionsAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading collections...";

            var collections = await _tciaService.GetCollectionsAsync();
            Collections = new ObservableCollection<TciaCollectionDto>(collections);

            StatusMessage = $"Loaded {collections.Count} collections";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            _dialogService.ShowError("Error", ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedCollectionChanged(TciaCollectionDto? value)
    {
        if (value != null)
        {
            _ = LoadPatientsAsync(value.Collection);
        }
    }

    private async Task LoadPatientsAsync(string collection)
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading patients...";

            var patients = await _tciaService.GetPatientsAsync(collection);
            Patients = new ObservableCollection<TciaPatientDto>(patients);
            Studies.Clear();
            SeriesList.Clear();

            StatusMessage = $"Loaded {patients.Count} patients";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedPatientChanged(TciaPatientDto? value)
    {
        if (value != null && SelectedCollection != null)
        {
            _ = LoadStudiesAsync(SelectedCollection.Collection, value.PatientId);
        }
    }

    private async Task LoadStudiesAsync(string collection, string patientId)
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading studies...";

            var studies = await _tciaService.GetPatientStudiesAsync(collection, patientId);
            Studies = new ObservableCollection<TciaStudyDto>(studies);
            SeriesList.Clear();

            StatusMessage = $"Loaded {studies.Count} studies";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedStudyChanged(TciaStudyDto? value)
    {
        if (value != null)
        {
            _ = LoadSeriesAsync(value.StudyInstanceUID);
        }
    }

    private async Task LoadSeriesAsync(string studyInstanceUid)
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading series...";

            var series = await _tciaService.GetSeriesAsync(studyInstanceUid);

            // Non-Image Modality 필터링
            // Filter non-image modalities
            var filteredSeries = series.Where(s => !IsNonImageModality(s.Modality)).ToList();

            SeriesList = new ObservableCollection<TciaSeriesDto>(filteredSeries);

            StatusMessage = $"Loaded {filteredSeries.Count} series (filtered from {series.Count})";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static readonly HashSet<string> NonImageModalities = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "RTSTRUCT", "RTPLAN", "RTDOSE", "SR", "PR", "SEG", "KO", "REG", "FID"
    };

    private static bool IsNonImageModality(string? modality)
    {
        return !string.IsNullOrEmpty(modality) && NonImageModalities.Contains(modality);
    }

    [RelayCommand]
    private async Task DownloadSeriesAsync()
    {
        if (SelectedSeries == null)
        {
            _dialogService.ShowMessage("Info", "Please select a series to download.");
            return;
        }

        try
        {
            IsLoading = true;
            DownloadProgress = 0;
            DownloadStatus = "Starting download...";

            var progress = new Progress<DownloadProgressInfo>(info =>
            {
                DownloadProgress = info.ProgressPercent;
                DownloadStatus = $"Downloaded: {info.BytesDownloaded / 1024 / 1024:F1} MB";
            });

            var zipData = await _tciaService.DownloadSeriesAsync(
                SelectedSeries.SeriesInstanceUID,
                progress);

            DownloadStatus = $"Download complete: {zipData.Length / 1024 / 1024:F1} MB";
            StatusMessage = "Series downloaded successfully";

            // TODO: ZIP 해제 및 DICOM 파일 로드
            // TODO: Extract ZIP and load DICOM files
            _dialogService.ShowMessage("Success", $"Downloaded {zipData.Length / 1024 / 1024:F1} MB");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Download failed: {ex.Message}";
            _dialogService.ShowError("Download Error", ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Testing connection...";

            var result = await _tciaService.TestConnectionAsync();

            if (result)
            {
                StatusMessage = "Connection successful";
                _dialogService.ShowMessage("Success", "TCIA connection successful!");
            }
            else
            {
                StatusMessage = "Connection failed";
                _dialogService.ShowError("Error", "Failed to connect to TCIA server.");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Connection error: {ex.Message}";
            _dialogService.ShowError("Connection Error", ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ResetView()
    {
        ZoomFactor = 1.0;
        PanX = 0;
        PanY = 0;
        RotationAngle = 0;
        FlipHorizontal = false;
        FlipVertical = false;
    }

    [RelayCommand]
    private void RotateLeft()
    {
        RotationAngle -= 90;
        if (RotationAngle < 0) RotationAngle += 360;
    }

    [RelayCommand]
    private void RotateRight()
    {
        RotationAngle += 90;
        if (RotationAngle >= 360) RotationAngle -= 360;
    }
}
