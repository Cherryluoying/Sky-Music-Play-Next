// 模块：SkyMusic.Infrastructure 工程领域 JsonMusicProjectStore
using System.Text.Json;
using System.Text.Json.Serialization;
using SkyMusic.Core.Projects;
using SkyMusic.Core.Services;

namespace SkyMusic.Infrastructure.Projects;

public sealed class JsonMusicProjectStore : IMusicProjectStore
{
    public const int CurrentFormatVersion = 1;

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public async ValueTask<MusicProject> LoadAsync(Stream source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        var document = await JsonSerializer.DeserializeAsync<ProjectDocument>(source, Options, cancellationToken)
            ?? throw new InvalidDataException("Project document is empty");
        if (document.FormatVersion != CurrentFormatVersion)
            throw new NotSupportedException($"Project format version {document.FormatVersion} is not supported");
        if (document.Project is null)
            throw new InvalidDataException("Project payload is missing");

        MusicProjectValidator.Validate(document.Project);
        return document.Project;
    }

    public async ValueTask SaveAsync(
        MusicProject project,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        MusicProjectValidator.Validate(project);
        var document = new ProjectDocument(CurrentFormatVersion, project);
        await JsonSerializer.SerializeAsync(destination, document, Options, cancellationToken);
    }

    private sealed record ProjectDocument(int FormatVersion, MusicProject? Project);
}
