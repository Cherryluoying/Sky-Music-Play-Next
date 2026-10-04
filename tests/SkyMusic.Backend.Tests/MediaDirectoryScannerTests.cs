// 模块：混合目录导入回归；真实 SQLite、MIDI 和乐谱验证去重、伴随文件、缓存与坏文件隔离。
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Common;
using SkyMusic.Core.Models;
using SkyMusic.Core.Services;
using SkyMusic.Infrastructure.Catalog;
using SkyMusic.Infrastructure.Scores;

namespace SkyMusic.Backend.Tests;

public sealed class MediaDirectoryScannerTests
{
    [Fact]
    public async Task MixedFolderScanPreservesFilesDeduplicatesAndReusesIndex()
    {
        var temporary = Path.Combine(Path.GetTempPath(), "SkyMusicTests", Guid.NewGuid().ToString("N"));
        var source = Path.Combine(temporary, "歌曲");
        Directory.CreateDirectory(Path.Combine(source, "子目录"));
        Directory.CreateDirectory(Path.Combine(source, "covers"));
        Directory.CreateDirectory(Path.Combine(source, "lyrics"));
        try
        {
            const string json = """[{"name":"扫描乐谱","author":"作者","bpm":120,"songNotes":[{"time":0,"key":"Key0","duration":90}]}]""";
            await File.WriteAllTextAsync(Path.Combine(source, "原谱.txt"), json);
            await File.WriteAllTextAsync(Path.Combine(source, "子目录", "重复.json"), json);
            await File.WriteAllTextAsync(Path.Combine(source, "说明.txt"), "不是曲谱");
            await File.WriteAllTextAsync(Path.Combine(source, "损坏.mid"), "not MIDI");
            new MidiFile(new TrackChunk(new NoteOnEvent((SevenBitNumber)60, (SevenBitNumber)90),
                new NoteOffEvent((SevenBitNumber)60, (SevenBitNumber)0) { DeltaTime = 480 }))
                .Write(Path.Combine(source, "子目录", "钢琴.mid"));
            await File.WriteAllBytesAsync(Path.Combine(source, "歌曲.wav"), [82, 73, 70, 70]);
            await File.WriteAllBytesAsync(Path.Combine(source, "covers", "钢琴.png"), [1]);
            await File.WriteAllBytesAsync(Path.Combine(source, "歌曲.jpg"), [2]);
            await File.WriteAllTextAsync(Path.Combine(source, "lyrics", "钢琴.lrc"), "[00:00]测试");
            await File.WriteAllTextAsync(Path.Combine(source, "歌曲.srt"), "1\n00:00:01,000 --> 00:00:03,000\n歌词");
            var store = new SqliteMediaLibraryStore(Path.Combine(temporary, "db", "library.db"));
            var importer = new CountingImporter(new MediaImportService(Path.Combine(temporary, "output"), new ScoreImportService()));
            var scanner = new MediaDirectoryScanner(importer, store);
            var result = await scanner.ScanAsync([source, Path.Combine(source, "子目录")]);
            Assert.Equal(3, result.Added);
            Assert.Equal(2, result.Errors.Count);
            var tracks = await store.GetAllAsync();
            Assert.Equal(3, tracks.Count);
            Assert.All(tracks, item => Assert.StartsWith(source, item.Track.SourcePath));
            Assert.False(Directory.Exists(Path.Combine(temporary, "output", "musicscore")));
            var midi = Assert.Single(tracks, item => item.Track.Kind == MediaKind.Midi).Track;
            Assert.Equal("钢琴", midi.Title);
            Assert.Equal(Path.Combine(source, "covers", "钢琴.png"), midi.CoverSource);
            Assert.Equal(Path.Combine(source, "lyrics", "钢琴.lrc"), midi.LyricsSourcePath);
            Assert.Equal(Path.Combine(source, "歌曲.jpg"), Assert.Single(tracks, item => item.Track.Kind == MediaKind.Audio).Track.CoverSource);
            Assert.Equal(Path.Combine(source, "歌曲.srt"), Assert.Single(tracks, item => item.Track.Kind == MediaKind.Audio).Track.LyricsSourcePath);
            await store.SetFavoriteAsync(midi.Id, true);
            await store.RecordPlayedAsync(midi.Id);
            File.Delete(Path.Combine(source, "说明.txt"));
            File.Delete(Path.Combine(source, "损坏.mid"));
            var reads = importer.Reads;
            var again = await scanner.ScanAsync([source]);
            Assert.Equal(0, again.Added);
            Assert.Equal(3, again.Updated);
            Assert.Empty(again.Errors);
            Assert.Equal(reads, importer.Reads); // 未变化文件不再解析，也不会再次计算内容哈希。
            var stored = Assert.Single(await store.GetAllAsync(), item => item.Track.Id == midi.Id);
            Assert.True(stored.IsFavorite);
            Assert.Equal(1, stored.PlayCount);
            Assert.Equal(3, (await store.GetPlaylistAsync()).Count);

            // 后加封面无需修改乐谱文件，重扫仍能关联。
            var score = Assert.Single(tracks, item => item.Track.Kind == MediaKind.Score).Track;
            await File.WriteAllBytesAsync(Path.Combine(source, "covers", score.Id + ".jpg"), [3]);
            await scanner.ScanAsync([source]);
            Assert.Equal(Path.Combine(source, "covers", score.Id + ".jpg"),
                Assert.Single(await store.GetAllAsync(), item => item.Track.Id == score.Id).Track.CoverSource);
        }
        finally { Directory.Delete(temporary, true); }
    }

    [Fact]
    public async Task ChangedFileIsReparsedAndCanceledScanStops()
    {
        var temporary = Path.Combine(Path.GetTempPath(), "SkyMusicTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            var file = Path.Combine(temporary, "music.mp3");
            await File.WriteAllBytesAsync(file, [1, 2]);
            var store = new SqliteMediaLibraryStore(Path.Combine(temporary, "library.db"));
            var importer = new CountingImporter(new MediaImportService(temporary, new ScoreImportService()));
            var scanner = new MediaDirectoryScanner(importer, store);
            await scanner.ScanAsync([temporary]);
            await File.WriteAllBytesAsync(file, [1, 2, 3]);
            await scanner.ScanAsync([temporary]);
            Assert.Equal(2, importer.Reads);
            using var cancel = new CancellationTokenSource();
            cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scanner.ScanAsync([temporary], cancellationToken: cancel.Token));
        }
        finally { Directory.Delete(temporary, true); }
    }

    private sealed class CountingImporter(IMediaImportService inner) : IMediaImportService
    {
        public int Reads { get; private set; }
        public IReadOnlySet<string> SupportedExtensions => inner.SupportedExtensions;
        public ValueTask<MusicTrack> ImportAsync(string sourcePath, CancellationToken cancellationToken = default)
            => inner.ImportAsync(sourcePath, cancellationToken);
        public ValueTask<MusicTrack> ImportInPlaceAsync(string sourcePath, string? libraryRoot = null, CancellationToken cancellationToken = default)
        {
            Reads++;
            return inner.ImportInPlaceAsync(sourcePath, libraryRoot, cancellationToken);
        }
    }
}
