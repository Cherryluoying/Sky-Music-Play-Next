// 模块：SkyMusic.Core 桌面服务 IPlaybackEventMonitor
using SkyMusic.Core.Playback;

namespace SkyMusic.Core.Services;

public interface IPlaybackEventMonitor
{
    event Action<PlaybackEvent>? EventPlayed;
}
