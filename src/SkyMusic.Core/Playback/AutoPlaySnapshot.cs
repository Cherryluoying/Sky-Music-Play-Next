// 模块：SkyMusic.Core 播放领域 AutoPlaySnapshot
namespace SkyMusic.Core.Playback;

public sealed record AutoPlaySnapshot(
    AutoPlayState State,
    long PositionMicroseconds,
    long DurationMicroseconds,
    double Speed,
    string? Error = null)
{
    public double Progress => DurationMicroseconds == 0
        ? 0
        : Math.Clamp((double)PositionMicroseconds / DurationMicroseconds, 0, 1);
}
