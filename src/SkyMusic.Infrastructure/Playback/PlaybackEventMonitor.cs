// 模块：SkyMusic.Infrastructure 播放领域 PlaybackEventMonitor
using SkyMusic.Core.Playback;
using SkyMusic.Core.Services;

namespace SkyMusic.Infrastructure.Playback;

public sealed class PlaybackEventMonitor : IPlaybackEventMonitor
{
    public event Action<PlaybackEvent>? EventPlayed;

    internal void Publish(PlaybackEvent playbackEvent) => EventPlayed?.Invoke(playbackEvent);
}
