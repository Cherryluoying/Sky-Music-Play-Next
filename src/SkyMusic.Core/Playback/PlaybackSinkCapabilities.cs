// 模块：SkyMusic.Core 播放领域 PlaybackSinkCapabilities
namespace SkyMusic.Core.Playback;

[Flags]
public enum PlaybackSinkCapabilities
{
    None = 0,
    ForegroundInput = 1 << 0,
    BackgroundWindow = 1 << 1,
    ScanCodes = 1 << 2,
    SimultaneousKeys = 1 << 3,
    MidiOutput = 1 << 4
}
