// 模块：本地封面和歌词伴随文件识别；只关联明确匹配的文件，不任意挑选目录中的图片。
using SkyMusic.Core.Models;

namespace SkyMusic.Infrastructure.Catalog;

internal static class LocalMediaCompanions
{
    private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".webp", ".bmp"];

    // 同名封面优先，其次 cover/covers 子目录，最后才使用同目录公共专辑图。
    public static MusicTrack Attach(MusicTrack track, string sourcePath, string? libraryRoot,
        Func<string, bool>? exists = null)
    {
        exists ??= File.Exists;
        var directory = Path.GetDirectoryName(sourcePath)!;
        var name = Path.GetFileNameWithoutExtension(sourcePath);
        var roots = new[] { directory, libraryRoot }.OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase);
        var cover = Find(directory, [name], ImageExtensions, exists);
        var lyrics = Find(directory, [name], [".lrc"], exists);
        foreach (var root in roots)
        {
            foreach (var folder in new[] { "cover", "covers" })
            {
                cover ??= Find(Path.Combine(root, folder), [name, track.Id], ImageExtensions, exists);
                // 旧库封面使用缓存哈希命名，搬库后通过原封面文件名重定位。
                if (!track.CoverSource.StartsWith("avares:", StringComparison.OrdinalIgnoreCase))
                {
                    var candidate = Path.Combine(root, folder, Path.GetFileName(track.CoverSource));
                    if (cover is null && exists(candidate)) cover = candidate;
                }
            }
            lyrics ??= Find(Path.Combine(root, "lyrics"), [name, track.Id], [".lrc"], exists);
        }
        cover ??= Find(directory, ["cover", "folder", "front"], ImageExtensions, exists);
        return track with { CoverSource = cover ?? track.CoverSource, LyricsSourcePath = lyrics ?? track.LyricsSourcePath };
    }

    private static string? Find(string directory, string[] names, string[] extensions, Func<string, bool> exists)
    {
        foreach (var name in names)
        foreach (var extension in extensions)
        {
            var candidate = Path.Combine(directory, name + extension);
            if (exists(candidate)) return candidate;
        }
        return null;
    }
}
