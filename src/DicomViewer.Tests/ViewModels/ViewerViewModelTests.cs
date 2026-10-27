using System.ComponentModel;

using DicomViewer.Application.DTOs;
using DicomViewer.Application.Exceptions;
using DicomViewer.Application.Services;
using DicomViewer.Domain.Enums;
using DicomViewer.Domain.ValueObjects;
using DicomViewer.ViewModels;

using FluentAssertions;

namespace DicomViewer.Tests.ViewModels;

/// <summary>
/// ViewerViewModel의 소비자 관찰 동작: 최신 요청 우선, 낡은 결과·오류 무시, 게시 버퍼 불변, UI 컨텍스트 게시, 닫기, 상태 전이, 측정.
/// 비동기 완료는 TaskCompletionSource와 게이트로 직접 제어하며 sleep을 쓰지 않는다 (교착 방지용 WaitAsync 제한 시간만 둔다).
/// Consumer-visible ViewerViewModel behavior: latest request wins, stale results and errors are ignored, published buffers are immutable,
/// publication on the UI context, close, state transitions, measurements. Async completion is controlled by completion sources and
/// gates; there are no sleeps (only a WaitAsync deadlock guard).
/// </summary>
public sealed class ViewerViewModelTests
{
    private static readonly TimeSpan DeadlockGuard = TimeSpan.FromSeconds(30);

    [Fact]
    public Task Older_slice_load_completing_last_is_not_published_and_does_not_clear_loading() => RunAsync(async h =>
    {
        await h.OpenAsync(ViewerData.Dataset(ViewerData.SpatialSeries("A", 3)));
        await h.CompleteAndShowAsync("A", 1);

        h.Vm.CurrentSliceIndex = 1;
        h.Vm.CurrentSliceIndex = 2;

        h.Images.Load(ViewerData.Path("A", 2)).SetResult(ViewerData.Image(2));
        await h.Ui.DrainAsync();

        h.Vm.Frame!.SourceId.Should().Be("A1");
        h.Vm.IsLoading.Should().BeTrue();
        h.Vm.State.Should().Be(ViewerState.Loading);

        await h.CompleteAndShowAsync("A", 3);
        await h.Vm.WaitForIdleAsync();

        h.Vm.Frame!.SourceId.Should().Be("A3");
        h.Vm.CurrentSliceIndex.Should().Be(2);
        h.Vm.IsLoading.Should().BeFalse();
        h.Vm.ErrorMessage.Should().BeNull();
        h.Vm.State.Should().Be(ViewerState.Ready);
    });

    [Fact]
    public Task Clearing_the_selection_during_a_pending_load_publishes_empty_and_a_late_result_stays_ignored() => RunAsync(async h =>
    {
        await h.OpenAsync(ViewerData.Dataset(ViewerData.SpatialSeries("A", 3)));
        h.Vm.IsLoading.Should().BeTrue();

        h.Vm.SelectedSeries = null;

        h.Vm.IsLoading.Should().BeFalse();
        h.Vm.Frame.Should().BeNull();
        h.Vm.State.Should().Be(ViewerState.Empty);

        h.Images.Load(ViewerData.Path("A", 1)).SetResult(ViewerData.Image(1));
        await h.Ui.DrainAsync();
        await h.Vm.WaitForIdleAsync();

        h.Vm.Frame.Should().BeNull();
        h.Vm.IsLoading.Should().BeFalse();
        h.Vm.State.Should().Be(ViewerState.Empty);
    });

