namespace DicomViewer.OpenSilverApp.ViewModels;

/// <summary>
/// TCIA 탐색기 ViewModel
/// TCIA Explorer ViewModel
/// </summary>
public sealed partial class TciaExplorerViewModel : ObservableObject
{
    private readonly ITciaService _tciaService;
    private readonly IDialogService _dialogService;
    private readonly MainWindowViewModel _mainViewModel;

    // 컬렉션 목록
    // Collection list
    [ObservableProperty]
    private ObservableCollection<TciaCollectionDto> _collections = new();

    [ObservableProperty]
    private TciaCollectionDto? _selectedCollection;

    // 환자 목록
    // Patient list
    [ObservableProperty]
    private ObservableCollection<TciaPatientDto> _patients = new();

    [ObservableProperty]
    private TciaPatientDto? _selectedPatient;

    // Study 목록
    // Study list
    [ObservableProperty]
    private ObservableCollection<TciaStudyDto> _studies = new();

    [ObservableProperty]
    private TciaStudyDto? _selectedStudy;

    // Series 목록
    // Series list
    [ObservableProperty]
    private ObservableCollection<TciaSeriesDto> _seriesList = new();

    [ObservableProperty]
    private TciaSeriesDto? _selectedSeries;

    // 로딩 상태
    // Loading state
    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    // 다운로드 진행률
    // Download progress
    [ObservableProperty]
    private double _downloadProgress;

    [ObservableProperty]
    private bool _isDownloading;

    // 검색 필터
    // Search filter
    [ObservableProperty]
    private string _searchKeyword = string.Empty;

