// 模块：SkyMusic.Backend.Tests 统一播放器异步回归测试
using SkyMusic.Core.Importing;
using SkyMusic.Core.Mapping;
using SkyMusic.Core.Models;
using SkyMusic.Core.Playback;
using SkyMusic.Core.Services;
using SkyMusic.Infrastructure.Playback;
using SkyMusic.Core.Plugins;
using Melanchall.DryWetMidi.Core;
using System.Collections.Concurrent;
using NoteEvent = SkyMusic.Core.Models.NoteEvent;

namespace SkyMusic.Backend.Tests;

public sealed class UnifiedPlaybackControllerTests
{
    // 系统输出交接完成前，插件不能开始发声；加载耗时不得推进播放位置。
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task InstrumentSwitchClosesSystemOutputAndPreservesTransport(bool playing)
    {
        using var fixture = new MidiFixture();
        await fixture.Player.LoadAsync(fixture.Track);
        await fixture.Player.SeekAsync(TimeSpan.FromSeconds(5));
        if (playing) await fixture.Player.PlayAsync();
        fixture.Host.LoadBarrier = new(TaskCreationOptions.RunContinuationsAsynchronously);

        var switching = fixture.Player.LoadInstrumentAsync(MidiFixture.Plugin).AsTask();
        await fixture.Host.LoadEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(PlaybackState.Paused, fixture.Player.Snapshot.State);
        var captured = fixture.Player.Snapshot.Position;
        Assert.True(captured >= TimeSpan.FromSeconds(5));
        Assert.Contains("close skymusic_media", fixture.Events);
        Assert.False(fixture.Mci.Open);
        fixture.Host.LoadBarrier.SetResult();
        await switching;

        Assert.True(fixture.Player.UseExternalInstrument);
        Assert.Equal((playing, captured), fixture.Host.Transport);
        var events = fixture.Events.ToArray();
        Assert.True(Array.IndexOf(events, "close skymusic_media") < Array.IndexOf(events, "plugin.load"));
        Assert.Equal(playing ? PlaybackState.Playing : PlaybackState.Paused, fixture.Player.Snapshot.State);
    }

    [Fact]
    public async Task ExternalPlaybackNeverReopensSystemOutputAndSerializesNextTrack()
    {
        using var fixture = new MidiFixture();
        await fixture.Player.LoadAsync(fixture.Track, true);
        fixture.Host.LoadBarrier = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var switching = fixture.Player.LoadInstrumentAsync(MidiFixture.Plugin).AsTask();
        await fixture.Host.LoadEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var next = fixture.Player.LoadAsync(fixture.Track with { Id = "second" }, true).AsTask();
        Assert.False(next.IsCompleted);
        fixture.Host.LoadBarrier.SetResult();
        await Task.WhenAll(switching, next);
        await fixture.Player.PauseAsync();
        await fixture.Player.SeekAsync(TimeSpan.FromSeconds(9));
        await fixture.Player.PlayAsync();
        await fixture.Player.LoadInstrumentAsync(MidiFixture.Plugin with { Id = "other" });

        Assert.Equal("second", fixture.Player.Snapshot.Track!.Id);
        Assert.Single(fixture.Events, e => e.StartsWith("open "));
        Assert.Single(fixture.Events, e => e == "play skymusic_media");
        Assert.False(fixture.Mci.Open);
        Assert.True(fixture.Host.Transport.Playing);
        Assert.True(fixture.Host.Transport.Position >= TimeSpan.FromSeconds(9));
    }