    [Fact]
    public Task Older_slice_failure_is_ignored_while_a_newer_request_is_pending() => RunAsync(async h =>
    {
        await h.OpenAsync(ViewerData.Dataset(ViewerData.SpatialSeries("A", 3)));
        await h.CompleteAndShowAsync("A", 1);

        h.Vm.CurrentSliceIndex = 1;
        h.Vm.CurrentSliceIndex = 2;

        h.Images.Load(ViewerData.Path("A", 2)).SetException(new IOException("stale failure"));
        await h.Ui.DrainAsync();

        h.Vm.ErrorMessage.Should().BeNull();
        h.Vm.IsLoading.Should().BeTrue();
        h.Vm.Frame!.SourceId.Should().Be("A1");

        await h.CompleteAndShowAsync("A", 3);
        await h.Vm.WaitForIdleAsync();

        h.Vm.ErrorMessage.Should().BeNull();
        h.Vm.IsLoading.Should().BeFalse();
        h.Vm.State.Should().Be(ViewerState.Ready);
    });

    [Fact]
    public Task Failed_current_slice_clears_the_frame_with_an_explicit_error_and_a_later_slice_recovers() => RunAsync(async h =>
    {
        await h.OpenAsync(ViewerData.Dataset(ViewerData.SpatialSeries("A", 3)));
        await h.CompleteAndShowAsync("A", 1);
        h.Vm.Measurements.Add(new MeasurementLine(new Point2D(0, 0), new Point2D(1, 1), null, "x"));

        h.Vm.CurrentSliceIndex = 1;
        h.Images.Load(ViewerData.Path("A", 2)).SetException(new UnsupportedDicomImageException("multi-frame images are not supported"));
        await h.WaitForAsync(() => h.Vm.State == ViewerState.Error);

        h.Vm.Frame.Should().BeNull();
        h.Vm.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        h.Vm.IsLoading.Should().BeFalse();

        h.Vm.CurrentSliceIndex = 2;
        await h.CompleteAndShowAsync("A", 3);
        await h.Vm.WaitForIdleAsync();

        h.Vm.State.Should().Be(ViewerState.Ready);
        h.Vm.ErrorMessage.Should().BeNull();
    });

    [Fact]
    public Task Window_level_publishes_only_the_latest_render_and_never_writes_a_published_buffer() => RunAsync(async h =>
    {
        await h.OpenAsync(ViewerData.Dataset(ViewerData.SpatialSeries("A", 2)));
        await h.CompleteAndShowAsync("A", 1);
        h.Vm.WindowWidth.Should().Be(8);

        var held = h.Images.HoldRender(5);
        h.Vm.WindowWidth = 5;
        await held.Entered.Task.WaitAsync(DeadlockGuard);

        h.Vm.WindowWidth = 6;
        await h.WaitForAsync(() => h.Vm.Frame!.Pixels[0] == ControlledImageService.Marker(1, 6));

        var published = h.Vm.Frame!;
        var before = published.Pixels.ToArray();

        // 낡은 렌더(너비 5)를 풀어 자기 출력에 쓰게 한다. 게시된 프레임과 그 버퍼는 그대로여야 한다.
        // Release the stale render (width 5) so it writes into its own output; the published frame and its buffer must stay as they are.
        held.Release.Set();
        await h.Vm.WaitForIdleAsync();

        h.Vm.Frame.Should().BeSameAs(published);
        published.Pixels.Should().Equal(before);
        published.Pixels.Should().OnlyContain(value => value == ControlledImageService.Marker(1, 6));
    });

