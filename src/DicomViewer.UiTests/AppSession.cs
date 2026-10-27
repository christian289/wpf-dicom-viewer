using System.Diagnostics;
using System.Globalization;
using System.Drawing;
using System.Runtime.InteropServices;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.UIA3;

namespace DicomViewer.UiTests;

/// <summary>
/// 이 테스트가 직접 시작한 앱 프로세스 하나만 소유한다. 다른 창이나 데스크톱 전체는 다루지 않는다.
/// Owns exactly one app process started by this test. Never touches other windows or the whole desktop.
/// </summary>
public sealed class AppSession : IDisposable
{
    public const string ExeVariable = "DICOM_VIEWER_APP_EXE";

    /// <summary>테스트 전용 수동 활성화 대기(초, 기본 0). / Test-only manual activation wait in seconds (default 0).</summary>
    public const string ManualWaitVariable = "DICOM_VIEWER_UI_MANUAL_FOREGROUND_WAIT_SECONDS";

    private readonly UIA3Automation _automation;
    private readonly Process _process;
    private readonly Application _application;

    private AppSession(UIA3Automation automation, Process process, Application application, Window window)
    {
        _automation = automation;
        _process = process;
        _application = application;
        Window = window;
    }

    public Window Window { get; }

    public int ProcessId => _process.Id;

    /// <summary>소유 창의 DPI (GetDpiForWindow). / DPI of the owned window.</summary>
    public uint WindowDpi => NativeMethods.GetDpiForWindow(Window.Properties.NativeWindowHandle.ValueOrDefault);

    /// <summary>테스트 프로세스가 DPI 인식인지. / Whether the test process is DPI-aware.</summary>
    public static bool TestProcessIsDpiAware => NativeMethods.IsProcessDPIAware();

    /// <summary>GetSystemMetrics 화면 폭·높이(테스트 프로세스 좌표 공간). / Screen size in the test process coordinate space.</summary>
    public static (int Width, int Height) ScreenSize => (NativeMethods.GetSystemMetrics(0), NativeMethods.GetSystemMetrics(1));

