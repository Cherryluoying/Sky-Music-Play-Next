// 模块：SkyMusic.Core 宏脚本领域 MacroEvent
namespace SkyMusic.Core.Automation;

public readonly record struct MacroEvent(string Key, MacroKeyAction Action, long TimeMicroseconds);
