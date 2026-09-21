// 模块：SkyMusic.Core 播放领域 PlaybackEvent
namespace SkyMusic.Core.Playback;

public enum PlaybackEventType
{
    KeyUp,
    KeyDown
}

public readonly record struct PlaybackEvent(
    long TimeMicroseconds,
    int MidiNote,
    PlaybackEventType Type,
    byte Velocity,
    int Track,
    int Channel);
