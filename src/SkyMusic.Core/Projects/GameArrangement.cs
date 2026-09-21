// 模块：SkyMusic.Core 工程领域 GameArrangement
using SkyMusic.Core.GameScores;

namespace SkyMusic.Core.Projects;

public enum OutOfRangePolicy
{
    Keep,
    Drop,
    FoldOctave,
    Clamp
}

public sealed record GameArrangement(
    Guid Id,
    string Name,
    string InstrumentProfileId,
    int Transpose,
    OutOfRangePolicy OutOfRangePolicy,
    IReadOnlyList<Guid> EnabledTrackIds,
    IReadOnlyDictionary<Guid, int> NoteOverrides,
    int QuantizeDivision = 16,
    bool ArpeggiateChords = false,
    GameScoreDocument? GameScore = null);