    [Fact]
    public Task Render_of_the_previous_slice_finishing_after_another_slice_is_selected_publishes_nothing() => RunAsync(async h =>
    {
        await h.OpenAsync(ViewerData.Dataset(ViewerData.SpatialSeries("A", 2)));
        await h.CompleteAndShowAsync("A", 1);
        var shown = h.Vm.Frame!;
        var frames = h.RecordFrames();

        // 슬라이스 1의 렌더(너비 5)를 풀 스레드에서 멈춰 둔 채 슬라이스 2를 선택한다 (슬라이스 2 로드는 보류).
        // Hold the render of slice 1 (width 5) on the pool thread, then select slice 2 (its load stays pending).
        var held = h.Images.HoldRender(5);
        h.Vm.WindowWidth = 5;
        await held.Entered.Task.WaitAsync(DeadlockGuard);
        h.Vm.CurrentSliceIndex = 1;

        // 낡은 렌더를 풀고, 그 await 연속이 UI 컨텍스트에서 실행을 마칠 때까지 확정한다.
        // Release the stale render and establish that its await continuation has run on the UI context.
        var arrived = h.Ui.NextPostAsync();
        held.Release.Set();
        await arrived.WaitAsync(DeadlockGuard);
        await h.Ui.DrainAsync();

        frames.Should().BeEmpty();
        h.Vm.Frame.Should().BeSameAs(shown);
        h.Vm.ErrorMessage.Should().BeNull();
        h.Vm.IsLoading.Should().BeTrue();
        h.Vm.State.Should().Be(ViewerState.Loading);

        await h.CompleteAndShowAsync("A", 2);
        await h.Vm.WaitForIdleAsync();

        frames.Should().Equal("A2");
        h.Vm.Frame!.Pixels[0].Should().Be(ControlledImageService.Marker(2, 5));
        h.Vm.IsLoading.Should().BeFalse();
        h.Vm.ErrorMessage.Should().BeNull();
        h.Vm.State.Should().Be(ViewerState.Ready);
    });

    [Fact]
    public Task Window_level_changed_while_another_slice_is_loading_applies_to_that_slice_and_never_re_renders_the_previous_one() => RunAsync(async h =>
    {
        await h.OpenAsync(ViewerData.Dataset(ViewerData.SpatialSeries("A", 2)));
        await h.CompleteAndShowAsync("A", 1);
        var shown = h.Vm.Frame!;
        var frames = h.RecordFrames();

        h.Vm.CurrentSliceIndex = 1;
        h.Vm.WindowWidth = 6;
        h.Vm.WindowCenter = 30;

        // 이전 슬라이스를 다시 렌더하려는 시도가 있었다면 풀 큐에서 이 마커 작업보다 앞선다. 마커가 끝난 뒤 UI 연속까지 비운다.
        // Any attempt to re-render the previous slice sits ahead of this marker in the pool queue; drain the UI continuations after it.
        await Task.Run(static () => { });
        await h.Ui.DrainAsync();

        h.Images.Rendered.ToArray().Should().Equal([(1, 8.0)]);
        h.Vm.Frame.Should().BeSameAs(shown);
        frames.Should().BeEmpty();
        h.Vm.IsLoading.Should().BeTrue();

        await h.CompleteAndShowAsync("A", 2);
        await h.Vm.WaitForIdleAsync();

        h.Images.Rendered.ToArray().Should().Equal([(1, 8.0), (2, 6.0)]);
        frames.Should().Equal("A2");
        h.Vm.Frame!.Pixels[0].Should().Be(ControlledImageService.Marker(2, 6));
        h.Vm.IsLoading.Should().BeFalse();
        h.Vm.State.Should().Be(ViewerState.Ready);
    });

    [Fact]
    public Task Published_frames_are_complete_snapshots_with_increasing_sequence_and_optional_histogram() => RunAsync(async h =>
    {
        await h.OpenAsync(ViewerData.Dataset(ViewerData.SpatialSeries("A", 2)));
        await h.CompleteAndShowAsync("A", 1);
        var first = h.Vm.Frame!;

        first.Pixels.Length.Should().Be(first.Width * first.Height);
        first.Histogram.Should().NotBeNull();
        first.Histogram!.Should().HaveCount(256);
        first.Histogram![ControlledImageService.Marker(1, 8)].Should().Be(first.Width * first.Height);

        h.Vm.WindowCenter = 41;
        await h.Vm.WaitForIdleAsync();
        var second = h.Vm.Frame!;
        second.Sequence.Should().BeGreaterThan(first.Sequence);
        second.Should().NotBeSameAs(first);
        second.Pixels.Should().NotBeSameAs(first.Pixels);

        h.Vm.ShowHistogram = false;
        await h.Vm.WaitForIdleAsync();
        h.Vm.Frame!.Histogram.Should().BeNull();
        h.Vm.Frame.Sequence.Should().BeGreaterThan(second.Sequence);
    });

