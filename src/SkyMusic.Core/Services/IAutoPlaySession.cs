// 模块：SkyMusic.Core 桌面服务 IAutoPlaySession
using SkyMusic.Core.Playback;

namespace SkyMusic.Core.Services;

public interface IAutoPlaySession : IAsyncDisposable
{
    AutoPlaySnapshot Snapshot { get; }

    event Action<AutoPlaySnapshot>? Changed;

    ValueTask LoadAsync(CompiledTimeline timeline, CancellationToken cancellationToken = default);

    ValueTask StartAsync(CancellationToken cancellationToken = default);

    ValueTask PauseAsync(CancellationToken cancellationToken = default);

    ValueTask StopAsync(CancellationToken cancellationToken = default);

    ValueTask SeekAsync(long positionMicroseconds, CancellationToken cancellationToken = default);

    ValueTask SetSpeedAsync(double speed, CancellationToken cancellationToken = default);
}
