// 模块：SkyMusic.Core 桌面服务 IScoreTimelineCompiler
using SkyMusic.Core.Models;
using SkyMusic.Core.Playback;

namespace SkyMusic.Core.Services;

public interface IScoreTimelineCompiler
{
    CompiledTimeline Compile(Score score, ScoreTimingSettings? timing = null);
}
