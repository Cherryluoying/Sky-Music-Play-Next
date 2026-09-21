// 模块：SkyMusic.Core 工程领域 TempoChange
namespace SkyMusic.Core.Projects;

public readonly record struct TempoChange(long Tick, int MicrosecondsPerQuarterNote)
{
    public double BeatsPerMinute => 60_000_000d / MicrosecondsPerQuarterNote;
}
