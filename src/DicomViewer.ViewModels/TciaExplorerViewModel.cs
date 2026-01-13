using DicomViewer.ViewModels.Messages;

namespace DicomViewer.ViewModels;

/// <summary>
/// TCIA 탐색기 ViewModel
/// TCIA Explorer ViewModel
/// </summary>
public sealed partial class TciaExplorerViewModel : ObservableRecipient
{
    private readonly ITciaService _tciaService;
    private readonly DicomStudyService _studyService;

    // 컬렉션 목록
    // Collection list
    [ObservableProperty]
    private ObservableCollection<TciaCollectionDto> _collections = [];

    [ObservableProperty]
    private TciaCollectionDto? _selectedCollection;

    // 환자 목록
    // Patient list
    [ObservableProperty]
    private ObservableCollection<TciaPatientDto> _patients = [];

    [ObservableProperty]
    private TciaPatientDto? _selectedPatient;

    // Study 목록
    // Study list
    [ObservableProperty]
    private ObservableCollection<TciaStudyDto> _studies = [];

    [ObservableProperty]
    private TciaStudyDto? _selectedStudy;

    // Series 목록
    // Series list
    [ObservableProperty]
    private ObservableCollection<TciaSeriesDto> _seriesList = [];

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
    // Search filters
    [ObservableProperty]
    private string _searchKeyword = string.Empty;

    // 캐시 정보
    // Cache information
    [ObservableProperty]
    private string _cacheSize = "0 B";

