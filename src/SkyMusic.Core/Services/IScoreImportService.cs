// 模块：SkyMusic.Core 桌面服务 IScoreImportService
using SkyMusic.Core.Importing;

namespace SkyMusic.Core.Services;

public interface IScoreImportService
{
    IReadOnlySet<string> SupportedExtensions { get; }

    ValueTask<ScoreImportResult> ImportAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}
