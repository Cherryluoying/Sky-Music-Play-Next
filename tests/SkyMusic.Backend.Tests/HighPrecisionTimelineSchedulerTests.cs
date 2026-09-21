// 模块：SkyMusic.Backend.Tests 后端测试 HighPrecisionTimelineSchedulerTests
using System.Collections.Concurrent;
using System.Collections.Immutable;
using SkyMusic.Core.Playback;
using SkyMusic.Core.Services;
using SkyMusic.Infrastructure.Playback;

namespace SkyMusic.Backend.Tests;

public sealed class HighPrecisionTimelineSchedulerTests
{
    [Fact]
    public async Task SendsTimelineThroughOneOrderedSink()
    {
        var events = ImmutableArray.Create(
            new PlaybackEvent(0, 60, PlaybackEventType.KeyDown, 100, 0, 0),
            new PlaybackEvent(1_000, 60, PlaybackEventType.KeyUp, 0, 0, 0),
            new PlaybackEvent(1_000, 64, PlaybackEventType.KeyDown, 100, 0, 0),
            new PlaybackEvent(2_000, 64, PlaybackEventType.KeyUp, 0, 0, 0));
        var sink = new RecordingSink();
        var scheduler = new HighPrecisionTimelineScheduler();

        await scheduler.PlayAsync(new CompiledTimeline(events, 2_000), sink);

        Assert.Equal(events, sink.Events);
        Assert.Equal(1, sink.ResetCount);
        Assert.False(scheduler.IsRunning);
    }

    [Fact]
    public async Task RestoresHeldNotesWhenStartingMidScore()
    {
        var events = ImmutableArray.Create(
            new PlaybackEvent(0, 60, PlaybackEventType.KeyDown, 90, 0, 0),
            new PlaybackEvent(10_000, 60, PlaybackEventType.KeyUp, 0, 0, 0));
        var sink = new RecordingSink();

        await new HighPrecisionTimelineScheduler().PlayAsync(
            new CompiledTimeline(events, 10_000),
            sink,
            startMicroseconds: 5_000);

        var captured = sink.Events.ToArray();
        Assert.Equal(2, captured.Length);
        Assert.Equal(PlaybackEventType.KeyDown, captured[0].Type);
        Assert.Equal(5_000, captured[0].TimeMicroseconds);
        Assert.Equal(PlaybackEventType.KeyUp, captured[1].Type);
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
