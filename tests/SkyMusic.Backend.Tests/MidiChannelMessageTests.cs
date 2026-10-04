// 模块：实时 MIDI 解码回归；覆盖双字节、14 位弯音和非法输入边界。
using SkyMusic.Core.Midi;

namespace SkyMusic.Backend.Tests;

public sealed class MidiChannelMessageTests
{
    [Theory]
    [InlineData(0x92, 60, 100, MidiChannelMessageKind.NoteOn, 60, 100, 2)]
    [InlineData(0x92, 60, 0, MidiChannelMessageKind.NoteOff, 60, 0, 2)]
    [InlineData(0x8F, 72, 45, MidiChannelMessageKind.NoteOff, 72, 45, 15)]
    [InlineData(0xB3, 64, 127, MidiChannelMessageKind.ControlChange, 64, 127, 3)]
    [InlineData(0xB3, 64, 0, MidiChannelMessageKind.ControlChange, 64, 0, 3)]
    [InlineData(0xE1, 0, 64, MidiChannelMessageKind.PitchBend, 8192, 0, 1)]
    [InlineData(0xE1, 127, 127, MidiChannelMessageKind.PitchBend, 16383, 0, 1)]
    [InlineData(0xE1, 0, 0, MidiChannelMessageKind.PitchBend, 0, 0, 1)]
    [InlineData(0xA0, 60, 50, MidiChannelMessageKind.PolyPressure, 60, 50, 0)]
    public void PreservesChannelAndControllerResolution(byte status, byte first, byte second,
        MidiChannelMessageKind kind, int data1, int data2, int channel)
    {
        Assert.True(MidiChannelMessage.TryParse([status, first, second], out var message));
        Assert.Equal(new(kind, data1, data2, channel), message);
        Assert.True(message.IsValid);
    }

    [Theory]
    [InlineData(0xC5, MidiChannelMessageKind.ProgramChange)]
    [InlineData(0xD5, MidiChannelMessageKind.ChannelPressure)]
    public void AcceptsTwoByteMessages(byte status, MidiChannelMessageKind kind)
    {
        Assert.True(MidiChannelMessage.TryParse([status, 42], out var message));
        Assert.Equal(new(kind, 42, 0, 5), message);
    }

    [Fact]
    public void RejectsTruncatedSystemAndInvalidData()
    {
        byte[][] invalid = [[], [0x90], [0x90, 60], [0xF0, 0, 0], [0x70, 0, 0], [0x90, 128, 0], [0xB0, 64, 128], [0xD0, 128]];
        foreach (var bytes in invalid) Assert.False(MidiChannelMessage.TryParse(bytes, out _));
        Assert.False(new MidiChannelMessage(MidiChannelMessageKind.PitchBend, 16384, 0, 0).IsValid);
        Assert.False(new MidiChannelMessage(MidiChannelMessageKind.ControlChange, 128, 0, 0).IsValid);
    }
}
