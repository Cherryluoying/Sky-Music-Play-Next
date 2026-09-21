// 模块：SkyMusic.Core 桌面服务 IScoreImporter
using SkyMusic.Core.Importing;

namespace SkyMusic.Core.Services;

public interface IScoreImporter
{
    IReadOnlySet<string> SupportedExtensions { get; }

    ValueTask<ScoreImportResult> ImportAsync(
        Stream source,
        string sourceName,
        CancellationToken cancellationToken = default);
}
