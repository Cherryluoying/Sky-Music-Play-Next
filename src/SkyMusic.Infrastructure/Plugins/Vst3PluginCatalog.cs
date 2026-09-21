// 模块：SkyMusic.Infrastructure 插件领域 Vst3PluginCatalog
using System.Security.Cryptography;
using System.Text;
using SkyMusic.Core.Plugins;

namespace SkyMusic.Infrastructure.Plugins;

public sealed class Vst3PluginCatalog
{
    public IReadOnlyList<InstrumentPluginInfo> Discover(IEnumerable<string> searchPaths)
    {
        var plugins = new List<InstrumentPluginInfo>();
        foreach (var root in searchPaths.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var candidates = Directory.EnumerateFileSystemEntries(root, "*.vst3", new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = true,
                ReturnSpecialDirectories = false
            });

            foreach (var path in candidates)
            {
                var fullPath = Path.GetFullPath(path);
                var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fullPath.ToUpperInvariant())))[..16];
                plugins.Add(new InstrumentPluginInfo(id, Path.GetFileNameWithoutExtension(path), fullPath, "VST3"));
            }
        }

        return plugins
            .DistinctBy(plugin => plugin.Path, StringComparer.OrdinalIgnoreCase)
            .OrderBy(plugin => plugin.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<string> GetDefaultSearchPaths()
    {
        var paths = new List<string>();
        var commonFiles = Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles);
        if (!string.IsNullOrWhiteSpace(commonFiles))
        {
            paths.Add(Path.Combine(commonFiles, "VST3"));
        }

        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(local))
        {
            paths.Add(Path.Combine(local, "Programs", "Common", "VST3"));
        }

        return paths;
    }
}