    private static string CacheFolderPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DicomViewer",
        "TciaCache");

    public TciaExplorerViewModel(
        ITciaService tciaService,
        DicomStudyService studyService)
    {
        _tciaService = tciaService;
        _studyService = studyService;

        // Messenger 활성화
        // Enable Messenger
        IsActive = true;

        // 캐시 크기 초기화
        // Initialize cache size
        UpdateCacheSize();
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
        }
        catch (Exception ex)
        {
            StatusMessage = $"컬렉션 로드 실패: {ex.Message} / Failed to load collections: {ex.Message}";
            Messenger.Send(new StatusMessage(StatusMessage, true));
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
        }
        catch (Exception ex)
        {
            StatusMessage = $"환자 목록 로드 실패: {ex.Message} / Failed to load patients: {ex.Message}";
            Messenger.Send(new StatusMessage(StatusMessage, true));
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
        }
        catch (Exception ex)
        {
            StatusMessage = $"Study 목록 로드 실패: {ex.Message} / Failed to load studies: {ex.Message}";
            Messenger.Send(new StatusMessage(StatusMessage, true));
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

    /// <summary>
    /// 이미지 픽셀 데이터가 없는 Modality 목록 (뷰어에서 표시 불가)
    /// Modalities without image pixel data (cannot be displayed in viewer)
    /// </summary>
    private static readonly HashSet<string> NonImageModalities = new(StringComparer.OrdinalIgnoreCase)
    {
        "RTSTRUCT",  // Radiotherapy Structure Set (방사선 치료 구조)
        "RTPLAN",    // Radiotherapy Plan (방사선 치료 계획)
        "RTDOSE",    // Radiotherapy Dose (방사선 선량)
        "SR",        // Structured Report (구조화된 보고서)
        "PR",        // Presentation State (프레젠테이션 상태)
        "SEG",       // Segmentation (분할)
        "KO",        // Key Object Selection (키 객체 선택)
        "REG",       // Registration (정합)
        "FID",       // Fiducials (기준점)
    };

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
        }
        catch (Exception ex)
        {
            StatusMessage = $"Series 목록 로드 실패: {ex.Message} / Failed to load series: {ex.Message}";
            Messenger.Send(new StatusMessage(StatusMessage, true));
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task DownloadAndOpenSeriesAsync()
    {
        if (SelectedSeries is null) return;

        IsDownloading = true;
        DownloadProgress = 0;
        StatusMessage = "Series를 다운로드하는 중... / Downloading series...";

        try
        {
            var progress = new Progress<DownloadProgressInfo>(info =>
            {
                DownloadProgress = info.ProgressPercent;

                // 남은 시간 계산
                // Calculate remaining time
                var etaText = info.EstimatedTimeRemaining.TotalSeconds > 0
                    ? $"ETA: {FormatTimeSpan(info.EstimatedTimeRemaining)}"
                    : "";

                StatusMessage = $"다운로드 중... {info.DownloadedFormatted} / {info.TotalFormatted} ({info.ProgressPercent:F0}%) - {info.SpeedFormatted} {etaText}";
            });

            var seriesPath = await _tciaService.DownloadSeriesAsync(
                SelectedSeries.SeriesInstanceUID,
                CacheFolderPath,
                progress);

            // 캐시 크기 업데이트
            // Update cache size
            UpdateCacheSize();

            StatusMessage = "다운로드 완료. 이미지를 로드합니다... / Download complete. Loading images...";

            // 다운로드된 폴더에서 Study 로드
            // Load study from downloaded folder
            Messenger.Send(new OpenFolderMessage(seriesPath));

            StatusMessage = "Series 로드 완료! / Series loaded!";
        }
        catch (Exception ex)
        {
            StatusMessage = $"다운로드 실패: {ex.Message} / Download failed: {ex.Message}";
            Messenger.Send(new StatusMessage(StatusMessage, true));
        }
        finally
        {
            IsDownloading = false;
            DownloadProgress = 0;
        }
    }

    private static string FormatTimeSpan(TimeSpan timeSpan)
    {
        if (timeSpan.TotalHours >= 1)
        {
            return $"{(int)timeSpan.TotalHours}h {timeSpan.Minutes}m";
        }
        if (timeSpan.TotalMinutes >= 1)
        {
            return $"{(int)timeSpan.TotalMinutes}m {timeSpan.Seconds}s";
        }
        return $"{timeSpan.Seconds}s";
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

            Messenger.Send(new StatusMessage(StatusMessage, !isConnected));
        }
        catch (Exception ex)
        {
            StatusMessage = $"연결 테스트 실패: {ex.Message} / Connection test failed: {ex.Message}";
            Messenger.Send(new StatusMessage(StatusMessage, true));
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ClearCacheAsync()
    {
        StatusMessage = "캐시를 삭제하는 중... / Clearing cache...";

        try
        {
            await Task.Run(() =>
            {
                if (Directory.Exists(CacheFolderPath))
                {
                    Directory.Delete(CacheFolderPath, true);
                }
            });

            UpdateCacheSize();
            StatusMessage = "캐시가 삭제되었습니다. / Cache cleared.";
            Messenger.Send(new StatusMessage(StatusMessage, false));
        }
        catch (Exception ex)
        {
            StatusMessage = $"캐시 삭제 실패: {ex.Message} / Failed to clear cache: {ex.Message}";
            Messenger.Send(new StatusMessage(StatusMessage, true));
        }
    }

    [RelayCommand]
    private void OpenCacheFolder()
    {
        try
        {
            if (!Directory.Exists(CacheFolderPath))
            {
                Directory.CreateDirectory(CacheFolderPath);
            }

            // 파일 탐색기에서 캐시 폴더 열기
            // Open cache folder in file explorer
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = CacheFolderPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"폴더 열기 실패: {ex.Message} / Failed to open folder: {ex.Message}";
            Messenger.Send(new StatusMessage(StatusMessage, true));
        }
    }

    [RelayCommand]
    private void UpdateCacheSize()
    {
        try
        {
            if (!Directory.Exists(CacheFolderPath))
            {
                CacheSize = "0 B";
                return;
            }

            var totalSize = Directory.GetFiles(CacheFolderPath, "*", SearchOption.AllDirectories)
                .Sum(f => new FileInfo(f).Length);

            CacheSize = FormatFileSize(totalSize);
        }
        catch
        {
            CacheSize = "알 수 없음 / Unknown";
        }
    }

    private static string FormatFileSize(long bytes)
    {
        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
        int suffixIndex = 0;
        double size = bytes;

        while (size >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            size /= 1024;
            suffixIndex++;
        }

        return $"{size:F2} {suffixes[suffixIndex]}";
    }
}
