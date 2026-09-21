// 模块：SkyMusic.Backend.Tests 后端测试 GenshinMusicAssetCatalogTests
using SkyMusic.Core.GameScores;
using SkyMusic.Infrastructure.Assets;

namespace SkyMusic.Backend.Tests;

public sealed class GenshinMusicAssetCatalogTests
{
    [Fact]
    public void MigratedCatalogContainsAllInstrumentSamples()
    {
        var audioRoot = FindAudioRoot();
        var catalog = new GenshinMusicAssetCatalog(audioRoot);

        Assert.Equal(34, catalog.GetInstruments(GameScoreProfile.Sky).Count);
        Assert.Equal(10, catalog.GetInstruments(GameScoreProfile.Genshin).Count);
        Assert.Equal(15, catalog.Find(GameScoreProfile.Sky, "Piano")?.NoteCount);
        Assert.Equal(21, catalog.Find(GameScoreProfile.Genshin, "Lyre")?.NoteCount);
        Assert.NotNull(catalog.GetEffectPath(Path.Combine("MetronomeSFX", "bar.mp3")));
        Assert.NotNull(catalog.GetEffectPath("reverb4.wav"));

        foreach (var profile in new[] { GameScoreProfile.Sky, GameScoreProfile.Genshin })
        foreach (var instrument in catalog.GetInstruments(profile))
        for (var index = 0; index < instrument.NoteCount; index++)
        {
            var sample = catalog.GetSamplePath(profile, instrument.Id, index);
            Assert.NotNull(sample);
            Assert.True(File.Exists(sample));
            Assert.Equal(index.ToString(), Path.GetFileNameWithoutExtension(sample));
        }
    }

    [Fact]
    public void MissingAndUnsafeAssetsReturnNull()
    {
        var catalog = new GenshinMusicAssetCatalog(FindAudioRoot());

        Assert.Null(catalog.GetSamplePath(GameScoreProfile.Sky, "Missing", 0));
        Assert.Null(catalog.GetSamplePath(GameScoreProfile.Sky, "Piano", -1));
        Assert.Null(catalog.GetSamplePath(GameScoreProfile.Sky, "Piano", 99));
        Assert.Null(catalog.GetEffectPath("..\\outside.wav"));
        Assert.Null(catalog.GetEffectPath(Path.GetFullPath("outside.wav")));
    }

    [Fact]
    public void CustomProfileUsesSkySampleSets()
    {
        var catalog = new GenshinMusicAssetCatalog(FindAudioRoot());

        Assert.Same(
            catalog.GetInstruments(GameScoreProfile.Sky),
            catalog.GetInstruments(GameScoreProfile.Custom));
    }

    private static string FindAudioRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "SkyMusic.App",
                "Assets",
                "GenshinMusic",
                "Audio");
            if (Directory.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("未找到迁移后的乐器音频目录");
    }
}
