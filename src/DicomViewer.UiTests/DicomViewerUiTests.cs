using System.Drawing;
using System.Globalization;
using System.Text.RegularExpressions;

namespace DicomViewer.UiTests;

/// <summary>
/// 소유 PID 창 안에서만 실행되는 UI 검증. 기본 실행에서는 Skip으로 보고된다.
/// UI verification confined to the owned PID window. Reports Skip in default runs.
/// </summary>
[Collection(UiCollection.Name)]
public sealed class DicomViewerUiTests
{
    // 고정 기준 자료: 512×512 px, 간격 1.0 mm, 150 슬라이스 (헤더 집계).
    // Pinned baseline: 512×512 px, 1.0 mm spacing, 150 slices (header tally).
    private const int ImagePixels = 512;
    private const int SliceCount = 150;
    private const string ExpectedDatasetId = "vhp-female-ankle-v1";
    private const string MmUnit = "Unit: mm";

    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan FirstFrameTimeout = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan ActionTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(15);

    private sealed record FrameState(int? SliceTotal, string Unit, bool StateShown, bool LoadingShown);

    [UiFact]
    public void Startup_WaitsForFirstFrame_ThenRapidSliderSettlesOnLastRequest()
    {
        using var session = AppSession.Start(StartupTimeout);

        // 메타데이터(150)는 픽셀보다 먼저 보일 수 있으므로, 첫 프레임 게시를 나타내는 mm 단위와 상태 패널 숨김을 함께 기다린다.
        // Metadata (150) can appear before pixels, so wait for the frame-publish signal: mm unit, state panel hidden, loading finished.
        WaitForFirstFrame(session);

        Assert.Equal(ExpectedDatasetId, session.ReadName("DatasetId"));
        Assert.Contains("National Library of Medicine", session.ReadName("DatasetAttribution"), StringComparison.Ordinal);
        Assert.Contains("Non-weight-bearing", session.ReadName("DatasetClassification"), StringComparison.OrdinalIgnoreCase);

        // 빠른 연속 요청: 74 → 149 → 0 → 74. 마지막 요청(인덱스 74, 표시 75)만 남아야 한다.
        // Rapid requests: 74 → 149 → 0 → 74. Only the last request (index 74, display 75) must remain.
        foreach (var index in new double[] { 74, 149, 0, 74 })
        {
            session.SetRangeValue("SliceSlider", index);
        }

        AppSession.WaitFor(
            () => ParseInt(session.ReadName("SliceNumber")),
            display => display == 75,
            ActionTimeout,
            "SliceNumber to settle on 75 (last requested index 74)");

        // 짧은 안정 구간 동안 이전 요청 값으로 되돌아가지 않는지 확인한다(고정 시간 대기가 아닌 명시적 관찰 구간).
        // Bounded stability window: the display must not drift back to an earlier request.
        var stableUntil = DateTime.UtcNow.AddMilliseconds(1500);
        while (DateTime.UtcNow < stableUntil)
        {
            Thread.Sleep(100);
            Assert.Equal(75, ParseInt(session.ReadName("SliceNumber")));
        }

        // 증거: 표시 75에서 소유 창만 캡처한다(임시 폴더, 저장소 밖). 환자 필드가 보이는지 사람이 검토한 뒤에만 공유한다.
        // Evidence: capture only the owned window at display 75 (temp folder, outside the repo). Review before sharing.
        session.CaptureOwnedWindow(Path.Combine(Path.GetTempPath(), "dicom-viewer-ui-evidence", $"slice75-{DateTime.UtcNow:yyyyMMddHHmmss}.png"));

        Assert.Equal(74.0, session.ReadRangeValue("SliceSlider"));
        Assert.Equal(SliceCount, ParseInt(session.ReadName("SliceTotal")));
    }

