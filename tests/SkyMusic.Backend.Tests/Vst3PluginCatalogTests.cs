// 模块：SkyMusic.Backend.Tests 后端测试 Vst3PluginCatalogTests
using SkyMusic.Infrastructure.Plugins;

namespace SkyMusic.Backend.Tests;

public sealed class Vst3PluginCatalogTests
{
    [Fact]
    public void DiscoversAndDeduplicatesVst3Bundles()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SkyMusicTests", Guid.NewGuid().ToString("N"));
        var plugin = Path.Combine(directory, "Piano.vst3");
        try
        {
            Directory.CreateDirectory(plugin);
            var result = new Vst3PluginCatalog().Discover([directory, directory]);
            var item = Assert.Single(result);
            Assert.Equal("Piano", item.Name);
            Assert.Equal("VST3", item.Format);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
