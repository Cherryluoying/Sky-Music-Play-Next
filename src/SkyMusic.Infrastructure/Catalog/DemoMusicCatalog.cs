// 模块：SkyMusic.Infrastructure 空白发现音乐目录
using SkyMusic.Core.Models;
using SkyMusic.Core.Services;

namespace SkyMusic.Infrastructure.Catalog;

public sealed class EmptyMusicCatalog : IMusicCatalog
{
    // 启动时不再注入内置演示歌曲；首页等待用户曲库或云端真实内容。
    public Task<IReadOnlyList<MusicTrack>> GetFeaturedAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<MusicTrack>>([]);
}