    [UiFact]
    public void Transforms_Drags_AndWindowLevel_UpdateReadouts_ThenResetsRestoreState()
    {
        using var session = AppSession.Start(StartupTimeout);
        WaitForFirstFrame(session);

        // 1) 버튼 경로: 변환 명령이 읽기 값에 반영되는지 확인한다.
        // 1) Button path: the transform commands are reflected in the readouts.
        session.Click("ResetView");
        WaitForRotation(session, 0);
        WaitForFlip(session, horizontal: false, vertical: false);
        WaitForPan(session, 0, 0);
        WaitForZoomPercent(session, 100);

        session.Click("RotateRight");
        WaitForRotation(session, 90);

        session.Click("FlipHorizontal");
        WaitForFlip(session, horizontal: true, vertical: false);

        session.Click("ResetView");
        WaitForRotation(session, 0);
        WaitForFlip(session, horizontal: false, vertical: false);

        // 2) 드래그 경로: 소유 ViewerSurface 중심에서 시작해 창 안에서만 드래그한다. 값은 정확한 배율이 아니라 "변했고 범위 안"만 확인한다.
        // 2) Drag path: start at the centre of the owned ViewerSurface and drag inside the owned window only. Assert the value changed and is bounded, not an exact zoom.
        var surface = session.Find("ViewerSurface").BoundingRectangle;
        var centre = new Point((int)(surface.Left + surface.Width / 2), (int)(surface.Top + surface.Height / 2));
        var baselineWl = WaitForWindowLevelRead(session);

        session.Click("ToolPan");
        session.DragLeft(centre, Offset(centre, 80, 50));
        AppSession.WaitFor(() => ReadPan(session), pan => !double.IsNaN(pan.X) && (pan.X != 0 || pan.Y != 0), ActionTimeout, "pan readout to change after a pan drag");

        session.Click("ToolZoom");
        session.DragLeft(centre, Offset(centre, 0, -80));
        AppSession.WaitFor(
            () => ReadZoomPercent(session),
            zoom => zoom is not null && zoom != 100 && zoom >= 10 && zoom <= 1000,
            ActionTimeout,
            "zoom readout to change to a bounded value (10% to 1000%)");

        session.Click("ToolWindowLevel");
        session.DragLeft(centre, Offset(centre, 60, 0));
        AppSession.WaitFor(() => ReadWindowLevel(session).Width, width => Math.Abs(width - baselineWl.Width) > 0.5, ActionTimeout, "window width readout to change after a horizontal W/L drag");

        session.DragLeft(centre, Offset(centre, 0, 40));
        AppSession.WaitFor(() => ReadWindowLevel(session).Level, level => Math.Abs(level - baselineWl.Level) > 0.5, ActionTimeout, "window level readout to change after a vertical W/L drag");

        // 3) VM 명령 복원: 드래그로 바뀐 바인딩 상태가 리셋 명령으로 기준값에 돌아오는지 확인한다.
        // 3) VM command restores: after drags, the reset commands must return the bound state to the baseline.
        session.Click("ResetView");
        WaitForPan(session, 0, 0);
        WaitForZoomPercent(session, 100);
        WaitForRotation(session, 0);

        session.Click("ResetWindowLevel");
        AppSession.WaitFor(() => ReadWindowLevel(session), wl => Math.Abs(wl.Width - baselineWl.Width) < 0.01 && Math.Abs(wl.Level - baselineWl.Level) < 0.01, ActionTimeout, $"default W/L {baselineWl.Width} / {baselineWl.Level} restored");

        session.Click("PresetLung");
        WaitForWindowLevel(session, width: 1500, level: -600);

        session.Click("ResetWindowLevel");
        AppSession.WaitFor(() => ReadWindowLevel(session), wl => Math.Abs(wl.Width - baselineWl.Width) < 0.01 && Math.Abs(wl.Level - baselineWl.Level) < 0.01, ActionTimeout, "W/L restored after preset");
    }

