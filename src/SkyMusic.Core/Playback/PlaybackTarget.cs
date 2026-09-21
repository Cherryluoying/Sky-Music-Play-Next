// 模块：SkyMusic.Core 播放领域 PlaybackTarget
namespace SkyMusic.Core.Playback;

public sealed record PlaybackTarget(
    string Id,
    string DisplayName,
    string Description,
    int KeyCount,
    PlaybackSinkCapabilities Capabilities,
    IReadOnlyList<string>? PreferredProcessNames = null);
