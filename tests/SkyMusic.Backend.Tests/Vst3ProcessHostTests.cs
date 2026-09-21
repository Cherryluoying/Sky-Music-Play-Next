// 模块：SkyMusic.Backend.Tests 后端测试 Vst3ProcessHostTests
using SkyMusic.Core.Plugins;
using SkyMusic.Infrastructure.Plugins;

namespace SkyMusic.Backend.Tests;

public sealed class Vst3ProcessHostTests
{
    [Fact]
    public async Task ReportsMissingNativeHost()
    {
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "SkyMusic.VstHost.exe");
        await using var host = new Vst3ProcessHost(new Vst3ProcessHostOptions(missing));
        var plugin = new InstrumentPluginInfo("test", "Test", "test.vst3", "VST3");

        var exception = await Assert.ThrowsAsync<FileNotFoundException>(() => host.LoadAsync(plugin).AsTask());

        Assert.Equal(missing, exception.FileName);
        Assert.Equal(InstrumentHostState.Faulted, host.Snapshot.State);
    }

    [Fact]
    public async Task RejectsMidiBeforePluginIsLoaded()
    {
        await using var host = new Vst3ProcessHost(new Vst3ProcessHostOptions("missing.exe"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => host.NoteOnAsync(60).AsTask());
    }
}
