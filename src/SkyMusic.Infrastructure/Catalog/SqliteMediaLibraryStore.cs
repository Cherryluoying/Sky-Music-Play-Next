// 模块：SkyMusic.Infrastructure SQLite 本地媒体库
using Microsoft.Data.Sqlite;
using SkyMusic.Core.Models;
using SkyMusic.Core.Services;

namespace SkyMusic.Infrastructure.Catalog;

public sealed class SqliteMediaLibraryStore(string databasePath) : IMediaLibraryStore
{
    private readonly string _databasePath = Path.GetFullPath(databasePath);

    // 初始化曲目、歌单关系、喜欢和播放历史所需的数据表。
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode = WAL;
            PRAGMA foreign_keys = ON;
            CREATE TABLE IF NOT EXISTS tracks (
                id TEXT PRIMARY KEY,
                title TEXT NOT NULL,
                artist TEXT NOT NULL,
                author TEXT NOT NULL,
                album TEXT NOT NULL,
                cover_source TEXT NOT NULL,
                source_path TEXT,
                media_kind INTEGER NOT NULL,
                duration_ticks INTEGER NOT NULL,
                is_favorite INTEGER NOT NULL DEFAULT 0,
                last_played_utc TEXT,
                play_count INTEGER NOT NULL DEFAULT 0,
                lyrics_path TEXT,
                created_utc TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS playlists (
                id TEXT PRIMARY KEY,
                title TEXT NOT NULL,
                created_utc TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS playlist_items (
                playlist_id TEXT NOT NULL,
                track_id TEXT NOT NULL,
                sort_index INTEGER NOT NULL,
                PRIMARY KEY (playlist_id, track_id),
                FOREIGN KEY (playlist_id) REFERENCES playlists(id) ON DELETE CASCADE,
                FOREIGN KEY (track_id) REFERENCES tracks(id) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS idx_tracks_favorite ON tracks(is_favorite);
            CREATE INDEX IF NOT EXISTS idx_tracks_recent ON tracks(last_played_utc DESC);
            INSERT OR IGNORE INTO playlists(id, title, created_utc)
            VALUES ('local', '本地歌单', CURRENT_TIMESTAMP);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
        await EnsureLyricsColumnAsync(connection, cancellationToken);
        await RemoveLegacyDemoTracksAsync(connection, cancellationToken);
    }

    // 旧版本曾把五首内置演示曲写入用户曲库；仅删除无本地源文件的固定演示 ID。
    private static async Task RemoveLegacyDemoTracksAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM tracks
            WHERE source_path IS NULL
              AND id IN ('cloud-field', 'blue-hour', 'flower-letter', 'quiet-station', 'piano-room');
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    // 按歌单顺序读取曲目，同一曲目只由 tracks 表保存一份元数据。
    public async Task<IReadOnlyList<StoredMediaTrack>> GetPlaylistAsync(
        string playlistId = "local",
        CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT t.id, t.title, t.artist, t.author, t.album, t.cover_source,
                   t.source_path, t.media_kind, t.duration_ticks, t.is_favorite,
                   t.last_played_utc, t.play_count, t.lyrics_path
            FROM playlist_items p
            JOIN tracks t ON t.id = p.track_id
            WHERE p.playlist_id = $playlistId
            ORDER BY p.sort_index, t.title COLLATE NOCASE;
            """;
        command.Parameters.AddWithValue("$playlistId", playlistId);

        return await ReadItemsAsync(command, cancellationToken);
    }

    public async Task<IReadOnlyList<StoredMediaTrack>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, title, artist, author, album, cover_source,
                   source_path, media_kind, duration_ticks, is_favorite,
                   last_played_utc, play_count, lyrics_path
            FROM tracks
            ORDER BY title COLLATE NOCASE;
            """;
        return await ReadItemsAsync(command, cancellationToken);
    }

    // Upsert 保证重复导入或收藏同一文件时不会生成重复歌曲。
    public async Task UpsertAsync(
        MusicTrack track,
        string? playlistId = "local",
        CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO tracks(
                    id, title, artist, author, album, cover_source, source_path,
                    media_kind, duration_ticks, lyrics_path, created_utc)
                VALUES(
                    $id, $title, $artist, $author, $album, $cover, $source,
                    $kind, $duration, $lyrics, $created)
                ON CONFLICT(id) DO UPDATE SET
                    title = excluded.title,
                    artist = excluded.artist,
                    author = excluded.author,
                    album = excluded.album,
                    cover_source = excluded.cover_source,
                    source_path = COALESCE(excluded.source_path, tracks.source_path),
                    media_kind = excluded.media_kind,
                    duration_ticks = excluded.duration_ticks,
                    lyrics_path = COALESCE(excluded.lyrics_path, tracks.lyrics_path);
                """;
            command.Parameters.AddWithValue("$id", track.Id);
            command.Parameters.AddWithValue("$title", track.Title);
            command.Parameters.AddWithValue("$artist", track.Artist);
            command.Parameters.AddWithValue("$author", track.Author);
            command.Parameters.AddWithValue("$album", track.Album);
            command.Parameters.AddWithValue("$cover", track.CoverSource);
            command.Parameters.AddWithValue("$source", (object?)track.SourcePath ?? DBNull.Value);
            command.Parameters.AddWithValue("$kind", (int)track.Kind);
            command.Parameters.AddWithValue("$duration", track.Duration.Ticks);
            command.Parameters.AddWithValue("$lyrics", (object?)track.LyricsSourcePath ?? DBNull.Value);
            command.Parameters.AddWithValue("$created", DateTimeOffset.UtcNow.ToString("O"));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(playlistId))
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT OR IGNORE INTO playlists(id, title, created_utc)
                VALUES($playlistId, $playlistTitle, $created);
                INSERT OR IGNORE INTO playlist_items(playlist_id, track_id, sort_index)
                VALUES(
                    $playlistId,
                    $trackId,
                    COALESCE((SELECT MAX(sort_index) + 1 FROM playlist_items WHERE playlist_id = $playlistId), 1));
                """;
            command.Parameters.AddWithValue("$playlistId", playlistId);
            command.Parameters.AddWithValue("$playlistTitle", playlistId == "local" ? "本地歌单" : playlistId);
            command.Parameters.AddWithValue("$trackId", track.Id);
            command.Parameters.AddWithValue("$created", DateTimeOffset.UtcNow.ToString("O"));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    // 后台补读标签只更新展示元数据，不覆盖并发写入的喜欢、历史、歌词或歌单关系。
    public async Task UpdateMetadataAsync(MusicTrack track, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE tracks SET title = $title, artist = $artist, author = $author,
                album = $album, cover_source = $cover, duration_ticks = $duration
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", track.Id);
        command.Parameters.AddWithValue("$title", track.Title);
        command.Parameters.AddWithValue("$artist", track.Artist);
        command.Parameters.AddWithValue("$author", track.Author);
        command.Parameters.AddWithValue("$album", track.Album);
        command.Parameters.AddWithValue("$cover", track.CoverSource);
        command.Parameters.AddWithValue("$duration", track.Duration.Ticks);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SetFavoriteAsync(
        string trackId,
        bool isFavorite,
        CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE tracks SET is_favorite = $favorite WHERE id = $id;";
        command.Parameters.AddWithValue("$favorite", isFavorite ? 1 : 0);
        command.Parameters.AddWithValue("$id", trackId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RecordPlayedAsync(string trackId, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE tracks
            SET last_played_utc = $played, play_count = play_count + 1
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$played", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$id", trackId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    // 单用户桌面库关闭连接池，确保应用退出后数据库文件立即释放。
    private SqliteConnection CreateConnection() => new(
        $"Data Source={_databasePath};Mode=ReadWriteCreate;Pooling=False");

    // 兼容已经创建的媒体库，仅补充新列，不重建用户数据库。
    private static async Task EnsureLyricsColumnAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var hasColumn = false;
        await using (var query = connection.CreateCommand())
        {
            query.CommandText = "PRAGMA table_info(tracks);";
            await using var reader = await query.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (reader.GetString(1).Equals("lyrics_path", StringComparison.OrdinalIgnoreCase))
                {
                    hasColumn = true;
                    break;
                }
            }
        }

        if (!hasColumn)
        {
            await using var alter = connection.CreateCommand();
            alter.CommandText = "ALTER TABLE tracks ADD COLUMN lyrics_path TEXT;";
            await alter.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task<IReadOnlyList<StoredMediaTrack>> ReadItemsAsync(
        SqliteCommand command,
        CancellationToken cancellationToken)
    {
        var items = new List<StoredMediaTrack>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var sourcePath = reader.IsDBNull(6) ? null : reader.GetString(6);
            var track = new MusicTrack(
                reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(4),
                reader.GetString(5), TimeSpan.FromTicks(reader.GetInt64(8)), [],
                (MediaKind)reader.GetInt32(7), sourcePath, reader.GetString(3),
                reader.IsDBNull(12) ? null : reader.GetString(12));
            DateTimeOffset? lastPlayed = reader.IsDBNull(10)
                ? null
                : DateTimeOffset.Parse(reader.GetString(10), System.Globalization.CultureInfo.InvariantCulture);
            items.Add(new StoredMediaTrack(track, reader.GetBoolean(9), lastPlayed, reader.GetInt64(11)));
        }
        return items;
    }
}
