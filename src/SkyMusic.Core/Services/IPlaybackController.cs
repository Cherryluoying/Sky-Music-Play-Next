// 模块：SkyMusic.Core 桌面服务 IPlaybackController
using SkyMusic.Core.Models;
using SkyMusic.Core.Playback;

namespace SkyMusic.Core.Services;

public interface IPlaybackController : IDisposable
{
    event EventHandler<PlaybackSnapshot>? SnapshotChanged;

    PlaybackSnapshot Snapshot { get; }

    void Load(MusicTrack track, bool autoplay = false);

    void Play();

    void Pause();

    void Seek(TimeSpan position);
}