    [Fact]
    public Task Default_window_level_applies_once_per_series_and_user_values_survive_scrolling() => RunAsync(async h =>
    {
        await h.OpenAsync(ViewerData.Dataset(ViewerData.SpatialSeries("A", 2)));
        await h.CompleteAndShowAsync("A", 1);
        h.Vm.WindowWidth.Should().Be(8);
        h.Vm.WindowCenter.Should().Be(40);

        h.Vm.WindowWidth = 9;
        h.Vm.WindowCenter = 41;
        await h.Vm.WaitForIdleAsync();

        h.Vm.CurrentSliceIndex = 1;
        h.Images.Load(ViewerData.Path("A", 2)).SetResult(ViewerData.Image(2, windowLevel: new WindowLevel(3, 7)));
        await h.WaitForAsync(() => h.Vm.Frame?.SourceId == "A2");
        await h.Vm.WaitForIdleAsync();

        h.Vm.WindowWidth.Should().Be(9);
        h.Vm.WindowCenter.Should().Be(41);
        h.Vm.Frame!.Pixels[0].Should().Be(ControlledImageService.Marker(2, 9));

        h.Vm.ResetWindowLevelCommand.Execute(null);
        await h.Vm.WaitForIdleAsync();

        h.Vm.WindowWidth.Should().Be(8);
        h.Vm.WindowCenter.Should().Be(40);
        h.Vm.Frame!.Pixels[0].Should().Be(ControlledImageService.Marker(2, 8));
    });

    [Fact]
    public Task Selecting_another_series_cancels_the_pending_load_and_clears_the_displayed_frame() => RunAsync(async h =>
    {
        await h.OpenAsync(ViewerData.Dataset(ViewerData.SpatialSeries("A", 3), ViewerData.SpatialSeries("B", 2)));
        await h.CompleteAndShowAsync("A", 1);
        h.Vm.CurrentSliceIndex = 1;

        h.Vm.SelectedSeries = h.Vm.SeriesList[1];

        h.Vm.Frame.Should().BeNull();
        h.Vm.State.Should().Be(ViewerState.Loading);
        h.Vm.CurrentSliceIndex.Should().Be(0);
        h.Vm.TotalSliceCount.Should().Be(2);

        h.Images.Load(ViewerData.Path("A", 2)).SetResult(ViewerData.Image(2));
        await h.Ui.DrainAsync();
        h.Vm.Frame.Should().BeNull();

        await h.CompleteAndShowAsync("B", 1);
        await h.Vm.WaitForIdleAsync();

        h.Vm.Frame!.SourceId.Should().Be("B1");
        h.Vm.State.Should().Be(ViewerState.Ready);
    });

    [Fact]
    public Task Close_waits_for_an_in_flight_load_that_ignores_cancellation_and_publishes_nothing() => RunAsync(async h =>
    {
        await h.OpenAsync(ViewerData.Dataset(ViewerData.SpatialSeries("A", 2)));

        var closing = h.Vm.CloseAsync();
        await h.Ui.DrainAsync();
        closing.IsCompleted.Should().BeFalse();

        h.Images.Load(ViewerData.Path("A", 1)).SetResult(ViewerData.Image(1));
        await closing.WaitAsync(DeadlockGuard);

        h.Vm.Frame.Should().BeNull();
        h.Vm.IsLoading.Should().BeFalse();
        h.Vm.State.Should().Be(ViewerState.Empty);
        h.Images.AppliedWidths.Should().BeEmpty();

        h.Vm.CurrentSliceIndex = 1;
        await h.Vm.WaitForIdleAsync();
        h.Vm.IsLoading.Should().BeFalse();
        h.Vm.Frame.Should().BeNull();
    });

