// 模块：SkyMusic.Infrastructure 本地音频、MIDI 与乐谱导入
using System.Security.Cryptography;
using SkyMusic.Core.Models;
using SkyMusic.Core.Services;
using SkyMusic.Infrastructure.Media;

namespace SkyMusic.Infrastructure.Catalog;

public sealed class MediaImportService(
    string libraryDirectory,
    IScoreImportService scoreImporter,
    string? ffmpegPath = null) : IMediaImportService
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
        var targetDirectory = Path.Combine(_libraryDirectory, folderName);
        Directory.CreateDirectory(targetDirectory);
        var targetPath = Path.Combine(targetDirectory, $"{id}{extension}");
        if (!File.Exists(targetPath))
        {
            await using var source = new FileStream(fullSourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, true);
            await using var target = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, true);
            await source.CopyToAsync(target, cancellationToken);
        }

        var title = Path.GetFileNameWithoutExtension(fullSourcePath);
        var author = string.Empty;
        var duration = TimeSpan.Zero;
        if (kind is MediaKind.Score or MediaKind.Midi)
        {
            var result = await _scoreImporter.ImportAsync(targetPath, cancellationToken);
            if (!result.IsSuccess || result.Score is null)
            {
                throw new InvalidDataException(string.Join(" · ", result.Issues.Select(issue => issue.Message)));
            }
            title = result.Score.Title;
            author = result.Score.Composer;
            duration = TimeSpan.FromTicks(result.Score.DurationMicroseconds * 10);
        }
        else
        {
            duration = FfmpegMediaDurationProbe.Probe(ffmpegPath, targetPath);
        }

        return new MusicTrack(
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
    }

    private static async Task<string> ComputeHashAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

}
