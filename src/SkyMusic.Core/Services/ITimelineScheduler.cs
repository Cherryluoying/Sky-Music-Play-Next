// 模块：SkyMusic.Core 桌面服务 ITimelineScheduler
using SkyMusic.Core.Playback;

namespace SkyMusic.Core.Services;

public interface ITimelineScheduler
{
    bool IsRunning { get; }

    Task PlayAsync(
        CompiledTimeline timeline,
        IPlaybackEventSink sink,
        double speed = 1,
        long startMicroseconds = 0,
        CancellationToken cancellationToken = default);
}
