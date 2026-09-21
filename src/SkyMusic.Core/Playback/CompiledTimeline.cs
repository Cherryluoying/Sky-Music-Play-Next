// 模块：SkyMusic.Core 播放领域 CompiledTimeline
using System.Collections.Immutable;

namespace SkyMusic.Core.Playback;

public sealed record CompiledTimeline(
    ImmutableArray<PlaybackEvent> Events,
    long DurationMicroseconds);
