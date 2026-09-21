// 模块：SkyMusic.Core 桌面服务 IScorePlaybackController
using SkyMusic.Core.Importing;
using SkyMusic.Core.Models;
using SkyMusic.Core.Playback;

namespace SkyMusic.Core.Services;

public interface IScorePlaybackController : IAsyncDisposable
{
    IReadOnlyList<PlaybackTarget> Targets { get; }

    IReadOnlyList<GameWindowInfo> Windows { get; }

    GameWindowInfo? SelectedWindow { get; }

    ScorePlaybackSnapshot Snapshot { get; }

    event Action<ScorePlaybackSnapshot>? Changed;

    ValueTask<ScoreImportResult> LoadAsync(string filePath, CancellationToken cancellationToken = default);

    ValueTask LoadScoreAsync(Score score, CancellationToken cancellationToken = default);

    ValueTask SelectTargetAsync(string targetId, CancellationToken cancellationToken = default);

    ValueTask RefreshWindowsAsync(CancellationToken cancellationToken = default);

    void SelectWindow(long handle);

    ValueTask StartAsync(CancellationToken cancellationToken = default);

    ValueTask PauseAsync(CancellationToken cancellationToken = default);

    ValueTask StopAsync(CancellationToken cancellationToken = default);

    ValueTask SeekAsync(long positionMicroseconds, CancellationToken cancellationToken = default);

    ValueTask SetSpeedAsync(double speed, CancellationToken cancellationToken = default);

    ValueTask SetTimingAsync(ScoreTimingSettings timing, CancellationToken cancellationToken = default);
}
