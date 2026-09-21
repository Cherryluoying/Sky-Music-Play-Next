// 模块：SkyMusic.Infrastructure 歌词领域 CloudLyricsProvider
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SkyMusic.Core.Lyrics;
using SkyMusic.Core.Models;
using SkyMusic.Core.Services;

namespace SkyMusic.Infrastructure.Lyrics;

public sealed class CloudLyricsProvider : ILyricsProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly string _cacheDirectory;

    public CloudLyricsProvider(HttpClient httpClient, string cacheDirectory)
    {
        _httpClient = httpClient;
        _cacheDirectory = cacheDirectory;
    }

    // 优先读取本地缓存并在缺失时请求云端歌词
    public async ValueTask<LyricsResult?> GetLyricsAsync(
        LyricsQuery query,
        CancellationToken cancellationToken = default)
    {
        var cachePath = GetCachePath(query);
        try
        {
            var queryString = $"title={Uri.EscapeDataString(query.Title)}&artist={Uri.EscapeDataString(query.Artist)}";
            var search = await _httpClient.GetFromJsonAsync<List<SearchResponse>>(
                $"v1/lyrics/search?{queryString}",
                JsonOptions,
                cancellationToken);
            var match = search?.FirstOrDefault();
            if (match is null)
            {
                return await ReadCacheAsync(cachePath, cancellationToken);
            }

            using var response = await _httpClient.GetAsync(
                $"v1/lyrics/{Uri.EscapeDataString(match.Id)}",
                cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return await ReadCacheAsync(cachePath, cancellationToken);
            }

            response.EnsureSuccessStatusCode();
            var document = await response.Content.ReadFromJsonAsync<LyricsResponse>(JsonOptions, cancellationToken);
            if (document is null)
            {
                return await ReadCacheAsync(cachePath, cancellationToken);
            }

            var result = new LyricsResult(
                document.Id,
                string.IsNullOrWhiteSpace(document.Provider) ? "SkyMusic Cloud" : document.Provider,
                document.Lines
                    .OrderBy(line => line.TimeMs)
                    .Select(line => new LyricLine(TimeSpan.FromMilliseconds(line.TimeMs), line.Text))
                    .ToArray());
            await WriteCacheAsync(cachePath, result, cancellationToken);
            return result;
        }
        catch (Exception exception) when (
            (exception is HttpRequestException or IOException or TaskCanceledException or JsonException) &&
            !cancellationToken.IsCancellationRequested)
        {
            // 网络失败时回退本地缓存，保持播放页可用
            return await ReadCacheAsync(cachePath, cancellationToken);
        }
    }

    private string GetCachePath(LyricsQuery query)
    {
        var key = $"{query.Title}\n{query.Artist}\n{query.Album}".ToLowerInvariant();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
        return Path.Combine(_cacheDirectory, hash + ".json");
    }

    private static async ValueTask<LyricsResult?> ReadCacheAsync(
        string path,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<LyricsResult>(stream, JsonOptions, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            return null;
        }
    }

    // 使用临时文件替换保证缓存写入完整
    private static async ValueTask WriteCacheAsync(
        string path,
        LyricsResult result,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, result, JsonOptions, cancellationToken);
    }

    private sealed record SearchResponse(string Id, string Title, string Artist);

    private sealed record LyricsResponse(string Id, string Provider, IReadOnlyList<LineResponse> Lines);

    private sealed record LineResponse(long TimeMs, string Text);
}