    [Fact]
    public Task Close_cancels_an_in_flight_load_that_honors_cancellation() => RunAsync(async h =>
    {
        await h.OpenAsync(ViewerData.Dataset(ViewerData.SpatialSeries("A", 2)));

        await h.Vm.CloseAsync().WaitAsync(DeadlockGuard);

        h.Vm.IsLoading.Should().BeFalse();
        h.Vm.ErrorMessage.Should().BeNull();
        h.Vm.Frame.Should().BeNull();
    }, honorImageCancellation: true);

    [Fact]
    public Task Close_waits_for_a_render_in_flight_and_nothing_is_published_afterwards() => RunAsync(async h =>
    {
        await h.OpenAsync(ViewerData.Dataset(ViewerData.SpatialSeries("A", 2)));
        var held = h.Images.HoldRender(8);
        h.Images.Load(ViewerData.Path("A", 1)).SetResult(ViewerData.Image(1));
        await held.Entered.Task.WaitAsync(DeadlockGuard);

        var closing = h.Vm.CloseAsync();
        await h.Ui.DrainAsync();
        closing.IsCompleted.Should().BeFalse();

        held.Release.Set();
        await closing.WaitAsync(DeadlockGuard);

        h.Vm.Frame.Should().BeNull();
        h.Vm.IsLoading.Should().BeFalse();
    });

    [Fact]
    public Task Open_failure_is_an_explicit_error_and_retry_recovers_through_loading_to_ready() => RunAsync(async h =>
    {
        var states = h.RecordStates();

        var first = h.Vm.OpenDatasetAsync();
        h.Source.Opens[0].SetException(new InvalidDataException("Dataset file 0001 is an unresolved Git LFS pointer; restore the object before verifying."));
        await first;

        h.Vm.State.Should().Be(ViewerState.Error);
        h.Vm.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        h.Vm.SeriesList.Should().BeEmpty();
        h.Vm.Frame.Should().BeNull();
        h.Vm.IsLoading.Should().BeFalse();

        var retry = h.Vm.OpenDatasetAsync();
        h.Source.Opens[1].SetResult(ViewerData.Dataset(ViewerData.SpatialSeries("A", 2)));
        await retry;
        h.Vm.State.Should().Be(ViewerState.Loading);
        h.Vm.ErrorMessage.Should().BeNull();

        await h.CompleteAndShowAsync("A", 1);
        await h.Vm.WaitForIdleAsync();

        h.Vm.State.Should().Be(ViewerState.Ready);
        states.Should().Equal(ViewerState.Loading, ViewerState.Error, ViewerState.Loading, ViewerState.Ready);
    });

    [Fact]
    public Task Dataset_without_images_is_an_explicit_error() => RunAsync(async h =>
    {
        var open = h.Vm.OpenDatasetAsync();
        h.Source.Opens[0].SetResult(ViewerData.Dataset(ViewerData.Series("A")));
        await open;

        h.Vm.State.Should().Be(ViewerState.Error);
        h.Vm.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        h.Vm.IsLoading.Should().BeFalse();
    });

    [Fact]
    public Task Older_open_completing_last_neither_replaces_the_dataset_nor_reports_its_failure() => RunAsync(async h =>
    {
        var first = h.Vm.OpenDatasetAsync();
        var second = h.Vm.OpenDatasetAsync();

        h.Source.Opens[1].SetResult(ViewerData.Dataset(ViewerData.SpatialSeries("B", 2)));
        await second;
        await h.CompleteAndShowAsync("B", 1);

        h.Source.Opens[0].SetResult(ViewerData.Dataset(ViewerData.SpatialSeries("A", 3)));
        await first;
        await h.Vm.WaitForIdleAsync();

        h.Vm.SeriesList.Should().ContainSingle().Which.SeriesInstanceUid.Should().Be("B");
        h.Vm.Frame!.SourceId.Should().Be("B1");
        h.Vm.State.Should().Be(ViewerState.Ready);
    }, honorSourceCancellation: false);