    /// <summary>
    /// 이미지 픽셀 데이터가 없는 Modality 목록 (뷰어에서 표시 불가)
    /// Modalities without image pixel data (cannot be displayed in viewer)
    /// </summary>
    private static readonly HashSet<string> NonImageModalities = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "RTSTRUCT",  // Radiotherapy Structure Set
        "RTPLAN",    // Radiotherapy Plan
        "RTDOSE",    // Radiotherapy Dose
        "SR",        // Structured Report
        "PR",        // Presentation State
        "SEG",       // Segmentation
        "KO",        // Key Object Selection
        "REG",       // Registration
        "FID",       // Fiducials
    };

    public TciaExplorerViewModel(
        ITciaService tciaService,
        IDialogService dialogService,
        MainWindowViewModel mainViewModel)
    {
        _tciaService = tciaService;
        _dialogService = dialogService;
        _mainViewModel = mainViewModel;
    }

    [RelayCommand]
    private async Task LoadCollectionsAsync()
    {
        IsLoading = true;
        StatusMessage = "컬렉션 목록을 불러오는 중... / Loading collections...";

        try
        {
            var collections = await _tciaService.GetCollectionsAsync();
            Collections = new ObservableCollection<TciaCollectionDto>(collections);
            StatusMessage = $"{collections.Count}개 컬렉션을 찾았습니다. / Found {collections.Count} collections.";
            _mainViewModel.SetStatus(StatusMessage);
        }
        catch (Exception ex)
        {
            StatusMessage = $"컬렉션 로드 실패: {ex.Message} / Failed to load collections: {ex.Message}";
            _mainViewModel.SetStatus(StatusMessage, true);
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedCollectionChanged(TciaCollectionDto? value)
    {
        if (value is null) return;

        _ = LoadPatientsAsync(value.Collection);
    }

    private async Task LoadPatientsAsync(string collection)
    {
        IsLoading = true;
        Patients.Clear();
        Studies.Clear();
        SeriesList.Clear();
        StatusMessage = "환자 목록을 불러오는 중... / Loading patients...";

        try
        {
            var patients = await _tciaService.GetPatientsAsync(collection);
            Patients = new ObservableCollection<TciaPatientDto>(patients);
            StatusMessage = $"{patients.Count}명의 환자를 찾았습니다. / Found {patients.Count} patients.";
            _mainViewModel.SetStatus(StatusMessage);
        }
        catch (Exception ex)
        {
            StatusMessage = $"환자 목록 로드 실패: {ex.Message} / Failed to load patients: {ex.Message}";
            _mainViewModel.SetStatus(StatusMessage, true);
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedPatientChanged(TciaPatientDto? value)
    {
        if (value is null || SelectedCollection is null) return;

        _ = LoadStudiesAsync(SelectedCollection.Collection, value.PatientId);
    }

    private async Task LoadStudiesAsync(string collection, string patientId)
    {
        IsLoading = true;
        Studies.Clear();
        SeriesList.Clear();
        StatusMessage = "Study 목록을 불러오는 중... / Loading studies...";

        try
        {
            var studies = await _tciaService.GetPatientStudiesAsync(collection, patientId);
            Studies = new ObservableCollection<TciaStudyDto>(studies);
            StatusMessage = $"{studies.Count}개의 Study를 찾았습니다. / Found {studies.Count} studies.";
            _mainViewModel.SetStatus(StatusMessage);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Study 목록 로드 실패: {ex.Message} / Failed to load studies: {ex.Message}";
            _mainViewModel.SetStatus(StatusMessage, true);
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedStudyChanged(TciaStudyDto? value)
    {
        if (value is null) return;

        _ = LoadSeriesAsync(value.StudyInstanceUID);
    }

    private async Task LoadSeriesAsync(string studyInstanceUid)
    {
        IsLoading = true;
        SeriesList.Clear();
        StatusMessage = "Series 목록을 불러오는 중... / Loading series...";

        try
        {
            var allSeries = await _tciaService.GetSeriesAsync(studyInstanceUid);

            // 이미지가 없는 Modality 필터링
            // Filter out modalities without image data
            var imageSeriesList = allSeries
                .Where(s => string.IsNullOrEmpty(s.Modality) || !NonImageModalities.Contains(s.Modality))
                .ToList();

            var filteredCount = allSeries.Count - imageSeriesList.Count;

            SeriesList = new ObservableCollection<TciaSeriesDto>(imageSeriesList);

            if (filteredCount > 0)
            {
                StatusMessage = $"{imageSeriesList.Count}개의 Series를 찾았습니다. ({filteredCount}개 비이미지 제외) / Found {imageSeriesList.Count} series. ({filteredCount} non-image excluded)";
            }
            else
            {
                StatusMessage = $"{imageSeriesList.Count}개의 Series를 찾았습니다. / Found {imageSeriesList.Count} series.";
            }
            _mainViewModel.SetStatus(StatusMessage);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Series 목록 로드 실패: {ex.Message} / Failed to load series: {ex.Message}";
            _mainViewModel.SetStatus(StatusMessage, true);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task DownloadAndOpenSeriesAsync()
    {
        if (SelectedSeries is null)
        {
            _dialogService.ShowMessage("Info", "Please select a series to download.");
            return;
        }

        IsDownloading = true;
        DownloadProgress = 0;
        StatusMessage = "Series를 다운로드하는 중... / Downloading series...";

        try
        {
            var progress = new Progress<DownloadProgressInfo>(info =>
            {
                DownloadProgress = info.ProgressPercent;
                StatusMessage = $"다운로드 중... {info.BytesDownloaded / 1024 / 1024:F1} MB ({info.ProgressPercent:F0}%)";
            });

            var zipData = await _tciaService.DownloadSeriesAsync(
                SelectedSeries.SeriesInstanceUID,
                progress);

            StatusMessage = $"다운로드 완료: {zipData.Length / 1024 / 1024:F1} MB / Download complete";
            _mainViewModel.SetStatus(StatusMessage);

            // 뷰어에서 열기
            // Open in viewer
            _mainViewModel.OpenInViewer(zipData);
        }
        catch (Exception ex)
        {
            StatusMessage = $"다운로드 실패: {ex.Message} / Download failed: {ex.Message}";
            _mainViewModel.SetStatus(StatusMessage, true);
            _dialogService.ShowError("Download Error", ex.Message);
        }
        finally
        {
            IsDownloading = false;
            DownloadProgress = 0;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadCollectionsAsync();
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        IsLoading = true;
        StatusMessage = "TCIA 연결 테스트 중... / Testing TCIA connection...";

        try
        {
            var isConnected = await _tciaService.TestConnectionAsync();
            StatusMessage = isConnected
                ? "TCIA 연결 성공! / TCIA connection successful!"
                : "TCIA 연결 실패. / TCIA connection failed.";

            _mainViewModel.SetStatus(StatusMessage, !isConnected);

            if (isConnected)
            {
                _dialogService.ShowMessage("Success", "TCIA connection successful!");
            }
            else
            {
                _dialogService.ShowError("Error", "Failed to connect to TCIA server.");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"연결 테스트 실패: {ex.Message} / Connection test failed: {ex.Message}";
            _mainViewModel.SetStatus(StatusMessage, true);
            _dialogService.ShowError("Connection Error", ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }
}
