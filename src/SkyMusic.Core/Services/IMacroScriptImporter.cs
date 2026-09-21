// 模块：SkyMusic.Core 桌面服务 IMacroScriptImporter
using SkyMusic.Core.Automation;

namespace SkyMusic.Core.Services;

public interface IMacroScriptImporter
{
    ValueTask<MacroImportResult> ImportAsync(
        Stream source,
        string sourceName,
        CancellationToken cancellationToken = default);
}
