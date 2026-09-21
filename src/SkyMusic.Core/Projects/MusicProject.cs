// 模块：SkyMusic.Core 工程领域 MusicProject
namespace SkyMusic.Core.Projects;

public sealed record MusicProject(
    Guid Id,
    ProjectMetadata Metadata,
    int Ppq,
    IReadOnlyList<TempoChange> TempoMap,
    IReadOnlyList<TimeSignatureChange> TimeSignatures,
    IReadOnlyList<ProjectTrack> Tracks,
    IReadOnlyList<ProjectMarker> Markers,
    IReadOnlyList<GameArrangement> GameArrangements)
{
    public const int DefaultPpq = 480;

    public static MusicProject Create(string title, string? author = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new MusicProject(
            Guid.NewGuid(),
            new ProjectMetadata(title, author ?? string.Empty, string.Empty, now, now),
            DefaultPpq,
            [new TempoChange(0, 500_000)],
            [new TimeSignatureChange(0, 4, 4)],
            [],
            [],
            []);
    }
}