    [Fact]
    public async Task FailedCloseDoesNotStartPluginAndCanBeRetried()
    {
        using var fixture = new MidiFixture();
        await fixture.Player.LoadAsync(fixture.Track, true);
        fixture.Mci.FailClose = true;
        await Assert.ThrowsAsync<IOException>(() => fixture.Player.LoadInstrumentAsync(MidiFixture.Plugin).AsTask());
        Assert.DoesNotContain("plugin.load", fixture.Events);
        Assert.Contains("pause skymusic_media", fixture.Events);
        Assert.Equal(PlaybackState.Paused, fixture.Player.Snapshot.State);

        fixture.Mci.FailClose = false;
        await fixture.Player.LoadInstrumentAsync(MidiFixture.Plugin);
        Assert.False(fixture.Mci.Open);
        Assert.False(fixture.Host.Transport.Playing);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedOrCanceledSwitchStaysSilentAndDoesNotFallBack(bool cancelAtStart)
    {
        using var fixture = new MidiFixture();
        await fixture.Player.LoadAsync(fixture.Track, true);
        fixture.Host.FailSequence = !cancelAtStart;
        fixture.Host.CancelAfterStart = cancelAtStart;
        await Assert.ThrowsAnyAsync<Exception>(() => fixture.Player.LoadInstrumentAsync(MidiFixture.Plugin).AsTask());
        Assert.False(fixture.Mci.Open);
        Assert.False(fixture.Host.Transport.Playing);
        Assert.Equal(PlaybackState.Paused, fixture.Player.Snapshot.State);
        Assert.True(fixture.Player.UseExternalInstrument);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Player.PlayAsync().AsTask());
        Assert.Single(fixture.Events, e => e == "play skymusic_media");
    }

    [Fact]
    public async Task SystemCommandsStayOnOneThreadAndPropagateErrors()
    {
        var threads = new ConcurrentBag<int>();
        using var dispatcher = new MciCommandDispatcher(command =>
        {
            threads.Add(Environment.CurrentManagedThreadId);
            if (command == "fail") throw new IOException("测试命令失败");
            return command;
        });
        await Task.WhenAll(Enumerable.Range(0, 12).Select(i => Task.Run(() =>
            Assert.Equal(i.ToString(), dispatcher.Execute(i.ToString())))));
        Assert.Throws<IOException>(() => dispatcher.Execute("fail"));
        Assert.Equal("close", dispatcher.Execute("close"));
        Assert.Single(threads.Distinct());
    }

    // 使用临时 MIDI 和可观测音源验证交接顺序，不依赖声卡或本机插件安装。
    private sealed class MidiFixture : IDisposable
    {
        public static readonly InstrumentPluginInfo Plugin = new("test", "测试音色", "test.vst3", "VST3");
        public ConcurrentQueue<string> Events { get; } = new();
        public FakeMci Mci { get; }
        public FakeInstrument Host { get; }
        public UnifiedPlaybackController Player { get; }
        public MusicTrack Track { get; }

        public MidiFixture()
        {
            var path = Path.Combine(Path.GetTempPath(), $"SkyMusic-route-{Guid.NewGuid():N}.mid");
            new MidiFile(new TrackChunk(new TextEvent("尾部休止") { DeltaTime = 57_600 }))
            { TimeDivision = new TicksPerQuarterNoteTimeDivision(480) }.Write(path);
            Track = new("midi", "测试", "", "", "", TimeSpan.FromSeconds(60), [], MediaKind.Midi, path);
            Mci = new(Events);
            Host = new(Events);
            Player = new(new DelayedScorePlaybackController(), null, Host, Mci);
        }

        public void Dispose()
        {
            Mci.FailClose = false;
            Player.Dispose();
            File.Delete(Track.SourcePath!);
        }
    }

    private sealed class FakeMci(ConcurrentQueue<string> events) : IMciCommands
    {
        public bool Open { get; private set; }
        public bool FailClose { get; set; }
        public string Execute(string command)
        {
            events.Enqueue(command);
            if (command.StartsWith("open ")) Open = true;
            else
            {
                Assert.True(Open, "不能向已关闭的系统音源发送命令");
                if (command.StartsWith("close "))
                {
                    if (FailClose) throw new IOException("模拟设备关闭失败");
                    Open = false;
                }
            }
            return "60000";
        }
        public void Dispose() { }
    }

    private sealed class FakeInstrument(ConcurrentQueue<string> events) : IInstrumentPluginHost, IMidiSequenceHost
    {
        public InstrumentHostSnapshot Snapshot { get; private set; } = new(InstrumentHostState.Stopped);
        public event Action<InstrumentHostSnapshot>? Changed;
        public TaskCompletionSource LoadEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource? LoadBarrier { get; set; }
        public bool FailSequence { get; set; }
        public bool CancelAfterStart { get; set; }
        public (bool Playing, TimeSpan Position) Transport { get; private set; }

