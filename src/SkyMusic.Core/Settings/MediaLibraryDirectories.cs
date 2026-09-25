// 模块：媒体分类目录解析；配置为空时沿用现有目录，配置路径必须是绝对路径。
namespace SkyMusic.Core.Settings;

public static class MediaLibraryDirectories
{
    public static string DefaultRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SkyMusicPlay", "Library");

    public static string Score(StorageSettings settings, string? root = null)
        => Resolve(settings.ScoreLibraryDirectory, root ?? DefaultRoot, "musicscore");

    public static string Midi(StorageSettings settings, string? root = null)
        => Resolve(settings.MidiLibraryDirectory, root ?? DefaultRoot, "midi");

    private static string Resolve(string? configured, string root, string folder)
    {
        if (string.IsNullOrWhiteSpace(configured)) return Path.GetFullPath(Path.Combine(root, folder));
        var path = configured.Trim();
        if (!Path.IsPathFullyQualified(path)) throw new ArgumentException("曲库目录必须使用完整的绝对路径");
        return Path.GetFullPath(path);
    }
}
