// 模块：SkyMusic.Core 宏脚本领域 MacroImportResult
namespace SkyMusic.Core.Automation;

public sealed record MacroImportResult(MacroScript? Script, IReadOnlyList<string> Errors)
{
    public bool IsSuccess => Script is not null && Errors.Count == 0;

    public static MacroImportResult Failed(params string[] errors) => new(null, errors);
}
