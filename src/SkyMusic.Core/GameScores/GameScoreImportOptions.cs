// 模块：SkyMusic.Core 游戏乐谱领域 GameScoreImportOptions
namespace SkyMusic.Core.GameScores;

public sealed record GameScoreImportOptions(
    GameScoreProfile Profile = GameScoreProfile.Sky,
    int Bpm = 120,
    int TransposeSemitones = 0,
    int OctaveFoldCount = 4,
    bool IncludeAccidentals = true,
    int Precision = 4,
    long? ChordThresholdMicroseconds = null,
    IReadOnlySet<int>? IncludedTracks = null,
    IReadOnlyDictionary<int, int>? TrackTransposeSemitones = null);
