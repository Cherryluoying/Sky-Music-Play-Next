// 模块：SkyMusic.Backend.Tests 后端测试 Vst3ProcessHostIntegrationTests
using SkyMusic.Core.Plugins;
using SkyMusic.Infrastructure.Plugins;

namespace SkyMusic.Backend.Tests;

public sealed class Vst3ProcessHostIntegrationTests
{
    [Fact]
    public async Task LoadsInstrumentAndSendsMidiWhenConfigured()
    {
        var hostPath = Environment.GetEnvironmentVariable("SKYMUSIC_VST3_TEST_HOST");
        var pluginPath = Environment.GetEnvironmentVariable("SKYMUSIC_VST3_TEST_PLUGIN");
        if (string.IsNullOrWhiteSpace(hostPath) || string.IsNullOrWhiteSpace(pluginPath))
        {
            return;
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var host = new Vst3ProcessHost(new Vst3ProcessHostOptions(hostPath));
        var plugin = new InstrumentPluginInfo("integration", Path.GetFileNameWithoutExtension(pluginPath), pluginPath, "VST3");

        await host.LoadAsync(plugin, timeout.Token);
        await host.NoteOnAsync(60, 100, 0, timeout.Token);
        await Task.Delay(100, timeout.Token);
        await host.NoteOffAsync(60, 0, 0, timeout.Token);
        await host.AllNotesOffAsync(timeout.Token);

        Assert.Equal(InstrumentHostState.Loaded, host.Snapshot.State);
    }
}
