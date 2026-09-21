// 模块：SkyMusic.Core 桌面服务 IGameWindowService
using SkyMusic.Core.Playback;

namespace SkyMusic.Core.Services;

public interface IGameWindowService
{
    ValueTask<IReadOnlyList<GameWindowInfo>> GetAvailableWindowsAsync(
        CancellationToken cancellationToken = default);

    ValueTask<bool> ActivateAsync(long handle, CancellationToken cancellationToken = default);
}
