// 模块：SkyMusic.Core 桌面服务 IMusicCatalog
using SkyMusic.Core.Models;

namespace SkyMusic.Core.Services;

public interface IMusicCatalog
{
    Task<IReadOnlyList<MusicTrack>> GetFeaturedAsync(CancellationToken cancellationToken = default);
}
