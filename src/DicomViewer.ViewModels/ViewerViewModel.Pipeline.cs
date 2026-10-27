namespace DicomViewer.ViewModels;

/// <summary>
/// 열기·선택·슬라이스·Window/Level 비동기 파이프라인. 최신 요청만 이긴다.
/// Open / selection / slice / Window-Level asynchronous pipeline. Only the latest request wins.
/// </summary>
/// <remarks>
/// 상태는 UI 컨텍스트에서만 바뀐다: <c>Task.Run</c>에서는 순수 계산(ApplyWindowLevel·히스토그램)만 하고 결과는 await 연속에서 게시한다.
/// 요청 세대(epoch)와 취소 토큰이 모두 맞아야 게시하며, 낡은 완료·오류·finally는 최신 요청의 IsLoading을 지우지 않는다.
/// State changes only on the UI context: <c>Task.Run</c> performs pure computation (ApplyWindowLevel, histogram) and results are published from
/// await continuations. A result is published only while both the request epoch and the cancellation token are current, and stale completions,
/// errors and finally blocks never clear the IsLoading of a newer request.
/// </remarks>
public sealed partial class ViewerViewModel
{
    private readonly object _inFlightGate = new();
    private readonly HashSet<Task> _inFlight = [];

    // 열기·시리즈 선택·슬라이스 로드 요청
    // Open / series selection / slice load requests
    private long _requestEpoch;
    private CancellationTokenSource? _requestCts;

    // Window/Level 렌더 요청
    // Window/Level render requests
    private long _renderEpoch;
    private CancellationTokenSource? _renderCts;
    private long _frameSequence;

    private LoadedImage? _loaded;
    private WindowLevel? _seriesDefaultWindowLevel;
    private bool _applyDefaultWindowLevel;
    private bool _suppressRender;
    private bool _syncingSliceIndex;
    private bool _closed;

    /// <summary>
    /// 고정 데이터셋을 연다(검증 → 헤더 한 번 읽기 → 첫 시리즈 선택). 이전 열기·로드·렌더는 모두 취소된다.
    /// Opens the fixed dataset (verify → one header pass → select the first series). Any earlier open, load and render is cancelled.
    /// </summary>
    [RelayCommand]
    public async Task OpenDatasetAsync()
    {
        if (_closed)
        {
            return;
        }

        ClearSession();
        var cts = BeginRequest(out var epoch);
        IsLoading = true;
        ErrorMessage = null;

        await Track(OpenAsync(epoch, cts.Token));
    }

    /// <summary>
    /// 진행 중인 모든 열기·로드·렌더가 끝날 때까지 기다린다. 취소된 낡은 작업도 포함한다.
    /// Waits until every in-flight open, load and render has finished, including cancelled stale ones.
    /// </summary>
    public async Task WaitForIdleAsync()
    {
        while (true)
        {
            Task[] pending;
            lock (_inFlightGate)
            {
                _inFlight.RemoveWhere(static task => task.IsCompleted);
                pending = [.. _inFlight];
            }

            if (pending.Length == 0)
            {
                return;
            }

            await Task.WhenAll(pending);
        }
    }

    /// <summary>
    /// 모든 진행 작업을 취소하고 정지할 때까지 기다린다. 이후 요청은 무시된다. 창 닫기 전에 await 한다.
    /// Cancels all in-flight work and waits until it has stopped. Later requests are ignored. Await it before the window closes.
    /// </summary>
    public async Task CloseAsync()
    {
        if (!_closed)
        {
            _closed = true;
            CancelRequests();
            CancelRenders();
        }

        await WaitForIdleAsync();
        IsLoading = false;
    }

    public ValueTask DisposeAsync() => new(CloseAsync());

    private async Task OpenAsync(long epoch, CancellationToken cancellationToken)
    {
        try
        {
            var dataset = await _datasetSource.OpenAsync(cancellationToken);
            if (IsStale(epoch, cancellationToken))
            {
                return;
            }

            ApplyDataset(dataset);
        }
        catch (Exception ex)
        {
            if (IsStale(epoch, cancellationToken))
            {
                return;
            }

            ErrorMessage = ViewerText.OpenFailed(ex.Message);
        }
        finally
        {
            FinishRequest(epoch);
        }
    }

