// 模块：SkyMusic.Backend.Tests 后端测试 ScorePlaybackControllerTests
using System.Collections.Concurrent;
using SkyMusic.Core.Importing;
using SkyMusic.Core.Models;
using SkyMusic.Core.Playback;
using SkyMusic.Core.Services;
using SkyMusic.Infrastructure.Playback;

namespace SkyMusic.Backend.Tests;

public sealed class ScorePlaybackControllerTests
{
    [Fact]
    public async Task LoadsScoreAndPreservesItWhenTargetChanges()
    {
        var firstSink = new RecordingSink("First");
        var secondSink = new RecordingSink("Second");
        await using var controller = CreateController(firstSink, secondSink);

        var result = await controller.LoadAsync("score.txt");
        await controller.SelectTargetAsync("second");

        Assert.True(result.IsSuccess);
        Assert.Equal("Test score", controller.Snapshot.ScoreTitle);
        Assert.Equal(2, controller.Snapshot.NoteCount);
        Assert.Equal("second", controller.Snapshot.TargetId);
        Assert.Equal(AutoPlayState.Ready, controller.Snapshot.Session.State);
    }

    [Fact]
    public async Task PlaysLoadedScoreThroughSelectedTarget()
    {
        var firstSink = new RecordingSink("First");
        var secondSink = new RecordingSink("Second");
        await using var controller = CreateController(firstSink, secondSink);
        await controller.LoadAsync("score.txt");
        await controller.SelectTargetAsync("second");

        await controller.StartAsync();
        await WaitForState(controller, AutoPlayState.Completed);

        Assert.Empty(firstSink.Events);
        Assert.Equal(4, secondSink.Events.Count);
        Assert.Equal(1, secondSink.ResetCount);
    }

    [Fact]
    public async Task LoadsInMemoryComposerScoreWithoutTemporaryFile()
    {
        var firstSink = new RecordingSink("First");
        var secondSink = new RecordingSink("Second");
        await using var controller = CreateController(firstSink, secondSink);
        var score = new Score("Composer", string.Empty, [new NoteEvent(72, 0, 1_000)]);

        await controller.LoadScoreAsync(score);

        Assert.Equal("Composer", controller.Snapshot.ScoreTitle);
        Assert.Equal(1, controller.Snapshot.NoteCount);
        Assert.Equal(AutoPlayState.Ready, controller.Snapshot.Session.State);
    }

    [Fact]
    public async Task ActivatesSelectedWindowBeforeKeyboardPlayback()
    {
        var sink = new RecordingSink("Keyboard");
        var windowService = new FakeWindowService(
            [new GameWindowInfo(42, "Sky", "Sky", 100)]);
        await using var controller = new ScorePlaybackController(
            new FakeImporter(),
            new ScoreTimelineCompiler(),
            new HighPrecisionTimelineScheduler(),
            windowService,
            [
                new PlaybackTargetRegistration(
                    new PlaybackTarget(
                        "sky",
                        "Sky",
                        "Sky",
                        15,
                        PlaybackSinkCapabilities.ForegroundInput,
                        ["Sky"]),
                    () => sink)
            ],
            "sky");
        await controller.RefreshWindowsAsync();
        await controller.LoadAsync("score.txt");

        await controller.StartAsync();
        await WaitForState(controller, AutoPlayState.Completed);

        Assert.Equal(42, windowService.ActivatedHandle);
        Assert.Equal(4, sink.Events.Count);
    }

    [Fact]
    public async Task RecompilesCurrentScoreWithIndependentTimingSettings()
    {
        var firstSink = new RecordingSink("First");
        var secondSink = new RecordingSink("Second");
        await using var controller = CreateController(firstSink, secondSink);
        await controller.LoadAsync("first.txt");

        await controller.SetTimingAsync(new ScoreTimingSettings(50, 25));

        Assert.Equal(50, controller.Snapshot.Timing.IntervalAdjustmentMilliseconds);
        Assert.Equal(25, controller.Snapshot.Timing.KeyReleaseDelayMilliseconds);
        Assert.Equal(77_000, controller.Snapshot.Session.DurationMicroseconds);

        await controller.LoadAsync("second.txt");
        Assert.Equal(ScoreTimingSettings.Default, controller.Snapshot.Timing);
    }

    private static ScorePlaybackController CreateController(
        RecordingSink firstSink,
        RecordingSink secondSink,
        IGameWindowService? windowService = null) => new(
            new FakeImporter(),
            new ScoreTimelineCompiler(),
            new HighPrecisionTimelineScheduler(),
            windowService ?? new FakeWindowService(),
            [
                CreateRegistration("first", firstSink),
                CreateRegistration("second", secondSink)
            ],
            "first");

    private static PlaybackTargetRegistration CreateRegistration(string id, RecordingSink sink) => new(
        new PlaybackTarget(id, id, id, 88, PlaybackSinkCapabilities.SimultaneousKeys),
        () => sink);

    private static async Task WaitForState(IScorePlaybackController controller, AutoPlayState state)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (controller.Snapshot.Session.State != state)
        {
            await Task.Delay(5, timeout.Token);
        }
    }

    private sealed class FakeImporter : IScoreImportService
    {
        public IReadOnlySet<string> SupportedExtensions { get; } = new HashSet<string> { ".txt" };

        public ValueTask<ScoreImportResult> ImportAsync(
            string filePath,
            CancellationToken cancellationToken = default)
        {
            var score = new Score(
                "Test score",
                "Tester",
                [
                    new NoteEvent(60, 0, 1_000),
                    new NoteEvent(64, 1_000, 1_000)
                ]);
            return ValueTask.FromResult(new ScoreImportResult(score, []));
        }
    }

    private sealed class FakeWindowService(IReadOnlyList<GameWindowInfo>? windows = null) : IGameWindowService
    {
        public long? ActivatedHandle { get; private set; }

        public ValueTask<IReadOnlyList<GameWindowInfo>> GetAvailableWindowsAsync(
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(windows ?? (IReadOnlyList<GameWindowInfo>)[]);

        public ValueTask<bool> ActivateAsync(long handle, CancellationToken cancellationToken = default)
        {
            ActivatedHandle = handle;
            return ValueTask.FromResult(true);
        }
    }

    private sealed class RecordingSink(string name) : IPlaybackEventSink
    {
        public string Name { get; } = name;

        public PlaybackSinkCapabilities Capabilities => PlaybackSinkCapabilities.SimultaneousKeys;

        public ConcurrentQueue<PlaybackEvent> Events { get; } = new();

        public int ResetCount { get; private set; }

        public void Send(PlaybackEvent playbackEvent) => Events.Enqueue(playbackEvent);

        public void Reset() => ResetCount++;
    }
}
