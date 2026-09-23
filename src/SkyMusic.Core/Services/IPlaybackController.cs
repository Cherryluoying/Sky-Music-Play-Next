// 模块：SkyMusic.Core 桌面服务 IPlaybackController
using SkyMusic.Core.Models;
using SkyMusic.Core.Playback;

namespace SkyMusic.Core.Services;

public interface IPlaybackController : IDisposable
{
    event EventHandler<PlaybackSnapshot>? SnapshotChanged;

    PlaybackSnapshot Snapshot { get; }

    ValueTask LoadAsync(
        MusicTrack track,
        bool autoplay = false,
        CancellationToken cancellationToken = default);

    ValueTask PlayAsync(CancellationToken cancellationToken = default);

    ValueTask PauseAsync(CancellationToken cancellationToken = default);

    ValueTask SeekAsync(TimeSpan position, CancellationToken cancellationToken = default);
}
