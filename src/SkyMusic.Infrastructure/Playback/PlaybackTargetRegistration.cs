// 模块：SkyMusic.Infrastructure 播放领域 PlaybackTargetRegistration
using SkyMusic.Core.Playback;
using SkyMusic.Core.Services;

namespace SkyMusic.Infrastructure.Playback;

public sealed record PlaybackTargetRegistration(
    PlaybackTarget Target,
    Func<IPlaybackEventSink> CreateSink);
