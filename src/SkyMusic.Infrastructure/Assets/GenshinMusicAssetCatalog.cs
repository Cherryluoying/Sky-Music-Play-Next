// 模块：SkyMusic.Infrastructure 素材目录 GenshinMusicAssetCatalog
using SkyMusic.Core.GameScores;
using SkyMusic.Core.Services;

namespace SkyMusic.Infrastructure.Assets;

public sealed class GenshinMusicAssetCatalog : IInstrumentAssetCatalog
{
    private readonly IReadOnlyDictionary<GameScoreProfile, IReadOnlyList<InstrumentAssetDefinition>> _instruments;

    public GenshinMusicAssetCatalog(string? audioRoot = null)
    {
        AudioRoot = Path.GetFullPath(audioRoot ?? Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "GenshinMusic",
            "Audio"));
        _instruments = new Dictionary<GameScoreProfile, IReadOnlyList<InstrumentAssetDefinition>>
        {
            [GameScoreProfile.Sky] = Discover(GameScoreProfile.Sky, "sky"),
            [GameScoreProfile.Genshin] = Discover(GameScoreProfile.Genshin, "genshin")
        };
    }

    public string AudioRoot { get; }

    public IReadOnlyList<InstrumentAssetDefinition> GetInstruments(GameScoreProfile profile) => profile switch
    {
        GameScoreProfile.Custom => GetInstruments(GameScoreProfile.Sky),
        _ => _instruments.TryGetValue(profile, out var instruments) ? instruments : []
    };

    public InstrumentAssetDefinition? Find(GameScoreProfile profile, string instrumentId) =>
        GetInstruments(profile).FirstOrDefault(item =>
            string.Equals(item.Id, instrumentId, StringComparison.OrdinalIgnoreCase));

    public string? GetSamplePath(GameScoreProfile profile, string instrumentId, int noteIndex)
    {
        var instrument = Find(profile, instrumentId);
        return instrument is not null && noteIndex >= 0 && noteIndex < instrument.SamplePaths.Count
            ? instrument.SamplePaths[noteIndex]
            : null;
    }

    public string? GetEffectPath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
            return null;

        var candidate = Path.GetFullPath(Path.Combine(AudioRoot, relativePath));
        var prefix = AudioRoot.EndsWith(Path.DirectorySeparatorChar)
            ? AudioRoot
            : AudioRoot + Path.DirectorySeparatorChar;
        return candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && File.Exists(candidate)
            ? candidate
            : null;
    }

    private IReadOnlyList<InstrumentAssetDefinition> Discover(GameScoreProfile profile, string directoryName)
    {
        var profileRoot = Path.Combine(AudioRoot, directoryName);
        if (!Directory.Exists(profileRoot))
            return [];

        return Directory.EnumerateDirectories(profileRoot)
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .Select(path => new InstrumentAssetDefinition(
                Path.GetFileName(path),
                profile,
                DiscoverSamples(path)))
            .Where(item => item.NoteCount > 0)
            .ToArray();
    }

    private static IReadOnlyList<string> DiscoverSamples(string directory)
    {
        // 仅接受连续数字命名 避免顺序受文件系统影响
        var indexed = Directory.EnumerateFiles(directory, "*.mp3")
            .Select(path => (Path: path, Name: Path.GetFileNameWithoutExtension(path)))
            .Where(item => int.TryParse(item.Name, out _))
            .ToDictionary(item => int.Parse(item.Name), item => item.Path);
        var samples = new List<string>();
        while (indexed.TryGetValue(samples.Count, out var path))
            samples.Add(path);
        return samples;
    }
}
