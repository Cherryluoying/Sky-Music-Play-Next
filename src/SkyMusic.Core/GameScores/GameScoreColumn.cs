// 模块：SkyMusic.Core 游戏乐谱领域 GameScoreColumn
namespace SkyMusic.Core.GameScores;

public sealed record GameScoreColumn(
    int TempoStep,
    IReadOnlyList<GameScoreNote> Notes)
{
    public static GameScoreColumn Empty { get; } = new(0, []);
}