    private void ApplyDataset(DicomDatasetDto dataset)
    {
        DatasetId = dataset.DatasetId;
        Attribution = dataset.Attribution;
        Classification = dataset.Classification;
        SeriesList = new ObservableCollection<DicomSeriesDto>(dataset.Series);

        var first = dataset.Series.FirstOrDefault(series => series.Instances.Count > 0);
        if (first is null)
        {
            ErrorMessage = ViewerText.NoImages;
            return;
        }

        SelectedSeries = first;
    }

    private void ClearSession()
    {
        DatasetId = string.Empty;
        Attribution = string.Empty;
        Classification = string.Empty;
        SelectedSeries = null;
        SeriesList = [];
        ClearSeries();
    }

    private void ClearSeries()
    {
        CancelRequests();
        // 취소된 요청은 더 이상 IsLoading을 지울 수 없으므로 여기서 로딩 소유권을 해제한다.
        // The cancelled request can no longer clear IsLoading, so release loading ownership here.
        IsLoading = false;
        CancelRenders();

        _navigation.Clear();
        _loaded = null;
        _seriesDefaultWindowLevel = null;
        _applyDefaultWindowLevel = false;

        TotalSliceCount = 0;
        SetSliceIndexSilently(0);
        GeometryStatus = _navigation.GeometryStatus;
        PublishFrame(null, PixelSpacing.Unknown);
    }

    private void BeginSeries(DicomSeriesDto series)
    {
        CancelRenders();

        _loaded = null;
        _seriesDefaultWindowLevel = null;
        _applyDefaultWindowLevel = true;
        PublishFrame(null, PixelSpacing.Unknown);

        _navigation.SetSeries(series.Instances);
        TotalSliceCount = _navigation.TotalCount;
        GeometryStatus = _navigation.GeometryStatus;
        ResetViewTransform();
        SetSliceIndexSilently(0);

        StartSliceLoad();
    }

    private void StartSliceLoad()
    {
        if (_closed)
        {
            return;
        }

        var instance = _navigation.GetCurrentInstance();

        // 새 슬라이스를 고르는 순간 이전 슬라이스의 렌더는 낡은 것이다. 진행 중인 렌더를 취소하고 로드된 원본을 버려서
        // 이전 슬라이스가 새로 게시되거나 Window/Level 변경이 이전 슬라이스를 다시 렌더하지 않게 한다. 화면의 현재 프레임은 로딩 동안 유지된다.
        // The moment another slice is selected, renders of the previous slice are stale. Cancel the in-flight render and drop the loaded source so the
        // previous slice can neither be newly published nor re-rendered by a Window/Level change. The visible frame stays on screen while loading.
        CancelRenders();
        _loaded = null;

        var cts = BeginRequest(out var epoch);
        if (instance is null)
        {
            ErrorMessage = ViewerText.NoImages;
            IsLoading = false;
            return;
        }

        IsLoading = true;
        ErrorMessage = null;
        _ = Track(LoadAsync(instance, epoch, cts.Token));
    }

    private async Task LoadAsync(DicomInstanceDto instance, long epoch, CancellationToken cancellationToken)
    {
        try
        {
            var image = await _imageService.LoadImageAsync(instance.FilePath, cancellationToken);
            if (IsStale(epoch, cancellationToken))
            {
                return;
            }

            _loaded = new LoadedImage(instance, image);
            if (_applyDefaultWindowLevel)
            {
                // 시리즈의 첫 성공 로드에서만 DICOM 기본값을 적용하고, 슬라이스 이동 중에는 사용자 값을 유지한다.
                // Apply the DICOM default only on the series' first successful load; keep the user's values while scrolling.
                _applyDefaultWindowLevel = false;
                _seriesDefaultWindowLevel = image.DefaultWindowLevel;
                _suppressRender = true;
                try
                {
                    WindowWidth = image.DefaultWindowLevel.WindowWidth;
                    WindowCenter = image.DefaultWindowLevel.WindowCenter;
                }
                finally
                {
                    _suppressRender = false;
                }
            }

            await RequestRender();
        }
        catch (Exception ex)
        {
            if (IsStale(epoch, cancellationToken))
            {
                return;
            }

            // 선택한 슬라이스를 표시할 수 없으면 이전 슬라이스를 현재 영상처럼 두지 않는다.
            // If the selected slice cannot be shown, the previous slice must not stay on screen as if it were current.
            CancelRenders();
            _loaded = null;
            PublishFrame(null, PixelSpacing.Unknown);
            ErrorMessage = ViewerText.LoadFailed(instance.InstanceNumber, ex.Message);
        }
        finally
        {
            FinishRequest(epoch);
        }
    }

