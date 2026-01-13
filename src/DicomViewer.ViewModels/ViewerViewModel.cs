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

    // 회전 각도 (도)
    // Rotation angle (degrees)
    [ObservableProperty]
    private double _rotationAngle;

    // 수평 반전
    // Horizontal flip
    [ObservableProperty]
    private bool _flipHorizontal;

    // 수직 반전
    // Vertical flip
    [ObservableProperty]
    private bool _flipVertical;

    // 네비게이션
    // Navigation
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SliceDisplayNumber))]
    private int _currentSliceIndex;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MaxSliceIndex))]
    private int _totalSliceCount;

    /// <summary>
    /// 슬라이더 Maximum 값 (0-based 인덱스 최대값)
    /// Slider maximum value (0-based index maximum)
    /// </summary>
    public int MaxSliceIndex => Math.Max(0, TotalSliceCount - 1);

    /// <summary>
    /// 사용자 표시용 슬라이스 번호 (1-based)
    /// Slice number for user display (1-based)
    /// </summary>
    public int SliceDisplayNumber => CurrentSliceIndex + 1;

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

    // 로딩 상태
    // Loading state
    [ObservableProperty]
    private bool _isLoading;

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

    // PixelSpacing (측정용)
    // PixelSpacing (for measurements)
    [ObservableProperty]
    private double _pixelSpacingX = 1.0;

    [ObservableProperty]
    private double _pixelSpacingY = 1.0;

    // 히스토그램 데이터
    // Histogram data
    [ObservableProperty]
    private int[]? _histogramData;

    // 히스토그램 표시 여부
    // Show histogram
    [ObservableProperty]
    private bool _showHistogram = true;

    // 측정 모드 여부
    // Measuring mode
    [ObservableProperty]
    private bool _isMeasuring;

    // 측정 시작점
    // Measurement start point
    [ObservableProperty]
    private double _measureStartX;

    [ObservableProperty]
    private double _measureStartY;

    // 측정 끝점
    // Measurement end point
    [ObservableProperty]
    private double _measureEndX;

    [ObservableProperty]
    private double _measureEndY;

    private PixelData? _currentPixelData;

    public ViewerViewModel(
        IDicomImageService imageService,
        ISeriesNavigationService navigationService)
    {
        _imageService = imageService;
        _navigationService = navigationService;

        // Messenger 활성화
        // Enable Messenger
        IsActive = true;
    }

    /// <summary>
    /// 폴더에서 DICOM 파일 로드
    /// Load DICOM files from folder
    /// </summary>
    public void LoadFromFolder(string folderPath)
    {
        if (!Directory.Exists(folderPath)) return;

        // DICOM 파일 검색 - 모든 파일 검색 후 디렉토리 제외
        // Search for DICOM files - get all files excluding directories
        var allFiles = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories)
            .Where(f => !f.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            .Where(f => !f.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            .Where(f => !f.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            .Where(f => !f.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        // .dcm 파일 우선, 없으면 모든 파일 사용
        // Prefer .dcm files, otherwise use all files
        var dcmFiles = allFiles.Where(f => f.EndsWith(".dcm", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (dcmFiles.Length == 0)
        {
            dcmFiles = allFiles;
        }

        if (dcmFiles.Length == 0) return;

        // 유효한 DICOM 이미지 파일만 필터링 (픽셀 데이터가 있는 파일)
        // Filter only valid DICOM image files (files with pixel data)
        var validDicomFiles = dcmFiles.Where(f => _imageService.IsValidDicomImage(f)).ToArray();

        if (validDicomFiles.Length == 0)
        {
            Messenger.Send(new StatusMessage("유효한 DICOM 이미지 파일을 찾을 수 없습니다. / No valid DICOM image files found.", true));
            return;
        }

        // 인스턴스 생성
        // Create instances
        var instances = validDicomFiles.Select((f, i) => new DicomInstanceDto(
            SopInstanceUid: Path.GetFileNameWithoutExtension(f),
            InstanceNumber: i + 1,
            FilePath: f,
            Rows: 512,
            Columns: 512,
            SliceLocation: null)).ToList();

        // 시리즈 생성
        // Create series
        var series = new DicomSeriesDto(
            SeriesInstanceUid: Path.GetFileName(folderPath),
            SeriesNumber: 1,
            Modality: "TCIA",
            SeriesDescription: "TCIA Downloaded Series",
            NumberOfInstances: instances.Count,
            Instances: instances);

        PatientName = "TCIA Patient";
        PatientId = "TCIA";
        StudyDescription = $"TCIA Downloaded Study ({instances.Count} images)";

        SeriesList = [series];
        SelectedSeries = series;

        // 상태 메시지 전송 (스킵된 파일 수 포함)
        // Send status message (including skipped file count)
        var skippedCount = dcmFiles.Length - validDicomFiles.Length;
        var message = skippedCount > 0
            ? $"{instances.Count}개 이미지 로드됨 ({skippedCount}개 파일 스킵) / {instances.Count} images loaded ({skippedCount} files skipped)"
            : $"{instances.Count}개 이미지 로드됨 / {instances.Count} images loaded";
        Messenger.Send(new StatusMessage(message, false));
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

    partial void OnCurrentSliceIndexChanged(int oldValue, int newValue)
    {
        // 네비게이션 서비스의 인덱스와 다른 경우에만 이미지 로드 (슬라이더 직접 조작 시)
        // Only load image when different from navigation service index (slider direct manipulation)
        if (_navigationService.CurrentIndex != newValue)
        {
            _navigationService.NavigateToIndex(newValue);
            _ = LoadCurrentImageAsync();
        }
    }

    [RelayCommand]
    private async Task LoadCurrentImageAsync()
    {
        var instance = _navigationService.GetCurrentInstance();
        if (instance is null) return;

        IsLoading = true;

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

            // PixelSpacing 로드
            // Load PixelSpacing
            var pixelSpacing = _imageService.GetPixelSpacing(instance.FilePath);
            PixelSpacingX = pixelSpacing.X;
            PixelSpacingY = pixelSpacing.Y;

            await RefreshImageAsync();
        }
        catch (Exception ex)
        {
            Messenger.Send(new StatusMessage($"이미지 로드 실패: {ex.Message} / Failed to load image: {ex.Message}", true));
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task RefreshImageAsync()
    {
        if (_currentPixelData is null) return;

        await Task.Run(() =>
        {
            var windowLevel = new WindowLevel(WindowWidth, WindowCenter);
            var pixels = _imageService.ApplyWindowLevel(_currentPixelData, windowLevel);
            CurrentImagePixels = pixels;

            // 히스토그램 계산
            // Calculate histogram
            if (ShowHistogram && pixels is not null)
            {
                var bins = new int[256];
                foreach (var pixel in pixels)
                {
                    bins[pixel]++;
                }
                HistogramData = bins;
            }
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
        RotationAngle = 0;
        FlipHorizontal = false;
        FlipVertical = false;
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

    /// <summary>
    /// 왼쪽으로 90도 회전
    /// Rotate 90 degrees left
    /// </summary>
    [RelayCommand]
    private void RotateLeft()
    {
        RotationAngle = (RotationAngle - 90 + 360) % 360;
    }

    /// <summary>
    /// 오른쪽으로 90도 회전
    /// Rotate 90 degrees right
    /// </summary>
    [RelayCommand]
    private void RotateRight()
    {
        RotationAngle = (RotationAngle + 90) % 360;
    }

    /// <summary>
    /// 수평 반전 토글
    /// Toggle horizontal flip
    /// </summary>
    [RelayCommand]
    private void ToggleFlipHorizontal()
    {
        FlipHorizontal = !FlipHorizontal;
    }

    /// <summary>
    /// 수직 반전 토글
    /// Toggle vertical flip
    /// </summary>
    [RelayCommand]
    private void ToggleFlipVertical()
    {
        FlipVertical = !FlipVertical;
    }

    /// <summary>
    /// 히스토그램 표시 토글
    /// Toggle histogram visibility
    /// </summary>
    [RelayCommand]
    private void ToggleHistogram()
    {
        ShowHistogram = !ShowHistogram;
    }

    /// <summary>
    /// 현재 측정 거리 계산 (mm)
    /// Calculate current measurement distance (mm)
    /// </summary>
    public double CurrentMeasurementDistanceMm
    {
        get
        {
            var dx = (MeasureEndX - MeasureStartX) * PixelSpacingX;
            var dy = (MeasureEndY - MeasureStartY) * PixelSpacingY;
            return Math.Sqrt(dx * dx + dy * dy);
        }
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
    WindowLevel,
    Rotate,
    Measure
}
