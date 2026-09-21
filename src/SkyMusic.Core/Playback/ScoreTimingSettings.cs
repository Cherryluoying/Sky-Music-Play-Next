// 模块：SkyMusic.Core 播放领域 ScoreTimingSettings
namespace SkyMusic.Core.Playback;

public sealed record ScoreTimingSettings(
    int IntervalAdjustmentMilliseconds = 0,
    int KeyReleaseDelayMilliseconds = 0)
{
    public static ScoreTimingSettings Default { get; } = new();
}
