// 模块：SkyMusic.Infrastructure 播放领域 HighPrecisionTimelineScheduler
using System.Diagnostics;
using SkyMusic.Core.Playback;
using SkyMusic.Core.Services;

namespace SkyMusic.Infrastructure.Playback;

public sealed class HighPrecisionTimelineScheduler : ITimelineScheduler
{
    private int _isRunning;

    public bool IsRunning => Volatile.Read(ref _isRunning) != 0;

    // 在独立长任务中按微秒时间线发送播放事件
    public Task PlayAsync(
        CompiledTimeline timeline,
        IPlaybackEventSink sink,
        double speed = 1,
        long startMicroseconds = 0,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        ArgumentNullException.ThrowIfNull(sink);

        if (!double.IsFinite(speed) || speed <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(speed), "Playback speed must be positive and finite");
        }

        if (startMicroseconds < 0 || startMicroseconds > timeline.DurationMicroseconds)
        {
            throw new ArgumentOutOfRangeException(nameof(startMicroseconds));
        }

        if (Interlocked.CompareExchange(ref _isRunning, 1, 0) != 0)
        {
            throw new InvalidOperationException("The scheduler is already running");
        }

        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => Run(
            timeline,
            sink,
            speed,
            startMicroseconds,
            cancellationToken,
            completion))
        {
            IsBackground = true,
            Name = "SkyMusic timeline scheduler",
            Priority = ThreadPriority.Highest
        };
        thread.Start();
        return completion.Task;
    }

    // 使用粗等待配合自旋等待降低事件触发抖动
    private void Run(
        CompiledTimeline timeline,
        IPlaybackEventSink sink,
        double speed,
        long startMicroseconds,
        CancellationToken cancellationToken,
        TaskCompletionSource<bool> completion)
    {
        Exception? failure = null;
        var canceled = false;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            RestoreActiveNotes(timeline, sink, startMicroseconds);

            var startIndex = FindFirstEventAtOrAfter(timeline, startMicroseconds);
            var origin = Stopwatch.GetTimestamp();

            // 绝对时间调度避免逐音符等待产生累计误差
            for (var index = startIndex; index < timeline.Events.Length; index++)
            {
                var playbackEvent = timeline.Events[index];
                var targetMicroseconds = (playbackEvent.TimeMicroseconds - startMicroseconds) / speed;
                WaitUntil(origin, targetMicroseconds, cancellationToken);
                sink.Send(playbackEvent);
            }

        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            canceled = true;
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            try
            {
                sink.Reset();
            }
            catch (Exception exception)
            {
                failure ??= exception;
            }
            finally
            {
                Volatile.Write(ref _isRunning, 0);
            }
        }

        if (failure is not null)
        {
            completion.TrySetException(failure);
        }
        else if (canceled)
        {
            completion.TrySetCanceled(cancellationToken);
        }
        else
        {
            completion.TrySetResult(true);
        }
    }

    // 从跳转位置恢复此前尚未释放的音符
    private static void RestoreActiveNotes(
        CompiledTimeline timeline,
        IPlaybackEventSink sink,
        long startMicroseconds)
    {
        if (startMicroseconds == 0)
        {
            return;
        }

        var active = new Dictionary<PlaybackKey, PlaybackEvent>();
        foreach (var playbackEvent in timeline.Events)
        {
            if (playbackEvent.TimeMicroseconds >= startMicroseconds)
            {
                break;
            }

            var key = new PlaybackKey(playbackEvent.MidiNote, playbackEvent.Track, playbackEvent.Channel);
            if (playbackEvent.Type == PlaybackEventType.KeyDown)
            {
                active[key] = playbackEvent;
            }
            else
            {
                active.Remove(key);
            }
        }

        foreach (var playbackEvent in active.Values.OrderBy(item => item.MidiNote))
        {
            sink.Send(playbackEvent with { TimeMicroseconds = startMicroseconds });
        }
    }

    private static int FindFirstEventAtOrAfter(CompiledTimeline timeline, long timeMicroseconds)
    {
        var low = 0;
        var high = timeline.Events.Length;

        while (low < high)
        {
            var middle = low + ((high - low) / 2);
            if (timeline.Events[middle].TimeMicroseconds < timeMicroseconds)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }

    // 根据剩余时间选择让出线程或短时自旋
    private static void WaitUntil(long originTimestamp, double targetMicroseconds, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var elapsedMicroseconds = Stopwatch.GetElapsedTime(originTimestamp).TotalMicroseconds;
            var remainingMicroseconds = targetMicroseconds - elapsedMicroseconds;
            if (remainingMicroseconds <= 0)
            {
                return;
            }

            if (remainingMicroseconds > 2_000)
            {
                // 长等待交给内核，最后两毫秒短暂自旋保证精度
                var waitMilliseconds = Math.Max(1, (int)(remainingMicroseconds / 1_000) - 1);
                if (cancellationToken.WaitHandle.WaitOne(waitMilliseconds))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
            else
            {
                Thread.SpinWait(64);
            }
        }
    }

    private readonly record struct PlaybackKey(int MidiNote, int Track, int Channel);
}
