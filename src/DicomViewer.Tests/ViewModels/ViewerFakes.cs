using System.Collections.Concurrent;

using DicomViewer.Application.DTOs;
using DicomViewer.Application.Interfaces;
using DicomViewer.Domain.ValueObjects;

namespace DicomViewer.Tests.ViewModels;

/// <summary>
/// 완료 시점을 테스트가 직접 제어하는 영상 서비스. 로드는 경로별 TaskCompletionSource, 렌더는 너비별 게이트로 멈춘다.
/// Image service whose completion the test controls: loads are per-path completion sources, renders are held by per-width gates.
/// </summary>
internal sealed class ControlledImageService : IDicomImageService
{
    private readonly object _gate = new();
    private readonly Dictionary<string, TaskCompletionSource<LoadedDicomImage>> _loads = [];
    private readonly Dictionary<double, RenderGate> _renderGates = [];

    /// <summary>true이면 취소 토큰이 취소될 때 로드를 취소로 마친다(실제 서비스처럼). false이면 취소를 무시하고 나중에 완료된다.</summary>
    public bool HonorCancellation { get; init; }

    public ConcurrentQueue<double> AppliedWidths { get; } = new();

    /// <summary>적용된 렌더마다 (슬라이스 번호, 너비)를 기록한다 / Records (slice id, width) for every applied render</summary>
    public ConcurrentQueue<(int SliceId, double Width)> Rendered { get; } = new();

    public TaskCompletionSource<LoadedDicomImage> Load(string path)
    {
        lock (_gate)
        {
            if (!_loads.TryGetValue(path, out var load))
            {
                load = new TaskCompletionSource<LoadedDicomImage>();
                _loads.Add(path, load);
            }

            return load;
        }
    }

    public RenderGate HoldRender(double windowWidth)
    {
        lock (_gate)
        {
            var gate = new RenderGate();
            _renderGates.Add(windowWidth, gate);
            return gate;
        }
    }

    public void AbandonAll()
    {
        lock (_gate)
        {
            foreach (var load in _loads.Values)
            {
                load.TrySetCanceled();
            }

            foreach (var gate in _renderGates.Values)
            {
                gate.Release.Set();
            }
        }
    }

    public Task<LoadedDicomImage> LoadImageAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var load = Load(filePath);
        if (HonorCancellation)
        {
            cancellationToken.Register(() => load.TrySetCanceled(cancellationToken));
        }

        return load.Task;
    }

    public Task<LoadedDicomImage> LoadImageFromStreamAsync(Stream dicomStream, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<DicomInstanceDto> ReadMetadataAsync(string filePath, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public void ApplyWindowLevel(
        PixelData pixelData,
        WindowLevel windowLevel,
        Span<byte> destination,
        CancellationToken cancellationToken = default)
    {
        AppliedWidths.Enqueue(windowLevel.WindowWidth);
        Rendered.Enqueue((pixelData.RawPixels[0], windowLevel.WindowWidth));

        RenderGate? gate;
        lock (_gate)
        {
            _renderGates.TryGetValue(windowLevel.WindowWidth, out gate);
        }

        if (gate is not null)
        {
            gate.Entered.TrySetResult();
            gate.Release.Wait();
        }

        // 슬라이스와 너비를 모두 반영한 값: 어느 렌더가 어느 버퍼에 썼는지 내용으로 구별된다.
        // Encodes both slice and width, so the content shows which render wrote which buffer.
        destination[..pixelData.PixelCount].Fill(Marker(pixelData.RawPixels[0], windowLevel.WindowWidth));
    }

    public static byte Marker(int sliceId, double windowWidth) => (byte)(sliceId * 16 + (int)windowWidth);

    internal sealed class RenderGate
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ManualResetEventSlim Release { get; } = new();
    }
}

internal sealed class ControlledDatasetSource : IDicomDatasetSource
{
    private readonly List<TaskCompletionSource<DicomDatasetDto>> _opens = [];

    public bool HonorCancellation { get; init; } = true;

    /// <summary>열기 요청마다 하나씩 생기는 완료 제어기. 인덱스는 요청 순서다.</summary>
    public IReadOnlyList<TaskCompletionSource<DicomDatasetDto>> Opens => _opens;

    public void AbandonAll()
    {
        foreach (var open in _opens)
        {
            open.TrySetCanceled();
        }
    }

    public Task<DicomDatasetDto> OpenAsync(CancellationToken cancellationToken = default)
    {
        var open = new TaskCompletionSource<DicomDatasetDto>();
        _opens.Add(open);
        if (HonorCancellation)
        {
            cancellationToken.Register(() => open.TrySetCanceled(cancellationToken));
        }

        return open.Task;
    }
}

internal static class ViewerData
{
    public static DicomDatasetDto Dataset(params DicomSeriesDto[] series) =>
        new("test-dataset", "Test attribution", "Test classification", series);

    /// <summary>위치·방향이 일관된 시리즈 (Spatial)</summary>
    public static DicomSeriesDto SpatialSeries(string id, int count) =>
        Series(id, Enumerable.Range(1, count).Select(n => Instance(id, n, position: [0.0, 0.0, n])).ToArray());

    public static DicomSeriesDto Series(string id, params DicomInstanceDto[] instances) =>
        new(id, null, 1, "CT", instances.Length, instances);

    public static DicomInstanceDto Instance(string seriesId, int number, double[]? position = null) =>
        new(
            SopInstanceUid: $"{seriesId}{number}",
            InstanceNumber: number,
            FilePath: Path(seriesId, number),
            Rows: 2,
            Columns: 4,
            SeriesInstanceUid: seriesId,
            FrameOfReferenceUid: "frame",
            ImagePositionPatient: position,
            ImageOrientationPatient: position is null ? null : [1.0, 0.0, 0.0, 0.0, 1.0, 0.0]);

    public static string Path(string seriesId, int number) => $"{seriesId}{number}.dcm";

    /// <summary>첫 바이트가 sliceId인 4x2 16비트 영상</summary>
    public static LoadedDicomImage Image(int sliceId, PixelSpacing? spacing = null, WindowLevel? windowLevel = null)
    {
        var raw = new byte[4 * 2 * 2];
        raw[0] = (byte)sliceId;
        return new LoadedDicomImage(
            new PixelData(4, 2, 16, 16, false, 1.0, 0.0, raw),
            spacing ?? new PixelSpacing(1.0, 1.0),
            windowLevel ?? new WindowLevel(8, 40));
    }
}