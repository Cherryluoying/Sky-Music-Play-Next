// 模块：SkyMusic.Core 工程领域 ProjectNote
namespace SkyMusic.Core.Projects;

public sealed record ProjectNote(
    Guid Id,
    long StartTick,
    long LengthTicks,
    int MidiNote,
    byte Velocity = 100,
    int Channel = 0)
{
    public long EndTick => StartTick + LengthTicks;
}
