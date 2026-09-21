// 模块：SkyMusic.Infrastructure 通用模型 ScoreImportService
using SkyMusic.Core.Importing;
using SkyMusic.Core.Services;

namespace SkyMusic.Infrastructure.Scores;

public sealed class ScoreImportService : IScoreImportService
{
    private readonly IReadOnlyDictionary<string, IScoreImporter> _importers;

    public ScoreImportService(IEnumerable<IScoreImporter>? importers = null)
    {
        var available = importers?.ToArray() ??
        [
            new SkyStudioScoreImporter(),
            new MidiScoreImporter()
        ];

        _importers = available
            .SelectMany(importer => importer.SupportedExtensions.Select(extension => (extension, importer)))
            .ToDictionary(item => Normalize(item.extension), item => item.importer, StringComparer.OrdinalIgnoreCase);
        SupportedExtensions = new HashSet<string>(_importers.Keys, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlySet<string> SupportedExtensions { get; }

    public async ValueTask<ScoreImportResult> ImportAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var extension = Normalize(Path.GetExtension(filePath));
        if (!_importers.TryGetValue(extension, out var importer))
        {
            return ScoreImportResult.Failed(new ScoreImportIssue(
                "unsupported_format",
                $"Unsupported score format: {extension}",
                ScoreImportIssueSeverity.Error));
        }

        try
        {
            await using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            return await importer.ImportAsync(stream, Path.GetFileName(filePath), cancellationToken);
        }
        catch (IOException exception)
        {
            return ScoreImportResult.Failed(new ScoreImportIssue(
                "open_failed",
                $"Unable to open score: {exception.Message}",
                ScoreImportIssueSeverity.Error));
        }
        catch (UnauthorizedAccessException exception)
        {
            return ScoreImportResult.Failed(new ScoreImportIssue(
                "access_denied",
                $"Unable to access score: {exception.Message}",
                ScoreImportIssueSeverity.Error));
        }
    }

    private static string Normalize(string extension) =>
        extension.StartsWith('.') ? extension.ToLowerInvariant() : $".{extension.ToLowerInvariant()}";
}