    private Task RequestRender()
    {
        if (_closed || _loaded is not { } source)
        {
            return Task.CompletedTask;
        }

        var epoch = ++_renderEpoch;
        Retire(ref _renderCts);
        var cts = _renderCts = new CancellationTokenSource();
        var request = new RenderRequest(source, new WindowLevel(WindowWidth, WindowCenter), ShowHistogram);

        return Track(RenderAsync(request, epoch, cts.Token));
    }

    private async Task RenderAsync(RenderRequest request, long epoch, CancellationToken cancellationToken)
    {
        try
        {
            var (pixels, histogram) = await Task.Run(() => Render(request, cancellationToken), cancellationToken);
            if (IsRenderStale(epoch, cancellationToken))
            {
                return;
            }

            var data = request.Source.Image.Pixels;
            PublishFrame(
                new RenderedFrame(request.Source.SourceId, ++_frameSequence, data.Width, data.Height, pixels, histogram),
                request.Source.Image.Spacing);
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            if (IsRenderStale(epoch, cancellationToken))
            {
                return;
            }

            ErrorMessage = ViewerText.RenderFailed(ex.Message);
        }
    }

    // 스레드 풀에서 실행: 호출마다 새 출력 배열에만 쓰므로 이미 게시된 프레임을 건드리지 않고 겹쳐 실행되는 렌더와도 충돌하지 않는다.
    // Runs on the thread pool: it writes only to a fresh output array per call, so it never touches a published frame or an overlapping render.
    private (byte[] Pixels, int[]? Histogram) Render(RenderRequest request, CancellationToken cancellationToken)
    {
        // 이미 취소된 요청은 출력 배열을 할당하기 전에 멈춘다.
        // A request that is already cancelled stops before the output array is allocated.
        cancellationToken.ThrowIfCancellationRequested();

        var data = request.Source.Image.Pixels;
        var pixels = new byte[data.PixelCount];
        _imageService.ApplyWindowLevel(data, request.WindowLevel, pixels, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        var histogram = request.ComputeHistogram ? _histogramService.CalculateHistogram(pixels).Bins : null;
        return (pixels, histogram);
    }

    private void PublishFrame(RenderedFrame? frame, PixelSpacing spacing)
    {
        if (!string.Equals(Frame?.SourceId, frame?.SourceId, StringComparison.Ordinal))
        {
            Measurements.Clear();
        }

        DisplayedSpacing = spacing;
        Frame = frame;
    }

    private bool IsStale(long epoch, CancellationToken cancellationToken) =>
        _closed || epoch != _requestEpoch || cancellationToken.IsCancellationRequested;

    private bool IsRenderStale(long epoch, CancellationToken cancellationToken) =>
        _closed || epoch != _renderEpoch || cancellationToken.IsCancellationRequested;

    private void FinishRequest(long epoch)
    {
        if (!_closed && epoch == _requestEpoch)
        {
            IsLoading = false;
        }
    }

    private CancellationTokenSource BeginRequest(out long epoch)
    {
        epoch = ++_requestEpoch;
        Retire(ref _requestCts);
        return _requestCts = new CancellationTokenSource();
    }

    private void CancelRequests()
    {
        _requestEpoch++;
        Retire(ref _requestCts);
    }

    private void CancelRenders()
    {
        _renderEpoch++;
        Retire(ref _renderCts);
    }

    private static void Retire(ref CancellationTokenSource? source)
    {
        var retired = source;
        source = null;
        if (retired is null)
        {
            return;
        }

        try
        {
            retired.Cancel();
        }
        finally
        {
            retired.Dispose();
        }
    }

    private Task Track(Task task)
    {
        lock (_inFlightGate)
        {
            _inFlight.RemoveWhere(static completed => completed.IsCompleted);
            if (!task.IsCompleted)
            {
                _inFlight.Add(task);
            }
        }

        return task;
    }

    private sealed record LoadedImage(DicomInstanceDto Instance, LoadedDicomImage Image)
    {
        // SOP Instance UID가 없으면 파일 경로로 구분한다 (같은 UID 없는 슬라이스끼리 측정선이 섞이지 않도록).
        // Falls back to the file path so slices without a UID never share measurement lines.
        public string SourceId => string.IsNullOrEmpty(Instance.SopInstanceUid) ? Instance.FilePath : Instance.SopInstanceUid;
    }

    private sealed record RenderRequest(LoadedImage Source, WindowLevel WindowLevel, bool ComputeHistogram);
}