// 模块：本地音频标签和内嵌封面读取，复用随应用提供的 FFprobe / FFmpeg。
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace SkyMusic.Infrastructure.Media;

internal sealed record AudioFileMetadata(
    string? Title = null, string? Artist = null, string? Album = null, string? Author = null,
    TimeSpan Duration = default, int? CoverStream = null, string? CoverPath = null);

internal static class AudioMetadataReader
{
    // 标签或图片损坏不能阻断歌曲导入；缺失字段由调用方保留原值。
    public static async Task<AudioFileMetadata> ReadAsync(
        string? ffmpegPath, string path, string coverDirectory, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ffmpegPath) || !File.Exists(path)) return new();
        var probe = FfmpegMediaDurationProbe.FindFfprobe(ffmpegPath, AppContext.BaseDirectory);
        if (probe is null) return new();
        AudioFileMetadata metadata;
        try
        {
            var json = await RunAsync(probe,
                ["-v", "error", "-show_entries",
                 "format=duration:format_tags:stream=index,codec_type:stream_tags:stream_disposition=attached_pic",
                 "-of", "json", Path.GetFullPath(path)], cancellationToken).ConfigureAwait(false);
            metadata = json is null ? new() : Parse(json);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or System.ComponentModel.Win32Exception)
        {
            return new();
        }
        if (metadata.CoverStream is not { } stream) return metadata;

        // 以源文件签名命名缓存；缩小过大的专辑图，避免每次打开曲库解码原尺寸图片。
        string? temporary = null;
        try
        {
            var file = new FileInfo(path);
            var key = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                Encoding.UTF8.GetBytes($"{file.FullName}|{file.Length}|{file.LastWriteTimeUtc.Ticks}"))).ToLowerInvariant();
            Directory.CreateDirectory(coverDirectory);
            var cover = Path.Combine(coverDirectory, $"{key}.jpg");
            if (!File.Exists(cover))
            {
                temporary = Path.Combine(coverDirectory, $"{key}.{Guid.NewGuid():N}.tmp.jpg");
                var result = await RunAsync(ffmpegPath,
                    ["-nostdin", "-v", "error", "-i", file.FullName, "-map", $"0:{stream}",
                     "-frames:v", "1", "-an", "-vf", "scale=1024:1024:force_original_aspect_ratio=decrease",
                     "-q:v", "2", "-y", temporary], cancellationToken).ConfigureAwait(false);
                if (result is null || !File.Exists(temporary) || new FileInfo(temporary).Length == 0) return metadata;
                File.Move(temporary, cover, overwrite: true);
            }
            return metadata with { CoverPath = cover };
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            return metadata;
        }
        finally
        {
            if (temporary is not null)
            {
                try { File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }

    // 容器标签优先，音频流标签补缺；大小写兼容 ID3、Vorbis、MP4 等标签习惯。
    internal static AudioFileMetadata Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        void AddTags(JsonElement element)
        {
            if (!element.TryGetProperty("tags", out var values) || values.ValueKind != JsonValueKind.Object) return;
            foreach (var tag in values.EnumerateObject())
                if (tag.Value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(tag.Value.GetString()))
                    tags.TryAdd(tag.Name, tag.Value.GetString()!.Trim().Trim('\0'));
        }
        var root = document.RootElement;
        var duration = TimeSpan.Zero;
        if (root.TryGetProperty("format", out var format))
        {
            AddTags(format);
            if (format.TryGetProperty("duration", out var time) &&
                double.TryParse(time.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) &&
                double.IsFinite(seconds) && seconds > 0 && seconds < TimeSpan.MaxValue.TotalSeconds)
                duration = TimeSpan.FromSeconds(seconds);
        }
        int? coverStream = null;
        if (root.TryGetProperty("streams", out var streams) && streams.ValueKind == JsonValueKind.Array)
        {
            foreach (var stream in streams.EnumerateArray())
            {
                if (stream.TryGetProperty("codec_type", out var type) && type.GetString() == "audio") AddTags(stream);
                if (!stream.TryGetProperty("disposition", out var disposition) ||
                    !disposition.TryGetProperty("attached_pic", out var attached) || !attached.TryGetInt32(out var flag) || flag != 1 ||
                    !stream.TryGetProperty("index", out var index) || !index.TryGetInt32(out var number)) continue;
                var front = stream.TryGetProperty("tags", out var imageTags) && imageTags.EnumerateObject().Any(tag =>
                    tag.Value.ValueKind == JsonValueKind.String &&
                    (tag.Value.GetString()?.Contains("front", StringComparison.OrdinalIgnoreCase) ?? false));
                if (coverStream is null || front) coverStream = number;
            }
        }
        string? Find(params string[] names) => names.Select(name => tags.GetValueOrDefault(name)).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
        return new(Find("title"), Find("artist", "album_artist", "albumartist", "performer"), Find("album"),
            Find("composer", "author", "writer", "lyricist"), duration, coverStream);
    }

    // 同时消费 stdout/stderr，并对整个读取过程计时，避免异常媒体或工具进程卡住界面。
    private static async Task<string?> RunAsync(string executable, string[] arguments, CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true,
            RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
        } };
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        if (!process.Start()) return null;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var errors = process.StandardError.ReadToEndAsync(timeout.Token);
            await Task.WhenAll(output, errors, process.WaitForExitAsync(timeout.Token)).ConfigureAwait(false);
            return process.ExitCode == 0 ? await output.ConfigureAwait(false) : null;
        }
        catch (OperationCanceledException)
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
            cancellationToken.ThrowIfCancellationRequested();
            return null;
        }
    }
}