    [UiFact]
    public void Measurement_KnownSpacing_ReadsMillimetres_ThenCloseExitsCleanly()
    {
        using var session = AppSession.Start(StartupTimeout);
        WaitForFirstFrame(session);

        session.Click("ResetView");
        WaitForPan(session, 0, 0);

        session.Click("ClearMeasurements");
        AppSession.WaitFor(() => session.CountById("MeasurementItem"), count => count == 0, ActionTimeout, "no measurements before the test");

        session.Click("ToolMeasure");
        AppSession.WaitFor(() => session.ReadName("MeasurementUnit"), unit => unit == MmUnit, ActionTimeout, "mm unit for the known 1.0 mm spacing");

        var surface = session.Find("ViewerSurface").BoundingRectangle;
        // BoundingRectangle는 System.Drawing.Rectangle(int)이므로 나눗셈을 double로 해야 한다.
        // BoundingRectangle is System.Drawing.Rectangle (int), so divide as double.
        var fit = Math.Min(surface.Width, surface.Height) / (double)ImagePixels;
        Assert.True(fit > 0, $"Viewer surface has no usable size: {surface}.");

        // 영상은 뷰포트 중앙에 맞춰 표시된다(줌 100%, Pan 0,0 확인 후). 영상 (100,100)→(130,140): 30×40 px → 50 mm.
        // Image is centred at 100% zoom with Pan 0,0 (confirmed above). Image (100,100)→(130,140): 30×40 px → 50 mm.
        var originX = surface.Left + (surface.Width - ImagePixels * fit) / 2.0;
        var originY = surface.Top + (surface.Height - ImagePixels * fit) / 2.0;
        var start = new Point((int)Math.Round(originX + 100 * fit), (int)Math.Round(originY + 100 * fit));
        var end = new Point((int)Math.Round(originX + 130 * fit), (int)Math.Round(originY + 140 * fit));

        session.DragLeft(start, end);

        var names = AppSession.WaitFor(
            () => session.NamesById("MeasurementItem"),
            items => items.Count == 1 && items[0].EndsWith(" mm", StringComparison.Ordinal),
            ActionTimeout,
            "one measurement labelled in mm");

        var length = ParseNumbers(names[0]).First();
        // 읽기 전용 진단: 경로·사용자 정보 없이 수치만 출력한다. / Read-only diagnostics: numbers only, no paths or user info.
        Console.WriteLine($"[measure-diag] testProcessDpiAware={AppSession.TestProcessIsDpiAware} screen={AppSession.ScreenSize.Width}x{AppSession.ScreenSize.Height} windowRect=({session.Window.BoundingRectangle.Left},{session.Window.BoundingRectangle.Top},{session.Window.BoundingRectangle.Width}x{session.Window.BoundingRectangle.Height}) windowDpi={session.WindowDpi} surfaceRect=({surface.Left},{surface.Top},{surface.Width}x{surface.Height}) fit={fit:F4} origin=({originX:F2},{originY:F2}) start={start} end={end} observedLabel='{names[0]}' parsedMm={length:F3}");

        // 좌표 반올림: 테스트 프로세스가 DPI 비인식이면 좌표는 가상(논리) 공간이며, 끝점마다 최대 0.5 좌표 단위 오차 → 영상 픽셀 약 0.5/fit씩, 합산 여유 2/fit mm(간격 1.0 mm).
        // Coordinate rounding: the test process is DPI-unaware, so coordinates are in the virtualized (logical) space. Each endpoint is off by up to 0.5 unit, about 0.5/fit image px; allow 2/fit mm total at 1.0 mm spacing.
        var tolerance = 2.0 / fit;
        Assert.InRange(length, 50.0 - tolerance, 50.0 + tolerance);

        session.Click("ClearMeasurements");
        AppSession.WaitFor(() => session.CountById("MeasurementItem"), count => count == 0, ActionTimeout, "measurements cleared");

        var (graceful, exitCode) = session.RequestClose(ExitTimeout);
        Assert.True(graceful, "The window did not close gracefully.");
        Assert.Equal(0, exitCode);
    }

