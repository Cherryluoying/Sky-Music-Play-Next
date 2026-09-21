// 模块：SkyMusic.Backend.Tests 后端测试 CloudLyricsProviderTests
using System.Net;
using System.Text;
using SkyMusic.Core.Lyrics;
using SkyMusic.Infrastructure.Lyrics;

namespace SkyMusic.Backend.Tests;

public sealed class CloudLyricsProviderTests
{
    [Fact]
    public async Task DownloadsAndCachesTimedLyrics()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SkyMusicTests", Guid.NewGuid().ToString("N"));
        try
        {
            using var client = new HttpClient(new LyricsHandler()) { BaseAddress = new Uri("http://localhost/") };
            var provider = new CloudLyricsProvider(client, directory);

            var result = await provider.GetLyricsAsync(new LyricsQuery("Song", "Artist", "Album", TimeSpan.FromMinutes(3)));

            Assert.NotNull(result);
            Assert.Equal("cloud-1", result.Id);
            Assert.Equal(TimeSpan.FromMilliseconds(1250), Assert.Single(result.Lines).Timestamp);
            Assert.Single(Directory.GetFiles(directory, "*.json"));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    private sealed class LyricsHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var json = request.RequestUri!.AbsolutePath.EndsWith("/search", StringComparison.Ordinal)
                ? "[{\"id\":\"cloud-1\",\"title\":\"Song\",\"artist\":\"Artist\"}]"
                : "{\"id\":\"cloud-1\",\"provider\":\"test\",\"lines\":[{\"timeMs\":1250,\"text\":\"line\"}]}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }
}
