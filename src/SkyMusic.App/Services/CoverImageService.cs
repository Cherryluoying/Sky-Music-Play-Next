// 模块：SkyMusic.App 桌面服务 CoverImageService
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace SkyMusic.App.Services;

public sealed class CoverImageService : IDisposable
{
    private readonly Dictionary<string, Bitmap?> _cache = [];

    public Bitmap? GetCover(string source)
    {
        if (_cache.TryGetValue(source, out var cached))
        {
            return cached;
        }

        // 统一处理资源和本地封面
        var bitmap = TryLoad(source);
        _cache[source] = bitmap;
        return bitmap;
    }

    private static Bitmap? TryLoad(string source)
    {
        try
        {
            var uri = new Uri(source, UriKind.RelativeOrAbsolute);
            using var stream = uri.IsAbsoluteUri && uri.Scheme == "avares"
                ? AssetLoader.Open(uri)
                : File.OpenRead(uri.IsAbsoluteUri ? uri.LocalPath : source);
            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        foreach (var bitmap in _cache.Values.OfType<Bitmap>().Distinct())
        {
            bitmap.Dispose();
        }

        _cache.Clear();
    }
}
