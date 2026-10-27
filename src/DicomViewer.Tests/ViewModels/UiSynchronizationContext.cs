using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;

namespace DicomViewer.Tests.ViewModels;

/// <summary>
/// 전용 스레드 하나로 동작하는 UI 흉내 동기화 컨텍스트. ViewModel은 이 컨텍스트에서 await 연속을 받으므로
/// "UI 컨텍스트에서만 상태가 바뀐다"는 계약을 스레드 ID로 확인할 수 있다.
/// A UI-like synchronization context backed by one dedicated thread. The ViewModel receives its await continuations here,
/// so the contract "state only changes on the UI context" can be checked by thread id.
/// </summary>
internal sealed class UiSynchronizationContext : SynchronizationContext, IDisposable
{
    private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _queue = [];
    private readonly Thread _thread;
    private TaskCompletionSource? _nextPost;

    public UiSynchronizationContext()
    {
        _thread = new Thread(Loop) { IsBackground = true, Name = "Test UI thread" };
        _thread.Start();
    }

    public int ThreadId => _thread.ManagedThreadId;

    public override void Post(SendOrPostCallback d, object? state)
    {
        _queue.Add((d, state));
        Interlocked.Exchange(ref _nextPost, null)?.TrySetResult();
    }

    /// <summary>
    /// 다음에 이 컨텍스트로 Post되는 연속이 큐에 들어갈 때까지 기다린다. 스레드 풀 작업의 await 연속이 도착했음을 sleep 없이 확정하는 데 쓴다.
    /// Completes when the next continuation is posted to this context. Establishes, without sleeping, that the await continuation of
    /// a thread-pool operation has arrived (a following <see cref="DrainAsync"/> then runs it).
    /// </summary>
    public Task NextPostAsync()
    {
        var next = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _nextPost = next;
        return next.Task;
    }

    public override void Send(SendOrPostCallback d, object? state)
    {
        if (Environment.CurrentManagedThreadId == ThreadId)
        {
            d(state);
            return;
        }

        using var done = new ManualResetEventSlim();
        ExceptionDispatchInfo? error = null;
        Post(_ =>
        {
            try
            {
                d(state);
            }
            catch (Exception ex)
            {
                error = ExceptionDispatchInfo.Capture(ex);
            }
            finally
            {
                done.Set();
            }
        }, null);
        done.Wait();
        error?.Throw();
    }

    /// <summary>
    /// 비동기 본문을 UI 스레드에서 실행한다. 본문의 예외(단언 실패 포함)는 반환된 작업으로 전달된다.
    /// Runs an asynchronous body on the UI thread. Exceptions of the body, including assertion failures, flow to the returned task.
    /// </summary>
    public Task RunAsync(Func<Task> body)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Post(async _ =>
        {
            try
            {
                await body();
                completion.SetResult();
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        }, null);

        return completion.Task;
    }

    /// <summary>
    /// 이 호출 이전에 UI 큐에 들어간 연속이 모두 실행될 때까지 기다린다 (큐는 FIFO).
    /// Waits until every continuation queued on the UI context before this call has run (the queue is FIFO).
    /// </summary>
    public async Task DrainAsync()
    {
        var drained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Post(_ => drained.SetResult(), null);
        await drained.Task;
    }

    public void Dispose()
    {
        _queue.CompleteAdding();
        _thread.Join();
    }

    private void Loop()
    {
        SetSynchronizationContext(this);
        foreach (var (callback, state) in _queue.GetConsumingEnumerable())
        {
            callback(state);
        }
    }
}