// 模块：SkyMusic.Core 桌面服务 IMacroInputSink
using SkyMusic.Core.Automation;

namespace SkyMusic.Core.Services;

public interface IMacroInputSink
{
    void Send(MacroEvent macroEvent);

    void Reset();
}