    [Fact]
    public Task Older_open_failing_last_does_not_replace_the_current_state_with_an_error() => RunAsync(async h =>
    {
        var first = h.Vm.OpenDatasetAsync();
        var second = h.Vm.OpenDatasetAsync();

        h.Source.Opens[1].SetResult(ViewerData.Dataset(ViewerData.SpatialSeries("B", 2)));
        await second;
        await h.CompleteAndShowAsync("B", 1);

        h.Source.Opens[0].SetException(new InvalidDataException("stale open failure"));
        await first;
        await h.Vm.WaitForIdleAsync();

        h.Vm.ErrorMessage.Should().BeNull();
        h.Vm.State.Should().Be(ViewerState.Ready);
    }, honorSourceCancellation: false);

    [Theory]
    [InlineData(SeriesGeometryStatus.Spatial)]
    [InlineData(SeriesGeometryStatus.MissingGeometry)]
    [InlineData(SeriesGeometryStatus.AmbiguousGeometry)]
    public Task Banner_is_shown_only_when_the_slice_order_is_not_physical(SeriesGeometryStatus expected) => RunAsync(async h =>
    {
        var series = expected switch
        {
            SeriesGeometryStatus.Spatial => ViewerData.SpatialSeries("A", 3),
            SeriesGeometryStatus.MissingGeometry => ViewerData.Series(
                "A", ViewerData.Instance("A", 1), ViewerData.Instance("A", 2), ViewerData.Instance("A", 3)),
            _ => ViewerData.Series(
                "A",
                ViewerData.Instance("A", 1, position: [0.0, 0.0, 1.0]),
                ViewerData.Instance("A", 2, position: [0.0, 0.0, 1.0]),
                ViewerData.Instance("A", 3, position: [0.0, 0.0, 2.0])),
        };

        await h.OpenAsync(ViewerData.Dataset(series));

        h.Vm.GeometryStatus.Should().Be(expected);
        (h.Vm.GeometryBanner is null).Should().Be(expected == SeriesGeometryStatus.Spatial);
    });

    [Fact]
    public Task Unknown_spacing_shows_a_notice_and_measures_in_pixels_with_null_millimeters() => RunAsync(async h =>
    {
        await h.OpenAsync(ViewerData.Dataset(ViewerData.SpatialSeries("A", 2)));
        h.Images.Load(ViewerData.Path("A", 1)).SetResult(ViewerData.Image(1, spacing: PixelSpacing.Unknown));
        await h.WaitForAsync(() => h.Vm.Frame is not null);

        h.Vm.SpacingBanner.Should().NotBeNull();
        h.Vm.MeasurementUnit.Should().Be("px");

        var request = new MeasurementRequest(h.Vm.Frame!.SourceId, new Point2D(0, 0), new Point2D(3, 4));
        h.Vm.AddMeasurementCommand.CanExecute(request).Should().BeTrue();
        h.Vm.AddMeasurementCommand.Execute(request);

        var line = h.Vm.Measurements.Should().ContainSingle().Subject;
        line.LengthMm.Should().BeNull();
        line.LengthPixels.Should().BeApproximately(5.0, 1e-9);
        line.StartPoint.Should().Be(new Point2D(0, 0));
        line.EndPoint.Should().Be(new Point2D(3, 4));
    });

    [Fact]
    public Task Known_anisotropic_spacing_measures_millimeters_per_axis() => RunAsync(async h =>
    {
        await h.OpenAsync(ViewerData.Dataset(ViewerData.SpatialSeries("A", 2)));

        // X는 열 방향 0.5mm, Y는 행 방향 0.25mm
        // X is 0.5 mm along columns, Y is 0.25 mm along rows
        h.Images.Load(ViewerData.Path("A", 1)).SetResult(ViewerData.Image(1, spacing: new PixelSpacing(0.5, 0.25)));
        await h.WaitForAsync(() => h.Vm.Frame is not null);

        h.Vm.SpacingBanner.Should().BeNull();
        h.Vm.MeasurementUnit.Should().Be("mm");

        h.Vm.AddMeasurementCommand.Execute(new MeasurementRequest(h.Vm.Frame!.SourceId, new Point2D(0, 0), new Point2D(4, 4)));

        var line = h.Vm.Measurements.Should().ContainSingle().Subject;
        line.LengthMm.Should().NotBeNull();
        line.LengthMm!.Value.Should().BeApproximately(Math.Sqrt(2.0 * 2.0 + 1.0 * 1.0), 1e-9);
    });

