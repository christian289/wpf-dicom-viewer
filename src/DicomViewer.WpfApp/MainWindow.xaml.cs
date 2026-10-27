using System.ComponentModel;

using Wpf.Ui.Controls;

namespace DicomViewer.WpfApp;

/// <summary>
/// MainWindow 코드 비하인드: 수명 연결만 한다(DataContext는 합성 루트가 설정). 닫기를 한 번 취소하고 진행 작업이 멈출 때까지 await 한 뒤 다시 닫는다.
/// MainWindow code-behind: lifecycle wiring only (the composition root sets DataContext). Closing is cancelled once, in-flight work is awaited,
/// then the window closes again, so the Dispatcher is never blocked.
/// </summary>
public partial class MainWindow : FluentWindow
{
    private bool _isClosing;
    private bool _isCloseCompleted;

    public MainWindow()
    {
        InitializeComponent();
    }

    protected override async void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);

        if (_isCloseCompleted)
        {
            return;
        }

        e.Cancel = true;
        if (_isClosing)
        {
            return;
        }

        _isClosing = true;
        try
        {
            if (DataContext is MainWindowViewModel viewModel)
            {
                await viewModel.CloseAsync();
            }
        }
        finally
        {
            _isCloseCompleted = true;

            // CloseAsync는 진행 작업이 없으면 동기적으로 끝나므로 이 연속이 아직 Closing 이벤트 안에서 실행될 수 있다.
            // 그 안에서 Close()를 호출하면 InvalidOperationException이 나므로 현재 메시지 처리 뒤로 큐에 넣는다.
            // CloseAsync completes synchronously when nothing is in flight, so this continuation may still run inside the Closing event,
            // where calling Close() throws InvalidOperationException; queue it behind the current message instead.
            await Dispatcher.InvokeAsync(Close);
        }
    }
}