// 模块：自定义封面持久化；自动标签、重复导入和播放历史不能覆盖用户选择。
using SkyMusic.Core.Models;
using SkyMusic.Infrastructure.Catalog;

namespace SkyMusic.Backend.Tests;

public sealed class CustomCoverTests
{
    [Theory]
    [InlineData(MediaKind.Audio)]
    [InlineData(MediaKind.Score)]
    public async Task CustomCoverSurvivesMetadataRefreshReimportAndReopen(MediaKind kind)
    {
        var root = Path.Combine(Path.GetTempPath(), "SkyMusicCoverTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var db = Path.Combine(root, "library.db");
            var store = new SqliteMediaLibraryStore(db);
            await store.InitializeAsync();
            var track = new MusicTrack("song", "标题", "歌手", "专辑", "embedded.png", TimeSpan.FromMinutes(1), [], kind);
            await store.UpsertAsync(track);
            await store.SetFavoriteAsync(track.Id, true);
            await store.SetCustomCoverAsync(track.Id, "custom.png");
            await store.UpsertAsync(track);
            await store.UpdateMetadataAsync(track with { CoverSource = "new-embedded.png" });
            await store.RecordPlayedAsync(track.Id);
            var reopened = new SqliteMediaLibraryStore(db);
            await reopened.InitializeAsync();
            var saved = Assert.Single(await reopened.GetAllAsync());
            Assert.Equal("custom.png", saved.Track.CoverSource);
            Assert.True(saved.IsFavorite);
            Assert.Equal(1, saved.PlayCount);
            Assert.Equal("custom.png", Assert.Single(await reopened.GetPlaylistAsync()).Track.CoverSource);
            await reopened.SetCustomCoverAsync(track.Id, "replacement.png");
            Assert.Equal("replacement.png", Assert.Single(await reopened.GetAllAsync()).Track.CoverSource);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task MidiCannotReceiveCustomCover()
    {
        var root = Path.Combine(Path.GetTempPath(), "SkyMusicCoverTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var store = new SqliteMediaLibraryStore(Path.Combine(root, "library.db"));
            await store.InitializeAsync();
            await store.UpsertAsync(new MusicTrack("midi", "MIDI", "", "", "default", TimeSpan.Zero, [], MediaKind.Midi));
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.SetCustomCoverAsync("midi", "custom.png"));
            Assert.Equal("default", Assert.Single(await store.GetAllAsync()).Track.CoverSource);
        }
        finally { Directory.Delete(root, true); }
    }
}