        public async ValueTask LoadAsync(InstrumentPluginInfo plugin, CancellationToken cancellationToken = default)
        {
            events.Enqueue("plugin.load");
            LoadEntered.TrySetResult();
            if (LoadBarrier is not null) await LoadBarrier.Task.WaitAsync(cancellationToken);
            Snapshot = new(InstrumentHostState.Loaded, plugin);
            Changed?.Invoke(Snapshot);
        }
        public ValueTask LoadSequenceAsync(string path, CancellationToken cancellationToken = default)
        {
            if (FailSequence) throw new IOException("模拟时间线加载失败");
            return ValueTask.CompletedTask;
        }
        public ValueTask SetTransportAsync(bool playing, TimeSpan? position = null, CancellationToken cancellationToken = default)
        {
            Transport = (playing, position ?? TimeSpan.Zero);
            events.Enqueue(playing ? "plugin.play" : "plugin.pause");
            if (playing && CancelAfterStart) throw new OperationCanceledException();
            return ValueTask.CompletedTask;
        }
        public ValueTask UnloadAsync(CancellationToken cancellationToken = default)
        {
            Transport = (false, Transport.Position);
            Snapshot = new(InstrumentHostState.Ready);
            return ValueTask.CompletedTask;
        }
        public IReadOnlyList<InstrumentPluginInfo> DiscoverPlugins(IEnumerable<string> searchPaths) => [];
        public ValueTask NoteOnAsync(int note, byte velocity = 100, int channel = 0, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask NoteOffAsync(int note, byte velocity = 0, int channel = 0, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask AllNotesOffAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Fact]
    public async Task ScoreLoadDoesNotSynchronouslyBlockCaller()
    {
        var score = new DelayedScorePlaybackController();
        using var controller = new UnifiedPlaybackController(score);
        var track = new MusicTrack(
            "score-1",
            "测试谱面",
            "测试作者",
            string.Empty,
            string.Empty,
            TimeSpan.FromSeconds(1),
            [],
            MediaKind.Score,
            "score.txt");

        var loadTask = controller.LoadAsync(track, autoplay: true).AsTask();

        Assert.False(loadTask.IsCompleted);
        score.CompleteLoad();
        await loadTask.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.Equal(1, score.StartCount);
    }

    private sealed class DelayedScorePlaybackController : IScorePlaybackController
    {
        private readonly TaskCompletionSource<ScoreImportResult> _loadCompletion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int StartCount { get; private set; }

        public IReadOnlyList<PlaybackTarget> Targets => [];

        public IReadOnlyList<GameWindowInfo> Windows => [];

        public GameWindowInfo? SelectedWindow => null;

        public ScorePlaybackSnapshot Snapshot { get; } = new(
            "测试谱面",
            "test",
            1,
            new AutoPlaySnapshot(AutoPlayState.Ready, 0, 1_000_000, 1),
            [],
            ScoreTimingSettings.Default);

        public event Action<ScorePlaybackSnapshot>? Changed;

        public void CompleteLoad() => _loadCompletion.TrySetResult(new ScoreImportResult(
            new Score("测试谱面", "测试作者", [new NoteEvent(60, 0, 100_000)]),
            []));

        public async ValueTask<ScoreImportResult> LoadAsync(
            string filePath,
            CancellationToken cancellationToken = default) =>
            await _loadCompletion.Task.WaitAsync(cancellationToken);

        public ValueTask LoadScoreAsync(Score score, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask SelectTargetAsync(string targetId, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask RefreshWindowsAsync(CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public void SelectWindow(long handle)
        {
        }

        public ValueTask StartAsync(CancellationToken cancellationToken = default)
        {
            StartCount++;
            Changed?.Invoke(Snapshot with
            {
                Session = Snapshot.Session with { State = AutoPlayState.Playing }
            });
            return ValueTask.CompletedTask;
        }

        public ValueTask PauseAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask StopAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask SeekAsync(long positionMicroseconds, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask SetSpeedAsync(double speed, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask SetTimingAsync(
            ScoreTimingSettings timing,
            CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask ReloadCustomKeyMappingsAsync(
            IReadOnlyList<KeyMappingDefinition> mappings,
            CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
