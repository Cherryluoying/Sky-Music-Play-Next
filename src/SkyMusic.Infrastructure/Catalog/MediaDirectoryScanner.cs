// 模块：整目录媒体索引；递归扫描、内容去重、错误隔离，保留原文件与已有收藏历史。
using SkyMusic.Core.Models;
using SkyMusic.Core.Services;

namespace SkyMusic.Infrastructure.Catalog;

public sealed record MediaScanResult(int Added, int Updated, IReadOnlyList<string> Errors)
{
    public string Summary => $"新增 {Added} 首，已识别 {Updated} 首已有曲目" +
        (Errors.Count == 0 ? string.Empty : $"；{Errors.Count} 项未能读取：{string.Join("；", Errors.Take(2))}");
}

public sealed class MediaDirectoryScanner(IMediaImportService importer, IMediaLibraryStore store)
{
    // 调用方在后台执行，进度通过 IProgress 回到界面线程；整个批次只读取一次已有曲库。
    public async Task<MediaScanResult> ScanAsync(IEnumerable<string> directories,
        IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        await store.InitializeAsync(cancellationToken).ConfigureAwait(false);
        var known = (await store.GetAllAsync(cancellationToken).ConfigureAwait(false))
            .ToDictionary(item => item.Track.Id, item => item.Track, StringComparer.OrdinalIgnoreCase);
        var index = (await store.GetFileIndexAsync(cancellationToken).ConfigureAwait(false))
            .ToDictionary(item => item.Path, StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var allFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var directory in directories.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var root = Path.GetFullPath(directory);
            Gather(root, root, files, allFiles, visited, errors, cancellationToken);
        }

        var added = 0;
        var updated = 0;
        var processed = 0;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var batch = new List<MusicTrack>();
        var stamps = new List<MediaFileStamp>();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        foreach (var (path, root) in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            processed++;
            if (processed == 1 || processed == files.Count || watch.ElapsedMilliseconds >= 150)
            {
                progress?.Report($"正在扫描 {processed}/{files.Count}：{Path.GetFileName(path)}");
                watch.Restart();
            }
            try
            {
                var info = new FileInfo(path);
                var length = info.Length;
                var modified = info.LastWriteTimeUtc.Ticks;
                MusicTrack track;
                if (index.TryGetValue(path, out var stamp) && stamp.Length == length && stamp.ModifiedTicks == modified
                    && known.TryGetValue(stamp.TrackId, out var cached))
                    track = cached with { SourcePath = path };
                else
                    track = await importer.ImportInPlaceAsync(path, root, cancellationToken).ConfigureAwait(false);

                info.Refresh();
                if (info.Length == length && info.LastWriteTimeUtc.Ticks == modified)
                    stamps.Add(new(path, length, modified, track.Id));
                if (!seen.Add(track.Id)) continue;
                if (known.TryGetValue(track.Id, out var existing))
                {
                    // 哈希命名的旧库文件不能覆盖原歌曲名；扫描只补资源位置和缺失信息。
                    track = track with
                    {
                        Title = track.Kind == MediaKind.Audio && track.Title != Path.GetFileNameWithoutExtension(path)
                            ? track.Title : existing.Title,
                        Artist = track.Kind == MediaKind.Audio && track.Artist != "本地音乐"
                            ? track.Artist : existing.Artist,
                        Album = track.Kind == MediaKind.Audio && track.Album != "本地音频"
                            ? track.Album : existing.Album,
                        Author = string.IsNullOrWhiteSpace(track.Author) ? existing.Author : track.Author,
                        CoverSource = track.CoverSource.StartsWith("avares:", StringComparison.OrdinalIgnoreCase)
                            ? existing.CoverSource : track.CoverSource,
                        LyricsSourcePath = track.LyricsSourcePath ?? existing.LyricsSourcePath,
                        Duration = track.Duration > TimeSpan.Zero ? track.Duration : existing.Duration
                    };
                    updated++;
                }
                else added++;
                track = LocalMediaCompanions.Attach(track, path, root, allFiles.Contains);
                batch.Add(track);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception)
            {
                // 解析库可能抛出自有异常类型；单个坏 MIDI / 曲谱不应终止整个目录。
                errors.Add($"{Path.GetFileName(path)}：{exception.Message}");
            }
            if (batch.Count >= 100 || stamps.Count >= 200)
            {
                await store.SaveScanBatchAsync(batch, stamps, cancellationToken).ConfigureAwait(false);
                batch.Clear();
                stamps.Clear();
            }
        }
        if (batch.Count > 0 || stamps.Count > 0)
            await store.SaveScanBatchAsync(batch, stamps, cancellationToken).ConfigureAwait(false);
        return new MediaScanResult(added, updated, errors);
    }

    // 每个目录单独处理权限错误，跳过链接以免循环；不把数据库、封面和歌词误认为歌曲。
    private void Gather(string directory, string root, Dictionary<string, string> files,
        HashSet<string> allFiles, HashSet<string> visited, List<string> errors, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!visited.Add(directory)) return;
        try
        {
            foreach (var file in Directory.EnumerateFiles(directory).Order(StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                allFiles.Add(file);
                var folder = Path.GetFileName(directory);
                if (!new[] { "lyrics", "cover", "covers" }.Contains(folder, StringComparer.OrdinalIgnoreCase)
                    && importer.SupportedExtensions.Contains(Path.GetExtension(file))) files.TryAdd(file, root);
            }
            foreach (var child in Directory.EnumerateDirectories(directory).Order(StringComparer.OrdinalIgnoreCase))
            {
                if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0) continue;
                Gather(child, root, files, allFiles, visited, errors, cancellationToken);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            errors.Add($"{directory}：{exception.Message}");
        }
    }
}
