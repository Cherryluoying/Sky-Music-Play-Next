// 模块：SkyMusic.Infrastructure 播放领域 AutoPlaySession
using System.Diagnostics;
using SkyMusic.Core.Playback;
using SkyMusic.Core.Services;

namespace SkyMusic.Infrastructure.Playback;

public sealed class AutoPlaySession : IAutoPlaySession
{
    private readonly object _stateSync = new();
    private readonly SemaphoreSlim _commandGate = new(1, 1);
    private readonly ITimelineScheduler _scheduler;
    private readonly IPlaybackEventSink _sink;

    private CompiledTimeline? _timeline;
    private CancellationTokenSource? _runCancellation;
    private Task? _runTask;
    private readonly Timer _progressTimer;
    private AutoPlayState _state = AutoPlayState.Empty;
    private long _positionMicroseconds;
    private long _runStartedTimestamp;
    private double _speed = 1;
    private string? _error;
    private bool _disposed;

    public AutoPlaySession(ITimelineScheduler scheduler, IPlaybackEventSink sink)
    {
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        _progressTimer = new Timer(OnProgressTimer, null, 100, 100);
    }

    public event Action<AutoPlaySnapshot>? Changed;

    public AutoPlaySnapshot Snapshot
    {
        get
        {
            lock (_stateSync)
            {
                return CreateSnapshot();
            }
        }
    }

    // 替换当前时间线并重置会话状态
    public async ValueTask LoadAsync(CompiledTimeline timeline, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        ThrowIfDisposed();

        await StopAsync(cancellationToken).ConfigureAwait(false);
        await _commandGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            lock (_stateSync)
            {
                _timeline = timeline;
                _positionMicroseconds = 0;
                _error = null;
                _state = AutoPlayState.Ready;
            }
        }
        finally
        {
            _commandGate.Release();
        }

        Publish();
    }

    // 从当前位置启动唯一的调度任务
    public async ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _commandGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            CompiledTimeline timeline;
            CancellationTokenSource runCancellation;
            long startPosition;
            double speed;

            lock (_stateSync)
            {
                if (_state == AutoPlayState.Playing)
                {
                    return;
                }

                timeline = _timeline ?? throw new InvalidOperationException("No timeline is loaded");
                if (_positionMicroseconds >= timeline.DurationMicroseconds)
                {
                    _positionMicroseconds = 0;
                }

                startPosition = _positionMicroseconds;
                speed = _speed;
                _error = null;
                _state = AutoPlayState.Playing;
                _runStartedTimestamp = Stopwatch.GetTimestamp();
                runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _runCancellation = runCancellation;
                _runTask = RunAsync(timeline, startPosition, speed, runCancellation);
            }
        }
        finally
        {
            _commandGate.Release();
        }

        Publish();
    }

    public async ValueTask PauseAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var runTask = await CancelRunAsync(AutoPlayState.Paused, resetPosition: false, cancellationToken).ConfigureAwait(false);
        if (runTask is not null)
        {
            await runTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var runTask = await CancelRunAsync(
            _timeline is null ? AutoPlayState.Empty : AutoPlayState.Ready,
            resetPosition: true,
            cancellationToken).ConfigureAwait(false);
        if (runTask is not null)
        {
            await runTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask SeekAsync(long positionMicroseconds, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var restart = Snapshot.State == AutoPlayState.Playing;
        if (restart)
        {
            await PauseAsync(cancellationToken).ConfigureAwait(false);
        }

        await _commandGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            lock (_stateSync)
            {
                var timeline = _timeline ?? throw new InvalidOperationException("No timeline is loaded");
                if (positionMicroseconds < 0 || positionMicroseconds > timeline.DurationMicroseconds)
                {
                    throw new ArgumentOutOfRangeException(nameof(positionMicroseconds));
                }

                _positionMicroseconds = positionMicroseconds;
                _error = null;
                _state = positionMicroseconds == timeline.DurationMicroseconds
                    ? AutoPlayState.Completed
                    : AutoPlayState.Paused;
            }
        }
        finally
        {
            _commandGate.Release();
        }

        Publish();
        if (restart && positionMicroseconds < Snapshot.DurationMicroseconds)
        {
            await StartAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask SetSpeedAsync(double speed, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (!double.IsFinite(speed) || speed <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(speed), "Playback speed must be positive and finite");
        }

        var restart = Snapshot.State == AutoPlayState.Playing;
        if (restart)
        {
            await PauseAsync(cancellationToken).ConfigureAwait(false);
        }

        lock (_stateSync)
        {
            _speed = speed;
        }

        Publish();
        if (restart)
        {
            await StartAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await StopAsync().ConfigureAwait(false);
        _disposed = true;
        await _progressTimer.DisposeAsync().ConfigureAwait(false);
        _runCancellation?.Dispose();
        _commandGate.Dispose();
    }

    // 取消旧调度任务并在锁外等待其退出
    private async ValueTask<Task?> CancelRunAsync(
        AutoPlayState nextState,
        bool resetPosition,
        CancellationToken cancellationToken)
    {
        await _commandGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        Task? runTask;
        try
        {
            lock (_stateSync)
            {
                if (_state == AutoPlayState.Playing)
                {
                    _positionMicroseconds = CurrentPosition();
                }

                if (resetPosition)
                {
                    _positionMicroseconds = 0;
                }

                _state = nextState;
                _error = null;
                _runCancellation?.Cancel();
                runTask = _runTask;
            }
        }
        finally
        {
            _commandGate.Release();
        }

        Publish();
        return runTask;
    }

    // 运行时间线并把结束状态安全发布给界面
    private async Task RunAsync(
        CompiledTimeline timeline,
        long startPosition,
        double speed,
        CancellationTokenSource runCancellation)
    {
        Exception? failure = null;
        var canceled = false;

        try
        {
            await _scheduler.PlayAsync(timeline, _sink, speed, startPosition, runCancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (runCancellation.IsCancellationRequested)
        {
            canceled = true;
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        await _commandGate.WaitAsync().ConfigureAwait(false);
        try
        {
            lock (_stateSync)
            {
                if (!ReferenceEquals(_runCancellation, runCancellation))
                {
                    return;
                }

                if (_state == AutoPlayState.Playing)
                {
                    if (failure is not null)
                    {
                        _positionMicroseconds = CurrentPosition();
                        _error = failure.Message;
                        _state = AutoPlayState.Faulted;
                    }
                    else if (canceled)
                    {
                        _positionMicroseconds = CurrentPosition();
                        _state = AutoPlayState.Paused;
                    }
                    else
                    {
                        _positionMicroseconds = timeline.DurationMicroseconds;
                        _state = AutoPlayState.Completed;
                    }
                }

                _runCancellation = null;
                _runTask = null;
            }

            runCancellation.Dispose();
        }
        finally
        {
            _commandGate.Release();
        }

        Publish();
    }

    private AutoPlaySnapshot CreateSnapshot()
    {
        var duration = _timeline?.DurationMicroseconds ?? 0;
        var position = _state == AutoPlayState.Playing ? CurrentPosition() : _positionMicroseconds;
        return new AutoPlaySnapshot(_state, Math.Min(position, duration), duration, _speed, _error);
    }

    private long CurrentPosition()
    {
        var elapsed = Stopwatch.GetElapsedTime(_runStartedTimestamp).TotalMicroseconds;
        return checked(_positionMicroseconds + (long)(elapsed * _speed));
    }

    private void Publish() => Changed?.Invoke(Snapshot);

    private void OnProgressTimer(object? _)
    {
        if (!_disposed && Snapshot.State == AutoPlayState.Playing)
        {
            Publish();
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
