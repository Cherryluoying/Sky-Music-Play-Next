// 模块：SkyMusic.Core 本地媒体库记录
namespace SkyMusic.Core.Models;

public sealed record StoredMediaTrack(
    MusicTrack Track,
    bool IsFavorite,
    DateTimeOffset? LastPlayedAt,
    long PlayCount);
