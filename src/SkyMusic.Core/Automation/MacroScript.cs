// 模块：SkyMusic.Core 宏脚本领域 MacroScript
namespace SkyMusic.Core.Automation;

public sealed record MacroScript(string Name, IReadOnlyList<MacroEvent> Events)
{
    public long DurationMicroseconds => Events.Count == 0 ? 0 : Events[^1].TimeMicroseconds;
}
