// 模块：SkyMusic.Core MIDI 领域 MidiNoteMessage
namespace SkyMusic.Core.Midi;

public readonly record struct MidiNoteMessage(
    int Note,
    byte Velocity,
    int Channel,
    bool IsNoteOn,
    long TimestampMicroseconds);