    public static AppSession Start(TimeSpan startupTimeout)
    {
        var exe = Environment.GetEnvironmentVariable(ExeVariable);
        if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe))
        {
            throw new InvalidOperationException($"{ExeVariable} must point to an existing built DicomViewer.WpfApp.exe.");
        }

        var startInfo = new ProcessStartInfo(Path.GetFullPath(exe))
        {
            UseShellExecute = false,
            // 작업 디렉터리를 저장소 밖 임시 폴더로 둔다. 앱이 현재 디렉터리에 의존하지 않음을 확인하기 위함이다.
            // Working directory is a temp folder outside the repo, to prove the app does not depend on the current directory.
            WorkingDirectory = Path.GetTempPath(),
        };

        var automation = new UIA3Automation();
        // 소유 Process를 직접 시작하고, FlaUI는 그 PID에 붙인다. 종료 코드는 이 Process에서만 읽는다.
        // Start the owned Process ourselves and attach FlaUI by its PID. Exit state is read only from this Process.
        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("The owned app process could not be started.");
        var application = Application.Attach(process.Id);
        try
        {
            var window = application.GetMainWindow(automation, startupTimeout)
                ?? throw new TimeoutException("The owned app did not expose a main window within the timeout.");
            return new AppSession(automation, process, application, window);
        }
        catch
        {
            // 실패 정리는 이 테스트가 시작한 프로세스에만 적용한다. / Failed cleanup applies only to the process this test started.
            if (!process.HasExited)
            {
                process.Kill();
            }

            automation.Dispose();
            process.Dispose();
            throw;
        }
    }

    /// <summary>없으면 null. 읽기 전용 폴링에서 사용한다. / Returns null when missing. Used by read-only polling.</summary>
    public AutomationElement? TryFind(string automationId) => Window.FindFirstDescendant(automationId);

    /// <summary>없으면 실패한다. 조작(동작)에서 사용한다. / Throws when missing. Used by actions.</summary>
    public AutomationElement Find(string automationId) =>
        TryFind(automationId)
        ?? throw new InvalidOperationException($"AutomationId '{automationId}' was not found in the owned window.");

    /// <summary>아직 생성되지 않았으면 빈 문자열. 대기 조건이 계속 다시 읽도록 한다. / Empty when not created yet, so waits keep polling.</summary>
    public string ReadName(string automationId) => TryFind(automationId)?.Name ?? string.Empty;

    public bool IsShown(string automationId)
    {
        var element = TryFind(automationId);
        return element is not null && !element.IsOffscreen;
    }

    public int CountById(string automationId) =>
        Window.FindAllDescendants(cf => cf.ByAutomationId(automationId)).Length;

    public IReadOnlyList<string> NamesById(string automationId) =>
        Window.FindAllDescendants(cf => cf.ByAutomationId(automationId))
            .Select(e => e.Name ?? string.Empty)
            .ToArray();

    /// <summary>
    /// 소유 창 영역만 캡처해 임시 폴더에 저장한다. 전체 데스크톱은 캡처하지 않는다.
    /// Captures only the owned window rectangle to a temp file. Never captures the whole desktop.
    /// </summary>
    public void CaptureOwnedWindow(string path)
    {
        RequireOwnedForeground();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        Window.CaptureToFile(path);
    }

    /// <summary>
    /// 소유 창만 복원·활성화를 시도하고(최대 3회), 매번 전경 PID가 소유 PID인지 확인한다.
    /// 실패하면 입력을 보내지 않고 일반적인 차단 메시지와 읽기 전용 진단을 던진다. 다른 프로세스의 이름이나 PID는 기록하지 않는다.
    /// Tries bounded activation of the owned window only (up to 3 attempts), checking the foreground PID each time.
    /// On failure it sends no input and throws a generic blocker with read-only diagnostics. Other processes are not named or logged.
    /// </summary>
    public void RequireOwnedForeground()
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            RestoreOwnedWindowIfMinimized();
            Window.SetForeground();
            if (WaitForOwnedForeground(TimeSpan.FromSeconds(1)))
            {
                return;
            }
        }

        if (ManualWaitSeconds() is var manualSeconds and > 0 && WaitForManualActivation(manualSeconds))
        {
            return;
        }

        throw new InvalidOperationException(
            "INTERACTIVE BLOCKER: the owned window did not become the foreground window within the allowed time; no input was sent. " +
            ForegroundDiagnostics());
    }

    /// <summary>
    /// 테스트 전용 옵션: 환경 변수로 지정한 초(최대 600) 동안 사용자가 소유 창을 전경으로 올리기를 읽기 전용으로 기다린다.
    /// 기본값은 0(기다리지 않음). 다른 창 활성화, 키 입력, 전경 잠금 우회는 하지 않는다.
    /// Test-only option: read-only wait (up to 600 s from the env var) for the user to bring the owned window to foreground.
    /// Default 0 (no wait). Never activates other windows, sends keys, or bypasses the foreground lock.
    /// </summary>
    public static int ManualWaitSeconds()
    {
        var raw = Environment.GetEnvironmentVariable(ManualWaitVariable);
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
            ? Math.Clamp(seconds, 0, 600)
            : 0;
    }

    private bool WaitForManualActivation(int seconds)
    {
        var hwnd = Window.Properties.NativeWindowHandle.ValueOrDefault;
        var status = Path.Combine(Path.GetTempPath(), "dicom-viewer-ui-evidence", "manual-wait.status");
        Directory.CreateDirectory(Path.GetDirectoryName(status)!);
        Report(status, $"waiting ownedPid={ProcessId} hwnd=0x{hwnd.ToInt64():X} waitSeconds={seconds}");

        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < deadline)
        {
            if (NativeMethods.GetForegroundProcessId() == (uint)ProcessId)
            {
                Report(status, $"activated ownedPid={ProcessId} hwnd=0x{hwnd.ToInt64():X}");
                return true;
            }

            Thread.Sleep(250);
        }

        Report(status, $"timeout ownedPid={ProcessId} hwnd=0x{hwnd.ToInt64():X} waitSeconds={seconds}");
        return false;
    }

    private static void Report(string statusFile, string line)
    {
        Console.WriteLine($"[manual-wait] {line}");
        File.WriteAllText(statusFile, line + Environment.NewLine);
    }

    private bool WaitForOwnedForeground(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (NativeMethods.GetForegroundProcessId() == (uint)ProcessId)
            {
                return true;
            }

            Thread.Sleep(100);
        }

        return NativeMethods.GetForegroundProcessId() == (uint)ProcessId;
    }

    private void RestoreOwnedWindowIfMinimized()
    {
        if (NativeMethods.IsIconic(Window.Properties.NativeWindowHandle.ValueOrDefault))
        {
            Window.Patterns.Window.Pattern.SetWindowVisualState(FlaUI.Core.Definitions.WindowVisualState.Normal);
        }
    }

    /// <summary>읽기 전용 진단: 최소화 여부, 전경 잠금 시간 제한, 마지막 사용자 입력 이후 경과 시간. / Read-only: iconic state, foreground lock timeout, idle time since last user input.</summary>
    public string ForegroundDiagnostics() =>
        $"iconic={NativeMethods.IsIconic(Window.Properties.NativeWindowHandle.ValueOrDefault)}; " +
        $"foregroundLockTimeoutMs={NativeMethods.ForegroundLockTimeoutMs()}; " +
        $"lastInputIdleMs={NativeMethods.LastInputIdleMs()}";

    /// <summary>소유 창 안의 요소를 클릭한다. / Clicks an element inside the owned window only.</summary>
    public void Click(string automationId)
    {
        var element = Find(automationId);
        RequireOwnedForeground();
        var rect = element.BoundingRectangle;
        var center = new Point((int)(rect.Left + rect.Width / 2), (int)(rect.Top + rect.Height / 2));
        RequireInsideWindow(center);
        element.Click(false);
    }

    /// <summary>
    /// 소유 창 안에서만 왼쪽 버튼 드래그를 수행한다. 버튼은 finally에서 항상 해제한다.
    /// Left-button drag confined to the owned window. The button is always released in finally.
    /// </summary>
    public void DragLeft(Point start, Point end)
    {
        RequireOwnedForeground();
        RequireInsideWindow(start);
        RequireInsideWindow(end);
        try
        {
            Mouse.MoveTo(start);
            Mouse.Down(MouseButton.Left);
            Mouse.MoveTo(new Point((start.X + end.X) / 2, (start.Y + end.Y) / 2));
            Mouse.MoveTo(end);
        }
        finally
        {
            Mouse.Up(MouseButton.Left);
        }
    }

    /// <summary>
    /// UIA RangeValue 패턴으로 슬라이더 값을 설정한다(마우스 입력 아님). / Sets a slider via the UIA RangeValue pattern (not mouse input).
    /// </summary>
    public void SetRangeValue(string automationId, double value) =>
        Find(automationId).Patterns.RangeValue.Pattern.SetValue(value);

    public double ReadRangeValue(string automationId) =>
        Find(automationId).Patterns.RangeValue.Pattern.Value.Value;

    /// <summary>
    /// 조건을 만족할 때까지 짧게 폴링한다. 시간 초과 시 마지막 관찰 값을 포함해 실패한다.
    /// Polls briefly until the condition holds. On timeout, fails with the last observed value.
    /// </summary>
    public static T WaitFor<T>(Func<T> read, Func<T, bool> isReady, TimeSpan timeout, string description)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            var last = read();
            if (isReady(last))
            {
                return last;
            }

            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException($"Timed out waiting for {description}. Last observed: '{last}'.");
            }

            Thread.Sleep(100);
        }
    }

    /// <summary>
    /// 창 닫기를 요청하고 종료를 기다린다. 종료하지 않으면 시간 초과로 실패한다.
    /// Requests a window close and waits for exit. Fails with a timeout if the process does not exit.
    /// </summary>
    public (bool Graceful, int ExitCode) RequestClose(TimeSpan timeout)
    {
        // FlaUI는 종료 요청에만 쓰고, 종료 상태와 코드는 이 테스트가 시작한 Process에서만 읽는다.
        // FlaUI is used only to request close; exit state and code are read only from the Process this test started.
        var graceful = _application.Close(killIfCloseFails: false);
        WaitFor(() => _process.HasExited, exited => exited, timeout, "the owned process to exit");
        return (graceful, _process.ExitCode);
    }

    public void Dispose()
    {
        try
        {
            // 아직 살아 있을 때만 정리한다. 강제 종료는 이 테스트가 시작한 프로세스에만 적용된다.
            // Clean up only if still alive. Forced termination applies only to the process this test started.
            if (!_process.HasExited)
            {
                _application.Close(killIfCloseFails: true);
            }
        }
        finally
        {
            _application.Dispose();
            _automation.Dispose();
            _process.Dispose();
        }
    }

    private void RequireInsideWindow(Point point)
    {
        var r = Window.BoundingRectangle;
        if (point.X < r.Left || point.X >= r.Right || point.Y < r.Top || point.Y >= r.Bottom)
        {
            throw new InvalidOperationException($"Point {point} is outside the owned window; refusing input.");
        }
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        public static extern uint GetDpiForWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool IsProcessDPIAware();

        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int index);
        [DllImport("user32.dll")]
        public static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(uint action, uint param, out uint value, uint winIni);

        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LastInputInfo info);

        [StructLayout(LayoutKind.Sequential)]
        private struct LastInputInfo
        {
            public uint Size;
            public uint Time;
        }

        private const uint SpiGetForegroundLockTimeout = 0x2000;

        public static uint ForegroundLockTimeoutMs()
        {
            return SystemParametersInfo(SpiGetForegroundLockTimeout, 0, out var value, 0) ? value : uint.MaxValue;
        }

        public static uint LastInputIdleMs()
        {
            var info = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
            if (!GetLastInputInfo(ref info))
            {
                return uint.MaxValue;
            }

            return unchecked((uint)Environment.TickCount) - info.Time;
        }

        public static uint GetForegroundProcessId()
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
            {
                return 0;
            }

            GetWindowThreadProcessId(hwnd, out var pid);
            return pid;
        }
    }
}
