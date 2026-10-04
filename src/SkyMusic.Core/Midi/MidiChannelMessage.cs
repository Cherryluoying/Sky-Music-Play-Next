// 模块：实时 MIDI 通道消息，保持与文件事件协议一致的类型和数值范围。
namespace SkyMusic.Core.Midi;

public enum MidiChannelMessageKind
{
    NoteOn, NoteOff, ControlChange, PitchBend, ChannelPressure, PolyPressure, ProgramChange
}

public readonly record struct MidiChannelMessage(
    MidiChannelMessageKind Kind, int Data1, int Data2, int Channel)
{
    public bool IsValid => Channel is >= 0 and <= 15 && Data2 is >= 0 and <= 127 &&
        Kind is >= MidiChannelMessageKind.NoteOn and <= MidiChannelMessageKind.ProgramChange &&
        Data1 >= 0 && Data1 <= (Kind == MidiChannelMessageKind.PitchBend ? 16383 : 127);

    // RtMidi 已还原 running status；两字节消息不能按三字节音符读取。
    public static bool TryParse(ReadOnlySpan<byte> bytes, out MidiChannelMessage message)
    {
        message = default;
        if (bytes.Length < 2 || bytes[0] is < 0x80 or >= 0xF0 || bytes[1] > 127)
            return false;
        var type = bytes[0] & 0xF0;
        var shortMessage = type is 0xC0 or 0xD0;
        if (!shortMessage && (bytes.Length < 3 || bytes[2] > 127))
            return false;
        var data1 = (int)bytes[1];
        var data2 = shortMessage ? 0 : bytes[2];
        var kind = type switch
        {
            0x80 => MidiChannelMessageKind.NoteOff,
            0x90 => data2 == 0 ? MidiChannelMessageKind.NoteOff : MidiChannelMessageKind.NoteOn,
            0xA0 => MidiChannelMessageKind.PolyPressure,
            0xB0 => MidiChannelMessageKind.ControlChange,
            0xC0 => MidiChannelMessageKind.ProgramChange,
            0xD0 => MidiChannelMessageKind.ChannelPressure,
            _ => MidiChannelMessageKind.PitchBend
        };
        if (type == 0xE0)
        {
            data1 |= data2 << 7;
            data2 = 0;
        }
        message = new(kind, data1, data2, bytes[0] & 0x0F);
        return true;
    }
}