    [Fact]
    public Task Measurement_is_accepted_only_for_the_displayed_image_and_lines_clear_when_the_image_changes() => RunAsync(async h =>
    {
        await h.OpenAsync(ViewerData.Dataset(ViewerData.SpatialSeries("A", 2)));
        await h.CompleteAndShowAsync("A", 1);

        var foreign = new MeasurementRequest("another-image", new Point2D(0, 0), new Point2D(2, 2));
        h.Vm.AddMeasurementCommand.CanExecute(foreign).Should().BeFalse();
        h.Vm.AddMeasurementCommand.Execute(foreign);
        h.Vm.Measurements.Should().BeEmpty();

        var own = new MeasurementRequest("A1", new Point2D(0, 0), new Point2D(2, 2));
        h.Vm.AddMeasurementCommand.Execute(own);
        h.Vm.Measurements.Should().HaveCount(1);

        // 새 슬라이스가 로드되는 동안에는 이전 영상이 표시 중이므로 그 영상의 측정선은 유지된다.
        // While the next slice loads the previous image is still displayed, so its lines stay.
        h.Vm.CurrentSliceIndex = 1;
        h.Vm.Measurements.Should().HaveCount(1);

        await h.CompleteAndShowAsync("A", 2);
        h.Vm.Measurements.Should().BeEmpty();
        h.Vm.AddMeasurementCommand.CanExecute(own).Should().BeFalse();
    });

    [Fact]
    public Task State_is_published_only_on_the_ui_thread() => RunAsync(async h =>
    {
        var threads = new List<int>();
        h.Vm.PropertyChanged += (_, _) => threads.Add(Environment.CurrentManagedThreadId);

        await h.OpenAsync(ViewerData.Dataset(ViewerData.SpatialSeries("A", 2)));
        await h.CompleteAndShowAsync("A", 1);
        h.Vm.WindowWidth = 6;
        h.Vm.WindowCenter = 12;
        await h.Vm.WaitForIdleAsync();

        threads.Should().NotBeEmpty();
        threads.Should().OnlyContain(thread => thread == h.Ui.ThreadId);
        h.Vm.Frame!.Pixels[0].Should().Be(ControlledImageService.Marker(1, 6));
    });

    [Fact]
    public Task Slice_index_is_clamped_and_navigation_commands_stay_in_range() => RunAsync(async h =>
    {
        await h.OpenAsync(ViewerData.Dataset(ViewerData.SpatialSeries("A", 3)));
        await h.CompleteAndShowAsync("A", 1);
        h.Vm.MaxSliceIndex.Should().Be(2);

        h.Vm.LastSliceCommand.Execute(null);
        h.Vm.CurrentSliceIndex.Should().Be(2);
        h.Vm.NextSliceCommand.Execute(null);
        h.Vm.CurrentSliceIndex.Should().Be(2);

        h.Vm.CurrentSliceIndex = 99;
        h.Vm.CurrentSliceIndex.Should().Be(2);

        await h.CompleteAndShowAsync("A", 3);
        h.Vm.FirstSliceCommand.Execute(null);
        h.Vm.PreviousSliceCommand.Execute(null);
        h.Vm.CurrentSliceIndex.Should().Be(0);
        h.Vm.SliceDisplayNumber.Should().Be(1);
    });

