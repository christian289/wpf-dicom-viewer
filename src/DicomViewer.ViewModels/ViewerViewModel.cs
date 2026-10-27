using DicomViewer.Domain.Enums;

namespace DicomViewer.ViewModels;

/// <summary>
/// DICOM 뷰어 ViewModel. 모든 멤버는 UI 동기화 컨텍스트에서만 호출한다. 비동기 작업의 결과는 await 연속에서(캡처된 UI 컨텍스트) 게시된다.
/// DICOM viewer ViewModel. Every member must be used from the UI synchronization context; asynchronous results are published from await
/// continuations on that captured context.
/// </summary>
public sealed partial class ViewerViewModel : ObservableObject, IAsyncDisposable
{
    private const double MinZoom = 0.1;
    private const double MaxZoom = 10.0;
    private const double ZoomStep = 1.2;

    private readonly IDicomImageService _imageService;
    private readonly ISeriesNavigationService _navigation;
    private readonly IDicomDatasetSource _datasetSource;
    private readonly HistogramService _histogramService;

    // 데이터셋
    // Dataset
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [ObservableProperty] private string _datasetId = string.Empty;

    [ObservableProperty] private string _attribution = string.Empty;

    [ObservableProperty] private string _classification = string.Empty;

    [ObservableProperty] private ObservableCollection<DicomSeriesDto> _seriesList = [];

    [ObservableProperty] private DicomSeriesDto? _selectedSeries;

