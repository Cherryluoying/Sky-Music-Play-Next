// 模块：SkyMusic.Backend.Tests 后端测试 JsonAppSettingsStoreTests
using SkyMusic.Core.Execution;
using SkyMusic.Core.Settings;
using SkyMusic.Infrastructure.Settings;

namespace SkyMusic.Backend.Tests;

public sealed class JsonAppSettingsStoreTests
{
    [Fact]
    public async Task SavesAndLoadsAllSettingGroups()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SkyMusicTests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "settings.json");
        try
        {
            var store = new JsonAppSettingsStore(path);
            var expected = new AppSettings
            {
                General = new GeneralSettings
                {
                    DefaultPlaybackTargetId = "genshin-21",
                    RememberLastTarget = false
                },
                ExternalTools = new ExternalToolSettings
                {
                    FfmpegPath = @"C:\Tools\ffmpeg.exe",
                    PianoTransPath = @"C:\Tools\PianoTrans"
                },
                AudioPlugins = new AudioPluginSettings
                {
                    Vst3SearchPaths = [@"C:\VST3"],
                    BufferSize = 256
                },
                Performance = new PerformanceSettings
                {
                    Mode = WorkloadExecutionMode.SingleThread,
                    MaximumConcurrency = 1
                },
                Network = new NetworkSettings
                {
                    CloudServiceUrl = "https://music.example.test/",
                    TimeoutSeconds = 12
                },
                Storage = new StorageSettings
                {
                    CacheDirectory = @"C:\Cache",
                    ScoreLibraryDirectory = @"D:\曲谱库",
                    MidiLibraryDirectory = @"D:\MIDI 库"
                }
            };

            await store.SaveAsync(expected);
            var actual = await store.LoadAsync();

            Assert.Equal(expected.General, actual.General);
            Assert.Equal(expected.ExternalTools, actual.ExternalTools);
            Assert.Equal(expected.AudioPlugins.Vst3SearchPaths, actual.AudioPlugins.Vst3SearchPaths);
            Assert.Equal(expected.AudioPlugins.BufferSize, actual.AudioPlugins.BufferSize);
            Assert.Equal(expected.Performance, actual.Performance);
            Assert.Equal(expected.Network, actual.Network);
            Assert.Equal(expected.Storage, actual.Storage);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    [Fact]
    public async Task MissingFileReturnsDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), "SkyMusicTests", Guid.NewGuid().ToString("N"), "settings.json");
        var settings = await new JsonAppSettingsStore(path).LoadAsync();

        Assert.Equal("sky-15", settings.General.DefaultPlaybackTargetId);
        Assert.Equal("http://127.0.0.1:8787/", settings.Network.CloudServiceUrl);
    }
}
