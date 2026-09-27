// 模块：媒体库根目录切换回归；备份当前记录、保留旧库、重启恢复与拒绝外部同名数据库。
using SkyMusic.Core.Models;
using SkyMusic.Core.Settings;
using SkyMusic.Infrastructure.Catalog;
using SkyMusic.Infrastructure.Settings;

namespace SkyMusic.Backend.Tests;

public sealed class ConfiguredMediaLibraryStoreTests
{
    [Fact]
    public async Task SwitchingRootCopiesRecordsAndFutureWritesUseNewDatabase()
    {
        var temporary = Path.Combine(Path.GetTempPath(), "SkyMusicTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            var oldRoot = Path.Combine(temporary, "旧库");
            var newRoot = Path.Combine(temporary, "新库;中文");
            var settings = new JsonAppSettingsStore(Path.Combine(temporary, "settings.json"));
            var store = new ConfiguredMediaLibraryStore(settings, oldRoot);
            await store.InitializeAsync();
            var track = new MusicTrack("id", "原歌名", "歌手", "专辑", "", TimeSpan.FromMinutes(3), []);
            await store.UpsertAsync(track);
            await store.SetFavoriteAsync(track.Id, true);
            await store.RecordPlayedAsync(track.Id);
            await store.ChangeDirectoryAsync(newRoot, () => settings.SaveAsync(new AppSettings
                { Storage = new StorageSettings { LibraryDirectory = newRoot } }).AsTask());
            var copied = Assert.Single(await store.GetPlaylistAsync());
            Assert.True(copied.IsFavorite);
            Assert.Equal(1, copied.PlayCount);
            await store.RecordPlayedAsync(track.Id);
            Assert.Equal(2, Assert.Single(await store.GetAllAsync()).PlayCount);
            var old = new SqliteMediaLibraryStore(Path.Combine(oldRoot, "library.db"));
            Assert.Equal(1, Assert.Single(await old.GetAllAsync()).PlayCount);
            var restarted = new ConfiguredMediaLibraryStore(settings, oldRoot);
            Assert.Equal(2, Assert.Single(await restarted.GetAllAsync()).PlayCount);
            Assert.Equal(Path.Combine(newRoot, "musicscore"), MediaLibraryDirectories.Score((await settings.LoadAsync()).Storage));
        }
        finally { Directory.Delete(temporary, true); }
    }

    [Fact]
    public async Task ExistingTargetLibraryIsReusedAndUnknownDatabaseIsNotModified()
    {
        var temporary = Path.Combine(Path.GetTempPath(), "SkyMusicTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            var target = Path.Combine(temporary, "已有库");
            var invalid = Path.Combine(temporary, "其他软件");
            Directory.CreateDirectory(invalid);
            var bytes = new byte[] { 1, 2, 3, 4 };
            await File.WriteAllBytesAsync(Path.Combine(invalid, "library.db"), bytes);
            var existing = new SqliteMediaLibraryStore(Path.Combine(target, "library.db"));
            await existing.InitializeAsync();
            await existing.UpsertAsync(new MusicTrack("target", "目标曲目", "", "", "", TimeSpan.Zero, []));
            var settings = new JsonAppSettingsStore(Path.Combine(temporary, "settings.json"));
            var store = new ConfiguredMediaLibraryStore(settings, Path.Combine(temporary, "旧库"));
            await store.InitializeAsync();
            await store.UpsertAsync(new MusicTrack("old", "旧库曲目", "", "", "", TimeSpan.Zero, []));
            await Assert.ThrowsAsync<InvalidDataException>(() => store.PrepareDirectoryAsync(invalid));
            Assert.Equal(bytes, await File.ReadAllBytesAsync(Path.Combine(invalid, "library.db")));
            await store.PrepareDirectoryAsync(target);
            await settings.SaveAsync(new AppSettings { Storage = new StorageSettings { LibraryDirectory = target } });
            Assert.Equal("target", Assert.Single(await store.GetAllAsync()).Track.Id);
        }
        finally { Directory.Delete(temporary, true); }
    }

    [Fact]
    public async Task BrokenCurrentDatabaseCanBeRecoveredByChoosingNewRoot()
    {
        var temporary = Path.Combine(Path.GetTempPath(), "SkyMusicTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            var settings = new JsonAppSettingsStore(Path.Combine(temporary, "settings.json"));
            var bytes = new byte[] { 1, 2, 3 };
            await File.WriteAllBytesAsync(Path.Combine(temporary, "library.db"), bytes);
            var store = new ConfiguredMediaLibraryStore(settings, temporary);
            await Assert.ThrowsAsync<InvalidDataException>(() => store.InitializeAsync());
            var target = Path.Combine(temporary, "new");
            await store.ChangeDirectoryAsync(target, () => settings.SaveAsync(new AppSettings
                { Storage = new StorageSettings { LibraryDirectory = target } }).AsTask());
            Assert.Empty(await store.GetAllAsync());
            Assert.Equal(bytes, await File.ReadAllBytesAsync(Path.Combine(temporary, "library.db")));
        }
        finally { Directory.Delete(temporary, true); }
    }
}
