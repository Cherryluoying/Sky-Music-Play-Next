// 模块：SkyMusic.Core 游戏乐谱领域 InstrumentAssetDefinition
namespace SkyMusic.Core.GameScores;

public sealed record InstrumentAssetDefinition(
    string Id,
    GameScoreProfile Profile,
    IReadOnlyList<string> SamplePaths)
{
    public int NoteCount => SamplePaths.Count;
}
