// 模块：SkyMusic.Core 工程领域 ProjectTrack
namespace SkyMusic.Core.Projects;

public enum ProjectTrackKind
{
    Instrument,
    Percussion
}

public sealed record ProjectTrack(
    Guid Id,
    string Name,
    ProjectTrackKind Kind,
    string Color,
    IReadOnlyList<ProjectNote> Notes,
    string? InstrumentId = null,
    float Gain = 1f,
    float Pan = 0f,
    bool IsMuted = false,
    bool IsSolo = false);
