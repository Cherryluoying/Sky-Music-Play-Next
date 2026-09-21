// 模块：SkyMusic.Core 界面模型 NoteEvent
namespace SkyMusic.Core.Models;

public readonly record struct NoteEvent(
    int MidiNote,
    long StartMicroseconds,
    long DurationMicroseconds,
    byte Velocity = 100,
    int Track = 0,
    int Channel = 0)
{
    public long EndMicroseconds => StartMicroseconds + DurationMicroseconds;
}
