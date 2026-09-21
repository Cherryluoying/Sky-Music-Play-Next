// 模块：SkyMusic.Core 导入结果 ScoreImportResult
using SkyMusic.Core.Models;

namespace SkyMusic.Core.Importing;

public sealed record ScoreImportResult(
    Score? Score,
    IReadOnlyList<ScoreImportIssue> Issues)
{
    public bool IsSuccess => Score is not null && Issues.All(issue => issue.Severity != ScoreImportIssueSeverity.Error);

    public static ScoreImportResult Failed(params ScoreImportIssue[] issues) => new(null, issues);
}
