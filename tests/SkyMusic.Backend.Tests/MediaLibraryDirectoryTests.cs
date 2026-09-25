// 模块：曲谱分类目录回归；验证真实导入使用配置路径、保留源文件并拒绝相对路径。
using SkyMusic.Core.Settings;
using SkyMusic.Infrastructure.Catalog;
using SkyMusic.Infrastructure.Scores;
using SkyMusic.Infrastructure.Settings;

namespace SkyMusic.Backend.Tests;

public sealed class MediaLibraryDirectoryTests
{
    [Fact]
    public async Task ScoreImportUsesConfiguredDirectoryWithoutChangingMidiOrSource()
    {
        var root = Path.Combine(Path.GetTempPath(), "SkyMusicTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var source = Path.Combine(root, "原始曲谱.txt");
            const string json = """[{"name":"目录测试","author":"Sky","bpm":120,"songNotes":[{"time":0,"key":"Key0","duration":90}]}]""";
            await File.WriteAllTextAsync(source, json);
            var scoreDirectory = Path.Combine(root, "用户曲谱");
            var midiDirectory = Path.Combine(root, "用户 MIDI");
            var store = new JsonAppSettingsStore(Path.Combine(root, "settings.json"));
            await store.SaveAsync(new AppSettings { Storage = new StorageSettings
            {
                ScoreLibraryDirectory = scoreDirectory, MidiLibraryDirectory = midiDirectory
            } });
            var importer = new MediaImportService(Path.Combine(root, "library"), new ScoreImportService(), settingsStore: store);
            var imported = await importer.ImportAsync(source);
            Assert.Equal(scoreDirectory, Path.GetDirectoryName(imported.SourcePath));
            Assert.Equal(json, await File.ReadAllTextAsync(imported.SourcePath!));
            Assert.Equal(json, await File.ReadAllTextAsync(source));
            Assert.False(Directory.Exists(midiDirectory));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void MissingConfigurationUsesLegacyDirectoriesAndRelativePathIsRejected()
    {
        var root = Path.Combine(Path.GetTempPath(), "SkyMusicTests", "library");
        Assert.Equal(Path.Combine(root, "musicscore"), MediaLibraryDirectories.Score(new StorageSettings(), root));
        Assert.Equal(Path.Combine(root, "midi"), MediaLibraryDirectories.Midi(new StorageSettings(), root));
        Assert.Throws<ArgumentException>(() => MediaLibraryDirectories.Score(new StorageSettings { ScoreLibraryDirectory = "relative" }, root));
    }
}
