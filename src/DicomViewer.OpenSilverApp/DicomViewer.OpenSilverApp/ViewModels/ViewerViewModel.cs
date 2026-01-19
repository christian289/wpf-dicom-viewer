namespace DicomViewer.OpenSilverApp.ViewModels;

/// <summary>
/// DICOM 뷰어 ViewModel
/// DICOM viewer ViewModel
/// </summary>
public sealed partial class ViewerViewModel : ObservableObject
{
    private readonly IDialogService _dialogService;
    private readonly MainWindowViewModel _mainViewModel;

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

    // 다운로드된 ZIP 데이터
    // Downloaded ZIP data
    private byte[]? _downloadedZipData;

    // 메시지
    // Message
    [ObservableProperty]
    private string _viewerMessage = "Select a series from TCIA Explorer and download to view DICOM images";

    public ViewerViewModel(
        IDialogService dialogService,
        MainWindowViewModel mainViewModel)
    {
        _dialogService = dialogService;
        _mainViewModel = mainViewModel;
    }

    /// <summary>
    /// ZIP 데이터에서 로드
    /// Load from ZIP data
    /// </summary>
    public void LoadFromZipData(byte[] zipData)
    {
        _downloadedZipData = zipData;

        // TODO: ZIP 해제 및 DICOM 파일 파싱 구현 필요
        // TODO: Need to implement ZIP extraction and DICOM file parsing

        PatientName = "TCIA Patient";
        PatientId = "TCIA";
        StudyDescription = $"Downloaded Series ({zipData.Length / 1024 / 1024:F1} MB)";
        SeriesDescription = "TCIA Downloaded Series";

        // 플레이스홀더 이미지 생성 (512x512 그라데이션)
        // Create placeholder image (512x512 gradient)
        ImageWidth = 512;
        ImageHeight = 512;
        TotalSliceCount = 1;
        CurrentSliceIndex = 0;

        var pixels = new byte[512 * 512];
        for (int y = 0; y < 512; y++)
        {
            for (int x = 0; x < 512; x++)
            {
                pixels[y * 512 + x] = (byte)((x + y) / 4);
            }
        }
        CurrentImagePixels = pixels;

        // 히스토그램 계산
        // Calculate histogram
        CalculateHistogram(pixels);

        ViewerMessage = $"ZIP downloaded ({zipData.Length / 1024 / 1024:F1} MB). DICOM parsing not yet implemented in WebAssembly.";
        _mainViewModel.SetStatus(ViewerMessage);
    }

    private void CalculateHistogram(byte[] pixels)
    {
        var bins = new int[256];
        foreach (var pixel in pixels)
        {
            bins[pixel]++;
        }
        HistogramData = bins;
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
        // Math.Clamp는 .NET Standard 2.0에서 사용 불가
        // Math.Clamp is not available in .NET Standard 2.0
        var newValue = ZoomFactor * factor;
        ZoomFactor = Math.Max(0.1, Math.Min(10.0, newValue));
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
