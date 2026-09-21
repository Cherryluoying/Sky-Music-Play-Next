// 模块：SkyMusic.Core 游戏乐谱领域 GameScoreMidiConversionResult
namespace SkyMusic.Core.GameScores;

public sealed record GameScoreMidiConversionResult(
    GameScoreDocument Document,
    int ImportedNoteCount,
    int AccidentalNoteCount,
    int BelowRangeCount,
    int AboveRangeCount,
    IReadOnlyList<int> SourceTracks);
