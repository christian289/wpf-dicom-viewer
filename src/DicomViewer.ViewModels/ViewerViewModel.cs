using DicomViewer.ViewModels.Messages;

namespace DicomViewer.ViewModels;

/// <summary>
/// DICOM 뷰어 ViewModel
/// DICOM viewer ViewModel
/// </summary>
public sealed partial class ViewerViewModel : ObservableRecipient
{
    private readonly IDicomImageService _imageService;
    private readonly ISeriesNavigationService _navigationService;

    // 현재 이미지 데이터 (8비트 그레이스케일)
    // Current image data (8-bit grayscale)
    [ObservableProperty]
    private byte[]? _currentImagePixels;

    [ObservableProperty]
    private int _imageWidth;

    [ObservableProperty]
    private int _imageHeight;

    // Window/Level
    [ObservableProperty]
    private double _windowWidth = 400;

    [ObservableProperty]
    private double _windowCenter = 40;

    // 변환
    // Transforms
    [ObservableProperty]
    private double _zoomFactor = 1.0;

    [ObservableProperty]
    private double _panX;

    [ObservableProperty]
    private double _panY;

    // 네비게이션
    // Navigation
    [ObservableProperty]
    private int _currentSliceIndex;

    [ObservableProperty]
    private int _totalSliceCount;

    // 시리즈 목록
    // Series list
    [ObservableProperty]
    private ObservableCollection<DicomSeriesDto> _seriesList = [];

    [ObservableProperty]
    private DicomSeriesDto? _selectedSeries;

    // 현재 도구
    // Current tool
    [ObservableProperty]
    private ViewerTool _currentTool = ViewerTool.WindowLevel;

    // 환자/Study 정보
    // Patient/Study information
    [ObservableProperty]
    private string _patientName = string.Empty;

    [ObservableProperty]
    private string _patientId = string.Empty;

    [ObservableProperty]
    private string _studyDescription = string.Empty;

    [ObservableProperty]
    private string _seriesDescription = string.Empty;

    private PixelData? _currentPixelData;

    public ViewerViewModel(
        IDicomImageService imageService,
        ISeriesNavigationService navigationService)
    {
        _imageService = imageService;
        _navigationService = navigationService;
    }

    public void LoadStudy(DicomStudyDto study)
    {
        PatientName = study.PatientName;
        PatientId = study.PatientId;
        StudyDescription = study.StudyDescription ?? string.Empty;

        SeriesList = new ObservableCollection<DicomSeriesDto>(study.Series);

        if (SeriesList.Count > 0)
        {
            SelectedSeries = SeriesList[0];
        }
    }

    partial void OnSelectedSeriesChanged(DicomSeriesDto? value)
    {
        if (value is null) return;

        SeriesDescription = value.SeriesDescription ?? string.Empty;
        _navigationService.SetSeries(value.Instances);
        TotalSliceCount = _navigationService.TotalCount;
        CurrentSliceIndex = 0;

        // 첫 번째 이미지 로드
        // Load first image
        _ = LoadCurrentImageAsync();
    }

    partial void OnWindowWidthChanged(double value)
    {
        _ = RefreshImageAsync();
    }

    partial void OnWindowCenterChanged(double value)
    {
        _ = RefreshImageAsync();
    }

    [RelayCommand]
    private async Task LoadCurrentImageAsync()
    {
        var instance = _navigationService.GetCurrentInstance();
        if (instance is null) return;

        try
        {
            _currentPixelData = await _imageService.LoadPixelDataAsync(instance.FilePath);

            ImageWidth = _currentPixelData.Width;
            ImageHeight = _currentPixelData.Height;

            // Window/Level 기본값 설정 (첫 로드 시)
            // Set default Window/Level (on first load)
            if (CurrentSliceIndex == 0)
            {
                var defaultWL = _imageService.GetDefaultWindowLevel(instance.FilePath);
                WindowWidth = defaultWL.WindowWidth;
                WindowCenter = defaultWL.WindowCenter;
            }

            await RefreshImageAsync();
        }
        catch (Exception ex)
        {
            Messenger.Send(new StatusMessage($"이미지 로드 실패: {ex.Message} / Failed to load image: {ex.Message}", true));
        }
    }

