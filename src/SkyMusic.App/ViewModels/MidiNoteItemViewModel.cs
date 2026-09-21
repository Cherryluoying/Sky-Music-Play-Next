// 模块：SkyMusic.App 界面状态 MidiNoteItemViewModel
using SkyMusic.Core.Midi;

namespace SkyMusic.App.ViewModels;

public sealed record MidiNoteItemViewModel(string NoteName, string State, string Velocity, string Time)
{
    public static MidiNoteItemViewModel FromMessage(MidiNoteMessage message) => new(
        GetNoteName(message.Note),
        message.IsNoteOn ? "按下" : "松开",
        message.Velocity.ToString(),
        TimeSpan.FromMilliseconds(message.TimestampMicroseconds / 1_000d).ToString(@"mm\:ss\.fff"));

    private static string GetNoteName(int note)
    {
        string[] names = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];
        return $"{names[note % 12]}{(note / 12) - 1}";
    }
}
