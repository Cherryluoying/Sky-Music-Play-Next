// 模块：SkyMusic.Infrastructure 本地音频、MIDI 与乐谱导入
using System.Security.Cryptography;
using SkyMusic.Core.Models;
using SkyMusic.Core.Services;
using SkyMusic.Core.Settings;
using SkyMusic.Infrastructure.Media;
using SkyMusic.Infrastructure.Scores;

namespace SkyMusic.Infrastructure.Catalog;

public sealed class MediaImportService(
    string libraryDirectory,
    IScoreImportService scoreImporter,
    string? ffmpegPath = null,
    IAppSettingsStore? settingsStore = null) : IMediaImportService
{
    private static readonly HashSet<string> AudioExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".mp3", ".wav", ".flac", ".m4a", ".aac", ".wma", ".ogg" };
    private static readonly HashSet<string> MidiExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".mid", ".midi" };
    private static readonly HashSet<string> ScoreExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".txt", ".json", ".skysheet" };
    private readonly string _libraryDirectory = Path.GetFullPath(libraryDirectory);
    private readonly IScoreImportService _scoreImporter = scoreImporter;

    public IReadOnlySet<string> SupportedExtensions { get; } =
        new HashSet<string>(AudioExtensions.Concat(MidiExtensions).Concat(ScoreExtensions), StringComparer.OrdinalIgnoreCase);

    // 计算内容哈希后复制到分类目录，重复导入同一内容只保留一个文件。
    public async ValueTask<MusicTrack> ImportAsync(
        string sourcePath,
        CancellationToken cancellationToken = default)
        => await ImportCoreAsync(sourcePath, true, null, cancellationToken).ConfigureAwait(false);

    public ValueTask<MusicTrack> ImportInPlaceAsync(string sourcePath, string? libraryRoot = null,
        CancellationToken cancellationToken = default)
        => ImportCoreAsync(sourcePath, false, libraryRoot, cancellationToken);

    // 文件导入与目录扫描共用解析逻辑，只有显式导入文件才复制到分类目录。
    private async ValueTask<MusicTrack> ImportCoreAsync(string sourcePath, bool copyToLibrary,
        string? libraryRoot, CancellationToken cancellationToken)
    {
        var fullSourcePath = Path.GetFullPath(sourcePath);
        var extension = Path.GetExtension(fullSourcePath).ToLowerInvariant();
        if (!SupportedExtensions.Contains(extension))
        {
            throw new NotSupportedException($"不支持的媒体格式：{extension}");
        }

        var kind = MidiExtensions.Contains(extension)
            ? MediaKind.Midi
            : ScoreExtensions.Contains(extension) ? MediaKind.Score : MediaKind.Audio;
        var folderName = kind switch
        {
            MediaKind.Audio => "music",
            MediaKind.Midi => "midi",
            _ => "musicscore"
        };
        var id = await ComputeHashAsync(fullSourcePath, cancellationToken);
        // 每次导入读取已保存的分类目录，设置立即生效；数据库中的已有文件路径不变。
        var storage = settingsStore is null
            ? new StorageSettings() : (await settingsStore.LoadAsync(cancellationToken)).Storage;
        var root = MediaLibraryDirectories.Root(storage, _libraryDirectory);
        var targetDirectory = kind switch
        {
            MediaKind.Score => MediaLibraryDirectories.Score(storage, _libraryDirectory),
            MediaKind.Midi => MediaLibraryDirectories.Midi(storage, _libraryDirectory),
            _ => Path.Combine(root, folderName)
        };
        var targetPath = copyToLibrary ? Path.Combine(targetDirectory, $"{id}{extension}") : fullSourcePath;
        if (copyToLibrary && !File.Exists(targetPath))
        {
            Directory.CreateDirectory(targetDirectory);
            await using var source = new FileStream(fullSourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, true);
            await using var target = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, true);
            await source.CopyToAsync(target, cancellationToken);
        }

        var title = Path.GetFileNameWithoutExtension(fullSourcePath);
        var author = string.Empty;
        var duration = TimeSpan.Zero;
        if (kind == MediaKind.Midi)
        {
            // 存储文件名可以是内容哈希，界面标题始终保留用户导入时的文件名。
            // EOT 和速度图给出完整时长，包含尾部休止与非钢琴通道。
            duration = (await Task.Run(() => MidiSequenceReader.Read(targetPath, cancellationToken), cancellationToken)).Duration;
        }
        else if (kind == MediaKind.Score)
        {
            var result = await _scoreImporter.ImportAsync(fullSourcePath, cancellationToken);
            if (!result.IsSuccess || result.Score is null)
            {
                throw new InvalidDataException(string.Join(" · ", result.Issues.Select(issue => issue.Message)));
            }
            title = result.Score.Title;
            author = result.Score.Composer;
            duration = TimeSpan.FromTicks(result.Score.DurationMicroseconds * 10);
        }
        var track = new MusicTrack(
            id,
            title,
            string.IsNullOrWhiteSpace(author) ? "本地音乐" : author,
            kind switch
            {
                MediaKind.Audio => "本地音频",
                MediaKind.Midi => "MIDI",
                _ => "游戏乐谱"
            },
            "avares://SkyMusic.App/Assets/Default-Music.png",
            duration,
            [],
            kind,
            targetPath,
            author);
        track = await RefreshMetadataAsync(track, cancellationToken).ConfigureAwait(false);
        return LocalMediaCompanions.Attach(track, fullSourcePath, libraryRoot ?? root);
    }

    // 新导入与旧曲库共用标签路径；没有标签时保留原文件标题和默认封面。
    public async ValueTask<MusicTrack> RefreshMetadataAsync(MusicTrack track, CancellationToken cancellationToken = default)
    {
        if (track.Kind != MediaKind.Audio || string.IsNullOrWhiteSpace(track.SourcePath)) return track;
        var storage = settingsStore is null ? new StorageSettings()
            : (await settingsStore.LoadAsync(cancellationToken).ConfigureAwait(false)).Storage;
        var metadata = await AudioMetadataReader.ReadAsync(ffmpegPath, track.SourcePath,
            Path.Combine(MediaLibraryDirectories.Root(storage, _libraryDirectory), "covers"), cancellationToken).ConfigureAwait(false);
        return track with
        {
            Title = metadata.Title ?? track.Title,
            Artist = metadata.Artist ?? track.Artist,
            Album = metadata.Album ?? track.Album,
            Author = metadata.Author ?? track.Author,
            CoverSource = metadata.CoverPath ?? track.CoverSource,
            Duration = metadata.Duration > TimeSpan.Zero ? metadata.Duration : track.Duration
        };
    }

    private static async Task<string> ComputeHashAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

}
