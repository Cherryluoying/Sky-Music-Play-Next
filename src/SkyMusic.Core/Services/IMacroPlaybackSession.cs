// 模块：SkyMusic.Core 桌面服务 IMacroPlaybackSession
using SkyMusic.Core.Automation;
using SkyMusic.Core.Playback;

namespace SkyMusic.Core.Services;

public interface IMacroPlaybackSession : IAsyncDisposable
{
    AutoPlaySnapshot Snapshot { get; }

    event Action<AutoPlaySnapshot>? Changed;

    ValueTask LoadAsync(MacroScript script, CancellationToken cancellationToken = default);

    ValueTask StartAsync(CancellationToken cancellationToken = default);

    ValueTask PauseAsync(CancellationToken cancellationToken = default);

    ValueTask StopAsync(CancellationToken cancellationToken = default);

    ValueTask SeekAsync(long positionMicroseconds, CancellationToken cancellationToken = default);

    ValueTask SetSpeedAsync(double speed, CancellationToken cancellationToken = default);
}
