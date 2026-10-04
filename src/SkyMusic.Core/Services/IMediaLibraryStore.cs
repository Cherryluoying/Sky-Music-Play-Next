// 模块：SkyMusic.Core 本地媒体库持久化契约
using SkyMusic.Core.Models;

namespace SkyMusic.Core.Services;

public interface IMediaLibraryStore
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    // 在保存根目录设置前验证目标库，并为新目录保留当前库的收藏、历史和歌单。
    Task PrepareDirectoryAsync(string directory, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    async Task ChangeDirectoryAsync(string directory, Func<Task> persistSettings, CancellationToken cancellationToken = default)
    {
        await PrepareDirectoryAsync(directory, cancellationToken);
        await persistSettings();
    }

    Task<IReadOnlyList<StoredMediaTrack>> GetPlaylistAsync(
        string playlistId = "local",
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StoredMediaTrack>> GetAllAsync(CancellationToken cancellationToken = default);

    Task UpsertAsync(
        MusicTrack track,
        string? playlistId = "local",
        CancellationToken cancellationToken = default);

    Task SetFavoriteAsync(string trackId, bool isFavorite, CancellationToken cancellationToken = default);

    Task UpdateMetadataAsync(MusicTrack track, CancellationToken cancellationToken = default);

    // 用户封面独立于自动标签读取，重扫和历史写入不能覆盖用户选择。
    Task SetCustomCoverAsync(string trackId, string coverPath, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("当前媒体库不支持自定义封面。");

    Task RecordPlayedAsync(string trackId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MediaFileStamp>> GetFileIndexAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<MediaFileStamp>>([]);

    // 扫描按批次提交曲目和文件签名；默认实现兼容其他媒体库提供程序。
    async Task SaveScanBatchAsync(IReadOnlyList<MusicTrack> tracks, IReadOnlyList<MediaFileStamp> files,
        CancellationToken cancellationToken = default)
    {
        foreach (var track in tracks) await UpsertAsync(track, cancellationToken: cancellationToken);
    }
}
