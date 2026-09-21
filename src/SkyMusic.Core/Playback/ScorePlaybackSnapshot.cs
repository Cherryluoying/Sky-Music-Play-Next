// 模块：SkyMusic.Core 播放领域 ScorePlaybackSnapshot
namespace SkyMusic.Core.Playback;

public sealed record ScorePlaybackSnapshot(
    string? ScoreTitle,
    string TargetId,
    int NoteCount,
    AutoPlaySnapshot Session,
    IReadOnlyList<string> Warnings,
    ScoreTimingSettings Timing);