    private async Task RefreshImageAsync()
    {
        if (_currentPixelData is null) return;

        await Task.Run(() =>
        {
            var windowLevel = new WindowLevel(WindowWidth, WindowCenter);
            CurrentImagePixels = _imageService.ApplyWindowLevel(_currentPixelData, windowLevel);
        });
    }

    [RelayCommand]
    private async Task NavigateToSliceAsync(int index)
    {
        _navigationService.NavigateToIndex(index);
        CurrentSliceIndex = _navigationService.CurrentIndex;
        await LoadCurrentImageAsync();
    }

    [RelayCommand]
    private async Task NextSliceAsync()
    {
        var instance = _navigationService.NavigateNext();
        if (instance is not null)
        {
            CurrentSliceIndex = _navigationService.CurrentIndex;
            await LoadCurrentImageAsync();
        }
    }

    [RelayCommand]
    private async Task PreviousSliceAsync()
    {
        var instance = _navigationService.NavigatePrevious();
        if (instance is not null)
        {
            CurrentSliceIndex = _navigationService.CurrentIndex;
            await LoadCurrentImageAsync();
        }
    }

    [RelayCommand]
    private async Task FirstSliceAsync()
    {
        var instance = _navigationService.NavigateFirst();
        if (instance is not null)
        {
            CurrentSliceIndex = _navigationService.CurrentIndex;
            await LoadCurrentImageAsync();
        }
    }

    [RelayCommand]
    private async Task LastSliceAsync()
    {
        var instance = _navigationService.NavigateLast();
        if (instance is not null)
        {
            CurrentSliceIndex = _navigationService.CurrentIndex;
            await LoadCurrentImageAsync();
        }
    }

    [RelayCommand]
    private void ApplyWindowLevelPreset(string presetName)
    {
        var preset = presetName switch
        {
            "Abdomen" => WindowLevel.CtAbdomen,
            "Lung" => WindowLevel.CtLung,
            "Bone" => WindowLevel.CtBone,
            "Brain" => WindowLevel.CtBrain,
            "Liver" => WindowLevel.CtLiver,
            "Mediastinum" => WindowLevel.CtMediastinum,
            "MR" => WindowLevel.MrDefault,
            _ => WindowLevel.Default
        };

        WindowWidth = preset.WindowWidth;
        WindowCenter = preset.WindowCenter;
    }

    [RelayCommand]
    private void ResetView()
    {
        ZoomFactor = 1.0;
        PanX = 0;
        PanY = 0;
    }

    [RelayCommand]
    private void ZoomIn()
    {
        ZoomFactor = Math.Min(ZoomFactor * 1.2, 10.0);
    }

    [RelayCommand]
    private void ZoomOut()
    {
        ZoomFactor = Math.Max(ZoomFactor / 1.2, 0.1);
    }

    [RelayCommand]
    private void FitToWindow()
    {
        // 화면에 맞게 줌 조정 (View에서 계산 필요)
        // Adjust zoom to fit window (calculation needed in View)
        ZoomFactor = 1.0;
        PanX = 0;
        PanY = 0;
    }

    [RelayCommand]
    private void SelectTool(ViewerTool tool)
    {
        CurrentTool = tool;
    }

    public void UpdateWindowLevel(double deltaWidth, double deltaCenter)
    {
        WindowWidth = Math.Max(1, WindowWidth + deltaWidth);
        WindowCenter = WindowCenter + deltaCenter;
    }

    public void UpdatePan(double deltaX, double deltaY)
    {
        PanX += deltaX;
        PanY += deltaY;
    }

    public void UpdateZoom(double factor)
    {
        ZoomFactor = Math.Clamp(ZoomFactor * factor, 0.1, 10.0);
    }
}

/// <summary>
/// 뷰어 도구 열거형
/// Viewer tool enumeration
/// </summary>
public enum ViewerTool
{
    None,
    Pan,
    Zoom,
    WindowLevel
}
