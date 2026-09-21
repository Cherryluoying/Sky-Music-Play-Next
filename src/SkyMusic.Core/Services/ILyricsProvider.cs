// 模块：SkyMusic.Core 桌面服务 ILyricsProvider
using SkyMusic.Core.Lyrics;

namespace SkyMusic.Core.Services;

public interface ILyricsProvider
{
    ValueTask<LyricsResult?> GetLyricsAsync(LyricsQuery query, CancellationToken cancellationToken = default);
}
