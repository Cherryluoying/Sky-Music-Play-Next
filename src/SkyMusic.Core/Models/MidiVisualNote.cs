// 模块：SkyMusic.Core MIDI 可视化音符
namespace SkyMusic.Core.Models;

public sealed record MidiVisualNote(
    int Note,
    TimeSpan Start,
    TimeSpan Duration,
    byte Velocity,
    int Track);
