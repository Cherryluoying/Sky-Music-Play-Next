// 模块：SkyMusic.Core 桌面服务 IPlaybackEventSink
using SkyMusic.Core.Playback;

namespace SkyMusic.Core.Services;

public interface IPlaybackEventSink
{
    string Name { get; }

    PlaybackSinkCapabilities Capabilities { get; }

    void Send(PlaybackEvent playbackEvent);

    void Reset();
}
