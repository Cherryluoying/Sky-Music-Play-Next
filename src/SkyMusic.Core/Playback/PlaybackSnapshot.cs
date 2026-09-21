// 模块：SkyMusic.Core 播放领域 PlaybackSnapshot
using SkyMusic.Core.Models;

namespace SkyMusic.Core.Playback;

public sealed record PlaybackSnapshot(
    MusicTrack? Track,
    PlaybackState State,
    TimeSpan Position,
    TimeSpan Duration);
