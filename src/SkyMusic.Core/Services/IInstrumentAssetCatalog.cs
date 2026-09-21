// 模块：SkyMusic.Core 桌面服务 IInstrumentAssetCatalog
using SkyMusic.Core.GameScores;

namespace SkyMusic.Core.Services;

public interface IInstrumentAssetCatalog
{
    string AudioRoot { get; }

    IReadOnlyList<InstrumentAssetDefinition> GetInstruments(GameScoreProfile profile);

    InstrumentAssetDefinition? Find(GameScoreProfile profile, string instrumentId);

    string? GetSamplePath(GameScoreProfile profile, string instrumentId, int noteIndex);

    string? GetEffectPath(string relativePath);
}