    private static Point Offset(Point origin, int dx, int dy) => new(origin.X + dx, origin.Y + dy);

    private static void WaitForFirstFrame(AppSession session) =>
        AppSession.WaitFor(
            () => ReadFrame(session),
            frame => frame.SliceTotal == SliceCount && frame.Unit == MmUnit && !frame.StateShown && !frame.LoadingShown,
            FirstFrameTimeout,
            "first frame published (150 slices, mm unit, state panel hidden, loading finished)");

    private static FrameState ReadFrame(AppSession session) =>
        new(
            ParseInt(session.ReadName("SliceTotal")),
            session.ReadName("MeasurementUnit"),
            session.IsShown("StatePanel"),
            session.IsShown("LoadingIndicator"));

    private static void WaitForRotation(AppSession session, double degrees) =>
        AppSession.WaitFor(
            () => ParseNumbers(session.ReadName("RotationReadout")).FirstOrDefault(),
            value => Math.Abs(value - degrees) < 0.01,
            ActionTimeout,
            $"rotation readout {degrees}");

    private static void WaitForPan(AppSession session, double x, double y) =>
        AppSession.WaitFor(
            () => ReadPan(session),
            pan => Math.Abs(pan.X - x) < 0.5 && Math.Abs(pan.Y - y) < 0.5,
            ActionTimeout,
            $"pan readout {x}, {y}");

    private static (double X, double Y) ReadPan(AppSession session)
    {
        var numbers = ParseNumbers(session.ReadName("PanReadout")).ToArray();
        return numbers.Length == 2 ? (numbers[0], numbers[1]) : (double.NaN, double.NaN);
    }

    private static double? ReadZoomPercent(AppSession session) =>
        ParseNumbers(session.ReadName("ZoomReadout")).Cast<double?>().FirstOrDefault();

    private static void WaitForZoomPercent(AppSession session, double percent) =>
        AppSession.WaitFor(
            () => ReadZoomPercent(session),
            zoom => zoom is not null && Math.Abs(zoom.Value - percent) < 0.5,
            ActionTimeout,
            $"zoom readout {percent}%");

    private static (double Width, double Level) ReadWindowLevel(AppSession session)
    {
        var numbers = ParseNumbers(session.ReadName("WindowLevelReadout")).ToArray();
        return numbers.Length == 2 ? (numbers[0], numbers[1]) : (double.NaN, double.NaN);
    }

    private static (double Width, double Level) WaitForWindowLevelRead(AppSession session) =>
        AppSession.WaitFor(
            () => ReadWindowLevel(session),
            wl => !double.IsNaN(wl.Width) && !double.IsNaN(wl.Level),
            ActionTimeout,
            "window/level readout to be readable before dragging");

    private static void WaitForWindowLevel(AppSession session, double width, double level) =>
        AppSession.WaitFor(
            () => ReadWindowLevel(session),
            wl => Math.Abs(wl.Width - width) < 0.01 && Math.Abs(wl.Level - level) < 0.01,
            ActionTimeout,
            $"window/level readout W {width} / L {level}");

    private static void WaitForFlip(AppSession session, bool horizontal, bool vertical) =>
        AppSession.WaitFor(
            () => ParseFlip(session.ReadName("FlipReadout")),
            flip => flip == (horizontal, vertical),
            ActionTimeout,
            $"flip readout H {horizontal} V {vertical}");

    private static (bool Horizontal, bool Vertical) ParseFlip(string text)
    {
        var match = Regex.Match(text, @"H: (True|False) V: (True|False)");
        return match.Success
            ? (match.Groups[1].Value == "True", match.Groups[2].Value == "True")
            : (false, false);
    }

    private static int? ParseInt(string text) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;

    private static IEnumerable<double> ParseNumbers(string text) =>
        Regex.Matches(text, @"-?\d+(?:\.\d+)?")
            .Select(m => double.Parse(m.Value, NumberStyles.Float, CultureInfo.InvariantCulture));
}