    [Fact]
    public Task Selecting_the_current_tool_again_keeps_it_selected_and_re_notifies_the_binding() => RunAsync(async h =>
    {
        var notifications = 0;
        h.Vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ViewerViewModel.CurrentTool))
            {
                notifications++;
            }
        };

        h.Vm.SelectToolCommand.Execute(ViewerTool.Measure);
        h.Vm.SelectToolCommand.Execute(ViewerTool.Measure);

        h.Vm.CurrentTool.Should().Be(ViewerTool.Measure);
        notifications.Should().Be(2);
        await Task.CompletedTask;
    });

    private static async Task RunAsync(
        Func<Harness, Task> body,
        bool honorImageCancellation = false,
        bool honorSourceCancellation = true)
    {
        using var ui = new UiSynchronizationContext();
        var images = new ControlledImageService { HonorCancellation = honorImageCancellation };
        var source = new ControlledDatasetSource { HonorCancellation = honorSourceCancellation };
        var viewModel = new ViewerViewModel(images, new SeriesNavigationService(), source, new HistogramService());
        var harness = new Harness(ui, images, source, viewModel);

        await ui.RunAsync(async () =>
        {
            try
            {
                await body(harness);
            }
            finally
            {
                // 실패한 테스트가 멈춘 작업을 남기지 않도록 보류 중인 작업을 모두 풀고 닫는다.
                // Release everything still pending so a failing test leaves no stuck work, then close.
                images.AbandonAll();
                source.AbandonAll();
                await viewModel.CloseAsync().WaitAsync(DeadlockGuard);
            }
        }).WaitAsync(DeadlockGuard);
    }

    private sealed class Harness(
        UiSynchronizationContext ui,
        ControlledImageService images,
        ControlledDatasetSource source,
        ViewerViewModel viewModel)
    {
        public UiSynchronizationContext Ui { get; } = ui;

        public ControlledImageService Images { get; } = images;

        public ControlledDatasetSource Source { get; } = source;

        public ViewerViewModel Vm { get; } = viewModel;

        /// <summary>데이터셋 열기를 요청하고 완료한다. 반환 시 첫 슬라이스 로드가 보류 중이다.</summary>
        public async Task OpenAsync(DicomDatasetDto dataset)
        {
            var open = Vm.OpenDatasetAsync();
            Source.Opens[^1].SetResult(dataset);
            await open;
        }

        /// <summary>시리즈 안의 n번째(1부터) 영상 로드를 완료시키고 그 영상의 프레임이 게시될 때까지 기다린다.</summary>
        public async Task CompleteAndShowAsync(string seriesId, int number)
        {
            Images.Load(ViewerData.Path(seriesId, number)).SetResult(ViewerData.Image(number));
            await WaitForAsync(() => Vm.Frame?.SourceId == $"{seriesId}{number}");
        }

        public Task WaitForAsync(Func<bool> condition)
        {
            if (condition())
            {
                return Task.CompletedTask;
            }

            var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            PropertyChangedEventHandler? handler = null;
            handler = (_, _) =>
            {
                if (condition())
                {
                    Vm.PropertyChanged -= handler;
                    reached.TrySetResult();
                }
            };
            Vm.PropertyChanged += handler;
            return reached.Task.WaitAsync(DeadlockGuard);
        }

        /// <summary>State가 바뀔 때마다 연속 중복을 제거한 값을 기록한다.</summary>
        public List<ViewerState> RecordStates()
        {
            var states = new List<ViewerState>();
            Vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ViewerViewModel.State) && (states.Count == 0 || states[^1] != Vm.State))
                {
                    states.Add(Vm.State);
                }
            };
            return states;
        }

        /// <summary>Frame이 바뀔 때마다 새로 게시된 프레임의 SourceId를 기록한다 / Records the SourceId of every newly published frame</summary>
        public List<string?> RecordFrames()
        {
            var frames = new List<string?>();
            Vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ViewerViewModel.Frame))
                {
                    frames.Add(Vm.Frame?.SourceId);
                }
            };
            return frames;
        }
    }
}