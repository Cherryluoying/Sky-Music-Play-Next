// 模块：SkyMusic.Backend.Tests SQLite 媒体库集成测试
using SkyMusic.Core.Models;
using SkyMusic.Infrastructure.Catalog;
using Microsoft.Data.Sqlite;

namespace SkyMusic.Backend.Tests;

public sealed class SqliteMediaLibraryStoreTests
{
    [Fact]
    public async Task Upsert_DeduplicatesPlaylistAndPersistsFavoriteAndHistory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"skymusic-library-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var store = new SqliteMediaLibraryStore(Path.Combine(directory, "library.db"));
            await store.InitializeAsync();
            var track = new MusicTrack(
                "same-content",
                "测试歌曲",
                "测试歌手",
                "测试专辑",
                string.Empty,
                TimeSpan.FromMinutes(3),
                [],
                MediaKind.Audio,
                Path.Combine(directory, "music", "same-content.mp3"),
                "测试作者",
                Path.Combine(directory, "lyrics", "same-content.lrc"));

            await store.UpsertAsync(track);
            await store.UpsertAsync(track);
            await store.SetFavoriteAsync(track.Id, true);
            await store.RecordPlayedAsync(track.Id);

            var playlist = await store.GetPlaylistAsync();
            var all = await store.GetAllAsync();
            var item = Assert.Single(playlist);
            Assert.Single(all);
            Assert.True(item.IsFavorite);
            Assert.Equal(1, item.PlayCount);
            Assert.NotNull(item.LastPlayedAt);
            Assert.Equal("测试作者", item.Track.Author);
            Assert.Equal(track.LyricsSourcePath, item.Track.LyricsSourcePath);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task MetadataBackfillPreservesPlaylistFavoritesHistoryAndNewLyrics()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"skymusic-metadata-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var store = new SqliteMediaLibraryStore(Path.Combine(directory, "library.db"));
            await store.InitializeAsync();
            var old = new MusicTrack("song-id", "文件名", "本地音乐", "本地音频", "default.png",
                TimeSpan.Zero, [], MediaKind.Audio, "song.flac", LyricsSourcePath: "old.lrc");
            await store.UpsertAsync(old, "my-playlist");
            await store.SetFavoriteAsync(old.Id, true);
            await store.RecordPlayedAsync(old.Id);
            await store.UpsertAsync(old with { LyricsSourcePath = "new.lrc" }, null);
            await store.UpdateMetadataAsync(old with
            {
                Title = "歌曲标题", Artist = "歌手", Album = "专辑", Author = "作曲者",
                CoverSource = "cover.jpg", Duration = TimeSpan.FromSeconds(200)
            });
            var updated = Assert.Single(await store.GetPlaylistAsync("my-playlist"));
            Assert.True(updated.IsFavorite);
            Assert.Equal(1, updated.PlayCount);
            Assert.NotNull(updated.LastPlayedAt);
            Assert.Equal("new.lrc", updated.Track.LyricsSourcePath);
            Assert.Equal("歌曲标题", updated.Track.Title);
            Assert.Equal("歌手", updated.Track.Artist);
            Assert.Equal("专辑", updated.Track.Album);
            Assert.Equal("作曲者", updated.Track.Author);
            Assert.Equal("cover.jpg", updated.Track.CoverSource);
            Assert.Equal(TimeSpan.FromSeconds(200), updated.Track.Duration);
            Assert.Empty(await store.GetPlaylistAsync());
            Assert.Single(await store.GetAllAsync());
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task Initialize_UpgradesExistingDatabaseWithLyricsColumn()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"skymusic-library-upgrade-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var databasePath = Path.Combine(directory, "library.db");
        try
        {
            await using (var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False"))
            {
                await connection.OpenAsync();
                await using var create = connection.CreateCommand();
                create.CommandText = """
                    CREATE TABLE tracks (
                        id TEXT PRIMARY KEY, title TEXT NOT NULL, artist TEXT NOT NULL,
                        author TEXT NOT NULL, album TEXT NOT NULL, cover_source TEXT NOT NULL,
                        source_path TEXT, media_kind INTEGER NOT NULL, duration_ticks INTEGER NOT NULL,
                        is_favorite INTEGER NOT NULL DEFAULT 0, last_played_utc TEXT,
                        play_count INTEGER NOT NULL DEFAULT 0, created_utc TEXT NOT NULL);
                    """;
                await create.ExecuteNonQueryAsync();
            }

            var store = new SqliteMediaLibraryStore(databasePath);
            await store.InitializeAsync();

            await using var verifyConnection = new SqliteConnection($"Data Source={databasePath};Pooling=False");
            await verifyConnection.OpenAsync();
            await using var query = verifyConnection.CreateCommand();
            query.CommandText = "SELECT COUNT(*) FROM pragma_table_info('tracks') WHERE name = 'lyrics_path';";
            Assert.Equal(1L, (long)(await query.ExecuteScalarAsync())!);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task Initialize_RemovesOnlyLegacyDemoTracksWithoutLocalSource()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"skymusic-library-cleanup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var store = new SqliteMediaLibraryStore(Path.Combine(directory, "library.db"));
            await store.InitializeAsync();
            await store.UpsertAsync(new MusicTrack(
                "cloud-field", "演示曲", "演示歌手", "演示专辑", string.Empty,
                TimeSpan.FromMinutes(3), []));
            await store.UpsertAsync(new MusicTrack(
                "user-track", "用户歌曲", "本地歌手", "本地专辑", string.Empty,
                TimeSpan.FromMinutes(4), [], MediaKind.Audio, Path.Combine(directory, "song.mp3")));

            await store.InitializeAsync();

            var remaining = await store.GetAllAsync();
            var track = Assert.Single(remaining);
            Assert.Equal("user-track", track.Track.Id);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
