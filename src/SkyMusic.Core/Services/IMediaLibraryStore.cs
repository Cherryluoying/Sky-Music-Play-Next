// 模块：SkyMusic.Core 本地媒体库持久化契约
using SkyMusic.Core.Models;

namespace SkyMusic.Core.Services;

public interface IMediaLibraryStore
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StoredMediaTrack>> GetPlaylistAsync(
        string playlistId = "local",
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StoredMediaTrack>> GetAllAsync(CancellationToken cancellationToken = default);

    Task UpsertAsync(
        MusicTrack track,
        string? playlistId = "local",
        CancellationToken cancellationToken = default);

    Task SetFavoriteAsync(string trackId, bool isFavorite, CancellationToken cancellationToken = default);

    Task RecordPlayedAsync(string trackId, CancellationToken cancellationToken = default);
}
