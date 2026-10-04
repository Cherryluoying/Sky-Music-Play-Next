// 模块：可切换根目录的 SQLite 媒体库；服务实例稳定，所有页面共用同一活动数据库。
using Microsoft.Data.Sqlite;
using SkyMusic.Core.Models;
using SkyMusic.Core.Services;
using SkyMusic.Core.Settings;

namespace SkyMusic.Infrastructure.Catalog;

public sealed class ConfiguredMediaLibraryStore(IAppSettingsStore settings, string? defaultRoot = null) : IMediaLibraryStore
{
    public Task SetCustomCoverAsync(string trackId, string coverPath, CancellationToken cancellationToken = default)
        => UseAsync(store => store.SetCustomCoverAsync(trackId, coverPath, cancellationToken), cancellationToken);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private SqliteMediaLibraryStore? _active;
    private string? _activePath;

    // 初始化与切换串行化，防止悬浮窗、播放历史和目录扫描交叉访问半初始化的库。
    private async Task<T> UseAsync<T>(Func<SqliteMediaLibraryStore, Task<T>> operation, CancellationToken token)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var storage = (await settings.LoadAsync(token).ConfigureAwait(false)).Storage;
            var path = Path.Combine(MediaLibraryDirectories.Root(storage, defaultRoot), "library.db");
            if (_active is null || !string.Equals(path, _activePath, StringComparison.OrdinalIgnoreCase))
            {
                var next = await PrepareAsync(path, _activePath, token).ConfigureAwait(false);
                _active = next;
                _activePath = path;
            }
            return await operation(_active).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    private Task UseAsync(Func<SqliteMediaLibraryStore, Task> operation, CancellationToken token)
        => UseAsync(async store => { await operation(store).ConfigureAwait(false); return true; }, token);

    // 保存设置之前先验证；备份使用 SQLite API，包含 WAL 中尚未检查点落盘的提交。
    public Task PrepareDirectoryAsync(string directory, CancellationToken cancellationToken = default)
        => UseAsync(async _ =>
        {
            await PrepareAsync(Path.Combine(Path.GetFullPath(directory), "library.db"), _activePath, cancellationToken)
                .ConfigureAwait(false);
        }, cancellationToken);

    // 复制数据库与提交目录设置共享写入门闩，避免迁移瞬间遗漏新收藏或播放历史。
    public async Task ChangeDirectoryAsync(string directory, Func<Task> persistSettings, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var previous = _activePath;
            if (previous is null)
            {
                var storage = (await settings.LoadAsync(cancellationToken).ConfigureAwait(false)).Storage;
                previous = Path.Combine(MediaLibraryDirectories.Root(storage, defaultRoot), "library.db");
                // 旧库损坏时允许从设置中选择新目录，保留损坏的原文件以便后续恢复。
                if (File.Exists(previous))
                {
                    try { await ValidateAsync(previous, cancellationToken).ConfigureAwait(false); }
                    catch (InvalidDataException) { previous = null; }
                }
            }
            var path = Path.Combine(Path.GetFullPath(directory), "library.db");
            var next = await PrepareAsync(path, previous, cancellationToken).ConfigureAwait(false);
            await persistSettings().ConfigureAwait(false);
            _active = next;
            _activePath = path;
        }
        finally { _gate.Release(); }
    }

    private static async Task<SqliteMediaLibraryStore> PrepareAsync(string path, string? previous, CancellationToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (!File.Exists(path) && previous is not null && File.Exists(previous))
        {
            var temporary = path + $".{Guid.NewGuid():N}.tmp";
            try
            {
                using (var source = new SqliteConnection(ConnectionString(previous, SqliteOpenMode.ReadOnly)))
                using (var destination = new SqliteConnection(ConnectionString(temporary, SqliteOpenMode.ReadWriteCreate)))
                {
                    await source.OpenAsync(token).ConfigureAwait(false);
                    await destination.OpenAsync(token).ConfigureAwait(false);
                    source.BackupDatabase(destination);
                }
                token.ThrowIfCancellationRequested();
                File.Move(temporary, path); // 已存在的目标库绝不覆盖。
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        if (File.Exists(path)) await ValidateAsync(path, token).ConfigureAwait(false);
        var store = new SqliteMediaLibraryStore(path);
        await store.InitializeAsync(token).ConfigureAwait(false);
        return store;
    }

    // 对已有文件只读检查结构；其他软件的同名数据库不可当成本软件曲库修改。
    private static async Task ValidateAsync(string path, CancellationToken token)
    {
        try
        {
            await using var connection = new SqliteConnection(ConnectionString(path, SqliteOpenMode.ReadOnly));
            await connection.OpenAsync(token).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT id, title, artist, author, album, cover_source, source_path, media_kind,
                       duration_ticks, is_favorite, last_played_utc, play_count, created_utc FROM tracks LIMIT 0;
                SELECT id, title, created_utc FROM playlists LIMIT 0;
                SELECT playlist_id, track_id, sort_index FROM playlist_items LIMIT 0;
                """;
            await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }
        catch (SqliteException exception)
        {
            throw new InvalidDataException($"所选目录的 library.db 不是可读取的猫橘咪音乐数据库：{path}", exception);
        }
    }

    private static string ConnectionString(string path, SqliteOpenMode mode) => new SqliteConnectionStringBuilder
        { DataSource = path, Mode = mode, Pooling = false }.ToString();

    public Task InitializeAsync(CancellationToken cancellationToken = default)
        => UseAsync(_ => Task.CompletedTask, cancellationToken);
    public Task<IReadOnlyList<StoredMediaTrack>> GetPlaylistAsync(string playlistId = "local", CancellationToken cancellationToken = default)
        => UseAsync(store => store.GetPlaylistAsync(playlistId, cancellationToken), cancellationToken);
    public Task<IReadOnlyList<StoredMediaTrack>> GetAllAsync(CancellationToken cancellationToken = default)
        => UseAsync(store => store.GetAllAsync(cancellationToken), cancellationToken);
    public Task UpsertAsync(MusicTrack track, string? playlistId = "local", CancellationToken cancellationToken = default)
        => UseAsync(store => store.UpsertAsync(track, playlistId, cancellationToken), cancellationToken);
    public Task SetFavoriteAsync(string trackId, bool isFavorite, CancellationToken cancellationToken = default)
        => UseAsync(store => store.SetFavoriteAsync(trackId, isFavorite, cancellationToken), cancellationToken);
    public Task UpdateMetadataAsync(MusicTrack track, CancellationToken cancellationToken = default)
        => UseAsync(store => store.UpdateMetadataAsync(track, cancellationToken), cancellationToken);
    public Task RecordPlayedAsync(string trackId, CancellationToken cancellationToken = default)
        => UseAsync(store => store.RecordPlayedAsync(trackId, cancellationToken), cancellationToken);
    public Task<IReadOnlyList<MediaFileStamp>> GetFileIndexAsync(CancellationToken cancellationToken = default)
        => UseAsync(store => store.GetFileIndexAsync(cancellationToken), cancellationToken);
    public Task SaveScanBatchAsync(IReadOnlyList<MusicTrack> tracks, IReadOnlyList<MediaFileStamp> files,
        CancellationToken cancellationToken = default)
        => UseAsync(store => store.SaveScanBatchAsync(tracks, files, cancellationToken), cancellationToken);
}
