// 模块：SkyMusic.Backend.Tests 统一播放器异步回归测试
using SkyMusic.Core.Importing;
using SkyMusic.Core.Mapping;
using SkyMusic.Core.Models;
using SkyMusic.Core.Playback;
using SkyMusic.Core.Services;
using SkyMusic.Infrastructure.Playback;

namespace SkyMusic.Backend.Tests;

public sealed class UnifiedPlaybackControllerTests
{
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
