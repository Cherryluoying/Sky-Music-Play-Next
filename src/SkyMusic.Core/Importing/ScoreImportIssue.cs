// 模块：SkyMusic.Core 导入结果 ScoreImportIssue
namespace SkyMusic.Core.Importing;

public enum ScoreImportIssueSeverity
{
    Warning,
    Error
}

public sealed record ScoreImportIssue(
    string Code,
    string Message,
    ScoreImportIssueSeverity Severity,
    int? ItemIndex = null);