    // 상태 (게시된 프레임은 단일 속성으로 원자적으로 교체된다)
    // State (the published frame is replaced atomically through one property)
    [NotifyPropertyChangedFor(nameof(State))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [ObservableProperty] private bool _isLoading;

    [NotifyPropertyChangedFor(nameof(State))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [ObservableProperty] private string? _errorMessage;

    [NotifyCanExecuteChangedFor(nameof(AddMeasurementCommand))]
    [NotifyPropertyChangedFor(nameof(State))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(IsMeasurementEnabled))]
    [NotifyPropertyChangedFor(nameof(SpacingBanner))]
    [ObservableProperty] private RenderedFrame? _frame;

    // 표시 중인 프레임에 대응하는 픽셀 간격 (프레임 게시와 같은 UI 턴에서 갱신)
    // Pixel spacing of the displayed frame (updated in the same UI turn as the frame publication)
    [NotifyPropertyChangedFor(nameof(SpacingBanner))]
    [NotifyPropertyChangedFor(nameof(MeasurementUnit))]
    [ObservableProperty] private PixelSpacing _displayedSpacing = PixelSpacing.Unknown;

    [NotifyPropertyChangedFor(nameof(GeometryBanner))]
    [ObservableProperty] private SeriesGeometryStatus _geometryStatus = SeriesGeometryStatus.Empty;

    // Window/Level
    [ObservableProperty] private double _windowWidth = WindowLevel.Default.WindowWidth;

    [ObservableProperty] private double _windowCenter = WindowLevel.Default.WindowCenter;

    // 변환 (ImageTransform과 같은 의미: 맞춤 배율 기준 확대, 시계 방향 회전 도, DIP 이동)
    // Transform (same meaning as ImageTransform: zoom over fit scale, clockwise rotation in degrees, DIP pan)
    [ObservableProperty] private double _zoomFactor = 1.0;

    [ObservableProperty] private double _panX;

    [ObservableProperty] private double _panY;

    [ObservableProperty] private double _rotationAngle;

    [ObservableProperty] private bool _flipHorizontal;

    [ObservableProperty] private bool _flipVertical;

    // 네비게이션 (0-based 인덱스)
    // Navigation (0-based index)
    [NotifyPropertyChangedFor(nameof(SliceDisplayNumber))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [ObservableProperty] private int _currentSliceIndex;

    [NotifyPropertyChangedFor(nameof(MaxSliceIndex))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [ObservableProperty] private int _totalSliceCount;

    // 도구
    // Tools
    [ObservableProperty] private ViewerTool _currentTool = ViewerTool.WindowLevel;

    [ObservableProperty] private bool _showHistogram = true;

    public ViewerViewModel(
        IDicomImageService imageService,
        ISeriesNavigationService navigation,
        IDicomDatasetSource datasetSource,
        HistogramService histogramService)
    {
        ArgumentNullException.ThrowIfNull(imageService);
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(datasetSource);
        ArgumentNullException.ThrowIfNull(histogramService);

        _imageService = imageService;
        _navigation = navigation;
        _datasetSource = datasetSource;
        _histogramService = histogramService;
    }

    /// <summary>
    /// 현재 표시 상태. 로드 중이면 이전 프레임이 남아 있어도 Loading이다.
    /// Current display state. Loading wins even while the previous frame is still shown.
    /// </summary>
    public ViewerState State =>
        IsLoading ? ViewerState.Loading
        : ErrorMessage is not null ? ViewerState.Error
        : Frame is null ? ViewerState.Empty
        : ViewerState.Ready;

    public string StatusText => State switch
    {
        ViewerState.Loading => ViewerText.Loading,
        ViewerState.Error => ErrorMessage!,
        ViewerState.Ready => ViewerText.Ready(DatasetId, SliceDisplayNumber, TotalSliceCount),
        _ => ViewerText.Empty
    };

    /// <summary>
    /// 슬라이더 Maximum (0-based 인덱스 최대값)
    /// Slider maximum (0-based index maximum)
    /// </summary>
    public int MaxSliceIndex => Math.Max(0, TotalSliceCount - 1);

    /// <summary>
    /// 사용자 표시용 슬라이스 번호 (1-based)
    /// Slice number for user display (1-based)
    /// </summary>
    public int SliceDisplayNumber => CurrentSliceIndex + 1;

    /// <summary>
    /// Spatial이 아니면 슬라이스 순서가 물리 순서가 아니라는 배너 문구, 아니면 null
    /// Banner text saying the slice order is not physical unless Spatial; otherwise null
    /// </summary>
    public string? GeometryBanner => GeometryStatus switch
    {
        SeriesGeometryStatus.MissingGeometry => ViewerText.GeometryMissing,
        SeriesGeometryStatus.AmbiguousGeometry => ViewerText.GeometryAmbiguous,
        _ => null
    };

    /// <summary>
    /// 표시 중인 영상의 간격이 미확인이면 px 단위 안내, 아니면 null
    /// px-unit notice when the displayed image has no known spacing; otherwise null
    /// </summary>
    public string? SpacingBanner => Frame is not null && !DisplayedSpacing.IsValid ? ViewerText.SpacingUnknown : null;

    public string MeasurementUnit => DisplayedSpacing.Unit == PixelSpacingUnit.Millimeter ? "mm" : "px";

    /// <summary>
    /// 표시 중인 프레임이 있을 때만 측정할 수 있다.
    /// Measurements are possible only while a frame is displayed.
    /// </summary>
    public bool IsMeasurementEnabled => Frame is not null;

    /// <summary>
    /// 표시 중인 영상의 측정선. 영상 픽셀 좌표이며 영상이 바뀌면 비워진다.
    /// Measurement lines of the displayed image, in image pixels; cleared when the image changes.
    /// </summary>
    public ObservableCollection<MeasurementLine> Measurements { get; } = [];

    partial void OnSelectedSeriesChanged(DicomSeriesDto? value)
    {
        if (_closed)
        {
            return;
        }

        if (value is null)
        {
            ClearSeries();
            return;
        }

        BeginSeries(value);
    }

    partial void OnCurrentSliceIndexChanged(int value)
    {
        if (_syncingSliceIndex || _closed || TotalSliceCount == 0)
        {
            return;
        }

        var clamped = Math.Clamp(value, 0, MaxSliceIndex);
        if (clamped != value)
        {
            SetSliceIndexSilently(clamped);
        }

        if (clamped == _navigation.CurrentIndex)
        {
            return;
        }

        _navigation.NavigateToIndex(clamped);
        StartSliceLoad();
    }

    partial void OnWindowWidthChanged(double value)
    {
        if (!double.IsFinite(value) || value < 1)
        {
            WindowWidth = 1;
            return;
        }

        if (!_suppressRender)
        {
            _ = RequestRender();
        }
    }

    partial void OnWindowCenterChanged(double oldValue, double newValue)
    {
        if (!double.IsFinite(newValue))
        {
            WindowCenter = double.IsFinite(oldValue) ? oldValue : 0;
            return;
        }

        if (!_suppressRender)
        {
            _ = RequestRender();
        }
    }

    partial void OnShowHistogramChanged(bool value)
    {
        _ = RequestRender();
    }

    [RelayCommand]
    private void NextSlice() => CurrentSliceIndex = Math.Min(CurrentSliceIndex + 1, MaxSliceIndex);

    [RelayCommand]
    private void PreviousSlice() => CurrentSliceIndex = Math.Max(CurrentSliceIndex - 1, 0);

    [RelayCommand]
    private void FirstSlice() => CurrentSliceIndex = 0;

    [RelayCommand]
    private void LastSlice() => CurrentSliceIndex = MaxSliceIndex;

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
            _ => null
        };

        if (preset is not null)
        {
            SetWindowLevel(preset);
        }
    }

    /// <summary>
    /// 도구는 라디오 방식이다: 이미 선택된 도구를 다시 눌러도 토글 버튼이 해제된 채 남지 않도록 같은 값이면 바인딩을 다시 알린다.
    /// Tools are radio-style: re-selecting the current tool re-raises the binding so the toggle button never stays unchecked.
    /// </summary>
    [RelayCommand]
    private void SelectTool(ViewerTool tool)
    {
        if (CurrentTool == tool)
        {
            OnPropertyChanged(nameof(CurrentTool));
            return;
        }

        CurrentTool = tool;
    }

    /// <summary>
    /// 시리즈 첫 영상의 DICOM 기본 Window/Level로 되돌린다.
    /// Restores the DICOM default Window/Level of the series' first image.
    /// </summary>
    [RelayCommand]
    private void ResetWindowLevel()
    {
        if (_seriesDefaultWindowLevel is { } defaultWindowLevel)
        {
            SetWindowLevel(defaultWindowLevel);
        }
    }

    [RelayCommand]
    private void ResetView() => ResetViewTransform();

    [RelayCommand]
    private void ZoomIn() => ZoomFactor = Math.Min(ZoomFactor * ZoomStep, MaxZoom);

    [RelayCommand]
    private void ZoomOut() => ZoomFactor = Math.Max(ZoomFactor / ZoomStep, MinZoom);

    /// <summary>
    /// 맞춤 배율이 확대율 1.0이므로 확대율과 이동만 초기화한다.
    /// Fit scale is zoom 1.0, so only zoom and pan are reset.
    /// </summary>
    [RelayCommand]
    private void FitToWindow()
    {
        ZoomFactor = 1.0;
        PanX = 0;
        PanY = 0;
    }

    [RelayCommand]
    private void RotateLeft() => RotationAngle = (RotationAngle - 90 + 360) % 360;

    [RelayCommand]
    private void RotateRight() => RotationAngle = (RotationAngle + 90) % 360;

    [RelayCommand]
    private void ToggleFlipHorizontal() => FlipHorizontal = !FlipHorizontal;

    [RelayCommand]
    private void ToggleFlipVertical() => FlipVertical = !FlipVertical;

    /// <summary>
    /// 표시 중인 바로 그 영상 위에서 그린 측정선만 추가한다. 간격이 미확인이면 px 단위, mm 값은 null이다.
    /// Adds only a line drawn on the image that is displayed right now. Unknown spacing yields px units and a null mm value.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAddMeasurement))]
    private void AddMeasurement(MeasurementRequest request)
    {
        if (!CanAddMeasurement(request))
        {
            return;
        }

        var spacing = DisplayedSpacing;
        var length = spacing.CalculateDistance(request.End.X - request.Start.X, request.End.Y - request.Start.Y);
        double? lengthMm = spacing.IsValid ? length : null;

        Measurements.Add(new MeasurementLine(
            request.Start,
            request.End,
            lengthMm,
            ViewerText.Distance(length, spacing.Unit)));
    }

    private bool CanAddMeasurement(MeasurementRequest request) =>
        Frame is { } frame && string.Equals(frame.SourceId, request.SourceId, StringComparison.Ordinal);

    [RelayCommand]
    private void ClearMeasurements() => Measurements.Clear();

    private void SetWindowLevel(WindowLevel windowLevel)
    {
        // 한 번의 렌더만 요청한다 (너비·중심을 따로 바꾸면 렌더가 두 번 요청된다).
        // Request a single render (changing width and center separately would request two).
        _suppressRender = true;
        try
        {
            WindowWidth = windowLevel.WindowWidth;
            WindowCenter = windowLevel.WindowCenter;
        }
        finally
        {
            _suppressRender = false;
        }

        _ = RequestRender();
    }

    private void SetSliceIndexSilently(int index)
    {
        _syncingSliceIndex = true;
        try
        {
            CurrentSliceIndex = index;
        }
        finally
        {
            _syncingSliceIndex = false;
        }
    }

    private void ResetViewTransform()
    {
        ZoomFactor = 1.0;
        PanX = 0;
        PanY = 0;
        RotationAngle = 0;
        FlipHorizontal = false;
        FlipVertical = false;
    }
}