// 模块：SkyMusic.Backend.Tests 后端测试 AutoPlaySessionTests
using System.Collections.Concurrent;
using System.Collections.Immutable;
using SkyMusic.Core.Playback;
using SkyMusic.Core.Services;
using SkyMusic.Infrastructure.Playback;

namespace SkyMusic.Backend.Tests;

public sealed class AutoPlaySessionTests
{
    [Fact]
    public async Task CompletesLoadedTimelineAndReportsProgress()
    {
        var timeline = CreateTimeline(3_000);
        var sink = new RecordingSink();
        await using var session = new AutoPlaySession(new HighPrecisionTimelineScheduler(), sink);

        await session.LoadAsync(timeline);
        await session.StartAsync();
        await WaitForState(session, AutoPlayState.Completed);

        Assert.Equal(1, session.Snapshot.Progress);
        Assert.Equal(2, sink.Events.Count);
        Assert.True(sink.ResetCount >= 1);
    }

    [Fact]
    public async Task PauseAndResumeContinueFromCurrentPosition()
    {
        var timeline = CreateTimeline(100_000);
        await using var session = new AutoPlaySession(new HighPrecisionTimelineScheduler(), new RecordingSink());
        await session.LoadAsync(timeline);

        await session.StartAsync();
        await Task.Delay(20);
        await session.PauseAsync();
        var pausedAt = session.Snapshot.PositionMicroseconds;

        Assert.Equal(AutoPlayState.Paused, session.Snapshot.State);
        Assert.InRange(pausedAt, 1, 99_999);

        await session.StartAsync();
        await WaitForState(session, AutoPlayState.Completed);
        Assert.Equal(100_000, session.Snapshot.PositionMicroseconds);
    }

    private static CompiledTimeline CreateTimeline(long duration) => new(
        ImmutableArray.Create(
            new PlaybackEvent(0, 60, PlaybackEventType.KeyDown, 100, 0, 0),
            new PlaybackEvent(duration, 60, PlaybackEventType.KeyUp, 0, 0, 0)),
        duration);

    private static async Task WaitForState(IAutoPlaySession session, AutoPlayState state)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (session.Snapshot.State != state)
        {
            await Task.Delay(5, timeout.Token);
        }
    }

    private sealed class RecordingSink : IPlaybackEventSink
    {
        public string Name => "Recording";

        public PlaybackSinkCapabilities Capabilities => PlaybackSinkCapabilities.None;

        public ConcurrentQueue<PlaybackEvent> Events { get; } = new();

        public int ResetCount { get; private set; }

        public void Send(PlaybackEvent playbackEvent) => Events.Enqueue(playbackEvent);

        public void Reset() => ResetCount++;
    }
}
