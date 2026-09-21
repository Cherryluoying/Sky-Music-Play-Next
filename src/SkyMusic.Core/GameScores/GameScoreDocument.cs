// 模块：SkyMusic.Core 游戏乐谱领域 GameScoreDocument
namespace SkyMusic.Core.GameScores;

public sealed record GameScoreDocument(
    string Name,
    GameScoreProfile Profile,
    int Bpm,
    string Pitch,
    bool Reverb,
    IReadOnlyList<GameInstrumentLayer> Instruments,
    IReadOnlyList<GameScoreColumn> Columns,
    IReadOnlyList<int> Breakpoints)
{
    public const int MaxLayers = 52;

    public int KeyCount => Profile switch
    {
        GameScoreProfile.Sky => 15,
        GameScoreProfile.Genshin => 21,
        _ => 24
    };

    public static GameScoreDocument Create(string name, GameScoreProfile profile = GameScoreProfile.Sky)
    {
        var instrument = profile == GameScoreProfile.Genshin ? "Lyre" : "Piano";
        return new GameScoreDocument(
            name,
            profile,
            220,
            "C",
            false,
            [new GameInstrumentLayer(Guid.NewGuid(), instrument)],
            Enumerable.Range(0, 32).Select(_ => GameScoreColumn.Empty).ToArray(),
            [0]);
    }
}
